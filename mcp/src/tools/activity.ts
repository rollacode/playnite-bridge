import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { PlayniteClient } from "../client.js";
import { formatPlaytime, formatDate } from "../utils/format.js";

export function registerActivityTools(server: McpServer, client: PlayniteClient): void {
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

        // Check for detailed activity data if available
        const gameAny = game as unknown as Record<string, unknown>;
        const sessions = gameAny.sessions as Array<{
          start?: string;
          end?: string;
          duration?: number;
        }> | undefined;

        if (sessions?.length) {
          lines.push("", "## Sessions");
          for (const s of sessions) {
            const start = formatDate(s.start);
            const dur = formatPlaytime(s.duration);
            lines.push(`- ${start}: ${dur}`);
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
}
