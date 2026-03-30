use axum::{
    extract::State,
    routing::get,
    Json, Router,
};
use crate::api::AppState;
use crate::db;
use crate::error::{AppError, AppResult};
use crate::models::{DashboardOverview, SyncLogEntry};

pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/dashboard/overview", get(overview))
        .route("/dashboard/sync-log", get(sync_log))
}

async fn overview(
    State(state): State<AppState>,
) -> AppResult<Json<DashboardOverview>> {
    let db = state.db.clone();

    let result = tokio::task::spawn_blocking(move || {
        let total_games = db::games::count(&db)?;
        let clients = db::clients::list_summaries(&db)?;
        let total_clients = clients.len() as i64;
        let total_playtime_hours = db::games::total_playtime_hours(&db)?;
        let recent_syncs = get_recent_syncs(&db, 20)?;
        let top_sources = db::games::sources_count(&db)?;
        let unsyncable_games = db::games::unsyncable_count(&db)?;
        let pending_clients = db::clients::pending_count(&db)?;

        Ok::<_, AppError>(DashboardOverview {
            total_games,
            total_clients,
            total_playtime_hours,
            clients,
            recent_syncs,
            top_sources,
            unsyncable_games,
            pending_clients,
        })
    }).await.map_err(|e| AppError::Internal(e.to_string()))??;

    Ok(Json(result))
}

async fn sync_log(
    State(state): State<AppState>,
) -> AppResult<Json<Vec<SyncLogEntry>>> {
    let db = state.db.clone();
    let entries = tokio::task::spawn_blocking(move || get_recent_syncs(&db, 100)).await
        .map_err(|e| AppError::Internal(e.to_string()))??;
    Ok(Json(entries))
}

fn get_recent_syncs(db: &db::Db, limit: i64) -> AppResult<Vec<SyncLogEntry>> {
    let conn = db.lock().unwrap();
    let mut stmt = conn.prepare(
        "SELECT sl.id, c.name, sl.entity_type, sl.direction, sl.record_count,
                sl.status, sl.error_message, sl.started_at, sl.completed_at
         FROM sync_log sl
         JOIN clients c ON sl.client_id = c.id
         ORDER BY sl.started_at DESC
         LIMIT ?1"
    )?;

    let entries = stmt.query_map(rusqlite::params![limit], |row| {
        Ok(SyncLogEntry {
            id: row.get(0)?,
            client_name: row.get(1)?,
            entity_type: row.get(2)?,
            direction: row.get(3)?,
            record_count: row.get(4)?,
            status: row.get(5)?,
            error_message: row.get(6)?,
            started_at: row.get(7)?,
            completed_at: row.get(8)?,
        })
    })?.collect::<Result<Vec<_>, _>>()?;

    Ok(entries)
}
