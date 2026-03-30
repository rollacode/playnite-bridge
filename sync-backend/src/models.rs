use serde::{Deserialize, Serialize};

// --- Client ---

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct Client {
    pub id: String,
    pub name: String,
    #[serde(skip_serializing)]
    pub api_key: String,
    pub status: String,
    pub last_seen: Option<String>,
    pub last_sync: Option<String>,
    pub ip_address: Option<String>,
    pub playnite_version: Option<String>,
    pub game_count: i64,
    pub created_at: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegisterClientRequest {
    pub name: String,
    pub playnite_version: Option<String>,
    pub registration_code: Option<String>,
}

#[derive(Debug, Serialize)]
pub struct RegisterClientResponse {
    pub client_id: String,
    pub api_key: String,
}

// Connection request (no auth needed)
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ConnectionRequest {
    pub name: String,
    pub playnite_version: Option<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ConnectionRequestResponse {
    pub client_id: String,
    pub status: String,
}

// Client status check response
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ClientStatusResponse {
    pub status: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub api_key: Option<String>,
}

// Approve with optional code
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ApproveRequest {
    pub registration_code: Option<String>,
}

// --- Game ---

#[derive(Debug, Serialize, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct Game {
    pub id: String,
    pub name: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub sorting_name: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub notes: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub game_id: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub source: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub release_date: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub community_score: Option<i64>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub critic_score: Option<i64>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub user_score: Option<i64>,
    #[serde(default)]
    pub favorite: bool,
    #[serde(default)]
    pub hidden: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub completion_status: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub version: Option<String>,
    #[serde(default)]
    pub playtime: i64,
    #[serde(default)]
    pub play_count: i64,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub last_activity: Option<String>,
    // Collections (resolved from join tables)
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub genres: Vec<String>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub categories: Vec<String>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub tags: Vec<String>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub features: Vec<String>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub developers: Vec<String>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub publishers: Vec<String>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub platforms: Vec<String>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub series: Vec<String>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub links: Vec<GameLink>,
    // Per-client (only set in context)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub is_installed: Option<bool>,
    // Timestamps
    #[serde(skip_serializing_if = "Option::is_none")]
    pub created_at: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub updated_at: Option<String>,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct GameLink {
    pub name: Option<String>,
    pub url: String,
}

// --- Sync ---

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SyncPushRequest {
    pub entity_type: String,
    pub items: Vec<serde_json::Value>,
    #[serde(default)]
    pub removals: Vec<CollectionRemoval>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CollectionRemoval {
    pub game_id: String,
    pub field: String,
    pub removed: Vec<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SyncPushResponse {
    pub accepted: usize,
    pub skipped: usize,
    pub conflicts: usize,
    pub new_cursor: String,
}

#[derive(Debug, Deserialize)]
pub struct SyncPullQuery {
    pub entity_type: Option<String>,
    pub since: Option<String>,
    pub limit: Option<i64>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SyncPullResponse {
    pub items: Vec<serde_json::Value>,
    pub cursor: String,
    pub has_more: bool,
}

// --- Dashboard ---

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DashboardOverview {
    pub total_games: i64,
    pub total_clients: i64,
    pub total_playtime_hours: f64,
    pub unsyncable_games: i64,
    pub pending_clients: i64,
    pub clients: Vec<ClientSummary>,
    pub recent_syncs: Vec<SyncLogEntry>,
    pub top_sources: Vec<SourceCount>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ClientSummary {
    pub id: String,
    pub name: String,
    pub game_count: i64,
    pub last_seen: Option<String>,
    pub last_sync: Option<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SyncLogEntry {
    pub id: i64,
    pub client_name: String,
    pub entity_type: String,
    pub direction: String,
    pub record_count: Option<i64>,
    pub status: String,
    pub error_message: Option<String>,
    pub started_at: String,
    pub completed_at: Option<String>,
}

#[derive(Debug, Serialize)]
pub struct SourceCount {
    pub source: String,
    pub count: i64,
}

// --- Commands ---

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ClientCommand {
    pub id: i64,
    pub command: String,
    pub payload: Option<String>,
    pub created_at: String,
}

// --- Network ---

#[derive(Debug, Serialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct NetworkInfo {
    pub local_ip: Option<String>,
    pub tailscale_ip: Option<String>,
    pub tailscale_hostname: Option<String>,
    pub port: u16,
    pub connect_url: String,
    pub has_tailscale: bool,
}

// --- Paginated response ---

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PaginatedGames {
    pub total: i64,
    pub offset: i64,
    pub limit: i64,
    pub games: Vec<Game>,
}

#[derive(Debug, Deserialize)]
pub struct GamesQuery {
    pub q: Option<String>,
    pub source: Option<String>,
    pub genre: Option<String>,
    pub category: Option<String>,
    pub tag: Option<String>,
    pub installed_on: Option<String>,
    pub limit: Option<i64>,
    pub offset: Option<i64>,
    pub sort: Option<String>,
    pub descending: Option<bool>,
}
