# Playnite Bridge HTTP API

**Port:** 19821
**Auth:** `Authorization: Bearer <token>` (get from Playnite menu)
**Format:** JSON (UTF-8)
**CORS:** Enabled

## Games

### GET /api/games

List/search games with pagination.

**Query parameters:**

| Parameter | Description |
| ----------- | ------------- |
| `q` | Name search (case-insensitive substring) |
| `installed` | `true` for installed only |
| `favorite` | `true` for favorites only |
| `hidden` | `true` to include hidden (excluded by default) |
| `uncategorized` | `true` for games without categories |
| `source` | Filter by source name (e.g. "Steam") |
| `genre` | Filter by genre name |
| `category` | Filter by category name |
| `tag` | Filter by tag name |
| `feature` | Filter by feature name |
| `platform` | Filter by platform name |
| `completionStatus` | Filter by status name |
| `limit` | Max results (default 500, max 5000) |
| `offset` | Pagination offset |

**Response:**

```json
{
  "total": 150,
  "offset": 0,
  "limit": 500,
  "games": [
    {
      "id": "guid",
      "name": "Game Name",
      "source": "Steam",
      "genres": ["Action"],
      "categories": ["Favorites"],
      "tags": [],
      "features": ["Single Player"],
      "platforms": ["PC (Windows)"],
      "completionStatus": "Played",
      "isInstalled": true,
      "favorite": false,
      "hidden": false,
      "playtime": 3600,
      "playCount": 5,
      "lastActivity": "2026-03-20T15:30:00.0000000+00:00",
      "userScore": 85
    }
  ]
}
```

---

### GET /api/games/{id}

Full game details including description, notes, developers, publishers, links, scores, dates, artwork status.

---

### PUT /api/games/{id}

Update any game fields. Send only the fields you want to change.

**Body fields (all optional):**

| Field | Type | Notes |
| ------- | ------ | ------- |
| `name` | string | |
| `sortingName` | string | |
| `description` | string | |
| `notes` | string | |
| `version` | string | |
| `hidden` | boolean | |
| `favorite` | boolean | |
| `userScore` | int/null | 0-100 |
| `communityScore` | int/null | 0-100 |
| `criticScore` | int/null | 0-100 |
| `releaseDate` | string | "YYYY-MM-DD", "YYYY-MM", or "YYYY" |
| `completionStatus` | string | Status name (must exist) |
| `categories` | string[] | Auto-created if missing |
| `tags` | string[] | Auto-created if missing |
| `features` | string[] | Auto-created if missing |
| `genres` | string[] | Auto-created if missing |
| `developers` | string[] | Auto-created if missing |
| `publishers` | string[] | Auto-created if missing |
| `series` | string[] | Auto-created if missing |
| `platforms` | string[] | Must exist |
| `ageRatings` | string[] | Must exist |
| `regions` | string[] | Must exist |
| `links` | object[] | `[{name, url}, ...]` |

---

### DELETE /api/games/{id}

Delete a game from the library.

---

### POST /api/games/{id}/launch

Launch an installed game.

### POST /api/games/{id}/install

Start game installation.

### POST /api/games/{id}/uninstall

Uninstall a game.

---

### PUT /api/games/{id}/categories

Replace all categories. Body: `{"categories": ["Cat1", "Cat2"]}`

### POST /api/games/{id}/categories

Add categories (append). Body: `{"categories": ["Cat1"]}`

### PUT /api/games/{id}/tags

Replace all tags. Body: `{"tags": ["Tag1"]}`

### POST /api/games/{id}/tags

Add tags (append). Body: `{"tags": ["Tag1"]}`

### PUT /api/games/{id}/features

Replace all features. Body: `{"features": ["Feature1"]}`

### POST /api/games/{id}/features

Add features (append). Body: `{"features": ["Feature1"]}`

### PUT /api/games/{id}/genres

Replace all genres. Body: `{"genres": ["Genre1"]}`

### POST /api/games/{id}/genres

Add genres (append). Body: `{"genres": ["Genre1"]}`

### PUT /api/games/{id}/status

Set completion status. Body: `{"status": "Completed"}`

---

### POST /api/games/{id}/fetch-art

Fetch missing artwork (Steam CDN + IGDB fallback).

### GET /api/games/missing-art

List games missing artwork.

---

## Database Collections

All return `[{id, name}, ...]`

| Method | Path |
| -------- | ------ |
| GET/POST | `/api/categories` |
| GET/POST | `/api/genres` |
| GET/POST | `/api/tags` |
| GET/POST | `/api/features` |
| GET | `/api/platforms` |
| GET | `/api/sources` |
| GET | `/api/companies` |
| GET/POST | `/api/series` |
| GET/POST | `/api/completion-statuses` |
| GET | `/api/age-ratings` |
| GET | `/api/regions` |
| GET | `/api/filter-presets` |
| GET | `/api/emulators` |

POST body: `{"name": "New Item"}`

---

## View Control

### GET /api/view/state

Current UI state (view mode, sort order, grouping, active filter, selection count).

### GET /api/view/selected

Currently selected games with full compact details.

### POST /api/view/select

Select games in UI. Body: `{"gameIds": ["guid1", "guid2"]}`

### POST /api/view/filter

Apply a saved filter preset. Body: `{"presetId": "guid"}`

---

## App & System

### GET /api/app/info

App version, mode (Desktop/Fullscreen), paths.

### GET /api/app/addons

Installed and disabled addon IDs.

### GET /api/stats

Library statistics: total games, installed, favorites, playtime, games by source, by completion status, top genres, recently played.

### POST /api/notifications

Show a notification in Playnite. Body: `{"text": "Hello!", "type": "info"}`
Type: `"info"` (default) or `"error"`.

---

## Automation

### POST /api/auto-categorize

Auto-categorize all uncategorized games by their primary genre.

### POST /api/fetch-all-art

Fetch missing artwork for all games in the library.

---

## Eval

### POST /api/eval

Compile and run C# inside the Playnite process. The snippet has access to `PlayniteApi`
(`IPlayniteAPI`) and `Plugin` (`GenericPlugin`).

**Body:**

| Field | Type | Notes |
| ------- | ------ | ------- |
| `code` | string | Required. No `;` means expression (auto-wrapped in `return (...)`); otherwise statements, use `return` explicitly |
| `timeoutMs` | int | 1000-30000, default 10000 |
| `onUiThread` | bool | Dispatch to the WPF UI thread, needed for UI operations |

**Example:**

```json
{"code": "PlayniteApi.Database.Games.Count", "timeoutMs": 10000}
```

**Success:**

```json
{"success": true, "result": 3097, "resultType": "Int32", "durationMs": 214}
```

**Compile failure:**

```json
{
  "success": false,
  "error": "Compilation failed",
  "errors": ["(17,5): CS0103 The name 'foo' does not exist in the current context"],
  "errorsDetailed": [
    {"line": 17, "column": 5, "errorNumber": "CS0103", "message": "The name 'foo' does not exist in the current context"}
  ]
}
```

`errors` is kept for backwards compatibility; `errorsDetailed` carries the parsed
`CompilerError` fields.

The compiler reference set is de-duplicated by assembly simple name, preferring the copy in
Playnite's own folder. This keeps `CS1703` (same identity imported twice) and `CS1704` (same
simple name, two versions) from breaking every call when extensions bundle their own copies of
shared assemblies.

**Runtime failure:**

```json
{"success": false, "error": "Object reference not set to an instance of an object.", "exceptionType": "NullReferenceException", "durationMs": 8}
```

**Validation failures** return HTTP 200 with `code: 400` in the body: missing or blank `code`,
malformed JSON, or a non-integer `timeoutMs`.

**Limitations:**

- **C# 5 only.** Compilation goes through the in-box `csc` via CodeDOM. `var`, lambdas, LINQ,
  `dynamic` and `async`/`await` are fine. Not available: string interpolation, `?.`, `nameof`,
  tuples, `out var`, pattern matching, local functions, switch expressions, `using var`,
  target-typed `new`.
- **One assembly per call, never unloaded.** Long sessions accumulate in-memory assemblies.
- **`timeoutMs` stops the wait, not the script.** The work keeps running on a background thread.

---

## Auth

### POST /api/auth/rotate

Rotate the API token. Returns the new token. Old token stops working immediately.

### GET /api/skill.md

Get the AI skill file with current token embedded.

---

## Error Responses

**401 Unauthorized:**

```json
{"error": "Unauthorized. Header required: Authorization: Bearer <token>"}
```

**404 Not Found:**

```json
{"error": "Not found", "help": "GET /api for endpoint list"}
```

**500 Internal Server Error:**

```json
{"error": "Error message"}
```
