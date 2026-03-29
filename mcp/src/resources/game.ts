import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { PlayniteClient } from "../client.js";
import { formatGameDetail, getSteamImageUrl } from "../utils/format.js";
import { buildGalleryHtml } from "../ui/gallery.js";
import { buildStatsDashboardHtml } from "../ui/stats-dashboard.js";
import { buildGameDetailHtml } from "../ui/game-detail.js";

export function registerResources(server: McpServer, client: PlayniteClient): void {
  // Game data resource
  server.resource(
    "game",
    "game://{gameId}",
    { description: "Game data by ID" },
    async (uri) => {
      const gameId = uri.pathname.replace(/^\/\//, "");
      const game = await client.getGame(gameId);
      const detail = formatGameDetail(game);
      const imageUrl = getSteamImageUrl(game);

      return {
        contents: [
          {
            uri: uri.href,
            mimeType: "text/plain",
            text: imageUrl ? `${detail}\n\nCover: ${imageUrl}` : detail,
          },
        ],
      };
    }
  );

  // UI resource: gallery
  server.resource(
    "gallery-ui",
    "ui://playnite/gallery",
    { description: "Game gallery with cover art cards" },
    async (uri) => {
      return {
        contents: [
          {
            uri: uri.href,
            mimeType: "text/html",
            text: buildGalleryHtml(),
          },
        ],
      };
    }
  );

  // UI resource: stats dashboard
  server.resource(
    "stats-ui",
    "ui://playnite/stats",
    { description: "Library statistics dashboard" },
    async (uri) => {
      return {
        contents: [
          {
            uri: uri.href,
            mimeType: "text/html",
            text: buildStatsDashboardHtml(),
          },
        ],
      };
    }
  );

  // UI resource: game detail
  server.resource(
    "game-detail-ui",
    "ui://playnite/game",
    { description: "Single game detail view" },
    async (uri) => {
      return {
        contents: [
          {
            uri: uri.href,
            mimeType: "text/html",
            text: buildGameDetailHtml(),
          },
        ],
      };
    }
  );
}
