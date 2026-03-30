use axum::{
    extract::{Path, Query, State},
    routing::get,
    Json, Router,
};
use crate::api::AppState;
use crate::db;
use crate::error::{AppError, AppResult};
use crate::models::{Game, GamesQuery, PaginatedGames};

pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/games", get(list_games))
        .route("/games/{id}", get(get_game))
}

async fn list_games(
    State(state): State<AppState>,
    Query(query): Query<GamesQuery>,
) -> AppResult<Json<PaginatedGames>> {
    let db = state.db.clone();
    let result = tokio::task::spawn_blocking(move || db::games::list(&db, &query)).await
        .map_err(|e| AppError::Internal(e.to_string()))??;
    Ok(Json(result))
}

async fn get_game(
    State(state): State<AppState>,
    Path(id): Path<String>,
) -> AppResult<Json<Game>> {
    let db = state.db.clone();
    let game = tokio::task::spawn_blocking(move || db::games::find_by_id(&db, &id)).await
        .map_err(|e| AppError::Internal(e.to_string()))??
        .ok_or_else(|| AppError::NotFound("Game not found".into()))?;
    Ok(Json(game))
}
