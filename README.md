# Playnite Bridge

A [Playnite](https://playnite.link/) plugin that exposes your entire game library through an HTTP API with token auth. Designed for AI integration — copy the skill file into any AI chat to give it full control over your library.

## Features

- **Full HTTP API** on `localhost:19821` — 50+ endpoints covering the entire Playnite SDK
- **AI Skill file** — paste [`skill.md`](skill.md) into any AI chat (ChatGPT, Claude, Gemini) and it can manage your library
- **Bearer token auth** with rotation
- **Game management** — search, create, update any field, delete, launch, install, uninstall
- **Library metadata** — categories, tags, features, genres, platforms, companies, series, and more
- **View control** — select games, apply filters, get UI state
- **Auto-categorization** by genre
- **Artwork fetching** from Steam CDN and IGDB
- **Library statistics** — counts, playtime, top genres, recently played
- **Notifications** — send messages to Playnite UI

## Installation

1. Download the latest release from [Releases](../../releases)
2. Drag the `.pext` file onto Playnite, or extract to:
   ```
   %AppData%\Playnite\Extensions\PlayniteBridge_f47ac10b\
   ```
   The folder should contain `PlayniteBridge.dll` and `extension.yaml`.
3. Restart Playnite. The API starts automatically on port **19821**.

## AI Integration

There are three ways to connect AI to your game library:

### Option 1: AI Skill File (simplest)

Paste a skill file directly into any AI chat — ChatGPT, Claude, Gemini, etc.

1. In Playnite: **Main Menu > Playnite Bridge > Copy AI Skill to Clipboard**
2. Paste into your AI chat
3. Done — the AI can now manage your library

### Option 2: MCP Server (richest)

MCP (Model Context Protocol) server with game card UI. Works with Claude Desktop, ChatGPT, and other MCP-compatible clients.

**Setup:**

1. Get your token: Playnite → **Main Menu > Playnite Bridge > Show API Token**
2. Build the MCP server:
   ```bash
   cd mcp && npm install && npm run build
   ```

**Claude Desktop** — add to `claude_desktop_config.json` (Settings → Developer → Edit Config):
```json
{
  "mcpServers": {
    "playnite": {
      "command": "node",
      "args": ["C:/path/to/playnite-bridge/mcp/dist/server/stdio-bridge.js"],
      "env": {
        "PLAYNITE_URL": "http://<playnite-pc-ip>:19821",
        "PLAYNITE_TOKEN": "<your-token>"
      }
    }
  }
}
```

**ChatGPT Desktop** — start the HTTP server, then add via UI:
```bash
cd mcp
PLAYNITE_TOKEN=<your-token> PLAYNITE_URL=http://<ip>:19821 npm start
```
In ChatGPT: Settings → Tools → Add MCP Server → `http://<ip>:3849/mcp`

**Available tools:** `search_games`, `get_game`, `query_games`, `library_stats`, `get_achievements`, `get_activity`, `update_game`, `manage_categories`, `list_collections`

### Option 3: Claude Code Skill

Copy `skill.md` to your Claude Code skills directory:

```bash
mkdir -p ~/.claude/skills/playnite-bridge
cp skill.md ~/.claude/skills/playnite-bridge/SKILL.md
```

Then use `/playnite-bridge` in any Claude Code session.

### Playnite Menu

The plugin adds these items under **Main Menu > Playnite Bridge**:

- **Copy AI Skill to Clipboard** — copies a skill file with your token embedded
- **Open AI Skill File** — opens the skill file in your editor
- **Show API Token** — displays your current token
- **Regenerate API Token** — rotates the token (old one stops working)

## API Overview

All endpoints require `Authorization: Bearer <token>` header. Full documentation in [`docs/api.md`](docs/api.md) and [`skill.md`](skill.md).

| Category | Endpoints | Examples |
|----------|-----------|---------|
| Games | 18 | Search, CRUD, launch, install, categories, tags, artwork |
| Collections | 18 | Categories, genres, tags, features, platforms, sources |
| View | 4 | UI state, selection, filters |
| App | 4 | Version, addons, stats, notifications |
| Automation | 3 | Auto-categorize, batch artwork fetch |
| Auth | 2 | Token rotation, API index |

## IGDB Setup (optional)

For non-Steam artwork fetching via [IGDB](https://www.igdb.com/):

1. Create an app at [Twitch Developer Portal](https://dev.twitch.tv/console/apps)
2. Create `igdb.json` in the plugin's data directory:
   ```
   %AppData%\Playnite\ExtensionsData\f47ac10b-58cc-4372-a567-0e02b2c3d479\igdb.json
   ```
   ```json
   {
     "client_id": "your_twitch_client_id",
     "client_secret": "your_twitch_client_secret"
   }
   ```

## Building from Source

```bash
git clone --recurse-submodules https://github.com/rollacode/playnite-categorizer.git
cd playnite-categorizer/src
dotnet build -c Release
```

Output: `src/bin/Release/net462/PlayniteBridge.dll`

## Security

- API listens on all interfaces (port 19821) — accessible from the local network, protected by Bearer token
- Token is auto-generated on first run, stored locally
- IGDB credentials stay in your local `igdb.json` (gitignored)
- The skill file contains your token — don't share it publicly. Use "Regenerate API Token" if compromised.

## License

[MIT](LICENSE)
