import { describe, it, expect } from "vitest";
import { readFileSync, existsSync } from "fs";
import { join } from "path";

const MCP_APP_HTML = join(__dirname, "..", "mcp-app.html");
const SERVER_MAIN = join(__dirname, "..", "server", "main.ts");

describe("MCP App HTML", () => {
  it("exists and is valid HTML", () => {
    expect(existsSync(MCP_APP_HTML)).toBe(true);
    const html = readFileSync(MCP_APP_HTML, "utf-8");
    expect(html).toContain("<!DOCTYPE html>");
    expect(html).toContain("</html>");
  });

  it("references mcp-app.ts module", () => {
    const html = readFileSync(MCP_APP_HTML, "utf-8");
    expect(html).toContain('type="module"');
    expect(html).toContain("mcp-app.ts");
  });

  it("uses dark theme colors", () => {
    const html = readFileSync(MCP_APP_HTML, "utf-8");
    expect(html).toContain("#0f1219");
    expect(html).toContain("#1a1f2e");
  });

  it("includes accent colors", () => {
    const html = readFileSync(MCP_APP_HTML, "utf-8");
    expect(html).toContain("#ff3d6a");
    expect(html).toContain("#53d8fb");
  });

  it("has detail overlay", () => {
    const html = readFileSync(MCP_APP_HTML, "utf-8");
    expect(html).toContain("detailOverlay");
    expect(html).toContain("detailContent");
  });
});

describe("Server main.ts", () => {
  it("exists", () => {
    expect(existsSync(SERVER_MAIN)).toBe(true);
  });

  it("uses StreamableHTTPServerTransport", () => {
    const src = readFileSync(SERVER_MAIN, "utf-8");
    expect(src).toContain("StreamableHTTPServerTransport");
    expect(src).not.toContain("StdioServerTransport");
  });

  it("uses @modelcontextprotocol/ext-apps", () => {
    const src = readFileSync(SERVER_MAIN, "utf-8");
    expect(src).toContain("registerAppTool");
    expect(src).toContain("registerAppResource");
    expect(src).toContain("RESOURCE_MIME_TYPE");
  });

  it("registers all 9 tools", () => {
    const src = readFileSync(SERVER_MAIN, "utf-8");
    const toolNames = [
      "search_games",
      "get_game",
      "query_games",
      "update_game",
      "get_achievements",
      "get_activity",
      "manage_categories",
      "list_collections",
      "library_stats",
    ];
    for (const name of toolNames) {
      expect(src).toContain(`"${name}"`);
    }
  });

  it("uses Express with CORS", () => {
    const src = readFileSync(SERVER_MAIN, "utf-8");
    expect(src).toContain("express");
    expect(src).toContain("cors");
  });

  it("has REST API endpoints", () => {
    const src = readFileSync(SERVER_MAIN, "utf-8");
    expect(src).toContain('"/api/games"');
    expect(src).toContain('"/api/stats"');
    expect(src).toContain('"/ui"');
  });

  it("has MCP endpoint", () => {
    const src = readFileSync(SERVER_MAIN, "utf-8");
    expect(src).toContain('"/mcp"');
    expect(src).toContain("handleRequest");
  });

  it("uses port 3849", () => {
    const src = readFileSync(SERVER_MAIN, "utf-8");
    expect(src).toContain("3849");
  });

  it("does not use console.log", () => {
    const src = readFileSync(SERVER_MAIN, "utf-8");
    expect(src).not.toMatch(/console\.log\(/);
  });
});

describe("MCP App client code", () => {
  const MCP_APP_TS = join(__dirname, "..", "src", "mcp-app.ts");

  it("exists", () => {
    expect(existsSync(MCP_APP_TS)).toBe(true);
  });

  it("imports App from @modelcontextprotocol/ext-apps", () => {
    const src = readFileSync(MCP_APP_TS, "utf-8");
    expect(src).toContain('@modelcontextprotocol/ext-apps');
    expect(src).toContain("new App(");
    expect(src).toContain("app.connect()");
  });

  it("handles ontoolresult", () => {
    const src = readFileSync(MCP_APP_TS, "utf-8");
    expect(src).toContain("app.ontoolresult");
  });

  it("can call server tools", () => {
    const src = readFileSync(MCP_APP_TS, "utf-8");
    expect(src).toContain("callServerTool");
  });

  it("has source color mapping", () => {
    const src = readFileSync(MCP_APP_TS, "utf-8");
    expect(src).toContain("Steam");
    expect(src).toContain("Epic Games");
    expect(src).toContain("Battle.net");
  });
});
