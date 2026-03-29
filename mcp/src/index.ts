import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { PlayniteClient } from "./client.js";
import { registerGameTools } from "./tools/games.js";
import { registerAchievementTools } from "./tools/achievements.js";
import { registerActivityTools } from "./tools/activity.js";
import { registerStatsTools } from "./tools/stats.js";
import { registerCollectionTools } from "./tools/collections.js";
import { registerResources } from "./resources/game.js";

async function main(): Promise<void> {
  // Create client — will auto-resolve token from env or auth.json
  let client: PlayniteClient;
  try {
    client = new PlayniteClient();
  } catch (err) {
    process.stderr.write(
      `Warning: ${err instanceof Error ? err.message : String(err)}\n`
    );
    process.stderr.write("Server will start but API calls will fail.\n");
    // Create with dummy token so server starts
    client = new PlayniteClient("http://localhost:19821", "INVALID_TOKEN");
  }

  const server = new McpServer({
    name: "playnite",
    version: "1.0.0",
  });

  // Register all tools
  registerGameTools(server, client);
  registerAchievementTools(server, client);
  registerActivityTools(server, client);
  registerStatsTools(server, client);
  registerCollectionTools(server, client);

  // Register resources
  registerResources(server, client);

  // Connect via stdio
  const transport = new StdioServerTransport();
  await server.connect(transport);

  process.stderr.write("Playnite MCP server running on stdio\n");
}

main().catch((err) => {
  process.stderr.write(`Fatal error: ${err}\n`);
  process.exit(1);
});
