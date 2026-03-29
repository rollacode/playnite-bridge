import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import {
  registerAppTool,
  registerAppResource,
  RESOURCE_MIME_TYPE,
} from "@modelcontextprotocol/ext-apps/server";
import cors from "cors";
import express from "express";
import { readFileSync, existsSync } from "fs";
import { join } from "path";
import { fileURLToPath } from "url";
import { z } from "zod";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const UI_PATH = join(__dirname, "..", "mcp-app.html");

// ---------------------------------------------------------------------------
// Playnite client (inline to keep server self-contained for compilation)
// ---------------------------------------------------------------------------

function resolveToken(): string {
  const envToken = process.env.PLAYNITE_TOKEN;
  if (envToken) return envToken;
  throw new Error(
    "PLAYNITE_TOKEN env var is required. Get it from Playnite: Main Menu > Playnite Bridge > Show API Token"
  );
}

interface PlayniteGame {
  id: string;
  name: string;
  source?: string;
  genres?: string[];
  categories?: string[];
  tags?: string[];
  features?: string[];
  platforms?: string[];
  completionStatus?: string;
  isInstalled?: boolean;
  favorite?: boolean;
  hidden?: boolean;
  playtime?: number;
  playCount?: number;
  lastActivity?: string;
  userScore?: number;
  description?: string;
  notes?: string;
  developers?: string[];
  publishers?: string[];
  series?: string[];
  releaseDate?: string;
  communityScore?: number;
  criticScore?: number;
  links?: Array<{ name: string; url: string }>;
  gameId?: string;
  coverImage?: string;
  backgroundImage?: string;
}

interface GamesResponse {
  total: number;
  offset: number;
  limit: number;
  games: PlayniteGame[];
}

interface StatsResponse {
  totalGames: number;
  installed: number;
  favorites: number;
  totalPlaytime: number;
  bySource: Record<string, number>;
  byCompletionStatus: Record<string, number>;
  topGenres: Array<{ name: string; count: number }>;
  recentlyPlayed: PlayniteGame[];
}

interface CollectionItem {
  id: string;
  name: string;
}

class PlayniteClient {
  readonly baseUrl: string;
  readonly token: string;

  constructor(baseUrl?: string, token?: string) {
    this.baseUrl =
      baseUrl || process.env.PLAYNITE_URL || "http://localhost:19821";
    this.token = token || resolveToken();
  }

  async get<T = unknown>(path: string): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, {
      headers: { Authorization: `Bearer ${this.token}` },
    });
    if (!res.ok) {
      const body = await res.text();
      throw new Error(`Playnite API ${res.status}: ${body}`);
    }
    return res.json() as Promise<T>;
  }

  async post<T = unknown>(path: string, body?: unknown): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${this.token}`,
        "Content-Type": "application/json",
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`Playnite API ${res.status}: ${text}`);
    }
    return res.json() as Promise<T>;
  }

  async put<T = unknown>(path: string, body: unknown): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, {
      method: "PUT",
      headers: {
        Authorization: `Bearer ${this.token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(body),
    });
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`Playnite API ${res.status}: ${text}`);
    }
    return res.json() as Promise<T>;
  }

  async searchGames(
    params: Record<string, string | number | boolean> = {}
  ): Promise<GamesResponse> {
    const qs = new URLSearchParams();
    for (const [k, v] of Object.entries(params)) {
      if (v !== undefined && v !== null && v !== "") qs.set(k, String(v));
    }
    const query = qs.toString();
    return this.get<GamesResponse>(`/api/games${query ? "?" + query : ""}`);
  }

  async getGame(id: string): Promise<PlayniteGame> {
    return this.get<PlayniteGame>(`/api/games/${id}`);
  }

  async updateGame(
    id: string,
    fields: Record<string, unknown>
  ): Promise<PlayniteGame> {
    return this.put<PlayniteGame>(`/api/games/${id}`, fields);
  }

  async getStats(): Promise<StatsResponse> {
    return this.get<StatsResponse>("/api/stats");
  }

  async getCollection(name: string): Promise<CollectionItem[]> {
    return this.get<CollectionItem[]>(`/api/${name}`);
  }

  async setCollection(
    gameId: string,
    field: string,
    values: string[],
    action: "set" | "add" = "set"
  ): Promise<unknown> {
    const body = { [field]: values };
    if (action === "add") {
      return this.post(`/api/games/${gameId}/${field}`, body);
    }
    return this.put(`/api/games/${gameId}/${field}`, body);
  }
}

// ---------------------------------------------------------------------------
// Format helpers
// ---------------------------------------------------------------------------

function formatPlaytime(seconds?: number): string {
  if (!seconds || seconds <= 0) return "Never played";
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours === 0) return `${minutes}m`;
  if (minutes === 0) return `${hours}h`;
  return `${hours}h ${minutes}m`;
}

function formatDate(dateStr?: string): string {
  if (!dateStr) return "Unknown";
  try {
    return new Date(dateStr).toLocaleDateString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  } catch {
    return dateStr;
  }
}

function getSteamImageUrl(game: PlayniteGame): string | null {
  if (game.source !== "Steam") return null;
  if (game.links) {
    for (const l of game.links) {
      const m = l.url?.match(/\/app\/(\d+)/);
      if (m)
        return `https://steamcdn-a.akamaihd.net/steam/apps/${m[1]}/header.jpg`;
    }
  }
  if (game.gameId && /^\d+$/.test(game.gameId))
    return `https://steamcdn-a.akamaihd.net/steam/apps/${game.gameId}/header.jpg`;
  return null;
}

function formatGameLine(game: PlayniteGame): string {
  const parts = [game.name];
  if (game.source) parts.push(`[${game.source}]`);
  if (game.playtime) parts.push(formatPlaytime(game.playtime));
  if (game.isInstalled) parts.push("installed");
  if (game.favorite) parts.push("*");
  return parts.join(" -- ");
}

function formatGameDetail(game: PlayniteGame): string {
  const lines: string[] = [`# ${game.name}`, ""];
  if (game.source) lines.push(`**Source:** ${game.source}`);
  if (game.platforms?.length)
    lines.push(`**Platforms:** ${game.platforms.join(", ")}`);
  if (game.genres?.length)
    lines.push(`**Genres:** ${game.genres.join(", ")}`);
  if (game.developers?.length)
    lines.push(`**Developers:** ${game.developers.join(", ")}`);
  if (game.publishers?.length)
    lines.push(`**Publishers:** ${game.publishers.join(", ")}`);
  if (game.releaseDate)
    lines.push(`**Release:** ${formatDate(game.releaseDate)}`);
  lines.push(`**Playtime:** ${formatPlaytime(game.playtime)}`);
  if (game.playCount) lines.push(`**Play count:** ${game.playCount}`);
  if (game.lastActivity)
    lines.push(`**Last played:** ${formatDate(game.lastActivity)}`);
  if (game.completionStatus)
    lines.push(`**Status:** ${game.completionStatus}`);
  if (game.userScore != null)
    lines.push(`**User score:** ${game.userScore}/100`);
  if (game.communityScore != null)
    lines.push(`**Community score:** ${game.communityScore}/100`);
  if (game.criticScore != null)
    lines.push(`**Critic score:** ${game.criticScore}/100`);
  if (game.categories?.length)
    lines.push(`**Categories:** ${game.categories.join(", ")}`);
  if (game.tags?.length) lines.push(`**Tags:** ${game.tags.join(", ")}`);
  if (game.features?.length)
    lines.push(`**Features:** ${game.features.join(", ")}`);
  if (game.series?.length)
    lines.push(`**Series:** ${game.series.join(", ")}`);
  if (game.description) {
    lines.push("", "## Description");
    lines.push(game.description.replace(/<[^>]+>/g, "").substring(0, 500));
  }
  if (game.notes) {
    lines.push("", "## Notes", game.notes);
  }
  return lines.join("\n");
}

// ---------------------------------------------------------------------------
// Init
// ---------------------------------------------------------------------------

let client: PlayniteClient;
try {
  client = new PlayniteClient();
} catch (err) {
  console.warn(
    `Warning: ${err instanceof Error ? err.message : String(err)}`
  );
  console.warn("Server will start but API calls will fail.");
  client = new PlayniteClient("http://localhost:19821", "INVALID_TOKEN");
}

// Load UI HTML
let uiHtml = "";
try {
  uiHtml = readFileSync(UI_PATH, "utf-8");
} catch {
  console.warn("UI HTML not found at", UI_PATH, "- will serve placeholder");
  uiHtml = "<html><body>UI not built yet. Run: npm run build:ui</body></html>";
}

// ---------------------------------------------------------------------------
// MCP Server
// ---------------------------------------------------------------------------

const server = new McpServer({
  name: "playnite",
  version: "2.0.0",
});

const resourceUri = "ui://playnite/mcp-app.html";

// ---- Tools with UI (registerAppTool) ----

registerAppTool(
  server,
  "search_games",
  {
    title: "Search Games",
    description:
      "Search and filter games in the Playnite library. Returns game cards with cover art.",
    inputSchema: {
      query: z.string().optional().describe("Search by name (substring match)"),
      limit: z.number().optional().describe("Max results (default 50)"),
      source: z.string().optional().describe("Filter by source (Steam, Epic Games, GOG, etc.)"),
      genre: z.string().optional().describe("Filter by genre"),
      category: z.string().optional().describe("Filter by category"),
      tag: z.string().optional().describe("Filter by tag"),
      installed: z.boolean().optional().describe("Filter installed games only"),
      favorite: z.boolean().optional().describe("Filter favorite games only"),
      platform: z.string().optional().describe("Filter by platform"),
      completionStatus: z.string().optional().describe("Filter by completion status"),
    },
    _meta: { ui: { resourceUri } },
  },
  async (args) => {
    try {
      const params: Record<string, string | number | boolean> = {};
      if (args.query) params.q = args.query;
      params.limit = args.limit || 50;
      if (args.source) params.source = args.source;
      if (args.genre) params.genre = args.genre;
      if (args.category) params.category = args.category;
      if (args.tag) params.tag = args.tag;
      if (args.installed !== undefined) params.installed = args.installed;
      if (args.favorite !== undefined) params.favorite = args.favorite;
      if (args.platform) params.platform = args.platform;
      if (args.completionStatus) params.completionStatus = args.completionStatus;

      const result = await client.searchGames(params);
      const lines = [
        `Found ${result.total} games (showing ${result.games.length}):`,
        "",
        ...result.games.map(
          (g, i) => `${i + 1}. ${formatGameLine(g)} [id: ${g.id}]`
        ),
      ];

      // Return full JSON data as second content block for UI parsing
      return {
        content: [
          { type: "text" as const, text: lines.join("\n") },
          {
            type: "text" as const,
            text: `\n<!--GAMES_JSON:${JSON.stringify(result.games)}-->`,
          },
        ],
      };
    } catch (err) {
      return {
        content: [
          {
            type: "text" as const,
            text: `Error searching games: ${err instanceof Error ? err.message : String(err)}`,
          },
        ],
        isError: true,
      };
    }
  }
);

registerAppTool(
  server,
  "get_game",
  {
    title: "Get Game Details",
    description: "Get full details for a specific game by ID with cover art.",
    inputSchema: {
      gameId: z.string().describe("The game's unique ID (GUID)"),
    },
    _meta: { ui: { resourceUri } },
  },
  async (args) => {
    try {
      const game = await client.getGame(args.gameId);
      const detail = formatGameDetail(game);
      const imageUrl = getSteamImageUrl(game);

      return {
        content: [
          { type: "text" as const, text: detail },
          {
            type: "text" as const,
            text: `\n<!--GAME_JSON:${JSON.stringify(game)}-->`,
          },
          ...(imageUrl
            ? [{ type: "text" as const, text: `\n![${game.name}](${imageUrl})` }]
            : []),
        ],
      };
    } catch (err) {
      return {
        content: [
          {
            type: "text" as const,
            text: `Error getting game: ${err instanceof Error ? err.message : String(err)}`,
          },
        ],
        isError: true,
      };
    }
  }
);

registerAppTool(
  server,
  "library_stats",
  {
    title: "Library Statistics",
    description:
      "Get comprehensive library statistics -- total games, playtime breakdown, top genres, sources, completion status.",
    inputSchema: {},
    _meta: { ui: { resourceUri } },
  },
  async () => {
    try {
      const stats = await client.getStats();
      const lines = [
        "# Playnite Library Stats",
        "",
        `**Total games:** ${stats.totalGames}`,
        `**Installed:** ${stats.installed}`,
        `**Favorites:** ${stats.favorites}`,
        `**Total playtime:** ${formatPlaytime(stats.totalPlaytime)}`,
        "",
        "## Games by Source",
      ];
      if (stats.bySource) {
        const sorted = Object.entries(stats.bySource).sort(
          ([, a], [, b]) => b - a
        );
        for (const [source, count] of sorted) {
          lines.push(`- **${source}:** ${count}`);
        }
      }
      lines.push("", "## Completion Status");
      if (stats.byCompletionStatus) {
        const sorted = Object.entries(stats.byCompletionStatus).sort(
          ([, a], [, b]) => b - a
        );
        for (const [status, count] of sorted) {
          lines.push(`- **${status}:** ${count}`);
        }
      }
      lines.push("", "## Top Genres");
      if (stats.topGenres?.length) {
        for (const g of stats.topGenres.slice(0, 15)) {
          lines.push(`- **${g.name}:** ${g.count}`);
        }
      }
      lines.push("", "## Recently Played");
      if (stats.recentlyPlayed?.length) {
        for (const g of stats.recentlyPlayed.slice(0, 10)) {
          lines.push(`- ${formatGameLine(g)}`);
        }
      }
      return {
        content: [
          { type: "text" as const, text: lines.join("\n") },
          {
            type: "text" as const,
            text: `\n<!--STATS_JSON:${JSON.stringify(stats)}-->`,
          },
        ],
      };
    } catch (err) {
      return {
        content: [
          {
            type: "text" as const,
            text: `Error getting stats: ${err instanceof Error ? err.message : String(err)}`,
          },
        ],
        isError: true,
      };
    }
  }
);

// ---- Launch tool ----

server.tool(
  "launch_game",
  "Launch a game on the computer via Playnite",
  {
    gameId: z.string().describe("The game's unique ID"),
  },
  async (args) => {
    try {
      await client.post(`/api/games/${args.gameId}/launch`);
      return {
        content: [
          { type: "text" as const, text: `Launching game ${args.gameId}...` },
        ],
      };
    } catch (err) {
      return {
        content: [
          {
            type: "text" as const,
            text: `Error launching game: ${err instanceof Error ? err.message : String(err)}`,
          },
        ],
        isError: true,
      };
    }
  }
);

// ---- Regular tools (no UI) ----

server.tool(
  "query_games",
  "Advanced game query with filters -- use for analytics and complex searches",
  {
    source: z.string().optional().describe("Filter by source"),
    genre: z.string().optional().describe("Filter by genre"),
    category: z.string().optional().describe("Filter by category"),
    tag: z.string().optional().describe("Filter by tag"),
    installed: z.boolean().optional().describe("Filter installed only"),
    favorite: z.boolean().optional().describe("Filter favorites only"),
    completionStatus: z
      .string()
      .optional()
      .describe("Filter by completion status"),
    limit: z.number().optional().describe("Max results"),
  },
  async (args) => {
    try {
      const params: Record<string, string | number | boolean> = {};
      if (args.source) params.source = args.source;
      if (args.genre) params.genre = args.genre;
      if (args.category) params.category = args.category;
      if (args.tag) params.tag = args.tag;
      if (args.installed !== undefined) params.installed = args.installed;
      if (args.favorite !== undefined) params.favorite = args.favorite;
      if (args.completionStatus) params.completionStatus = args.completionStatus;
      params.limit = args.limit || 100;

      const result = await client.searchGames(params);
      const lines = [
        `Query returned ${result.total} games (showing ${result.games.length}):`,
        "",
        ...result.games.map(
          (g, i) => `${i + 1}. ${formatGameLine(g)} [id: ${g.id}]`
        ),
      ];
      return {
        content: [{ type: "text" as const, text: lines.join("\n") }],
      };
    } catch (err) {
      return {
        content: [
          {
            type: "text" as const,
            text: `Error querying games: ${err instanceof Error ? err.message : String(err)}`,
          },
        ],
        isError: true,
      };
    }
  }
);

server.tool(
  "update_game",
  "Update game fields (name, categories, tags, scores, status, etc.)",
  {
    gameId: z.string().describe("The game's unique ID"),
    name: z.string().optional().describe("New game name"),
    description: z.string().optional().describe("New description"),
    notes: z.string().optional().describe("New notes"),
    favorite: z.boolean().optional().describe("Set favorite status"),
    hidden: z.boolean().optional().describe("Set hidden status"),
    userScore: z.number().optional().describe("User score 0-100"),
    completionStatus: z
      .string()
      .optional()
      .describe("Completion status name"),
    categories: z.array(z.string()).optional().describe("Replace categories"),
    tags: z.array(z.string()).optional().describe("Replace tags"),
    genres: z.array(z.string()).optional().describe("Replace genres"),
    features: z.array(z.string()).optional().describe("Replace features"),
  },
  async (args) => {
    try {
      const { gameId, ...fields } = args;
      const updates: Record<string, unknown> = {};
      for (const [k, v] of Object.entries(fields)) {
        if (v !== undefined) updates[k] = v;
      }
      if (Object.keys(updates).length === 0) {
        return {
          content: [{ type: "text" as const, text: "No fields to update." }],
        };
      }
      const game = await client.updateGame(gameId, updates);
      return {
        content: [
          {
            type: "text" as const,
            text: `Updated "${game.name}" successfully.\n\n${formatGameDetail(game)}`,
          },
        ],
      };
    } catch (err) {
      return {
        content: [
          {
            type: "text" as const,
            text: `Error updating game: ${err instanceof Error ? err.message : String(err)}`,
          },
        ],
        isError: true,
      };
    }
  }
);

server.tool(
  "get_achievements",
  "Get achievements for a game (requires SuccessStory plugin)",
  {
    gameId: z.string().describe("The game's unique ID"),
  },
  async (args) => {
    try {
      const game = await client.getGame(args.gameId);
      const gameAny = game as unknown as Record<string, unknown>;
      const achData = gameAny.achievements as
        | {
            achievements?: Array<{
              name: string;
              description?: string;
              unlocked?: boolean;
              dateUnlocked?: string;
              percent?: number;
            }>;
            total?: number;
            unlocked?: number;
          }
        | undefined;

      if (!achData?.achievements?.length) {
        return {
          content: [
            {
              type: "text" as const,
              text: `No achievements found for "${game.name}". The SuccessStory plugin may not be installed, or this game has no achievements.`,
            },
          ],
        };
      }

      const total = achData.total ?? achData.achievements.length;
      const unlocked =
        achData.unlocked ??
        achData.achievements.filter((a) => a.unlocked).length;
      const pct = total > 0 ? Math.round((unlocked / total) * 100) : 0;

      const lines = [
        `# Achievements for ${game.name}`,
        `**Progress:** ${unlocked}/${total} (${pct}%)`,
        "",
      ];
      for (const ach of achData.achievements) {
        const status = ach.unlocked ? "DONE" : "TODO";
        const rarity =
          ach.percent != null ? ` (${ach.percent}% of players)` : "";
        const date = ach.dateUnlocked ? ` -- ${ach.dateUnlocked}` : "";
        lines.push(`[${status}] **${ach.name}**${rarity}${date}`);
        if (ach.description) lines.push(`   ${ach.description}`);
      }
      return {
        content: [{ type: "text" as const, text: lines.join("\n") }],
      };
    } catch (err) {
      return {
        content: [
          {
            type: "text" as const,
            text: `Error getting achievements: ${err instanceof Error ? err.message : String(err)}`,
          },
        ],
        isError: true,
      };
    }
  }
);

server.tool(
  "get_activity",
  "Get play sessions and activity history for a game",
  {
    gameId: z.string().describe("The game's unique ID"),
  },
  async (args) => {
    try {
      const game = await client.getGame(args.gameId);
      const lines = [
        `# Activity for ${game.name}`,
        "",
        `**Total playtime:** ${formatPlaytime(game.playtime)}`,
        `**Play count:** ${game.playCount ?? 0}`,
        `**Last played:** ${formatDate(game.lastActivity)}`,
      ];

      const gameAny = game as unknown as Record<string, unknown>;
      const sessions = gameAny.sessions as
        | Array<{ start?: string; end?: string; duration?: number }>
        | undefined;

      if (sessions?.length) {
        lines.push("", "## Sessions");
        for (const s of sessions) {
          lines.push(`- ${formatDate(s.start)}: ${formatPlaytime(s.duration)}`);
        }
      }
      return {
        content: [{ type: "text" as const, text: lines.join("\n") }],
      };
    } catch (err) {
      return {
        content: [
          {
            type: "text" as const,
            text: `Error getting activity: ${err instanceof Error ? err.message : String(err)}`,
          },
        ],
        isError: true,
      };
    }
  }
);

server.tool(
  "manage_categories",
  "Set or add categories, tags, genres, or features for a game",
  {
    gameId: z.string().describe("The game's unique ID"),
    field: z
      .enum(["categories", "tags", "genres", "features"])
      .describe("Which collection field to modify"),
    action: z
      .enum(["set", "add"])
      .describe("'set' replaces all values, 'add' appends"),
    values: z.array(z.string()).describe("The values to set or add"),
  },
  async (args) => {
    try {
      const result = await client.setCollection(
        args.gameId,
        args.field,
        args.values,
        args.action
      );
      return {
        content: [
          {
            type: "text" as const,
            text: `Successfully ${args.action === "set" ? "set" : "added"} ${args.field} for game ${args.gameId}: ${args.values.join(", ")}\n\nResult: ${JSON.stringify(result)}`,
          },
        ],
      };
    } catch (err) {
      return {
        content: [
          {
            type: "text" as const,
            text: `Error managing ${args.field}: ${err instanceof Error ? err.message : String(err)}`,
          },
        ],
        isError: true,
      };
    }
  }
);

server.tool(
  "list_collections",
  "List available categories, tags, genres, features, sources, platforms, or completion statuses",
  {
    collection: z
      .enum([
        "categories",
        "tags",
        "genres",
        "features",
        "sources",
        "platforms",
        "completion-statuses",
        "series",
      ])
      .describe("Which collection to list"),
  },
  async (args) => {
    try {
      const items = await client.getCollection(args.collection);
      const lines = [
        `# ${args.collection} (${items.length})`,
        "",
        ...items.map((item) => `- ${item.name} [${item.id}]`),
      ];
      return {
        content: [{ type: "text" as const, text: lines.join("\n") }],
      };
    } catch (err) {
      return {
        content: [
          {
            type: "text" as const,
            text: `Error listing ${args.collection}: ${err instanceof Error ? err.message : String(err)}`,
          },
        ],
        isError: true,
      };
    }
  }
);

// ---- UI Resource (MCP Apps) ----

registerAppResource(
  server,
  resourceUri,
  resourceUri,
  { mimeType: RESOURCE_MIME_TYPE },
  async () => ({
    contents: [{ uri: resourceUri, mimeType: RESOURCE_MIME_TYPE, text: uiHtml }],
  })
);

// ---------------------------------------------------------------------------
// Express + Streamable HTTP
// ---------------------------------------------------------------------------

const app = express();
app.use(cors());

// REST API — direct endpoints for standalone UI
app.get("/api/games", async (req, res) => {
  try {
    const params: Record<string, string | number | boolean> = {};
    if (req.query.q) params.q = req.query.q as string;
    if (req.query.limit) params.limit = Number(req.query.limit);
    else params.limit = 50;
    if (req.query.source) params.source = req.query.source as string;
    if (req.query.genre) params.genre = req.query.genre as string;
    if (req.query.installed === "true") params.installed = true;
    if (req.query.favorite === "true") params.favorite = true;
    const result = await client.searchGames(params);
    res.json(result);
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    res.status(500).json({ error: msg });
  }
});

app.get("/api/games/:id", async (req, res) => {
  try {
    const game = await client.getGame(req.params.id);
    res.json(game);
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    res.status(500).json({ error: msg });
  }
});

app.get("/api/stats", async (_req, res) => {
  try {
    const stats = await client.getStats();
    res.json(stats);
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    res.status(500).json({ error: msg });
  }
});

// Serve standalone UI
app.get("/ui", (_req, res) => {
  res.type("html").send(uiHtml);
});

// MCP endpoint (Streamable HTTP)
app.post("/mcp", express.json(), async (req, res) => {
  try {
    const transport = new StreamableHTTPServerTransport({
      sessionIdGenerator: undefined,
      enableJsonResponse: true,
    });
    res.on("close", () => {
      transport.close();
    });
    await server.connect(transport);
    await transport.handleRequest(req, res, req.body);
  } catch (err: unknown) {
    console.error("[MCP] ERROR:", err);
    if (!res.headersSent) {
      const msg = err instanceof Error ? err.message : String(err);
      res.status(500).json({ error: msg });
    }
  }
});

app.get("/mcp", (_req, res) => {
  res.status(405).json({ error: "Method not allowed. Use POST." });
});

app.delete("/mcp", (_req, res) => {
  res.status(200).end();
});

const PORT = Number(process.env.PORT) || 3849;
const httpServer = app.listen(PORT, "0.0.0.0", () => {
  console.warn(`Playnite MCP server listening on http://0.0.0.0:${PORT}`);
  console.warn(`  MCP:  http://0.0.0.0:${PORT}/mcp`);
  console.warn(`  UI:   http://0.0.0.0:${PORT}/ui`);
  console.warn(`  API:  http://0.0.0.0:${PORT}/api/games`);
});

for (const sig of ["SIGINT", "SIGTERM"]) {
  process.on(sig, () => {
    console.warn(`\n${sig} received, shutting down...`);
    httpServer.close(() => process.exit(0));
    setTimeout(() => process.exit(1), 5000);
  });
}
