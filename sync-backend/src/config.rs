use serde::Deserialize;
use std::path::{Path, PathBuf};

#[derive(Debug, Deserialize, Clone)]
#[allow(dead_code)] // discovery and sync fields used in future phases
pub struct Config {
    pub server: ServerConfig,
    pub database: DatabaseConfig,
    #[serde(default)]
    pub discovery: DiscoveryConfig,
    #[serde(default)]
    pub sync: SyncConfig,
}

#[derive(Debug, Deserialize, Clone)]
pub struct ServerConfig {
    #[serde(default = "default_port")]
    pub port: u16,
    #[serde(default = "default_host")]
    pub host: String,
}

#[derive(Debug, Deserialize, Clone)]
pub struct DatabaseConfig {
    #[serde(default = "default_db_path")]
    pub path: String,
}

#[derive(Debug, Deserialize, Clone)]
#[allow(dead_code)]
pub struct DiscoveryConfig {
    #[serde(default = "default_true")]
    pub enabled: bool,
    #[serde(default = "default_service_name")]
    pub service_name: String,
}

#[derive(Debug, Deserialize, Clone)]
#[allow(dead_code)]
pub struct SyncConfig {
    #[serde(default = "default_max_push_batch")]
    pub max_push_batch: usize,
    #[serde(default = "default_log_retention")]
    pub log_retention_days: u32,
}

fn default_port() -> u16 { 19822 }
fn default_host() -> String { "0.0.0.0".into() }
fn default_db_path() -> String { "./playnite-sync.db".into() }
fn default_true() -> bool { true }
fn default_service_name() -> String { "Playnite Sync".into() }
fn default_max_push_batch() -> usize { 500 }
fn default_log_retention() -> u32 { 30 }

impl Default for DiscoveryConfig {
    fn default() -> Self {
        Self { enabled: default_true(), service_name: default_service_name() }
    }
}

impl Default for SyncConfig {
    fn default() -> Self {
        Self { max_push_batch: default_max_push_batch(), log_retention_days: default_log_retention() }
    }
}

impl Config {
    pub fn load(path: &Path) -> Result<Self, Box<dyn std::error::Error>> {
        let content = std::fs::read_to_string(path)?;
        let config: Config = toml::from_str(&content)?;
        Ok(config)
    }

    pub fn default_config() -> Self {
        Self {
            server: ServerConfig { port: default_port(), host: default_host() },
            database: DatabaseConfig { path: default_db_path() },
            discovery: DiscoveryConfig::default(),
            sync: SyncConfig::default(),
        }
    }

    pub fn db_path(&self) -> PathBuf {
        PathBuf::from(&self.database.path)
    }
}
