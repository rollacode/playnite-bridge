// ═══ Playnite Sync Dashboard ═══

const BASE = '/api';

async function api(path, opts = {}) {
    const res = await fetch(`${BASE}${path}`, {
        ...opts,
        headers: { 'Content-Type': 'application/json', ...opts.headers },
    });
    if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
    return res.json();
}

// ═══ Navigation ═══

document.querySelectorAll('.nav-item').forEach(btn => {
    btn.addEventListener('click', () => {
        document.querySelectorAll('.nav-item').forEach(b => b.classList.remove('active'));
        document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
        btn.classList.add('active');
        const page = btn.dataset.page;
        document.getElementById(`page-${page}`).classList.add('active');

        const loaders = {
            overview: loadOverview,
            games: loadGames,
            'sync-log': loadSyncLog,
            settings: loadSettings,
        };
        loaders[page]?.();
    });
});

// ═══ Helpers ═══

function esc(str) {
    if (!str) return '';
    const d = document.createElement('div');
    d.textContent = str;
    return d.innerHTML;
}

function timeAgo(iso) {
    if (!iso) return '<span style="color:var(--text-muted)">—</span>';
    const diff = Date.now() - new Date(iso).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 0) return 'just now';
    if (mins < 1) return 'just now';
    if (mins < 60) return `${mins}m ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs}h ago`;
    const days = Math.floor(hrs / 24);
    return `${days}d ago`;
}

function formatHours(seconds) {
    if (!seconds || seconds === 0) return '<span style="color:var(--text-muted)">—</span>';
    const h = seconds / 3600;
    if (h < 1) return `${Math.round(h * 60)}m`;
    if (h < 100) return `${h.toFixed(1)}h`;
    return `${Math.round(h).toLocaleString()}h`;
}

function clientStatusDot(lastSeen) {
    if (!lastSeen) return '<span class="dot dot-offline"></span>';
    const diff = Date.now() - new Date(lastSeen).getTime();
    const mins = diff / 60000;
    if (mins < 5) return '<span class="dot dot-online"></span>';
    if (mins < 60) return '<span class="dot dot-stale"></span>';
    return '<span class="dot dot-offline"></span>';
}

function dirBadge(dir) {
    return `<span class="badge badge-${dir}">${dir.toUpperCase()}</span>`;
}

function statusBadge(status) {
    return `<span class="badge badge-${status}">${status}</span>`;
}

function toast(msg) {
    const el = document.getElementById('toast');
    el.textContent = msg;
    el.classList.add('show');
    setTimeout(() => el.classList.remove('show'), 2200);
}

// ═══ Overview ═══

async function loadOverview() {
    try {
        const data = await api('/dashboard/overview');

        document.getElementById('stat-games').textContent = data.totalGames.toLocaleString();
        document.getElementById('stat-clients').textContent = data.totalClients;
        document.getElementById('stat-playtime').textContent = Math.round(data.totalPlaytimeHours).toLocaleString();
        document.getElementById('stat-syncs').textContent = data.recentSyncs.length;

        // Sidebar
        document.getElementById('sidebar-games').textContent = data.totalGames.toLocaleString();
        document.getElementById('sidebar-clients').textContent = data.totalClients;

        // Server status
        const ss = document.getElementById('server-status');
        ss.className = 'server-status online';
        ss.querySelector('.status-text').textContent = 'ONLINE';

        // Pending approvals
        try {
            const pending = await api('/clients/pending');
            const pendSec = document.getElementById('pending-section');
            if (pending.length > 0) {
                pendSec.style.display = 'block';
                document.getElementById('pending-overview').innerHTML = pending.map(c => `
                    <tr>
                        <td style="font-weight:500;color:var(--amber)">${esc(c.name)}</td>
                        <td>${timeAgo(c.created_at || c.createdAt)}</td>
                        <td style="text-align:right">
                            <button class="btn-pag" style="color:var(--green);border-color:var(--green)" onclick="approveClient('${c.id}')">Approve</button>
                            <button class="btn-pag" style="color:var(--red);border-color:var(--red);margin-left:4px" onclick="rejectClient('${c.id}')">Reject</button>
                        </td>
                    </tr>
                `).join('');
            } else {
                pendSec.style.display = 'none';
            }
        } catch(_) {}

        // Clients
        const clientsTbody = document.getElementById('clients-overview');
        const clientsEmpty = document.getElementById('clients-empty');
        if (data.clients.length === 0) {
            clientsTbody.innerHTML = '';
            clientsEmpty.style.display = 'flex';
        } else {
            clientsEmpty.style.display = 'none';
            clientsTbody.innerHTML = data.clients.map(c => `
                <tr>
                    <td>${esc(c.name)}</td>
                    <td style="font-family:var(--font-mono);font-size:11px">${c.gameCount}</td>
                    <td>${timeAgo(c.lastSync)}</td>
                    <td>${clientStatusDot(c.lastSync)}</td>
                    <td><button class="btn-pag" style="color:var(--red);border-color:var(--red);font-size:10px" onclick="removeClient('${c.id}')">Remove</button></td>
                </tr>
            `).join('');
        }

        // Sources
        const sourcesEl = document.getElementById('sources-chart');
        const sourcesEmpty = document.getElementById('sources-empty');
        if (data.topSources.length === 0) {
            sourcesEl.innerHTML = '';
            sourcesEmpty.style.display = 'flex';
        } else {
            sourcesEmpty.style.display = 'none';
            const max = Math.max(...data.topSources.map(s => s.count), 1);
            sourcesEl.innerHTML = data.topSources.slice(0, 8).map(s => `
                <div class="source-row">
                    <span class="source-name" title="${esc(s.source)}">${esc(s.source)}</span>
                    <div class="source-bar-track">
                        <div class="source-bar-fill" style="width:${(s.count / max * 100).toFixed(1)}%"></div>
                    </div>
                    <span class="source-count">${s.count}</span>
                </div>
            `).join('');
        }

        // Recent syncs
        const syncTbody = document.getElementById('recent-syncs');
        const activityEmpty = document.getElementById('activity-empty');
        if (data.recentSyncs.length === 0) {
            syncTbody.innerHTML = '';
            activityEmpty.style.display = 'flex';
        } else {
            activityEmpty.style.display = 'none';
            syncTbody.innerHTML = data.recentSyncs.slice(0, 6).map(s => `
                <tr>
                    <td>${esc(s.clientName)}</td>
                    <td style="font-family:var(--font-mono);font-size:11px">${s.entityType}</td>
                    <td>${dirBadge(s.direction)}</td>
                    <td style="font-family:var(--font-mono);font-size:11px">${s.recordCount ?? '—'}</td>
                    <td>${statusBadge(s.status)}</td>
                    <td style="color:var(--text-dim)">${timeAgo(s.startedAt)}</td>
                </tr>
            `).join('');
        }
    } catch (e) {
        const ss = document.getElementById('server-status');
        ss.className = 'server-status offline';
        ss.querySelector('.status-text').textContent = 'OFFLINE';
        console.error('Overview load failed:', e);
    }
}

// ═══ Clients ═══

async function loadClients() {
    try {
        // Load pending clients
        const pending = await api('/clients/pending');
        const pendingEl = document.getElementById('pending-clients');
        if (pending.length > 0) {
            pendingEl.style.display = 'block';
            document.getElementById('pending-tbody').innerHTML = pending.map(c => `
                <tr>
                    <td style="font-weight:500;color:var(--amber)">${esc(c.name)}</td>
                    <td style="font-size:11px;color:var(--text-dim)">${c.playnite_version || c.playniteVersion || '—'}</td>
                    <td>${timeAgo(c.created_at || c.createdAt)}</td>
                    <td>
                        <button class="btn-pag" style="color:var(--green);border-color:var(--green)" onclick="approveClient('${c.id}')">Approve</button>
                        <button class="btn-pag" style="color:var(--red);border-color:var(--red);margin-left:4px" onclick="rejectClient('${c.id}')">Reject</button>
                    </td>
                </tr>
            `).join('');
        } else {
            pendingEl.style.display = 'none';
        }

        // Load active clients
        const clients = await api('/clients');
        const active = clients.filter(c => c.status === 'active');
        const countEl = document.getElementById('clients-count');
        countEl.textContent = `${active.length}${pending.length ? ` + ${pending.length} pending` : ''}`;

        const tbody = document.getElementById('clients-table');
        const empty = document.getElementById('clients-page-empty');

        if (active.length === 0 && pending.length === 0) {
            tbody.innerHTML = '';
            empty.style.display = 'flex';
        } else {
            empty.style.display = 'none';
            tbody.innerHTML = active.map(c => `
                <tr>
                    <td>${clientStatusDot(c.last_seen || c.lastSeen)}</td>
                    <td style="font-weight:500;color:var(--text-bright)">${esc(c.name)}</td>
                    <td style="font-family:var(--font-mono);font-size:11px;color:var(--text-dim)">${c.ip_address || c.ipAddress || '—'}</td>
                    <td style="font-size:11px;color:var(--text-dim)">${c.playnite_version || c.playniteVersion || '—'}</td>
                    <td style="font-family:var(--font-mono);font-size:11px">${c.game_count || c.gameCount || 0}</td>
                    <td>${timeAgo(c.last_seen || c.lastSeen)}</td>
                    <td>${timeAgo(c.last_sync || c.lastSync)}</td>
                    <td><code style="font-size:10px;color:var(--text-muted)" title="${c.id}">${c.id.slice(0, 8)}</code></td>
                </tr>
            `).join('');
        }
    } catch (e) {
        console.error('Clients load failed:', e);
    }
}

async function approveClient(id) {
    try {
        // Dashboard runs on the same host as backend — use internal approve endpoint
        await fetch(`${BASE}/clients/${id}/dashboard-approve`, { method: 'POST' });
        toast('Client approved');
        loadOverview();
    } catch (e) {
        toast('Approve failed: ' + e.message);
    }
}

async function removeClient(id) {
    if (!confirm('Remove this client?')) return;
    try {
        await fetch(`${BASE}/clients/${id}/dashboard-remove`, { method: 'POST' });
        toast('Client removed');
        loadOverview();
    } catch (e) { toast('Failed: ' + e.message); }
}

async function rejectClient(id) {
    try {
        await fetch(`${BASE}/clients/${id}/dashboard-reject`, { method: 'POST' });
        toast('Client rejected');
        loadOverview();
    } catch (e) {
        toast('Reject failed: ' + e.message);
    }
}

// ═══ Games / Library (infinite scroll) ═══

let gamesOffset = 0;
const gamesLimit = 50;
let gamesTotalCount = 0;
let gamesLoading = false;
let gamesAllLoaded = false;

async function loadGames(reset = true) {
    if (reset) {
        gamesOffset = 0;
        gamesAllLoaded = false;
        document.getElementById('games-table').innerHTML = '';
    }
    if (gamesLoading || gamesAllLoaded) return;
    gamesLoading = true;
    document.getElementById('games-loading').style.display = 'flex';

    try {
        const search = document.getElementById('game-search').value;
        const source = document.getElementById('game-source-filter').value;
        const sort = document.getElementById('game-sort').value;

        const params = new URLSearchParams({
            limit: gamesLimit, offset: gamesOffset, sort,
            descending: sort !== 'name' ? 'true' : 'false',
        });
        if (search) params.set('q', search);
        if (source) params.set('source', source);

        const data = await api(`/games?${params}`);
        gamesTotalCount = data.total;
        document.getElementById('games-count').textContent = data.total.toLocaleString();
        document.getElementById('games-info').innerHTML = `<span>${data.total.toLocaleString()} games</span>`;

        const tbody = document.getElementById('games-table');
        const html = data.games.map(g => `
            <tr>
                <td style="font-weight:500;color:var(--text-bright)">${esc(g.name)}</td>
                <td>${g.source ? `<span class="badge badge-source">${esc(g.source)}</span>` : '<span style="color:var(--text-muted)">—</span>'}</td>
                <td style="font-family:var(--font-mono);font-size:11px">${formatHours(g.playtime)}</td>
                <td style="font-size:11px">${timeAgo(g.lastActivity)}</td>
                <td style="font-size:11px;color:var(--text-dim)">${g.completionStatus || '—'}</td>
            </tr>
        `).join('');
        tbody.insertAdjacentHTML('beforeend', html);

        gamesOffset += data.games.length;
        if (gamesOffset >= data.total || data.games.length < gamesLimit) gamesAllLoaded = true;

        // Populate source filter (once)
        const sel = document.getElementById('game-source-filter');
        if (!sel.dataset.loaded) {
            try {
                const overview = await api('/dashboard/overview');
                overview.topSources.forEach(s => {
                    const opt = document.createElement('option');
                    opt.value = s.source;
                    opt.textContent = `${s.source} (${s.count})`;
                    sel.appendChild(opt);
                });
            } catch (_) {}
            sel.dataset.loaded = '1';
        }
    } catch (e) {
        console.error('Games load failed:', e);
    } finally {
        gamesLoading = false;
        document.getElementById('games-loading').style.display = 'none';
    }
}

// Infinite scroll
document.getElementById('games-scroll-container')?.addEventListener('scroll', (e) => {
    const el = e.target;
    if (el.scrollTop + el.clientHeight >= el.scrollHeight - 100) {
        loadGames(false);
    }
});

// Debounced search
let searchTimer;
document.getElementById('game-search')?.addEventListener('input', () => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => loadGames(true), 250);
});
document.getElementById('game-source-filter')?.addEventListener('change', () => loadGames(true));
document.getElementById('game-sort')?.addEventListener('change', () => loadGames(true));

// ═══ Sync Log ═══

async function loadSyncLog() {
    try {
        const entries = await api('/dashboard/sync-log');
        const tbody = document.getElementById('sync-log-table');
        const empty = document.getElementById('synclog-empty');

        if (entries.length === 0) {
            tbody.innerHTML = '';
            empty.style.display = 'flex';
        } else {
            empty.style.display = 'none';
            tbody.innerHTML = entries.map(s => `
                <tr>
                    <td style="font-weight:500">${esc(s.clientName)}</td>
                    <td style="font-family:var(--font-mono);font-size:11px">${s.entityType}</td>
                    <td>${dirBadge(s.direction)}</td>
                    <td style="font-family:var(--font-mono);font-size:11px">${s.recordCount ?? '—'}</td>
                    <td>${statusBadge(s.status)}</td>
                    <td style="color:var(--text-dim)">${timeAgo(s.startedAt)}</td>
                    <td style="font-size:11px;color:var(--red)">${s.errorMessage ? esc(s.errorMessage) : '<span style="color:var(--text-muted)">—</span>'}</td>
                </tr>
            `).join('');
        }
    } catch (e) {
        console.error('Sync log load failed:', e);
    }
}

// ═══ Settings ═══

async function loadSettings() {
    try {
        const data = await api('/config/regcode');
        document.getElementById('setting-regcode').textContent = data.code;
    } catch (e) {
        document.getElementById('setting-regcode').textContent = '—';
    }
}

function copyRegCode() {
    const code = document.getElementById('setting-regcode').textContent;
    if (code && code !== '—' && code !== 'loading...') {
        navigator.clipboard.writeText(code);
        toast('Registration code copied');
    }
}

// ═══ Auto-refresh ═══

let refreshInterval;

function startAutoRefresh() {
    refreshInterval = setInterval(() => {
        const activePage = document.querySelector('.nav-item.active')?.dataset.page;
        if (activePage === 'overview') loadOverview();
    }, 5000); // 5s
}

// ═══ Init ═══

loadOverview();
startAutoRefresh();
