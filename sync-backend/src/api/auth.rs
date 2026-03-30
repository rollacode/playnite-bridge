use axum::{
    extract::{Request, State},
    http::StatusCode,
    middleware::Next,
    response::Response,
};
use crate::api::AppState;
use crate::db;

pub async fn require_auth(
    State(state): State<AppState>,
    request: Request,
    next: Next,
) -> Result<Response, StatusCode> {
    let auth_header = request
        .headers()
        .get("authorization")
        .and_then(|v| v.to_str().ok())
        .unwrap_or("");

    if !auth_header.starts_with("Bearer ") {
        return Err(StatusCode::UNAUTHORIZED);
    }

    let token = &auth_header[7..];

    // Admin key always works
    if token == state.admin_key {
        return Ok(next.run(request).await);
    }

    // Check if it's a valid client key
    let db = state.db.clone();
    let token_owned = token.to_string();
    let valid = tokio::task::spawn_blocking(move || {
        db::clients::find_by_api_key(&db, &token_owned).ok().flatten().is_some()
    })
    .await
    .unwrap_or(false);

    if valid {
        Ok(next.run(request).await)
    } else {
        Err(StatusCode::UNAUTHORIZED)
    }
}

/// Extract client_id from the bearer token (for sync endpoints that need it)
pub fn client_id_from_token(db: &crate::db::Db, auth_header: &str) -> Option<String> {
    if !auth_header.starts_with("Bearer ") {
        return None;
    }
    let token = &auth_header[7..];
    db::clients::find_by_api_key(db, token)
        .ok()
        .flatten()
        .map(|c| c.id)
}
