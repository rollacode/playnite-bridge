/** Stats dashboard MCP App — library analytics with CSS bars */

export function buildStatsDashboardHtml(): string {
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Playnite Stats</title>
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
    background: #0f1219;
    color: #e4e8f0;
    min-height: 100vh;
    padding: 20px;
  }
  h1 {
    font-size: 24px;
    font-weight: 700;
    margin-bottom: 20px;
    background: linear-gradient(135deg, #ff3d6a, #53d8fb);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
  }
  .kpi-row {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
    gap: 12px;
    margin-bottom: 28px;
  }
  .kpi {
    background: #1a1f2e;
    border-radius: 12px;
    padding: 16px;
    border: 1px solid #252b3d;
  }
  .kpi-value {
    font-size: 28px;
    font-weight: 700;
    color: #53d8fb;
    line-height: 1.1;
  }
  .kpi-label {
    font-size: 12px;
    color: #8891a5;
    margin-top: 4px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
  }
  .section {
    margin-bottom: 28px;
  }
  .section h2 {
    font-size: 16px;
    font-weight: 600;
    margin-bottom: 12px;
    color: #c0c8d8;
  }
  .bar-chart {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }
  .bar-row {
    display: flex;
    align-items: center;
    gap: 10px;
  }
  .bar-label {
    font-size: 13px;
    min-width: 140px;
    text-align: right;
    color: #8891a5;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .bar-track {
    flex: 1;
    height: 22px;
    background: #1a1f2e;
    border-radius: 6px;
    overflow: hidden;
    position: relative;
  }
  .bar-fill {
    height: 100%;
    border-radius: 6px;
    min-width: 2px;
    transition: width 0.5s ease;
  }
  .bar-fill.gradient-1 { background: linear-gradient(90deg, #ff3d6a, #ff6b8a); }
  .bar-fill.gradient-2 { background: linear-gradient(90deg, #53d8fb, #8be5fc); }
  .bar-fill.gradient-3 { background: linear-gradient(90deg, #3dffe0, #7ffff0); }
  .bar-fill.gradient-4 { background: linear-gradient(90deg, #ff9f43, #ffc078); }
  .bar-fill.gradient-5 { background: linear-gradient(90deg, #a55eea, #c99eff); }
  .bar-count {
    font-size: 12px;
    min-width: 40px;
    color: #8891a5;
    text-align: right;
  }
  .recent-list {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }
  .recent-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    background: #1a1f2e;
    border-radius: 8px;
    padding: 10px 14px;
    border: 1px solid #252b3d;
  }
  .recent-name {
    font-size: 14px;
    font-weight: 500;
  }
  .recent-meta {
    font-size: 12px;
    color: #8891a5;
  }
  .loading { text-align: center; padding: 40px; color: #8891a5; }
</style>
</head>
<body>
<h1>Library Dashboard</h1>
<div id="dashboard">
  <div class="loading">Loading statistics...</div>
</div>

<script>
function formatPlaytime(s) {
  if (!s || s <= 0) return '0h';
  const h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60);
  if (h === 0) return m + 'm';
  return h + 'h';
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

function renderBars(entries, maxVal) {
  const gradients = ['gradient-1', 'gradient-2', 'gradient-3', 'gradient-4', 'gradient-5'];
  return entries.map((e, i) => {
    const pct = maxVal > 0 ? Math.round((e[1] / maxVal) * 100) : 0;
    return '<div class="bar-row">' +
      '<span class="bar-label">' + esc(e[0]) + '</span>' +
      '<div class="bar-track"><div class="bar-fill ' + gradients[i % gradients.length] + '" style="width:' + pct + '%"></div></div>' +
      '<span class="bar-count">' + e[1] + '</span>' +
    '</div>';
  }).join('');
}

async function load() {
  const dash = document.getElementById('dashboard');
  try {
    const result = await callTool('library_stats', {});
    const text = result?.content?.[0]?.text || '';
    // Parse the text-based stats
    const lines = text.split('\\n');
    dash.innerHTML = '<div class="loading">Stats loaded via text. Full dashboard requires structured data.</div>';
    // Display a formatted version of the stats text
    dash.innerHTML = '<pre style="white-space:pre-wrap;font-family:inherit;color:#c0c8d8;line-height:1.6">' +
      text.replace(/^#+ /gm, '').replace(/\\*\\*/g, '') + '</pre>';
  } catch (err) {
    dash.innerHTML = '<div class="loading">Could not load stats. Is Playnite running?</div>';
  }
}

load();
</script>
</body>
</html>`;
}
