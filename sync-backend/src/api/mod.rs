pub mod auth;
pub mod clients;
pub mod games;
pub mod sync;
pub mod dashboard;

use axum::{Router, middleware};
use tower_http::cors::CorsLayer;
use tower_http::services::ServeDir;
use crate::db::Db;
use crate::models::NetworkInfo;

#[derive(Clone)]
pub struct AppState {
    pub db: Db,
    pub admin_key: String,
    pub registration_code: String,
    pub network: NetworkInfo,
}

pub fn router(state: AppState, static_dir: Option<&str>) -> Router {
    // Protected endpoints (require auth)
    let protected = Router::new()
        .merge(clients::routes())
        .merge(sync::routes())
        .layer(middleware::from_fn_with_state(state.clone(), auth::require_auth))
        .with_state(state.clone());

    // Public endpoints (dashboard, games, network, connection flow)
    let public = Router::new()
        .merge(games::routes())
        .merge(dashboard::routes())
        .merge(sync::public_routes())
        .merge(clients::public_routes())
        .with_state(state.clone());

    let mut app = Router::new()
        .nest("/api", protected)
        .nest("/api", public)
        .layer(CorsLayer::permissive());

    if let Some(dir) = static_dir {
        if std::path::Path::new(dir).exists() {
            app = app.fallback_service(ServeDir::new(dir));
        }
    }

    app
}
