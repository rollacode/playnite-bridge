import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { PlayniteClient } from "../client.js";

export function registerCollectionTools(server: McpServer, client: PlayniteClient): void {
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
      values: z
        .array(z.string())
        .describe("The values to set or add"),
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
}
