# Playnite Bridge

A [Playnite](https://playnite.link/) plugin with REST API, cross-PC game library sync, and Xbox button integration for handhelds.

**REST API** — 50+ endpoints for AI agents and automation
**Sync** — playtime, categories, tags, artwork across machines via Tailscale/LAN
**Xbox Button** — replace Xbox FSE with Playnite on ROG Ally, Legion Go, etc.

## Features

### HTTP API
- **50+ endpoints** on `localhost:19821` covering the entire Playnite SDK
- **AI Skill file** — paste into any AI chat (ChatGPT, Claude, Gemini) for full library control
- **Bearer token auth** with rotation
- **Game management** — search, create, update, delete, launch, install/uninstall
- **Library metadata** — categories, tags, genres, features, platforms, series, and more
- **Advanced query** — filters, sorting, groupBy analytics
- **View control** — select games, apply filters, get UI state
- **Auto-categorization** by genre
- **Artwork fetching** from Steam CDN and IGDB
- **Code execution** — run arbitrary C# inside Playnite via `/api/eval`
- **Plugin integration** — SuccessStory achievements, GameActivity sessions

### Game Library Sync
- **Sync across PCs** — playtime, categories, tags, genres, completion status, favorites
- **Artwork sync** — covers, icons, backgrounds via content-addressed storage (SHA-256)
- **Text metadata** — descriptions, developers, publishers, features, platforms, series
- **Reactive sync** — changes push within 5 seconds of editing a game
- **Tailscale auto-discovery** — finds sync backend across networks automatically
- **Connection approval** — PSR registration code or dashboard approve button
- **Conflict resolution** — MAX playtime, UNION collections, last-writer-wins for metadata
- **Canonical key dedup** — same game from different PCs merges by store ID (e.g. `steam:292030`)
- **Web dashboard** — monitor clients, browse library, manage connections
- **Windows tray app** — sync backend runs silently in system tray

## Installation

### Plugin
1. Download `PlayniteBridge_vX.X.X.pext` from [Releases](../../releases)
2. Drag onto Playnite to install
3. Restart Playnite — API starts on port **19821**

### Sync Backend (optional)
1. Download `playnite-sync-vX.X.X-windows-x64.zip` from [Releases](../../releases)
2. Extract anywhere, run `playnite-sync.exe`
3. A tray icon appears — right-click for dashboard
4. Note the **PSR registration code** in dashboard Settings

### Connecting
1. In Playnite: **Settings > Plugins > Playnite Bridge > Sync**
2. Click **Scan for Backends** — finds the server on Tailscale or LAN
3. Click **Connect**, then enter PSR code or approve from dashboard
4. Sync starts automatically

## Sync Settings

Per-client settings in the plugin:
- **Sync interval** — 1 / 5 / 15 / 30 minutes / Off
- **Sync on game changes** — reactive sync when you edit status, categories, tags
- **Sync artwork** — covers, icons, backgrounds

### What Syncs

| Data | Strategy |
|------|----------|
| Playtime | MAX across machines |
| Categories, tags, genres, features | Merged from all |
| Completion status, favorite, hidden | Last edit wins |
| Description, developers, publishers | Fills empty fields |
| Covers, icons, backgrounds | Content-addressed (SHA-256 hash diff) |
| Custom statuses (e.g. "Don't Care") | Auto-created if missing |
| Installation status | Per-machine (not synced) |

Games are matched by store ID (e.g. Steam AppID). Games without a source are not synced.

## AI Integration

### Quick Start
1. **Main Menu > Playnite Bridge > Copy AI Skill to Clipboard**
2. Paste into any AI chat
3. Done — the AI can manage your library

### Settings
**Settings > Plugins > Playnite Bridge** — API status, token, AI skill, MCP config for Claude Desktop, ChatGPT URL.

## API Overview

All endpoints require `Authorization: Bearer <token>`. Full docs in [`docs/api.md`](docs/api.md) and [`skill.md`](skill.md).

| Category | Endpoints | Examples |
|----------|-----------|---------|
| Games | 20 | Search, CRUD, launch, install, categories, tags, cover art |
| Query | 1 | Advanced filters, sort, groupBy analytics |
| Collections | 18 | Categories, genres, tags, features, platforms, sources |
| Plugin Data | 3 | Achievements, activity, plugins |
| View | 4 | UI state, selection, filters |
| App | 4 | Version, addons, stats, notifications |
| Automation | 2 | Auto-categorize, batch artwork fetch |
| Auth | 2 | Token rotation, skill generation |
| Eval | 1 | Execute C# code inside Playnite |

## Sync Backend

Lightweight Rust server (~4.5MB binary):
- **SQLite** database (WAL mode)
- **Tailscale** auto-detection for cross-network sync
- **Web dashboard** with infinite scroll library browser
- **Content-addressed image storage** on disk (~500MB for 600 games)
- **Launch on Startup** toggle in dashboard settings
- **Docker-compatible** (`--headless` mode)

## Building from Source

### Plugin
```bash
cd src && dotnet build -c Release
```

### Sync Backend
```bash
cd sync-backend && cargo build --release
```

### Tests
```bash
cd tests && dotnet test          # 174 C# tests
cd sync-backend && cargo test    # 10 Rust tests
```

## Xbox Button / Handheld (FSE)

Replace the Xbox Full Screen Experience with Playnite on handhelds (ROG Ally, Legion Go, etc.):

1. In Playnite: **Settings > Plugins > Playnite Bridge > Xbox Button (FSE)**
2. Click **Install** (requires admin for certificate)
3. Go to **Windows Settings > Gaming > Full Screen Experience**
4. Select **Playnite** from the dropdown

Now the Xbox/Legion/ROG button opens Playnite in fullscreen. To remove: click Uninstall in plugin settings, or right-click "Playnite" in Start Menu > Uninstall.

## Security

- Plugin API on port 19821, protected by Bearer token
- Sync backend on port 19822, client registration requires approval
- Tailscale provides encrypted transport for cross-network sync
- Token auto-generated, rotatable, stored locally
- Images addressed by SHA-256 hash — unguessable URLs

## License

[MIT](LICENSE)
