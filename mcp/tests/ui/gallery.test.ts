import { describe, it, expect } from "vitest";
import { buildGalleryHtml } from "../../src/ui/gallery.js";
import { buildStatsDashboardHtml } from "../../src/ui/stats-dashboard.js";
import { buildGameDetailHtml } from "../../src/ui/game-detail.js";

describe("Gallery HTML", () => {
  const html = buildGalleryHtml();

  it("returns valid HTML document", () => {
    expect(html).toContain("<!DOCTYPE html>");
    expect(html).toContain("</html>");
  });

  it("contains search input", () => {
    expect(html).toContain('id="searchInput"');
  });

  it("contains games grid container", () => {
    expect(html).toContain('id="gamesGrid"');
  });

  it("uses dark theme colors", () => {
    expect(html).toContain("#0f1219");
    expect(html).toContain("#1a1f2e");
  });

  it("includes accent colors", () => {
    expect(html).toContain("#ff3d6a");
    expect(html).toContain("#53d8fb");
    expect(html).toContain("#3dffe0");
  });

  it("contains MCP tool call function", () => {
    expect(html).toContain("callTool");
    expect(html).toContain("tools/call");
    expect(html).toContain("postMessage");
  });

  it("contains source color mapping", () => {
    expect(html).toContain("Steam");
    expect(html).toContain("Epic Games");
    expect(html).toContain("GOG");
  });

  it("is under 100KB", () => {
    expect(html.length).toBeLessThan(100 * 1024);
  });
});

describe("Stats Dashboard HTML", () => {
  const html = buildStatsDashboardHtml();

  it("returns valid HTML document", () => {
    expect(html).toContain("<!DOCTYPE html>");
    expect(html).toContain("Library Dashboard");
  });

  it("contains dashboard container", () => {
    expect(html).toContain('id="dashboard"');
  });

  it("uses dark theme", () => {
    expect(html).toContain("#0f1219");
  });

  it("is under 100KB", () => {
    expect(html.length).toBeLessThan(100 * 1024);
  });
});

describe("Game Detail HTML", () => {
  const html = buildGameDetailHtml();

  it("returns valid HTML document", () => {
    expect(html).toContain("<!DOCTYPE html>");
    expect(html).toContain("Game Detail");
  });

  it("contains detail container", () => {
    expect(html).toContain('id="detail"');
  });

  it("contains hero image styles", () => {
    expect(html).toContain(".hero");
    expect(html).toContain(".hero-placeholder");
  });

  it("contains progress bar styles", () => {
    expect(html).toContain(".progress-track");
    expect(html).toContain(".progress-fill");
  });

  it("is under 100KB", () => {
    expect(html.length).toBeLessThan(100 * 1024);
  });
});
