# Playnite Bridge — AI Skill

You have access to a Playnite game library manager running on the user's local machine.
Use HTTP requests (curl, fetch, etc.) to interact with it.

> **This file contains a personal API token. Do not share publicly.**

## Connection

- **Base URL:** `http://%%HOST%%:%%PORT%%`
- **Auth header:** `Authorization: Bearer %%TOKEN%%`
- **Format:** JSON (UTF-8)

## Endpoints

### Games

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/games` | List/search games (paginated) |
| GET | `/api/games/{id}` | Full game details |
| PUT | `/api/games/{id}` | Update game fields |
| DELETE | `/api/games/{id}` | Delete game from library |
| POST | `/api/games/{id}/launch` | Launch game |
| POST | `/api/games/{id}/install` | Start game installation |
| POST | `/api/games/{id}/uninstall` | Uninstall game |
| POST | `/api/games/{id}/fetch-art` | Fetch missing artwork |
| PUT | `/api/games/{id}/categories` | Set categories (replace) |
| POST | `/api/games/{id}/categories` | Add categories (append) |
| PUT | `/api/games/{id}/tags` | Set tags (replace) |
| POST | `/api/games/{id}/tags` | Add tags (append) |
| PUT | `/api/games/{id}/features` | Set features (replace) |
| POST | `/api/games/{id}/features` | Add features (append) |
| PUT | `/api/games/{id}/genres` | Set genres (replace) |
| POST | `/api/games/{id}/genres` | Add genres (append) |
| PUT | `/api/games/{id}/status` | Set completion status |
| GET | `/api/games/missing-art` | Games missing artwork |

### Game Search — GET /api/games

Query parameters (all optional):
- `q` — name search (substring, case-insensitive)
- `installed=true` — installed games only
- `favorite=true` — favorites only
- `hidden=true` — include hidden games (excluded by default)
- `uncategorized=true` — games without categories
- `source` — filter by source name (e.g. Steam)
- `genre`, `category`, `tag`, `feature`, `platform` — filter by name
- `completionStatus` — filter by status name
- `limit` — max results (default 500, max 5000)
- `offset` — pagination offset

Response: `{total, offset, limit, games: [...]}`

### Game Update — PUT /api/games/{id}

Send a JSON body with any combination of fields to update:

**Text fields:** `name`, `sortingName`, `description`, `notes`, `version`
**Booleans:** `hidden`, `favorite`
**Scores:** `userScore`, `communityScore`, `criticScore` (0-100 or null)
**Date:** `releaseDate` (YYYY-MM-DD or YYYY-MM or YYYY)
**Status:** `completionStatus` (name string)
**Collections** (arrays of name strings — auto-created if missing):
  `categories`, `tags`, `features`, `genres`, `developers`, `publishers`, `series`
**Collections** (lookup only, must exist):
  `platforms`, `ageRatings`, `regions`
**Links:** `links` (array of `{name, url}` objects)

### Database Collections

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/categories` | All categories |
| POST | `/api/categories` | Create category `{name}` |
| GET | `/api/genres` | All genres |
| POST | `/api/genres` | Create genre `{name}` |
| GET | `/api/tags` | All tags |
| POST | `/api/tags` | Create tag `{name}` |
| GET | `/api/features` | All features |
| POST | `/api/features` | Create feature `{name}` |
| GET | `/api/platforms` | All platforms |
| GET | `/api/sources` | All library sources |
| GET | `/api/companies` | All developers/publishers |
| GET | `/api/series` | All game series |
| POST | `/api/series` | Create series `{name}` |
| GET | `/api/completion-statuses` | All completion statuses |
| POST | `/api/completion-statuses` | Create status `{name}` |
| GET | `/api/age-ratings` | All age ratings |
| GET | `/api/regions` | All regions |
| GET | `/api/filter-presets` | Saved filter presets |
| GET | `/api/emulators` | All emulators |

### View Control

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/view/state` | Current UI state (view mode, sort, selection count) |
| GET | `/api/view/selected` | Currently selected games with details |
| POST | `/api/view/select` | Select games in UI `{gameIds: [...]}` |
| POST | `/api/view/filter` | Apply filter preset `{presetId: "..."}` |

### App & System

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/app/info` | App version, mode, paths |
| GET | `/api/app/addons` | Installed addons |
| GET | `/api/stats` | Library statistics (counts, playtime, top genres, recent) |
| POST | `/api/notifications` | Show notification `{text, type: "info"|"error"}` |

### Advanced Query — POST /api/games/query

Powerful game search with filters, sorting, and grouping. Send a JSON body:

```json
{
  "q": "search text",
  "genres": ["RPG", "Strategy"],
  "developers": ["CD Projekt RED"],
  "source": "Steam",
  "playtimeMin": 3600,
  "playtimeMax": 360000,
  "releaseYearMin": 2020,
  "installed": true,
  "uncategorized": true,
  "sort": "playtime",
  "descending": true,
  "groupBy": "developer",
  "limit": 100
}
```

**Filters:** `q`, `installed`, `favorite`, `hidden`, `playtimeMin/Max` (seconds), `releaseYearMin/Max`, `genres`, `categories`, `tags`, `features`, `developers`, `publishers`, `platforms` (arrays, AND logic), `source`, `completionStatus`, `uncategorized`, `untagged`

**Sort:** `name` (default), `playtime`, `added`, `release`, `lastplayed` + `descending: true`

**GroupBy:** `genre`, `developer`, `publisher`, `source`, `platform`, `year`, `completionStatus` — returns `[{group, count, totalHours}]` instead of game list

### Duplicates — GET /api/games/duplicates

Games owned on multiple platforms. Returns: `[{name, copies, sources, totalPlaytimeHours, games: [{id, source, installed, playtimeHours}]}]`

### Batch — POST /api/batch

Execute multiple API calls in one request (max 50):

```json
{"requests": [
  {"method": "GET", "path": "/api/stats"},
  {"method": "GET", "path": "/api/games?q=Witcher&limit=3"},
  {"method": "POST", "path": "/api/games/query", "body": {"genres": ["RPG"], "groupBy": "developer"}}
]}
```

Response: `{results: [{status, data}, ...]}`

### Analytics — GET /api/analytics

Full library analytics in one call: library stats, by source, top genres, top developers, top games by playtime, by release year, duplicate count.

### Plugin Integration

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/games/{id}/achievements` | Achievements from SuccessStory plugin |
| GET | `/api/games/{id}/activity` | Play sessions from GameActivity plugin |
| GET | `/api/plugins` | List loaded/installed/disabled plugins |

### Automation

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/auto-categorize` | Auto-categorize all uncategorized games by primary genre |
| POST | `/api/fetch-all-art` | Fetch missing artwork for all games |

### Eval — Execute C# Code

`POST /api/eval` — compile and run arbitrary C# inside Playnite. The code has access to `PlayniteApi` (IPlayniteAPI) and `Plugin` (GenericPlugin).

```json
{"code": "PlayniteApi.Database.Games.Count", "timeoutMs": 10000, "onUiThread": false}
```

- If code has no `;` it is treated as an expression (auto-wrapped in `return (...)`)
- Otherwise treated as statements — use `return` explicitly to return a value
- `onUiThread: true` dispatches to WPF UI thread (needed for UI operations)
- Timeout: 1-30 seconds, default 10s
- All loaded assemblies (SDK, plugins, System.*) are available
- Access other plugins: `PlayniteApi.Addons.Plugins.FirstOrDefault(p => p.Id == someGuid)`

Response: `{success, result, resultType, durationMs}` or `{success: false, error, errors}`

### Auth

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/auth/rotate` | Rotate API token (returns new token) |
| GET | `/api/skill.md` | Get this skill file with current token |

## Important Notes

- **Game IDs** are GUIDs (e.g. `a1b2c3d4-e5f6-7890-abcd-ef1234567890`). Get them from `GET /api/games` first.
- **Playtime** is in seconds. Divide by 3600 for hours.
- **Collection names** (categories, tags, features, genres, series) are auto-created when referenced. No need to create them first.
- **Launch/install/uninstall** affect the actual computer. Ask the user before launching games.
- **Delete** permanently removes a game from the library. Always confirm with the user first.
- **GET /api/stats** is a great starting point to understand the library before diving into details.
- **Token rotation:** After `POST /api/auth/rotate`, the old token stops working immediately. The user will need to update this skill file.
- **Eval** executes real C# in the Playnite process. Use for complex queries, batch ops, cross-plugin data access via reflection.
