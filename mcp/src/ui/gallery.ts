/** Game gallery MCP App — grid of game cards with cover art */

export function buildGalleryHtml(): string {
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Playnite Gallery</title>
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
    background: #0f1219;
    color: #e4e8f0;
    min-height: 100vh;
    padding: 16px;
  }
  .header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 20px;
    flex-wrap: wrap;
    gap: 12px;
  }
  .header h1 {
    font-size: 22px;
    font-weight: 700;
    background: linear-gradient(135deg, #ff3d6a, #53d8fb);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
  }
  .search-bar {
    display: flex;
    gap: 8px;
  }
  .search-bar input {
    background: #1a1f2e;
    border: 1px solid #2a3040;
    border-radius: 8px;
    padding: 8px 14px;
    color: #e4e8f0;
    font-size: 14px;
    width: 260px;
    outline: none;
    transition: border-color 0.2s;
  }
  .search-bar input:focus {
    border-color: #53d8fb;
  }
  .search-bar button {
    background: linear-gradient(135deg, #ff3d6a, #53d8fb);
    border: none;
    border-radius: 8px;
    padding: 8px 18px;
    color: #fff;
    font-weight: 600;
    cursor: pointer;
    font-size: 14px;
    transition: opacity 0.2s;
  }
  .search-bar button:hover { opacity: 0.85; }
  .stats-bar {
    font-size: 13px;
    color: #8891a5;
    margin-bottom: 16px;
  }
  .grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    gap: 16px;
  }
  .card {
    background: #1a1f2e;
    border-radius: 12px;
    overflow: hidden;
    border: 1px solid #252b3d;
    transition: transform 0.2s, border-color 0.2s, box-shadow 0.2s;
    cursor: pointer;
  }
  .card:hover {
    transform: translateY(-3px);
    border-color: #53d8fb40;
    box-shadow: 0 8px 24px rgba(83, 216, 251, 0.08);
  }
  .card-img {
    width: 100%;
    aspect-ratio: 460/215;
    background: #252b3d;
    display: flex;
    align-items: center;
    justify-content: center;
    overflow: hidden;
    position: relative;
  }
  .card-img img {
    width: 100%;
    height: 100%;
    object-fit: cover;
  }
  .card-img .placeholder {
    font-size: 32px;
    color: #3a4260;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 2px;
  }
  .card-body {
    padding: 12px 14px;
  }
  .card-title {
    font-size: 15px;
    font-weight: 600;
    margin-bottom: 6px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .card-meta {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-wrap: wrap;
  }
  .badge {
    font-size: 11px;
    padding: 2px 8px;
    border-radius: 4px;
    font-weight: 600;
    color: #fff;
    white-space: nowrap;
  }
  .playtime {
    font-size: 12px;
    color: #8891a5;
  }
  .installed-dot {
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: #3dffe0;
    display: inline-block;
  }
  .fav-star { color: #ffcc00; font-size: 14px; }
  .loading {
    text-align: center;
    padding: 40px;
    color: #8891a5;
    font-size: 15px;
  }
  .detail-overlay {
    display: none;
    position: fixed;
    inset: 0;
    background: rgba(15, 18, 25, 0.92);
    z-index: 100;
    overflow-y: auto;
    padding: 24px;
  }
  .detail-overlay.active { display: block; }
  .detail-close {
    position: fixed;
    top: 16px;
    right: 20px;
    background: #252b3d;
    border: 1px solid #3a4260;
    border-radius: 8px;
    color: #e4e8f0;
    font-size: 18px;
    width: 36px;
    height: 36px;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 101;
  }
  .detail-content {
    max-width: 700px;
    margin: 0 auto;
  }
  .detail-hero {
    width: 100%;
    border-radius: 12px;
    margin-bottom: 20px;
  }
  .detail-content h2 {
    font-size: 24px;
    margin-bottom: 12px;
  }
  .detail-row {
    display: flex;
    gap: 8px;
    margin-bottom: 6px;
    font-size: 14px;
  }
  .detail-label {
    color: #8891a5;
    min-width: 100px;
  }
  .detail-value { color: #e4e8f0; }
  .progress-bar {
    width: 100%;
    height: 8px;
    background: #252b3d;
    border-radius: 4px;
    overflow: hidden;
    margin-top: 8px;
  }
  .progress-fill {
    height: 100%;
    background: linear-gradient(90deg, #ff3d6a, #3dffe0);
    border-radius: 4px;
    transition: width 0.3s;
  }
</style>
</head>
<body>
<div class="header">
  <h1>Playnite Library</h1>
  <div class="search-bar">
    <input id="searchInput" type="text" placeholder="Search games..." />
    <button onclick="doSearch()">Search</button>
  </div>
</div>
<div class="stats-bar" id="statsBar">Loading...</div>
<div class="grid" id="gamesGrid"></div>
<div class="loading" id="loading">Loading games...</div>

<div class="detail-overlay" id="detailOverlay">
  <button class="detail-close" onclick="closeDetail()">&times;</button>
  <div class="detail-content" id="detailContent"></div>
</div>

<script>
const SOURCE_COLORS = {
  Steam: '#1b2838', 'Epic Games': '#2a2a2a', Xbox: '#107c10',
  'Xbox Game Pass': '#107c10', 'EA Play': '#ff4747', 'EA app': '#ff4747',
  GOG: '#86328a', PlayStation: '#003087', Nintendo: '#e60012',
  Ubisoft: '#0070ff', 'Amazon Games': '#ff9900', 'Battle.net': '#00aeff',
  'itch.io': '#fa5c5c', 'Humble Bundle': '#cc2929'
};

function formatPlaytime(s) {
  if (!s || s <= 0) return '';
  const h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60);
  if (h === 0) return m + 'm';
  if (m === 0) return h + 'h';
  return h + 'h ' + m + 'm';
}

function getSteamImg(game) {
  if (game.source !== 'Steam') return null;
  if (game.links) {
    for (const l of game.links) {
      const m = l.url?.match(/\\/app\\/(\\d+)/);
      if (m) return 'https://steamcdn-a.akamaihd.net/steam/apps/' + m[1] + '/header.jpg';
    }
  }
  if (game.gameId && /^\\d+$/.test(game.gameId))
    return 'https://steamcdn-a.akamaihd.net/steam/apps/' + game.gameId + '/header.jpg';
  return null;
}

async function callTool(name, args) {
  const id = crypto.randomUUID();
  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error('Tool call timeout')), 15000);
    window.addEventListener('message', function handler(event) {
      if (event.data?.jsonrpc === '2.0' && event.data?.id === id) {
        window.removeEventListener('message', handler);
        clearTimeout(timeout);
        if (event.data.error) reject(new Error(event.data.error.message));
        else resolve(event.data.result);
      }
    });
    window.parent.postMessage({
      jsonrpc: '2.0', id,
      method: 'tools/call',
      params: { name, arguments: args }
    }, '*');
  });
}

let allGames = [];

async function loadGames(query) {
  const grid = document.getElementById('gamesGrid');
  const loading = document.getElementById('loading');
  loading.style.display = 'block';
  grid.innerHTML = '';
  try {
    const args = { limit: 200 };
    if (query) args.query = query;
    const result = await callTool('search_games', args);
    // Parse text response to extract game data
    // The tool returns structured data via MCP
    const text = result?.content?.[0]?.text || '';
    document.getElementById('statsBar').textContent = text.split('\\n')[0] || '';
    // Re-fetch with the raw API to get structured data
    // For now, render from the text
    loading.style.display = 'none';
    loading.textContent = 'Use search to find games';
  } catch (err) {
    loading.textContent = 'Could not load games. Is Playnite running?';
  }
}

function renderGames(games) {
  const grid = document.getElementById('gamesGrid');
  grid.innerHTML = '';
  document.getElementById('statsBar').textContent = games.length + ' games';
  for (const g of games) {
    const card = document.createElement('div');
    card.className = 'card';
    card.onclick = () => showDetail(g);
    const imgUrl = getSteamImg(g);
    const srcColor = SOURCE_COLORS[g.source] || '#555';
    card.innerHTML = \`
      <div class="card-img">
        \${imgUrl ? '<img src="' + imgUrl + '" alt="" loading="lazy">' :
          '<span class="placeholder">' + (g.name || '?').substring(0, 3) + '</span>'}
      </div>
      <div class="card-body">
        <div class="card-title">\${esc(g.name)}</div>
        <div class="card-meta">
          \${g.source ? '<span class="badge" style="background:' + srcColor + '">' + esc(g.source) + '</span>' : ''}
          \${g.playtime ? '<span class="playtime">' + formatPlaytime(g.playtime) + '</span>' : ''}
          \${g.isInstalled ? '<span class="installed-dot" title="Installed"></span>' : ''}
          \${g.favorite ? '<span class="fav-star">★</span>' : ''}
        </div>
      </div>
    \`;
    grid.appendChild(card);
  }
}

function esc(s) { const d = document.createElement('div'); d.textContent = s || ''; return d.innerHTML; }

function doSearch() {
  const q = document.getElementById('searchInput').value.trim();
  loadGames(q || undefined);
}

function showDetail(game) {
  const overlay = document.getElementById('detailOverlay');
  const content = document.getElementById('detailContent');
  const imgUrl = getSteamImg(game);
  content.innerHTML = \`
    \${imgUrl ? '<img class="detail-hero" src="' + imgUrl + '" alt="">' : ''}
    <h2>\${esc(game.name)}</h2>
    <div class="detail-row"><span class="detail-label">Source</span><span class="detail-value">\${esc(game.source || 'Unknown')}</span></div>
    <div class="detail-row"><span class="detail-label">Playtime</span><span class="detail-value">\${formatPlaytime(game.playtime) || 'Never played'}</span></div>
    <div class="detail-row"><span class="detail-label">Status</span><span class="detail-value">\${esc(game.completionStatus || 'Not set')}</span></div>
    \${game.genres?.length ? '<div class="detail-row"><span class="detail-label">Genres</span><span class="detail-value">' + game.genres.map(esc).join(', ') + '</span></div>' : ''}
    \${game.categories?.length ? '<div class="detail-row"><span class="detail-label">Categories</span><span class="detail-value">' + game.categories.map(esc).join(', ') + '</span></div>' : ''}
  \`;
  overlay.classList.add('active');
}

function closeDetail() {
  document.getElementById('detailOverlay').classList.remove('active');
}

document.getElementById('searchInput').addEventListener('keydown', e => {
  if (e.key === 'Enter') doSearch();
});

loadGames();
</script>
</body>
</html>`;
}
