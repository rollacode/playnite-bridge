import { App } from "@modelcontextprotocol/ext-apps";

const appEl = document.getElementById("app")!;

const SOURCE_COLORS: Record<string, string> = {
  Steam: "#1b2838",
  "Epic Games": "#2a2a2a",
  Xbox: "#107c10",
  "Xbox Game Pass": "#107c10",
  "EA Play": "#ff4747",
  "EA app": "#ff4747",
  GOG: "#86328a",
  PlayStation: "#003087",
  Nintendo: "#e60012",
  Ubisoft: "#0070ff",
  "Amazon Games": "#ff9900",
  "Battle.net": "#00aeff",
  "itch.io": "#fa5c5c",
  "Humble Bundle": "#cc2929",
};

interface Game {
  id: string;
  name: string;
  source?: string;
  genres?: string[];
  categories?: string[];
  tags?: string[];
  features?: string[];
  platforms?: string[];
  completionStatus?: string;
  isInstalled?: boolean;
  favorite?: boolean;
  playtime?: number;
  playCount?: number;
  lastActivity?: string;
  userScore?: number;
  description?: string;
  developers?: string[];
  publishers?: string[];
  releaseDate?: string;
  communityScore?: number;
  criticScore?: number;
  links?: Array<{ name: string; url: string }>;
  gameId?: string;
}

interface Stats {
  totalGames: number;
  installed: number;
  favorites: number;
  totalPlaytime: number;
  bySource: Record<string, number>;
  byCompletionStatus: Record<string, number>;
  topGenres: Array<{ name: string; count: number }>;
  recentlyPlayed: Game[];
}

function esc(s: string | undefined): string {
  const d = document.createElement("div");
  d.textContent = s || "";
  return d.innerHTML;
}

function formatPlaytime(s?: number): string {
  if (!s || s <= 0) return "";
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  if (h === 0) return m + "m";
  if (m === 0) return h + "h";
  return h + "h " + m + "m";
}

function formatPlaytimeFull(s?: number): string {
  if (!s || s <= 0) return "Never played";
  return formatPlaytime(s);
}

function getSteamImg(game: Game): string | null {
  if (game.source !== "Steam") return null;
  if (game.links) {
    for (const l of game.links) {
      const m = l.url?.match(/\/app\/(\d+)/);
      if (m)
        return (
          "https://steamcdn-a.akamaihd.net/steam/apps/" + m[1] + "/header.jpg"
        );
    }
  }
  if (game.gameId && /^\d+$/.test(game.gameId))
    return (
      "https://steamcdn-a.akamaihd.net/steam/apps/" +
      game.gameId +
      "/header.jpg"
    );
  return null;
}

// ---------------------------------------------------------------------------
// MCP App connection
// ---------------------------------------------------------------------------

const app = new App({ name: "Playnite Bridge", version: "2.0.0" });
app.connect();

app.ontoolresult = (result: { content?: Array<{ type: string; text?: string }> }) => {
  const texts = result.content?.filter((c) => c.type === "text").map((c) => c.text || "") || [];
  const fullText = texts.join("\n");

  // Try to extract embedded JSON data
  const gamesMatch = fullText.match(/<!--GAMES_JSON:([\s\S]*?)-->/);
  if (gamesMatch) {
    try {
      const games: Game[] = JSON.parse(gamesMatch[1]);
      renderGames(games);
      return;
    } catch {
      // Fall through
    }
  }

  const gameMatch = fullText.match(/<!--GAME_JSON:([\s\S]*?)-->/);
  if (gameMatch) {
    try {
      const game: Game = JSON.parse(gameMatch[1]);
      showDetail(game);
      return;
    } catch {
      // Fall through
    }
  }

  const statsMatch = fullText.match(/<!--STATS_JSON:([\s\S]*?)-->/);
  if (statsMatch) {
    try {
      const stats: Stats = JSON.parse(statsMatch[1]);
      renderStats(stats);
      return;
    } catch {
      // Fall through
    }
  }

  // Fallback: render as text
  appEl.innerHTML =
    '<pre style="white-space:pre-wrap;font-family:inherit;color:#c0c8d8;line-height:1.6;padding:16px">' +
    esc(texts[0] || "No data") +
    "</pre>";
};

// ---------------------------------------------------------------------------
// Render: Game Gallery
// ---------------------------------------------------------------------------

function renderGames(games: Game[]) {
  if (!games.length) {
    appEl.innerHTML = '<div class="empty">No games found</div>';
    return;
  }

  let html = `
    <div class="header">
      <h1>Playnite Library</h1>
    </div>
    <div class="stats-bar">${games.length} games</div>
    <div class="grid">
  `;

  for (const g of games) {
    const imgUrl = getSteamImg(g);
    const srcColor = SOURCE_COLORS[g.source || ""] || "#555";
    html += `
      <div class="card" data-game-id="${esc(g.id)}">
        <div class="card-img">
          ${
            imgUrl
              ? '<img src="' + imgUrl + '" alt="" loading="lazy">'
              : '<span class="placeholder">' +
                esc((g.name || "?").substring(0, 3)) +
                "</span>"
          }
        </div>
        <div class="card-body">
          <div class="card-title">${esc(g.name)}</div>
          <div class="card-meta">
            ${g.source ? '<span class="badge" style="background:' + srcColor + '">' + esc(g.source) + "</span>" : ""}
            ${g.playtime ? '<span class="playtime">' + formatPlaytime(g.playtime) + "</span>" : ""}
            ${g.isInstalled ? '<span class="installed-dot" title="Installed"></span>' : ""}
            ${g.favorite ? '<span class="fav-star">*</span>' : ""}
          </div>
        </div>
      </div>
    `;
  }

  html += "</div>";
  appEl.innerHTML = html;

  // Bind click to fetch detail via MCP
  appEl.querySelectorAll(".card").forEach((card) => {
    card.addEventListener("click", async () => {
      const gameId = (card as HTMLElement).dataset.gameId;
      if (!gameId) return;
      try {
        const result = await app.callServerTool({
          name: "get_game",
          arguments: { gameId },
        });
        const texts =
          result.content
            ?.filter((c: { type: string }) => c.type === "text")
            .map((c: { text?: string }) => c.text || "") || [];
        const text = texts.join("\n");
        const match = text.match(/<!--GAME_JSON:([\s\S]*?)-->/);
        if (match) {
          showDetail(JSON.parse(match[1]));
        }
      } catch (err) {
        console.error("Failed to load game:", err);
      }
    });
  });
}

// ---------------------------------------------------------------------------
// Render: Game Detail
// ---------------------------------------------------------------------------

function showDetail(game: Game) {
  const overlay = document.getElementById("detailOverlay")!;
  const content = document.getElementById("detailContent")!;
  const imgUrl = getSteamImg(game);

  let html = "";
  if (imgUrl) {
    html += `<img class="detail-hero" src="${imgUrl}" alt="">`;
  }
  html += `<h2>${esc(game.name)}</h2>`;

  const rows: [string, string][] = [
    ["Source", game.source || "Unknown"],
    ["Playtime", formatPlaytimeFull(game.playtime)],
    ["Status", game.completionStatus || "Not set"],
  ];
  if (game.developers?.length)
    rows.push(["Developers", game.developers.join(", ")]);
  if (game.publishers?.length)
    rows.push(["Publishers", game.publishers.join(", ")]);
  if (game.platforms?.length)
    rows.push(["Platforms", game.platforms.join(", ")]);
  if (game.releaseDate) rows.push(["Release", game.releaseDate]);
  if (game.playCount) rows.push(["Play count", String(game.playCount)]);
  if (game.userScore != null)
    rows.push(["User score", game.userScore + "/100"]);
  if (game.communityScore != null)
    rows.push(["Community", game.communityScore + "/100"]);
  if (game.criticScore != null)
    rows.push(["Critic", game.criticScore + "/100"]);

  for (const [label, value] of rows) {
    html += `<div class="detail-row"><span class="detail-label">${esc(label)}</span><span class="detail-value">${esc(value)}</span></div>`;
  }

  if (game.genres?.length) {
    html += '<div class="tags">';
    for (const g of game.genres) {
      html += `<span class="tag">${esc(g)}</span>`;
    }
    html += "</div>";
  }

  if (game.categories?.length) {
    html +=
      '<div style="margin-top:8px;font-size:12px;color:#8891a5">Categories: ' +
      game.categories.map(esc).join(", ") +
      "</div>";
  }

  if (game.description) {
    const clean = game.description.replace(/<[^>]+>/g, "").substring(0, 500);
    html += `<div style="margin-top:16px;background:#1a1f2e;border-radius:10px;padding:14px;font-size:14px;line-height:1.6;color:#c0c8d8;border:1px solid #252b3d">${esc(clean)}</div>`;
  }

  content.innerHTML = html;
  overlay.classList.add("active");
}

document.getElementById("detailClose")?.addEventListener("click", () => {
  document.getElementById("detailOverlay")?.classList.remove("active");
});

// ---------------------------------------------------------------------------
// Render: Stats Dashboard
// ---------------------------------------------------------------------------

function renderStats(stats: Stats) {
  let html = `
    <div class="header"><h1>Library Dashboard</h1></div>
    <div class="kpi-row">
      <div class="kpi"><div class="kpi-value">${stats.totalGames}</div><div class="kpi-label">Total Games</div></div>
      <div class="kpi"><div class="kpi-value">${stats.installed}</div><div class="kpi-label">Installed</div></div>
      <div class="kpi"><div class="kpi-value">${stats.favorites}</div><div class="kpi-label">Favorites</div></div>
      <div class="kpi"><div class="kpi-value">${formatPlaytimeFull(stats.totalPlaytime)}</div><div class="kpi-label">Total Playtime</div></div>
    </div>
  `;

  // Sources bar chart
  if (stats.bySource) {
    const entries = Object.entries(stats.bySource)
      .sort(([, a], [, b]) => b - a)
      .slice(0, 10);
    const maxVal = entries.length > 0 ? entries[0][1] : 1;
    html += '<div class="section"><h2>Games by Source</h2><div class="bar-chart">';
    const gradients = ["g1", "g2", "g3", "g4", "g5"];
    entries.forEach(([name, count], i) => {
      const pct = Math.round((count / maxVal) * 100);
      html += `
        <div class="bar-row">
          <span class="bar-label">${esc(name)}</span>
          <div class="bar-track"><div class="bar-fill ${gradients[i % gradients.length]}" style="width:${pct}%"></div></div>
          <span class="bar-count">${count}</span>
        </div>`;
    });
    html += "</div></div>";
  }

  // Genres
  if (stats.topGenres?.length) {
    const maxG = stats.topGenres[0].count;
    const gradients = ["g1", "g2", "g3", "g4", "g5"];
    html += '<div class="section"><h2>Top Genres</h2><div class="bar-chart">';
    stats.topGenres.slice(0, 10).forEach((g, i) => {
      const pct = Math.round((g.count / maxG) * 100);
      html += `
        <div class="bar-row">
          <span class="bar-label">${esc(g.name)}</span>
          <div class="bar-track"><div class="bar-fill ${gradients[i % gradients.length]}" style="width:${pct}%"></div></div>
          <span class="bar-count">${g.count}</span>
        </div>`;
    });
    html += "</div></div>";
  }

  // Completion status
  if (stats.byCompletionStatus) {
    const entries = Object.entries(stats.byCompletionStatus)
      .sort(([, a], [, b]) => b - a)
      .slice(0, 8);
    const maxVal = entries.length > 0 ? entries[0][1] : 1;
    const gradients = ["g1", "g2", "g3", "g4", "g5"];
    html +=
      '<div class="section"><h2>Completion Status</h2><div class="bar-chart">';
    entries.forEach(([name, count], i) => {
      const pct = Math.round((count / maxVal) * 100);
      html += `
        <div class="bar-row">
          <span class="bar-label">${esc(name)}</span>
          <div class="bar-track"><div class="bar-fill ${gradients[i % gradients.length]}" style="width:${pct}%"></div></div>
          <span class="bar-count">${count}</span>
        </div>`;
    });
    html += "</div></div>";
  }

  // Recently played
  if (stats.recentlyPlayed?.length) {
    html += '<div class="section"><h2>Recently Played</h2>';
    for (const g of stats.recentlyPlayed.slice(0, 10)) {
      html += `
        <div class="recent-item">
          <span class="recent-name">${esc(g.name)}</span>
          <span class="recent-meta">${formatPlaytimeFull(g.playtime)}${g.source ? " -- " + esc(g.source) : ""}</span>
        </div>`;
    }
    html += "</div>";
  }

  appEl.innerHTML = html;
}
