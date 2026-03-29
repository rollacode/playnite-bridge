import { describe, it, expect } from "vitest";
import {
  formatPlaytime,
  formatDate,
  getSteamImageUrl,
  formatGameLine,
  formatGameDetail,
  getSourceColor,
} from "../src/utils/format.js";

describe("formatPlaytime", () => {
  it("returns 'Never played' for 0 or undefined", () => {
    expect(formatPlaytime(0)).toBe("Never played");
    expect(formatPlaytime(undefined)).toBe("Never played");
    expect(formatPlaytime(-1)).toBe("Never played");
  });

  it("formats minutes only", () => {
    expect(formatPlaytime(300)).toBe("5m");
  });

  it("formats hours only", () => {
    expect(formatPlaytime(7200)).toBe("2h");
  });

  it("formats hours and minutes", () => {
    expect(formatPlaytime(5400)).toBe("1h 30m");
  });
});

describe("formatDate", () => {
  it("returns 'Unknown' for falsy input", () => {
    expect(formatDate(undefined)).toBe("Unknown");
    expect(formatDate("")).toBe("Unknown");
  });

  it("formats ISO date string", () => {
    const result = formatDate("2024-03-15T12:00:00Z");
    expect(result).toContain("2024");
    expect(result).toContain("Mar");
  });
});

describe("getSteamImageUrl", () => {
  it("returns null for non-Steam games", () => {
    expect(getSteamImageUrl({ id: "1", name: "Test", source: "GOG" })).toBeNull();
  });

  it("extracts Steam app ID from links", () => {
    const url = getSteamImageUrl({
      id: "1",
      name: "Test",
      source: "Steam",
      links: [{ name: "Steam", url: "https://store.steampowered.com/app/292030/" }],
    });
    expect(url).toBe("https://steamcdn-a.akamaihd.net/steam/apps/292030/header.jpg");
  });

  it("falls back to gameId", () => {
    const url = getSteamImageUrl({
      id: "1",
      name: "Test",
      source: "Steam",
      gameId: "292030",
    });
    expect(url).toBe("https://steamcdn-a.akamaihd.net/steam/apps/292030/header.jpg");
  });

  it("returns null for Steam game without links or gameId", () => {
    expect(getSteamImageUrl({ id: "1", name: "Test", source: "Steam" })).toBeNull();
  });
});

describe("formatGameLine", () => {
  it("includes name, source, playtime", () => {
    const line = formatGameLine({
      id: "1",
      name: "Witcher 3",
      source: "Steam",
      playtime: 72000,
      isInstalled: true,
      favorite: true,
    });
    expect(line).toContain("Witcher 3");
    expect(line).toContain("[Steam]");
    expect(line).toContain("20h");
    expect(line).toContain("installed");
    expect(line).toContain("★");
  });
});

describe("formatGameDetail", () => {
  it("produces markdown-formatted detail", () => {
    const detail = formatGameDetail({
      id: "1",
      name: "Elden Ring",
      source: "Steam",
      genres: ["Action", "RPG"],
      playtime: 360000,
      completionStatus: "Completed",
    });
    expect(detail).toContain("# Elden Ring");
    expect(detail).toContain("**Source:** Steam");
    expect(detail).toContain("Action, RPG");
    expect(detail).toContain("100h");
    expect(detail).toContain("Completed");
  });

  it("strips HTML from description", () => {
    const detail = formatGameDetail({
      id: "1",
      name: "Test",
      description: "<p>Hello <b>world</b></p>",
    });
    expect(detail).toContain("Hello world");
    expect(detail).not.toContain("<p>");
  });
});

describe("getSourceColor", () => {
  it("returns correct color for known sources", () => {
    expect(getSourceColor("Steam")).toBe("#1b2838");
    expect(getSourceColor("GOG")).toBe("#86328a");
  });

  it("returns default for unknown source", () => {
    expect(getSourceColor("SomeStore")).toBe("#555555");
    expect(getSourceColor(undefined)).toBe("#555555");
  });
});
