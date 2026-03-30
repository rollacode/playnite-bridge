use axum::{
    extract::{Path, State},
    http::HeaderMap,
    routing::{get, post},
    Json, Router,
};
use crate::api::AppState;
use crate::db;
use crate::error::{AppError, AppResult};
use crate::models::*;

/// Protected routes (require active client/admin auth)
pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/clients", get(list))
        .route("/clients/{id}", get(get_client).delete(delete_client))
        .route("/clients/{id}/reject", post(reject_client))
}

/// Public routes (no auth needed — connection request flow)
pub fn public_routes() -> Router<AppState> {
    Router::new()
        .route("/clients/request", post(request_connection))
        .route("/clients/pending", get(list_pending))
        .route("/clients/{id}/status", get(check_status))
        .route("/clients/{id}/approve", post(approve_client))
        .route("/clients/{id}/dashboard-approve", post(dashboard_approve))
        .route("/clients/{id}/dashboard-reject", post(dashboard_reject))
        // Legacy register endpoint (admin key or reg code required)
        .route("/clients/register", post(register))
}

/// Request a connection (no auth — creates pending client)
async fn request_connection(
    State(state): State<AppState>,
    Json(req): Json<ConnectionRequest>,
) -> AppResult<Json<ConnectionRequestResponse>> {
    let client_id = uuid::Uuid::new_v4().to_string();
    let now = chrono::Utc::now().to_rfc3339();

    let client = Client {
        id: client_id.clone(),
        name: req.name,
        api_key: String::new(), // no key until approved
        status: "pending".into(),
        last_seen: Some(now.clone()),
        last_sync: None,
        ip_address: None,
        playnite_version: req.playnite_version,
        game_count: 0,
        created_at: now,
    };

    let db = state.db.clone();
    let c = client.clone();
    tokio::task::spawn_blocking(move || db::clients::insert(&db, &c)).await
        .map_err(|e| AppError::Internal(e.to_string()))??;

    tracing::info!("Connection request from: {} ({})", client.name, client_id);

    Ok(Json(ConnectionRequestResponse {
        client_id,
        status: "pending".into(),
    }))
}

/// Check connection status (no auth — client polls this after requesting)
async fn check_status(
    State(state): State<AppState>,
    Path(id): Path<String>,
) -> AppResult<Json<ClientStatusResponse>> {
    let db = state.db.clone();

    let result = tokio::task::spawn_blocking(move || {
        let client = db::clients::find_by_id(&db, &id)?
            .ok_or_else(|| AppError::NotFound("Client not found".into()))?;
        Ok::<_, AppError>(ClientStatusResponse {
            status: client.status.clone(),
            api_key: if client.status == "active" && !client.api_key.is_empty() {
                Some(client.api_key)
            } else {
                None
            },
        })
    }).await.map_err(|e| AppError::Internal(e.to_string()))??;

    Ok(Json(result))
}

/// Approve a pending client (requires admin key or registration code)
async fn approve_client(
    State(state): State<AppState>,
    headers: HeaderMap,
    Path(id): Path<String>,

    Json(body): Json<ApproveRequest>,
) -> AppResult<Json<ClientStatusResponse>> {
    // Check auth: admin key in header OR registration code in body
    let auth = headers.get("authorization")
        .and_then(|v| v.to_str().ok()).unwrap_or("");
    let token = if auth.starts_with("Bearer ") { &auth[7..] } else { "" };
    let body_code = body.registration_code;

    let authorized = token == state.admin_key
        || token == state.registration_code
        || body_code.as_deref() == Some(&state.registration_code);

    if !authorized {
        return Err(AppError::Unauthorized);
    }

    let db = state.db.clone();
    let api_key = tokio::task::spawn_blocking(move || db::clients::approve(&db, &id)).await
        .map_err(|e| AppError::Internal(e.to_string()))??;

    tracing::info!("Client approved: {}", api_key.chars().take(12).collect::<String>());

    Ok(Json(ClientStatusResponse {
        status: "active".into(),
        api_key: Some(api_key),
    }))
}

/// Dashboard approve (no auth — dashboard is on the same machine)
async fn dashboard_approve(
    State(state): State<AppState>,
    Path(id): Path<String>,
) -> AppResult<Json<ClientStatusResponse>> {
    let db = state.db.clone();
    let api_key = tokio::task::spawn_blocking(move || db::clients::approve(&db, &id)).await
        .map_err(|e| AppError::Internal(e.to_string()))??;
    tracing::info!("Client approved via dashboard");
    Ok(Json(ClientStatusResponse { status: "active".into(), api_key: Some(api_key) }))
}

/// Dashboard reject (no auth)
async fn dashboard_reject(
    State(state): State<AppState>,
    Path(id): Path<String>,
) -> AppResult<Json<serde_json::Value>> {
    let db = state.db.clone();
    tokio::task::spawn_blocking(move || db::clients::reject(&db, &id)).await
        .map_err(|e| AppError::Internal(e.to_string()))??;
    tracing::info!("Client rejected via dashboard");
    Ok(Json(serde_json::json!({"ok": true})))
}

/// Reject a pending client (admin only)
async fn reject_client(
    State(state): State<AppState>,
    Path(id): Path<String>,
) -> AppResult<Json<serde_json::Value>> {
    let db = state.db.clone();
    tokio::task::spawn_blocking(move || db::clients::reject(&db, &id)).await
        .map_err(|e| AppError::Internal(e.to_string()))??;
    Ok(Json(serde_json::json!({"ok": true})))
}

/// Legacy register (admin key or reg code required, creates active client directly)
async fn register(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(req): Json<RegisterClientRequest>,
) -> AppResult<Json<RegisterClientResponse>> {
    let auth = headers.get("authorization")
        .and_then(|v| v.to_str().ok()).unwrap_or("");
    let token = if auth.starts_with("Bearer ") { &auth[7..] } else { "" };

    let authorized = token == state.admin_key
        || token == state.registration_code
        || req.registration_code.as_deref() == Some(&state.registration_code);

    if !authorized {
        return Err(AppError::Unauthorized);
    }

    let db = state.db.clone();
    let result = tokio::task::spawn_blocking(move || {
        let client_id = uuid::Uuid::new_v4().to_string();
        let now = chrono::Utc::now().to_rfc3339();

        use rand::Rng;
        let mut rng = rand::thread_rng();
        let bytes: [u8; 24] = rng.gen();
        let api_key = format!("pbs_{}", bytes.iter().map(|b| format!("{b:02x}")).collect::<String>());

        let client = Client {
            id: client_id.clone(),
            name: req.name,
            api_key: api_key.clone(),
            status: "active".into(),
            last_seen: Some(now.clone()),
            last_sync: None,
            ip_address: None,
            playnite_version: req.playnite_version,
            game_count: 0,
            created_at: now,
        };
        db::clients::insert(&db, &client)?;
        Ok::<_, AppError>(RegisterClientResponse { client_id, api_key })
    }).await.map_err(|e| AppError::Internal(e.to_string()))??;

    Ok(Json(result))
}

async fn list_pending(State(state): State<AppState>) -> AppResult<Json<Vec<Client>>> {
    let db = state.db.clone();
    let clients = tokio::task::spawn_blocking(move || db::clients::list_pending(&db)).await
        .map_err(|e| AppError::Internal(e.to_string()))??;
    Ok(Json(clients))
}

async fn list(State(state): State<AppState>) -> AppResult<Json<Vec<Client>>> {
    let db = state.db.clone();
    let clients = tokio::task::spawn_blocking(move || db::clients::list(&db)).await
        .map_err(|e| AppError::Internal(e.to_string()))??;
    Ok(Json(clients))
}

async fn get_client(State(state): State<AppState>, Path(id): Path<String>) -> AppResult<Json<Client>> {
    let db = state.db.clone();
    let client = tokio::task::spawn_blocking(move || db::clients::find_by_id(&db, &id)).await
        .map_err(|e| AppError::Internal(e.to_string()))??
        .ok_or_else(|| AppError::NotFound("Client not found".into()))?;
    Ok(Json(client))
}

async fn delete_client(State(state): State<AppState>, Path(id): Path<String>) -> AppResult<Json<serde_json::Value>> {
    let db = state.db.clone();
    let deleted = tokio::task::spawn_blocking(move || db::clients::delete(&db, &id)).await
        .map_err(|e| AppError::Internal(e.to_string()))??;
    if deleted { Ok(Json(serde_json::json!({"ok": true}))) }
    else { Err(AppError::NotFound("Client not found".into())) }
}
