# Playnite Bridge

Playnite plugin (`PlayniteBridge.dll`) exposing full HTTP API on `localhost:19821` with Bearer token auth.
Includes **Playnite Sync** — a Rust backend for syncing game libraries across machines.

## Build & Deploy

### Plugin
```bash
cd src && dotnet build -c Release
```
Output: `src/bin/Release/net462/PlayniteBridge.dll`

### Sync Backend
```bash
cd sync-backend && cargo build --release
```
Output: `sync-backend/target/release/playnite-sync.exe` (~4.5MB)

## Tests

### Plugin tests
```bash
cd tests && dotnet test
```
- Framework: NUnit 3 + NSubstitute
- 160 tests, ~2 seconds

### Backend tests
```bash
cd sync-backend && cargo build && cargo run -- --headless &
# Then run test scripts via curl (see tests in conversation history)
```

## Deploy

### Plugin
```bash
taskkill //F //IM Playnite.DesktopApp.exe
cp src/bin/Release/net462/PlayniteBridge.dll "C:/Games/Playnite/Extensions/PlayniteBridge/"
"C:/Games/Playnite/Playnite.DesktopApp.exe"
```

### Sync Backend
```bash
taskkill //F //IM playnite-sync.exe
cp sync-backend/target/release/playnite-sync.exe "C:/tools/playnite-sync/"
cp sync-backend/config.toml "C:/tools/playnite-sync/"
cp -r sync-backend/static/* "C:/tools/playnite-sync/static/"
cd "C:/tools/playnite-sync" && ./playnite-sync.exe
```
- Deployment folder: `C:/tools/playnite-sync/`
- Database: `./playnite-sync.db` (created on first run)
- Dashboard: `http://localhost:19822`
- Tray icon in system tray, right-click → Open Dashboard / Quit

### Full Release Process
1. `cd src && dotnet build -c Release` — plugin
2. `cd sync-backend && cargo build --release` — backend
3. `cd tests && dotnet test` — verify all 160 tests pass
4. Kill Playnite + sync backend
5. Copy DLL to Playnite extensions folder
6. Copy exe + config + static to deployment folder
7. Wipe `playnite-sync.db` if clean install needed
8. Wipe `sync_state.json` in plugin data if re-registering
9. Start backend, start Playnite

## Architecture

### Plugin
- .NET Framework 4.6.2, Playnite SDK 6.15.0
- Plugin ID: `PlayniteBridge_f47ac10b`
- Auth token auto-generated on first run
- Main plugin: `src/PlayniteBridgePlugin.cs`
- Services: `src/Services/` (GameQueryService, EvalService, PluginIntegrationService, GameSerializationService, SyncClient, SyncEngine, SyncState, BackendDiscovery)
- Helpers: `src/Helpers/` (DictExtensions, CollectionResolver, JsonHelper, NetworkHelper)
- Server: `src/Server/` (HttpApiServer, Router, RequestContext)

### Sync Backend
- Rust, Axum 0.8, rusqlite (bundled SQLite, WAL mode)
- Port: 19822
- Tailscale auto-detection for cross-network sync
- Registration code (PSR-XXXX) for client approval
- Dashboard: static HTML/JS/CSS (no build step)
- Tray app: tray-icon + wry (WebView2)
- Conflict resolution: MAX(playtime), UNION(collections), LWW(scalars)
- Canonical key dedup: `source:gameId` (e.g., `steam:292030`)
- Games without canonical key are not synced

### Connection Flow
1. Plugin scans for backends (Tailscale → localhost)
2. User clicks Connect → creates pending client on backend
3. Approve via dashboard button OR enter PSR code in plugin
4. Once approved → automatic sync starts

## Key Files
- `src/PlayniteBridgePlugin.cs` — main plugin, sync init, event hooks
- `src/Services/SyncEngine.cs` — push/pull orchestration
- `src/Services/SyncClient.cs` — HTTP client for backend
- `src/Services/BackendDiscovery.cs` — Tailscale peer scanning
- `sync-backend/src/main.rs` — backend entry, Tailscale detection
- `sync-backend/src/api/sync.rs` — push/pull/commands endpoints
- `sync-backend/src/api/clients.rs` — registration/approval flow
- `sync-backend/src/db/games.rs` — canonical key dedup, LWW merge
- `sync-backend/static/` — dashboard HTML/JS/CSS

## Key Gotchas
- **Encoding:** `ReadBody` must use `Encoding.UTF8` explicitly
- **Deploy:** Must kill Playnite before copying DLL (file lock)
- **Game readonly props:** Source, Genres, etc. are read-only (DB-backed)
- **Sync canonical key:** Only games with source + gameId sync
- **Tailscale discovery timeout:** HttpClient needs 5s+ timeout for peer scanning
- **Backend DB path:** Relative to working directory, not exe location
- **Client delete cascade:** `db::clients::delete()` manually cleans all FK-dependent tables. If you add a new table with `client_id` foreign key, add a DELETE there too — SQLite FK cascade is not enabled for this
