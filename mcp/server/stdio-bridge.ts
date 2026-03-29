/**
 * Stdio bridge — starts the Express MCP server AND connects a stdio transport.
 * Claude Desktop launches this via command/args, communicates over stdin/stdout.
 * The Express server also starts for standalone UI at /ui.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  registerAppTool,
  registerAppResource,
  RESOURCE_MIME_TYPE,
} from "@modelcontextprotocol/ext-apps/server";
import { readFileSync } from "fs";
import { join } from "path";
import { fileURLToPath } from "url";
import { z } from "zod";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const UI_PATH = join(__dirname, "..", "mcp-app.html");

// -- Token resolution --
function resolveToken(): string {
  const envToken = process.env.PLAYNITE_TOKEN;
  if (envToken) return envToken;
  throw new Error("PLAYNITE_TOKEN env var is required. Get it from Playnite: Main Menu > Playnite Bridge > Show API Token");
}

// -- Playnite client --
interface PlayniteGame {
  id: string; name: string; source?: string; genres?: string[];
  categories?: string[]; tags?: string[]; features?: string[];
  platforms?: string[]; completionStatus?: string;
  isInstalled?: boolean; favorite?: boolean; hidden?: boolean;
  playtime?: number; playCount?: number; lastActivity?: string;
  userScore?: number; description?: string; notes?: string;
  developers?: string[]; publishers?: string[]; series?: string[];
  releaseDate?: string; communityScore?: number; criticScore?: number;
  links?: Array<{ name: string; url: string }>; gameId?: string;
}
interface GamesResponse { total: number; offset: number; limit: number; games: PlayniteGame[]; }

class PlayniteClient {
  readonly baseUrl: string;
  readonly token: string;
  constructor(baseUrl?: string, token?: string) {
    this.baseUrl = baseUrl || process.env.PLAYNITE_URL || "http://localhost:19821";
    this.token = token || resolveToken();
  }
  async get<T = unknown>(path: string): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, { headers: { Authorization: `Bearer ${this.token}` } });
    if (!res.ok) throw new Error(`Playnite API ${res.status}: ${await res.text()}`);
    return res.json() as Promise<T>;
  }
  async post<T = unknown>(path: string, body?: unknown): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, { method: "POST", headers: { Authorization: `Bearer ${this.token}`, "Content-Type": "application/json" }, body: body ? JSON.stringify(body) : undefined });
    if (!res.ok) throw new Error(`Playnite API ${res.status}: ${await res.text()}`);
    return res.json() as Promise<T>;
  }
  async put<T = unknown>(path: string, body: unknown): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, { method: "PUT", headers: { Authorization: `Bearer ${this.token}`, "Content-Type": "application/json" }, body: JSON.stringify(body) });
    if (!res.ok) throw new Error(`Playnite API ${res.status}: ${await res.text()}`);
    return res.json() as Promise<T>;
  }
  async searchGames(params: Record<string, string | number | boolean> = {}): Promise<GamesResponse> {
    const qs = new URLSearchParams();
    for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== "") qs.set(k, String(v));
    const query = qs.toString();
    return this.get<GamesResponse>(`/api/games${query ? "?" + query : ""}`);
  }
  async getGame(id: string): Promise<PlayniteGame> { return this.get(`/api/games/${id}`); }
  async updateGame(id: string, fields: Record<string, unknown>): Promise<PlayniteGame> { return this.put(`/api/games/${id}`, fields); }
  async getStats(): Promise<unknown> { return this.get("/api/stats"); }
  async getCollection(name: string): Promise<Array<{id:string;name:string}>> { return this.get(`/api/${name}`); }
  async setCollection(gameId: string, field: string, values: string[], action: "set"|"add" = "set"): Promise<unknown> {
    const body = { [field]: values };
    return action === "add" ? this.post(`/api/games/${gameId}/${field}`, body) : this.put(`/api/games/${gameId}/${field}`, body);
  }
}

// -- Formatters --
function formatPlaytime(s?: number): string { if (!s || s <= 0) return "Never played"; const h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60); return h === 0 ? `${m}m` : m === 0 ? `${h}h` : `${h}h ${m}m`; }
function formatDate(d?: string): string { if (!d) return "Unknown"; try { return new Date(d).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" }); } catch { return d; } }
function getSteamImageUrl(g: PlayniteGame): string | null {
  if (g.source !== "Steam") return null;
  if (g.links) for (const l of g.links) { const m = l.url?.match(/\/app\/(\d+)/); if (m) return `https://steamcdn-a.akamaihd.net/steam/apps/${m[1]}/header.jpg`; }
  if (g.gameId && /^\d+$/.test(g.gameId)) return `https://steamcdn-a.akamaihd.net/steam/apps/${g.gameId}/header.jpg`;
  return null;
}
function formatGameLine(g: PlayniteGame): string { const p = [g.name]; if (g.source) p.push(`[${g.source}]`); if (g.playtime) p.push(formatPlaytime(g.playtime)); if (g.isInstalled) p.push("installed"); return p.join(" -- "); }
function formatGameDetail(g: PlayniteGame): string {
  const l: string[] = [`# ${g.name}`, ""];
  if (g.source) l.push(`**Source:** ${g.source}`);
  if (g.platforms?.length) l.push(`**Platforms:** ${g.platforms.join(", ")}`);
  if (g.genres?.length) l.push(`**Genres:** ${g.genres.join(", ")}`);
  if (g.developers?.length) l.push(`**Developers:** ${g.developers.join(", ")}`);
  l.push(`**Playtime:** ${formatPlaytime(g.playtime)}`);
  if (g.completionStatus) l.push(`**Status:** ${g.completionStatus}`);
  if (g.releaseDate) l.push(`**Release:** ${formatDate(g.releaseDate)}`);
  return l.join("\n");
}

// -- Init --
let client: PlayniteClient;
try { client = new PlayniteClient(); } catch (err) {
  process.stderr.write(`Warning: ${err instanceof Error ? err.message : String(err)}\n`);
  client = new PlayniteClient("http://localhost:19821", "INVALID_TOKEN");
}

let uiHtml = "";
try { uiHtml = readFileSync(UI_PATH, "utf-8"); } catch { uiHtml = "<html><body>UI not built</body></html>"; }

// -- MCP Server --
const server = new McpServer({ name: "playnite", version: "2.0.0" });
const resourceUri = "ui://playnite/mcp-app.html";

// Tools with UI
registerAppTool(server, "search_games", {
  title: "Search Games", description: "Search and filter games in the Playnite library",
  inputSchema: {
    query: z.string().optional().describe("Search by name"),
    limit: z.number().optional().describe("Max results (default 50)"),
    source: z.string().optional().describe("Filter by source"),
    genre: z.string().optional().describe("Filter by genre"),
    installed: z.boolean().optional().describe("Installed only"),
    favorite: z.boolean().optional().describe("Favorites only"),
  },
  _meta: { ui: { resourceUri } },
}, async (args) => {
  const params: Record<string, string | number | boolean> = {};
  if (args.query) params.q = args.query;
  params.limit = args.limit || 50;
  if (args.source) params.source = args.source;
  if (args.genre) params.genre = args.genre;
  if (args.installed !== undefined) params.installed = args.installed;
  if (args.favorite !== undefined) params.favorite = args.favorite;
  const result = await client.searchGames(params);
  const lines = [`Found ${result.total} games (showing ${result.games.length}):`, "", ...result.games.map((g, i) => `${i + 1}. ${formatGameLine(g)} [id: ${g.id}]`)];
  return { content: [{ type: "text" as const, text: lines.join("\n") }, { type: "text" as const, text: `\n<!--GAMES_JSON:${JSON.stringify(result.games)}-->` }] };
});

registerAppTool(server, "get_game", {
  title: "Get Game Details", description: "Get full details for a specific game",
  inputSchema: { gameId: z.string().describe("The game's unique ID") },
  _meta: { ui: { resourceUri } },
}, async (args) => {
  const game = await client.getGame(args.gameId);
  const url = getSteamImageUrl(game);
  return { content: [{ type: "text" as const, text: formatGameDetail(game) }, { type: "text" as const, text: `\n<!--GAME_JSON:${JSON.stringify(game)}-->` }, ...(url ? [{ type: "text" as const, text: `\n![${game.name}](${url})` }] : [])] };
});

registerAppTool(server, "library_stats", {
  title: "Library Statistics", description: "Get comprehensive library statistics",
  inputSchema: {},
  _meta: { ui: { resourceUri } },
}, async () => {
  const stats = await client.getStats() as any;
  const lines = [`# Library Stats`, "", `**Total:** ${stats.totalGames} games`, `**Installed:** ${stats.installed}`, `**Playtime:** ${formatPlaytime(stats.totalPlaytime)}`];
  return { content: [{ type: "text" as const, text: lines.join("\n") }, { type: "text" as const, text: `\n<!--STATS_JSON:${JSON.stringify(stats)}-->` }] };
});

// Regular tools
server.tool("update_game", "Update game fields", {
  gameId: z.string().describe("Game ID"),
  name: z.string().optional(), notes: z.string().optional(),
  favorite: z.boolean().optional(), hidden: z.boolean().optional(),
  userScore: z.number().optional(), completionStatus: z.string().optional(),
  categories: z.array(z.string()).optional(), tags: z.array(z.string()).optional(),
}, async (args) => {
  const { gameId, ...fields } = args;
  const updates: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(fields)) if (v !== undefined) updates[k] = v;
  if (!Object.keys(updates).length) return { content: [{ type: "text" as const, text: "No fields to update." }] };
  const game = await client.updateGame(gameId, updates);
  return { content: [{ type: "text" as const, text: `Updated "${game.name}".\n\n${formatGameDetail(game)}` }] };
});

server.tool("get_achievements", "Get achievements for a game", { gameId: z.string() }, async (args) => {
  const data = await client.get<any>(`/api/games/${args.gameId}/achievements`);
  if (!data.achievements?.length) return { content: [{ type: "text" as const, text: `No achievements found. ${data.message || ""}` }] };
  const lines = [`# Achievements: ${data.gameName}`, `**Progress:** ${data.unlocked}/${data.total}`, ""];
  for (const a of data.achievements) { lines.push(`[${a.unlocked ? "DONE" : "    "}] ${a.name} (${a.percent}%)`); }
  return { content: [{ type: "text" as const, text: lines.join("\n") }] };
});

server.tool("get_activity", "Get play sessions for a game", { gameId: z.string() }, async (args) => {
  const data = await client.get<any>(`/api/games/${args.gameId}/activity`);
  const lines = [`# Activity: ${data.gameName}`, `**Total:** ${data.totalHours}h (${data.sessionCount} sessions)`, ""];
  if (data.sessions) for (const s of data.sessions) lines.push(`- ${s.date}: ${Math.round(s.elapsedSeconds / 60)}min`);
  return { content: [{ type: "text" as const, text: lines.join("\n") }] };
});

server.tool("manage_categories", "Set or add categories/tags/genres/features", {
  gameId: z.string(), field: z.enum(["categories", "tags", "genres", "features"]),
  action: z.enum(["set", "add"]), values: z.array(z.string()),
}, async (args) => {
  const result = await client.setCollection(args.gameId, args.field, args.values, args.action);
  return { content: [{ type: "text" as const, text: `${args.action === "set" ? "Set" : "Added"} ${args.field}: ${args.values.join(", ")}\n${JSON.stringify(result)}` }] };
});

server.tool("list_collections", "List categories, tags, genres, etc.", {
  collection: z.enum(["categories", "tags", "genres", "features", "sources", "platforms", "completion-statuses", "series"]),
}, async (args) => {
  const items = await client.getCollection(args.collection);
  return { content: [{ type: "text" as const, text: `# ${args.collection} (${items.length})\n\n${items.map(i => `- ${i.name}`).join("\n")}` }] };
});

// UI Resource
registerAppResource(server, resourceUri, resourceUri, { mimeType: RESOURCE_MIME_TYPE }, async () => ({
  contents: [{ uri: resourceUri, mimeType: RESOURCE_MIME_TYPE, text: uiHtml }],
}));

// -- Connect via stdio --
const transport = new StdioServerTransport();
await server.connect(transport);
process.stderr.write("Playnite MCP server running (stdio)\n");
