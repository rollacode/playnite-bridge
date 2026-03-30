use axum::{
    extract::{Path, Query, State},
    http::HeaderMap,
    routing::{get, post},
    Json, Router,
};
use crate::api::auth::client_id_from_token;
use crate::api::AppState;
use crate::db;
use crate::error::{AppError, AppResult};
use crate::models::*;

pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/sync/push", post(push))
        .route("/sync/pull", get(pull))
        .route("/sync/status", get(status))
        .route("/sync/commands", get(get_commands))
        .route("/sync/commands/{id}/ack", post(ack_command))
        .route("/admin/resync", post(admin_resync))
}

pub fn public_routes() -> Router<AppState> {
    Router::new()
        .route("/network", get(network_info))
        .route("/config/regcode", get(get_regcode))
}

async fn get_regcode(State(state): State<AppState>) -> Json<serde_json::Value> {
    Json(serde_json::json!({"code": state.registration_code}))
}

async fn push(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(req): Json<SyncPushRequest>,
) -> AppResult<Json<SyncPushResponse>> {
    let auth = headers.get("authorization").and_then(|v| v.to_str().ok()).unwrap_or("");
    let db = state.db.clone();
    let auth_owned = auth.to_string();

    let result = tokio::task::spawn_blocking(move || {
        let client_id = client_id_from_token(&db, &auth_owned)
            .ok_or_else(|| AppError::Unauthorized)?;
        db::clients::update_last_seen(&db, &client_id, None)?;

        match req.entity_type.as_str() {
            "games" => push_games(&db, &client_id, &req),
            other => Err(AppError::BadRequest(format!("Unknown entity_type: {other}"))),
        }
    }).await.map_err(|e| AppError::Internal(e.to_string()))??;

    Ok(Json(result))
}

fn push_games(db: &db::Db, client_id: &str, req: &SyncPushRequest) -> AppResult<SyncPushResponse> {
    let mut accepted = 0;
    let mut skipped = 0;
    let mut conflicts = 0;

    for item in &req.items {
        match serde_json::from_value::<Game>(item.clone()) {
            Ok(game) => {
                match db::games::upsert(db, &game, client_id)? {
                    Some(canonical_id) => {
                        upsert_client_game(db, client_id, &canonical_id, &game)?;
                        accepted += 1;
                    }
                    None => {
                        if skipped < 3 {
                            tracing::warn!("Skipped game '{}': source={:?} game_id={:?}",
                                game.name, game.source, game.game_id);
                        }
                        skipped += 1;
                    }
                }
            }
            Err(e) => {
                tracing::warn!("Failed to parse game in sync push: {e}");
                conflicts += 1;
            }
        }
    }

    // Process collection removals (tombstones)
    if !req.removals.is_empty() {
        db::games::apply_removals(db, &req.removals, client_id)?;
    }

    let now = chrono::Utc::now().to_rfc3339();
    {
        let conn = db.lock().unwrap();
        conn.execute(
            "INSERT INTO sync_cursors (client_id, entity_type, last_sync_at)
             VALUES (?1, 'games', ?2) ON CONFLICT(client_id, entity_type) DO UPDATE SET last_sync_at = ?2",
            rusqlite::params![client_id, now],
        )?;
        let game_count: i64 = conn.query_row(
            "SELECT COUNT(*) FROM client_games WHERE client_id = ?1",
            rusqlite::params![client_id], |row| row.get(0),
        )?;
        conn.execute(
            "UPDATE clients SET last_sync = ?2, game_count = ?3 WHERE id = ?1",
            rusqlite::params![client_id, now, game_count],
        )?;
        conn.execute(
            "INSERT INTO sync_log (client_id, entity_type, direction, record_count, status, started_at, completed_at)
             VALUES (?1, 'games', 'push', ?2, 'success', ?3, ?3)",
            rusqlite::params![client_id, accepted, now],
        )?;
    }

    Ok(SyncPushResponse { accepted, skipped, conflicts, new_cursor: now })
}

fn upsert_client_game(db: &db::Db, client_id: &str, canonical_id: &str, game: &Game) -> AppResult<()> {
    let conn = db.lock().unwrap();
    let now = chrono::Utc::now().to_rfc3339();
    conn.execute(
        "INSERT INTO client_games (client_id, game_id, is_installed, playtime, play_count, last_activity, last_synced, client_modified)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)
         ON CONFLICT(client_id, game_id) DO UPDATE SET
            is_installed = excluded.is_installed, playtime = excluded.playtime,
            play_count = excluded.play_count, last_activity = excluded.last_activity,
            last_synced = excluded.last_synced, client_modified = excluded.client_modified",
        rusqlite::params![client_id, canonical_id, game.is_installed.unwrap_or(false) as i32,
            game.playtime, game.play_count, game.last_activity, now, game.updated_at],
    )?;
    Ok(())
}

async fn pull(
    State(state): State<AppState>,
    headers: HeaderMap,
    Query(query): Query<SyncPullQuery>,
) -> AppResult<Json<SyncPullResponse>> {
    let auth = headers.get("authorization").and_then(|v| v.to_str().ok()).unwrap_or("");
    let db = state.db.clone();
    let auth_owned = auth.to_string();

    let result = tokio::task::spawn_blocking(move || {
        let _client_id = client_id_from_token(&db, &auth_owned)
            .ok_or_else(|| AppError::Unauthorized)?;
        let since = query.since.as_deref().unwrap_or("1970-01-01T00:00:00Z");
        let limit = query.limit.unwrap_or(500);

        match query.entity_type.as_deref().unwrap_or("games") {
            "games" => {
                let (games, has_more) = db::games::changed_since(&db, since, limit)?;
                let cursor = games.last().and_then(|g| g.updated_at.clone()).unwrap_or_else(|| since.to_string());
                let items = games.into_iter().map(|g| serde_json::to_value(g).unwrap_or_default()).collect();
                Ok(SyncPullResponse { items, cursor, has_more })
            }
            other => Err(AppError::BadRequest(format!("Unknown entity_type: {other}"))),
        }
    }).await.map_err(|e| AppError::Internal(e.to_string()))??;

    Ok(Json(result))
}

async fn status(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> AppResult<Json<serde_json::Value>> {
    let auth = headers.get("authorization").and_then(|v| v.to_str().ok()).unwrap_or("");
    let db = state.db.clone();
    let auth_owned = auth.to_string();

    let result = tokio::task::spawn_blocking(move || {
        let client_id = client_id_from_token(&db, &auth_owned)
            .ok_or_else(|| AppError::Unauthorized)?;
        let conn = db.lock().unwrap();
        let mut stmt = conn.prepare("SELECT entity_type, last_sync_at FROM sync_cursors WHERE client_id = ?1")?;
        let cursors: serde_json::Map<String, serde_json::Value> = stmt.query_map(
            rusqlite::params![client_id], |row| Ok((row.get::<_, String>(0)?, row.get::<_, String>(1)?)),
        )?.collect::<Result<Vec<_>, _>>()?.into_iter()
            .map(|(k, v)| (k, serde_json::Value::String(v))).collect();

        Ok::<_, AppError>(serde_json::json!({ "client_id": client_id, "cursors": cursors }))
    }).await.map_err(|e| AppError::Internal(e.to_string()))??;

    Ok(Json(result))
}

// --- Commands ---

async fn get_commands(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> AppResult<Json<Vec<ClientCommand>>> {
    let auth = headers.get("authorization").and_then(|v| v.to_str().ok()).unwrap_or("");
    let db = state.db.clone();
    let auth_owned = auth.to_string();

    let cmds = tokio::task::spawn_blocking(move || {
        let client_id = client_id_from_token(&db, &auth_owned)
            .ok_or_else(|| AppError::Unauthorized)?;
        let conn = db.lock().unwrap();
        let mut stmt = conn.prepare(
            "SELECT id, command, payload, created_at FROM client_commands
             WHERE client_id = ?1 AND acknowledged_at IS NULL ORDER BY id"
        )?;
        let cmds = stmt.query_map(rusqlite::params![client_id], |row| {
            Ok(ClientCommand { id: row.get(0)?, command: row.get(1)?, payload: row.get(2)?, created_at: row.get(3)? })
        })?.collect::<Result<Vec<_>, _>>()?;
        Ok::<_, AppError>(cmds)
    }).await.map_err(|e| AppError::Internal(e.to_string()))??;

    Ok(Json(cmds))
}

async fn ack_command(
    State(state): State<AppState>,
    headers: HeaderMap,
    Path(cmd_id): Path<i64>,
) -> AppResult<Json<serde_json::Value>> {
    let auth = headers.get("authorization").and_then(|v| v.to_str().ok()).unwrap_or("");
    let db = state.db.clone();
    let auth_owned = auth.to_string();

    tokio::task::spawn_blocking(move || {
        let client_id = client_id_from_token(&db, &auth_owned)
            .ok_or_else(|| AppError::Unauthorized)?;
        let conn = db.lock().unwrap();
        let changed = conn.execute(
            "UPDATE client_commands SET acknowledged_at = datetime('now')
             WHERE id = ?1 AND client_id = ?2 AND acknowledged_at IS NULL",
            rusqlite::params![cmd_id, client_id],
        )?;
        if changed == 0 { return Err(AppError::NotFound("Command not found".into())); }
        Ok::<_, AppError>(())
    }).await.map_err(|e| AppError::Internal(e.to_string()))??;

    Ok(Json(serde_json::json!({"ok": true})))
}

// --- Admin ---

#[derive(serde::Deserialize)]
struct ResyncRequest {
    client_id: Option<String>, // None = all clients
}

async fn admin_resync(
    State(state): State<AppState>,
    Json(req): Json<ResyncRequest>,
) -> AppResult<Json<serde_json::Value>> {
    let db = state.db.clone();

    let count = tokio::task::spawn_blocking(move || {
        let conn = db.lock().unwrap();
        let clients: Vec<String> = if let Some(ref cid) = req.client_id {
            vec![cid.clone()]
        } else {
            let mut stmt = conn.prepare("SELECT id FROM clients")?;
            let result = stmt.query_map([], |row| row.get(0))?.collect::<Result<Vec<_>, _>>()?;
            result
        };
        for cid in &clients {
            conn.execute(
                "INSERT INTO client_commands (client_id, command) VALUES (?1, 'full_resync')",
                rusqlite::params![cid],
            )?;
        }
        Ok::<_, AppError>(clients.len())
    }).await.map_err(|e| AppError::Internal(e.to_string()))??;

    Ok(Json(serde_json::json!({"ok": true, "clients_notified": count})))
}

// --- Network / Tailscale ---

async fn network_info(
    State(state): State<AppState>,
) -> AppResult<Json<NetworkInfo>> {
    Ok(Json(state.network.clone()))
}
