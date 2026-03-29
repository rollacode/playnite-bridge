import { describe, it, expect, vi, beforeEach } from "vitest";
import { PlayniteClient } from "../src/client.js";

// Mock global fetch
const mockFetch = vi.fn();
vi.stubGlobal("fetch", mockFetch);

describe("PlayniteClient", () => {
  let client: PlayniteClient;

  beforeEach(() => {
    client = new PlayniteClient("http://localhost:19821", "test-token");
    mockFetch.mockReset();
  });

  it("should construct with explicit params", () => {
    expect(client.baseUrl).toBe("http://localhost:19821");
    expect(client.token).toBe("test-token");
  });

  it("GET sends authorization header", async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({ total: 0, games: [] }),
    });

    await client.get("/api/games");

    expect(mockFetch).toHaveBeenCalledWith(
      "http://localhost:19821/api/games",
      expect.objectContaining({
        headers: { Authorization: "Bearer test-token" },
      })
    );
  });

  it("POST sends JSON body and auth header", async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({ ok: true }),
    });

    await client.post("/api/games/123/categories", { categories: ["RPG"] });

    expect(mockFetch).toHaveBeenCalledWith(
      "http://localhost:19821/api/games/123/categories",
      expect.objectContaining({
        method: "POST",
        headers: {
          Authorization: "Bearer test-token",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ categories: ["RPG"] }),
      })
    );
  });

  it("PUT sends JSON body and auth header", async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({ name: "Test" }),
    });

    await client.put("/api/games/123", { name: "Test" });

    expect(mockFetch).toHaveBeenCalledWith(
      "http://localhost:19821/api/games/123",
      expect.objectContaining({
        method: "PUT",
        body: JSON.stringify({ name: "Test" }),
      })
    );
  });

  it("throws on non-OK response", async () => {
    mockFetch.mockResolvedValue({
      ok: false,
      status: 404,
      text: async () => '{"error":"Not found"}',
    });

    await expect(client.get("/api/games/bad-id")).rejects.toThrow(
      "Playnite API 404"
    );
  });

  it("searchGames builds query string", async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({ total: 1, offset: 0, limit: 50, games: [] }),
    });

    await client.searchGames({ q: "witcher", limit: 10, installed: true });

    const url = mockFetch.mock.calls[0][0] as string;
    expect(url).toContain("q=witcher");
    expect(url).toContain("limit=10");
    expect(url).toContain("installed=true");
  });

  it("getGame calls correct endpoint", async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({ id: "abc", name: "Test Game" }),
    });

    const game = await client.getGame("abc");
    expect(game.name).toBe("Test Game");
    expect(mockFetch.mock.calls[0][0]).toBe(
      "http://localhost:19821/api/games/abc"
    );
  });

  it("setCollection uses POST for add action", async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({ ok: true }),
    });

    await client.setCollection("abc", "tags", ["RPG", "Action"], "add");

    expect(mockFetch).toHaveBeenCalledWith(
      "http://localhost:19821/api/games/abc/tags",
      expect.objectContaining({ method: "POST" })
    );
  });

  it("setCollection uses PUT for set action", async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({ ok: true }),
    });

    await client.setCollection("abc", "categories", ["Indie"]);

    expect(mockFetch).toHaveBeenCalledWith(
      "http://localhost:19821/api/games/abc/categories",
      expect.objectContaining({ method: "PUT" })
    );
  });

  it("delete calls correct endpoint", async () => {
    mockFetch.mockResolvedValue({ ok: true });

    await client.delete("/api/games/abc");

    expect(mockFetch).toHaveBeenCalledWith(
      "http://localhost:19821/api/games/abc",
      expect.objectContaining({ method: "DELETE" })
    );
  });
});
