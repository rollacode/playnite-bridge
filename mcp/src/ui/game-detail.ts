/** Game detail MCP App — single game view with cover art and metadata */

export function buildGameDetailHtml(): string {
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Game Detail</title>
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
    background: #0f1219;
    color: #e4e8f0;
    min-height: 100vh;
    padding: 20px;
  }
  .hero {
    width: 100%;
    max-height: 300px;
    object-fit: cover;
    border-radius: 12px;
    margin-bottom: 20px;
  }
  .hero-placeholder {
    width: 100%;
    height: 200px;
    background: linear-gradient(135deg, #1a1f2e, #252b3d);
    border-radius: 12px;
    display: flex;
    align-items: center;
    justify-content: center;
    margin-bottom: 20px;
  }
  .hero-placeholder span {
    font-size: 48px;
    color: #3a4260;
    font-weight: 700;
    text-transform: uppercase;
  }
  h1 {
    font-size: 28px;
    font-weight: 700;
    margin-bottom: 8px;
  }
  .subtitle {
    font-size: 14px;
    color: #8891a5;
    margin-bottom: 20px;
    display: flex;
    gap: 12px;
    flex-wrap: wrap;
    align-items: center;
  }
  .badge {
    font-size: 11px;
    padding: 3px 10px;
    border-radius: 4px;
    font-weight: 600;
    color: #fff;
    white-space: nowrap;
  }
  .info-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 12px;
    margin-bottom: 24px;
  }
  .info-card {
    background: #1a1f2e;
    border-radius: 10px;
    padding: 14px;
    border: 1px solid #252b3d;
  }
  .info-card .label {
    font-size: 11px;
    color: #8891a5;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    margin-bottom: 4px;
  }
  .info-card .value {
    font-size: 16px;
    font-weight: 600;
    color: #53d8fb;
  }
  .tags-section {
    margin-bottom: 20px;
  }
  .tags-section h3 {
    font-size: 14px;
    color: #8891a5;
    margin-bottom: 8px;
  }
  .tags {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
  }
  .tag {
    background: #252b3d;
    border-radius: 6px;
    padding: 4px 10px;
    font-size: 12px;
    color: #c0c8d8;
    border: 1px solid #3a4260;
  }
  .progress-section {
    margin-bottom: 24px;
  }
  .progress-header {
    display: flex;
    justify-content: space-between;
    margin-bottom: 6px;
    font-size: 13px;
  }
  .progress-track {
    width: 100%;
    height: 10px;
    background: #1a1f2e;
    border-radius: 5px;
    overflow: hidden;
  }
  .progress-fill {
    height: 100%;
    background: linear-gradient(90deg, #ff3d6a, #3dffe0);
    border-radius: 5px;
  }
  .description {
    background: #1a1f2e;
    border-radius: 10px;
    padding: 16px;
    font-size: 14px;
    line-height: 1.6;
    color: #c0c8d8;
    border: 1px solid #252b3d;
    max-height: 300px;
    overflow-y: auto;
  }
  .loading { text-align: center; padding: 60px; color: #8891a5; }
</style>
</head>
<body>
<div id="detail">
  <div class="loading">Loading game details...</div>
</div>

<script>
const SOURCE_COLORS = {
  Steam: '#1b2838', 'Epic Games': '#2a2a2a', Xbox: '#107c10',
  GOG: '#86328a', PlayStation: '#003087', Nintendo: '#e60012',
  Ubisoft: '#0070ff', 'EA app': '#ff4747', 'Battle.net': '#00aeff'
};

function formatPlaytime(s) {
  if (!s || s <= 0) return 'Never played';
  const h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60);
  if (h === 0) return m + 'm';
  if (m === 0) return h + 'h';
  return h + 'h ' + m + 'm';
}

function esc(s) { const d = document.createElement('div'); d.textContent = s || ''; return d.innerHTML; }

async function callTool(name, args) {
  const id = crypto.randomUUID();
  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error('Timeout')), 15000);
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

// Get gameId from URL params or wait for message
const params = new URLSearchParams(window.location.search);
const gameId = params.get('gameId');

if (gameId) {
  loadGame(gameId);
} else {
  document.getElementById('detail').innerHTML =
    '<div class="loading">No game ID provided. Use get_game tool to view a game.</div>';
}

async function loadGame(id) {
  const container = document.getElementById('detail');
  try {
    const result = await callTool('get_game', { gameId: id });
    const text = result?.content?.[0]?.text || 'No data';
    container.innerHTML = '<pre style="white-space:pre-wrap;font-family:inherit;color:#c0c8d8;line-height:1.8">' +
      text.replace(/^#+ /gm, '').replace(/\\*\\*/g, '') + '</pre>';
  } catch (err) {
    container.innerHTML = '<div class="loading">Could not load game details.</div>';
  }
}
</script>
</body>
</html>`;
}
