import { describe, it, expect, vi, beforeEach } from "vitest";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { PlayniteClient } from "../../src/client.js";
import { registerGameTools } from "../../src/tools/games.js";

// We'll test through the tool handlers directly by extracting them
describe("Game tools registration", () => {
  let server: McpServer;
  let client: PlayniteClient;

  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
    client = new PlayniteClient("http://localhost:19821", "test-token");
    server = new McpServer({ name: "test", version: "0.0.1" });
  });

  it("registers without error", () => {
    expect(() => registerGameTools(server, client)).not.toThrow();
  });
});

describe("search_games error handling", () => {
  it("returns error content when API is unreachable", async () => {
    const mockFetch = vi.fn().mockRejectedValue(new Error("ECONNREFUSED"));
    vi.stubGlobal("fetch", mockFetch);

    const client = new PlayniteClient("http://localhost:19821", "test-token");

    // Test the client method directly since we can't easily call tools
    await expect(client.searchGames({ q: "test" })).rejects.toThrow(
      "ECONNREFUSED"
    );
  });
});

describe("get_game error handling", () => {
  it("throws on 404", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 404,
      text: async () => '{"error":"Not found"}',
    });
    vi.stubGlobal("fetch", mockFetch);

    const client = new PlayniteClient("http://localhost:19821", "test-token");

    await expect(client.getGame("nonexistent")).rejects.toThrow(
      "Playnite API 404"
    );
  });
});

describe("update_game", () => {
  it("sends correct update payload", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ id: "abc", name: "Updated" }),
    });
    vi.stubGlobal("fetch", mockFetch);

    const client = new PlayniteClient("http://localhost:19821", "test-token");
    const result = await client.updateGame("abc", {
      name: "Updated",
      favorite: true,
    });

    expect(result.name).toBe("Updated");
    expect(mockFetch).toHaveBeenCalledWith(
      "http://localhost:19821/api/games/abc",
      expect.objectContaining({
        method: "PUT",
        body: JSON.stringify({ name: "Updated", favorite: true }),
      })
    );
  });
});
