import { describe, it, expect, vi, beforeEach } from "vitest";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { PlayniteClient } from "../../src/client.js";
import { registerStatsTools } from "../../src/tools/stats.js";

describe("Stats tools", () => {
  let server: McpServer;
  let client: PlayniteClient;

  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
    client = new PlayniteClient("http://localhost:19821", "test-token");
    server = new McpServer({ name: "test", version: "0.0.1" });
  });

  it("registers without error", () => {
    expect(() => registerStatsTools(server, client)).not.toThrow();
  });
});

describe("Stats data", () => {
  it("getStats returns structured data", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        totalGames: 150,
        installed: 45,
        favorites: 12,
        totalPlaytime: 360000,
        bySource: { Steam: 100, GOG: 30, "Epic Games": 20 },
        byCompletionStatus: { Played: 80, Completed: 30, "Not Played": 40 },
        topGenres: [
          { name: "Action", count: 60 },
          { name: "RPG", count: 40 },
        ],
        recentlyPlayed: [
          { id: "1", name: "Witcher 3", playtime: 72000 },
        ],
      }),
    });
    vi.stubGlobal("fetch", mockFetch);

    const client = new PlayniteClient("http://localhost:19821", "test-token");
    const stats = await client.getStats();

    expect(stats.totalGames).toBe(150);
    expect(stats.installed).toBe(45);
    expect(stats.bySource.Steam).toBe(100);
    expect(stats.topGenres).toHaveLength(2);
    expect(stats.recentlyPlayed).toHaveLength(1);
  });

  it("throws on API error", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      text: async () => '{"error":"Internal error"}',
    });
    vi.stubGlobal("fetch", mockFetch);

    const client = new PlayniteClient("http://localhost:19821", "test-token");
    await expect(client.getStats()).rejects.toThrow("Playnite API 500");
  });
});
