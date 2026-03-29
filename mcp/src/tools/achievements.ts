import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { PlayniteClient } from "../client.js";

interface Achievement {
  name: string;
  description?: string;
  unlocked?: boolean;
  dateUnlocked?: string;
  percent?: number;
}

interface AchievementsResponse {
  achievements?: Achievement[];
  total?: number;
  unlocked?: number;
}

export function registerAchievementTools(server: McpServer, client: PlayniteClient): void {
  server.tool(
    "get_achievements",
    "Get achievements for a game (requires SuccessStory plugin)",
    {
      gameId: z.string().describe("The game's unique ID"),
    },
    async (args) => {
      try {
        // The Playnite Bridge API exposes achievements at /api/games/{id} in the detail response
        // SuccessStory data is included if the plugin is installed
        const game = await client.getGame(args.gameId);

        // Check if game detail includes achievements data
        const gameAny = game as unknown as Record<string, unknown>;
        const achData = gameAny.achievements as AchievementsResponse | undefined;

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
        const unlocked = achData.unlocked ?? achData.achievements.filter((a) => a.unlocked).length;
        const pct = total > 0 ? Math.round((unlocked / total) * 100) : 0;

        const lines = [
          `# Achievements for ${game.name}`,
          `**Progress:** ${unlocked}/${total} (${pct}%)`,
          "",
        ];

        for (const ach of achData.achievements) {
          const status = ach.unlocked ? "✅" : "⬜";
          const rarity = ach.percent != null ? ` (${ach.percent}% of players)` : "";
          const date = ach.dateUnlocked ? ` — ${ach.dateUnlocked}` : "";
          lines.push(`${status} **${ach.name}**${rarity}${date}`);
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
}
