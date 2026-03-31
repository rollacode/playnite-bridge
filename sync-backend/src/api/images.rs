use axum::{
    body::Bytes,
    extract::{Path, State},
    http::{HeaderMap, StatusCode, header},
    response::{IntoResponse, Response},
    routing::{get, put},
    Router,
};
use crate::api::AppState;
use crate::db;
use crate::error::{AppError, AppResult};

/// Protected routes (require client auth)
pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/images/{hash}", put(upload_image))
}

/// Public routes (hash is unguessable)
pub fn public_routes() -> Router<AppState> {
    Router::new()
        .route("/images/{hash}", get(download_image))
}

async fn upload_image(
    State(state): State<AppState>,
    Path(hash): Path<String>,
    body: Bytes,
) -> AppResult<Response> {
    if hash.len() < 8 || hash.len() > 128 {
        return Err(AppError::BadRequest("Invalid hash length".into()));
    }
    if !hash.chars().all(|c| c.is_ascii_hexdigit()) {
        return Err(AppError::BadRequest("Hash must be hex string".into()));
    }
    if body.is_empty() {
        return Err(AppError::BadRequest("Empty body".into()));
    }
    // 10 MB limit
    if body.len() > 10 * 1024 * 1024 {
        return Err(AppError::BadRequest("Image too large (max 10MB)".into()));
    }

    let data_dir = state.data_dir.clone();
    tokio::task::spawn_blocking(move || {
        db::images::store(&data_dir, &hash, &body)
    }).await.map_err(|e| AppError::Internal(e.to_string()))??;

    Ok(StatusCode::NO_CONTENT.into_response())
}

async fn download_image(
    State(state): State<AppState>,
    Path(hash): Path<String>,
) -> AppResult<Response> {
    if !hash.chars().all(|c| c.is_ascii_hexdigit()) || hash.len() < 8 {
        return Err(AppError::BadRequest("Invalid hash".into()));
    }

    let data_dir = state.data_dir.clone();
    let bytes = tokio::task::spawn_blocking(move || {
        db::images::load(&data_dir, &hash)
    }).await.map_err(|e| AppError::Internal(e.to_string()))??;

    // Detect content type from magic bytes
    let content_type = detect_content_type(&bytes);

    let mut headers = HeaderMap::new();
    headers.insert(header::CONTENT_TYPE, content_type.parse().unwrap());
    headers.insert(header::CACHE_CONTROL, "public, max-age=31536000, immutable".parse().unwrap());

    Ok((headers, bytes).into_response())
}

fn detect_content_type(bytes: &[u8]) -> &'static str {
    if bytes.starts_with(&[0x89, b'P', b'N', b'G']) {
        "image/png"
    } else if bytes.starts_with(&[0xFF, 0xD8, 0xFF]) {
        "image/jpeg"
    } else if bytes.starts_with(b"GIF8") {
        "image/gif"
    } else if bytes.starts_with(b"RIFF") && bytes.get(8..12) == Some(b"WEBP") {
        "image/webp"
    } else if bytes.starts_with(&[0x00, 0x00, 0x01, 0x00]) {
        "image/x-icon"
    } else {
        "application/octet-stream"
    }
}
