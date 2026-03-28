# Playnite Bridge — AI Skill

You have access to a Playnite game library manager through an HTTP API running on the user's local machine.

> **Before using this skill**, ask the user for their API token. They can find it in Playnite: **Main Menu > Playnite Bridge > Show API Token**.

## Connection

- **Base URL:** `http://localhost:19821`
- **Auth header:** `Authorization: Bearer <TOKEN>`
- **Format:** JSON (UTF-8)

## Making Requests

Use Python `urllib.request` for all API calls (curl on Windows may break non-ASCII encoding):

```python
import json, urllib.request

TOKEN = '<ask user for token>'

def api(method, path, body=None):
    url = f'http://localhost:19821{path}'
    data = json.dumps(body, ensure_ascii=False).encode('utf-8') if body else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header('Authorization', f'Bearer {TOKEN}')
    req.add_header('Content-Type', 'application/json; charset=utf-8')
    with urllib.request.urlopen(req, timeout=30) as resp:
        return json.loads(resp.read())
```

## Endpoints

### Games

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/games` | List/search games (paginated) |
| GET | `/api/games/{id}` | Full game details |
| POST | `/api/games` | Create new game |
| PUT | `/api/games/{id}` | Update game fields |
| DELETE | `/api/games/{id}` | Delete game |
| POST | `/api/games/{id}/launch` | Launch game |
| POST | `/api/games/{id}/install` | Start installation |
| POST | `/api/games/{id}/uninstall` | Uninstall game |
| POST | `/api/games/{id}/fetch-art` | Fetch missing artwork |

### Game Search — GET /api/games

Query parameters (all optional):
- `q` — name search (substring, case-insensitive)
- `installed=true` — installed games only
- `favorite=true` — favorites only
- `hidden=true` — include hidden games (excluded by default)
- `uncategorized=true` — games without categories
- `source` — filter by source name (e.g. "Steam")
- `genre`, `category`, `tag`, `feature`, `platform` — filter by name
- `completionStatus` — filter by status name
- `limit` — max results (default 500, max 5000)
- `offset` — pagination offset

Response: `{total, offset, limit, games: [...]}`

### Game Create — POST /api/games

```json
{"name": "Game Name", "source": "PlayStation", "platforms": ["Sony PlayStation 4"], "categories": ["RPG"]}
```

### Game Update — PUT /api/games/{id}

Send any combination of fields to update:

**Text:** `name`, `sortingName`, `description`, `notes`, `version`
**Booleans:** `hidden`, `favorite`
**Numbers:** `userScore`, `communityScore`, `criticScore` (0-100 or null), `playtime` (seconds), `playCount`
**Date:** `releaseDate` (YYYY-MM-DD)
**Status:** `completionStatus` (name string)
**Collections** (string arrays, auto-created if missing): `categories`, `tags`, `features`, `genres`, `developers`, `publishers`, `series`
**Collections** (must exist): `platforms`, `ageRatings`, `regions`
**Links:** `links` ([{name, url}])

### Game Sub-resources

| PUT | `/api/games/{id}/categories` | Set categories (replace) |
| POST | `/api/games/{id}/categories` | Add categories (append) |
| PUT | `/api/games/{id}/tags` | Set tags (replace) |
| POST | `/api/games/{id}/tags` | Add tags (append) |
| PUT | `/api/games/{id}/features` | Set features (replace) |
| POST | `/api/games/{id}/features` | Add features (append) |
| PUT | `/api/games/{id}/genres` | Set genres (replace) |
| POST | `/api/games/{id}/genres` | Add genres (append) |
| PUT | `/api/games/{id}/status` | Set completion status `{status: "name"}` |

Body for set/add: `{"categories": ["Name1", "Name2"]}` (or `tags`, `features`, `genres`)

### Collections

GET (list all) + POST `{name}` (create) for: `/api/categories`, `/api/genres`, `/api/tags`, `/api/features`, `/api/series`, `/api/completion-statuses`

GET only: `/api/platforms`, `/api/sources`, `/api/companies`, `/api/age-ratings`, `/api/regions`, `/api/filter-presets`, `/api/emulators`

DELETE: `/api/categories/{id}`, `/api/tags/{id}`, `/api/genres/{id}`, `/api/features/{id}`, `/api/series/{id}`

All return `[{id, name}, ...]`

### View Control

| GET | `/api/view/state` | Current UI state (view mode, sort, selection count) |
| GET | `/api/view/selected` | Currently selected games with details |
| POST | `/api/view/select` | Select games in UI `{gameIds: [...]}` |
| POST | `/api/view/filter` | Apply filter preset `{presetId: "..."}` |

### App & System

| GET | `/api/app/info` | App version, mode, paths |
| GET | `/api/app/addons` | Installed addons |
| GET | `/api/stats` | Library statistics (counts, playtime, genres, recently played) |
| POST | `/api/notifications` | Show notification `{text, type: "info"/"error"}` |

### Automation

| POST | `/api/auto-categorize` | Auto-categorize uncategorized games by primary genre |
| POST | `/api/fetch-all-art` | Fetch missing artwork for all games (uses Steam CDN + IGDB) |
| GET | `/api/games/missing-art` | List games missing artwork |

### Auth

| POST | `/api/auth/rotate` | Rotate API token (returns new token, old stops working) |
| GET | `/api` | API index with all endpoints |

## Important Notes

- **Game IDs** are GUIDs. Get them from `GET /api/games` first.
- **Playtime** is in seconds. Divide by 3600 for hours.
- **Collection names** (categories, tags, features, genres, series) are auto-created when you reference them by name in updates.
- **Launch/install/uninstall** affect the actual computer. Always confirm with the user.
- **Delete** permanently removes a game from the library. Always confirm with the user.
- **`GET /api/stats`** is a great starting point to understand the library.
- **Artwork fetching** requires IGDB setup (Twitch API credentials in `igdb.json`). Steam games use Steam CDN automatically.
