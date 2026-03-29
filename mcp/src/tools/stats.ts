import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { PlayniteClient } from "../client.js";
import { formatPlaytime, formatGameLine } from "../utils/format.js";

export function registerStatsTools(server: McpServer, client: PlayniteClient): void {
  server.tool(
    "library_stats",
    "Get comprehensive library statistics — total games, playtime breakdown, top genres, sources, completion status",
    {},
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
          content: [{ type: "text" as const, text: lines.join("\n") }],
          _meta: { ui: { resourceUri: "ui://playnite/stats" } },
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
}
