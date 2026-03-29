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

// Placeholder gradient colors based on first letter hash
const PLACEHOLDER_GRADIENTS = [
  ["#2d1b4e", "#4a2d6e"],
  ["#1b2e4e", "#2d4a6e"],
  ["#1b4e3a", "#2d6e52"],
  ["#4e1b2d", "#6e2d4a"],
  ["#4e3a1b", "#6e522d"],
  ["#1b3a4e", "#2d526e"],
  ["#3a1b4e", "#522d6e"],
  ["#4e1b1b", "#6e2d2d"],
];

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
  coverUrl?: string;
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

function getSteamCoverUrl(game: Game): string | null {
  if (game.source !== "Steam") return null;
  // Extract Steam app ID for portrait cover (library_600x900)
  if (game.links) {
    for (const l of game.links) {
      const m = l.url?.match(/\/app\/(\d+)/);
      if (m)
        return `https://steamcdn-a.akamaihd.net/steam/apps/${m[1]}/library_600x900_2x.jpg`;
    }
  }
  if (game.gameId && /^\d+$/.test(game.gameId))
    return `https://steamcdn-a.akamaihd.net/steam/apps/${game.gameId}/library_600x900_2x.jpg`;
  return null;
}

function getSteamHeaderUrl(game: Game): string | null {
  if (game.source !== "Steam") return null;
  if (game.links) {
    for (const l of game.links) {
      const m = l.url?.match(/\/app\/(\d+)/);
      if (m)
        return `https://steamcdn-a.akamaihd.net/steam/apps/${m[1]}/header.jpg`;
    }
  }
  if (game.gameId && /^\d+$/.test(game.gameId))
    return `https://steamcdn-a.akamaihd.net/steam/apps/${game.gameId}/header.jpg`;
  return null;
}

function getPlaceholderStyle(name: string): string {
  const code = (name || "?").charCodeAt(0) % PLACEHOLDER_GRADIENTS.length;
  const [c1, c2] = PLACEHOLDER_GRADIENTS[code];
  return `background: linear-gradient(135deg, ${c1}, ${c2})`;
}

// ---------------------------------------------------------------------------
// MCP App connection
// ---------------------------------------------------------------------------

const app = new App({ name: "Playnite Bridge", version: "2.0.0" });
app.connect();

app.ontoolresult = (result: { content?: Array<{ type: string; text?: string }> }) => {
  const texts = result.content?.filter((c) => c.type === "text").map((c) => c.text || "") || [];
  const fullText = texts.join("\n");

  const gamesMatch = fullText.match(/<!--GAMES_JSON:([\s\S]*?)-->/);
  if (gamesMatch) {
    try {
      const games: Game[] = JSON.parse(gamesMatch[1]);
      renderGames(games);
      return;
    } catch { /* Fall through */ }
  }

  const gameMatch = fullText.match(/<!--GAME_JSON:([\s\S]*?)-->/);
  if (gameMatch) {
    try {
      const game: Game = JSON.parse(gameMatch[1]);
      showDetail(game);
      return;
    } catch { /* Fall through */ }
  }

  const statsMatch = fullText.match(/<!--STATS_JSON:([\s\S]*?)-->/);
  if (statsMatch) {
    try {
      const stats: Stats = JSON.parse(statsMatch[1]);
      renderStats(stats);
      return;
    } catch { /* Fall through */ }
  }

  appEl.innerHTML =
    '<pre style="white-space:pre-wrap;font-family:inherit;color:#8891a5;line-height:1.5;padding:12px;font-size:12px">' +
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
    const coverUrl = g.coverUrl || getSteamCoverUrl(g);
    const srcColor = SOURCE_COLORS[g.source || ""] || "#3a4260";
    const placeholderLetter = esc((g.name || "?").substring(0, 1));
    const placeholderStyle = getPlaceholderStyle(g.name || "?");

    html += `
      <div class="card" data-game-id="${esc(g.id)}">
        <div class="card-img"${!coverUrl ? ` style="${placeholderStyle}"` : ""}>
          ${
            coverUrl
              ? `<img src="${coverUrl}" alt="" loading="lazy" onerror="this.parentElement.innerHTML='<span class=placeholder>${placeholderLetter}</span>';this.parentElement.style='${placeholderStyle}'">`
              : `<span class="placeholder">${placeholderLetter}</span>`
          }
          ${g.isInstalled ? '<span class="card-installed"></span>' : ""}
        </div>
        <div class="card-body">
          <div class="card-title" title="${esc(g.name)}">${esc(g.name)}</div>
          <div class="card-meta">
            ${g.source ? `<span class="badge" style="background:${srcColor}">${esc(g.source)}</span>` : ""}
            ${g.favorite ? '<span class="fav-star">*</span>' : ""}
            ${g.playtime ? `<span class="playtime">${formatPlaytime(g.playtime)}</span>` : ""}
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
  const headerUrl = getSteamHeaderUrl(game);

  let html = "";
  if (headerUrl) {
    html += `<img class="detail-hero" src="${headerUrl}" alt="">`;
  }
  html += `<h2>${esc(game.name)}</h2>`;

  // Launch button
  html += `<button class="launch-btn" data-game-id="${esc(game.id)}">&#9654; Launch Game</button>`;

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
      '<div style="margin-top:6px;font-size:10px;color:#5a6378">Categories: ' +
      game.categories.map(esc).join(", ") +
      "</div>";
  }

  if (game.description) {
    const clean = game.description.replace(/<[^>]+>/g, "").substring(0, 400);
    html += `<div style="margin-top:10px;background:#1a1f2e;border-radius:6px;padding:10px;font-size:12px;line-height:1.5;color:#8891a5;border:1px solid #252b3d">${esc(clean)}</div>`;
  }

  content.innerHTML = html;
  overlay.classList.add("active");

  // Wire up launch button
  const launchBtn = content.querySelector(".launch-btn") as HTMLButtonElement;
  if (launchBtn) {
    launchBtn.addEventListener("click", async (e) => {
      e.stopPropagation();
      const gameId = launchBtn.dataset.gameId;
      if (!gameId) return;
      launchBtn.classList.add("launching");
      launchBtn.textContent = "Launching...";
      try {
        await app.callServerTool({
          name: "launch_game",
          arguments: { gameId },
        });
        launchBtn.textContent = "Launched!";
        setTimeout(() => {
          launchBtn.classList.remove("launching");
          launchBtn.innerHTML = "&#9654; Launch Game";
        }, 2000);
      } catch (err) {
        console.error("Failed to launch game:", err);
        launchBtn.textContent = "Launch failed";
        launchBtn.classList.remove("launching");
        setTimeout(() => {
          launchBtn.innerHTML = "&#9654; Launch Game";
        }, 2000);
      }
    });
  }
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
