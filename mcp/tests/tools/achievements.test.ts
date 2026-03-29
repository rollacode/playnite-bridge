import { describe, it, expect, vi, beforeEach } from "vitest";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { PlayniteClient } from "../../src/client.js";
import { registerAchievementTools } from "../../src/tools/achievements.js";

describe("Achievement tools", () => {
  let server: McpServer;
  let client: PlayniteClient;

  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
    client = new PlayniteClient("http://localhost:19821", "test-token");
    server = new McpServer({ name: "test", version: "0.0.1" });
  });

  it("registers without error", () => {
    expect(() => registerAchievementTools(server, client)).not.toThrow();
  });
});

describe("Achievement data parsing", () => {
  it("handles game with no achievements", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ id: "abc", name: "Test Game" }),
    });
    vi.stubGlobal("fetch", mockFetch);

    const client = new PlayniteClient("http://localhost:19821", "test-token");
    const game = await client.getGame("abc");

    // Game has no achievements field — this is the expected case
    expect((game as Record<string, unknown>).achievements).toBeUndefined();
  });

  it("handles game with achievements data", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        id: "abc",
        name: "Test Game",
        achievements: {
          total: 10,
          unlocked: 3,
          achievements: [
            { name: "First Blood", unlocked: true, percent: 80 },
            { name: "Master", unlocked: false, percent: 5 },
          ],
        },
      }),
    });
    vi.stubGlobal("fetch", mockFetch);

    const client = new PlayniteClient("http://localhost:19821", "test-token");
    const game = await client.getGame("abc");
    const data = (game as unknown as Record<string, unknown>).achievements as {
      total: number;
      unlocked: number;
      achievements: Array<{ name: string; unlocked: boolean }>;
    };

    expect(data.total).toBe(10);
    expect(data.unlocked).toBe(3);
    expect(data.achievements).toHaveLength(2);
    expect(data.achievements[0].name).toBe("First Blood");
  });
});
