import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { PlayniteClient } from "../client.js";
import { formatGameLine, formatGameDetail, getSteamImageUrl } from "../utils/format.js";

export function registerGameTools(server: McpServer, client: PlayniteClient): void {
  server.tool(
    "search_games",
    "Search and filter games in the Playnite library",
    {
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
    async (args) => {
      try {
        const params: Record<string, string | number | boolean> = {};
        if (args.query) params.q = args.query;
        if (args.limit) params.limit = args.limit;
        else params.limit = 50;
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

        const content: Array<{ type: "text"; text: string } | { type: "image"; data: string; mimeType: string }> = [
          { type: "text" as const, text: lines.join("\n") },
        ];

        // Include cover images for first few Steam games
        for (const game of result.games.slice(0, 5)) {
          const url = getSteamImageUrl(game);
          if (url) {
            content.push({
              type: "text" as const,
              text: `\n![${game.name}](${url})`,
            });
          }
        }

        return {
          content,
          _meta: { ui: { resourceUri: "ui://playnite/gallery" } },
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

  server.tool(
    "get_game",
    "Get full details for a specific game by ID",
    {
      gameId: z.string().describe("The game's unique ID (GUID)"),
    },
    async (args) => {
      try {
        const game = await client.getGame(args.gameId);
        const detail = formatGameDetail(game);
        const imageUrl = getSteamImageUrl(game);

        const content: Array<{ type: "text"; text: string }> = [
          { type: "text" as const, text: detail },
        ];

        if (imageUrl) {
          content.push({
            type: "text" as const,
            text: `\n![${game.name}](${imageUrl})`,
          });
        }

        return {
          content,
          _meta: { ui: { resourceUri: "ui://playnite/game" } },
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

  server.tool(
    "query_games",
    "Advanced game query with filters — use for analytics and complex searches",
    {
      source: z.string().optional().describe("Filter by source"),
      genre: z.string().optional().describe("Filter by genre"),
      category: z.string().optional().describe("Filter by category"),
      tag: z.string().optional().describe("Filter by tag"),
      installed: z.boolean().optional().describe("Filter installed only"),
      favorite: z.boolean().optional().describe("Filter favorites only"),
      completionStatus: z.string().optional().describe("Filter by completion status"),
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
        if (args.limit) params.limit = args.limit;
        else params.limit = 100;

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
      completionStatus: z.string().optional().describe("Completion status name"),
      categories: z.array(z.string()).optional().describe("Replace categories"),
      tags: z.array(z.string()).optional().describe("Replace tags"),
      genres: z.array(z.string()).optional().describe("Replace genres"),
      features: z.array(z.string()).optional().describe("Replace features"),
    },
    async (args) => {
      try {
        const { gameId, ...fields } = args;
        // Filter out undefined
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
}
