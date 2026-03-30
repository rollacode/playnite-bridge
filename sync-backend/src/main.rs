mod config;
mod error;
mod models;
mod db;
mod api;
mod tray;

use clap::Parser;
use std::path::PathBuf;
use std::process::Command;
use tracing_subscriber::EnvFilter;
use models::NetworkInfo;

#[derive(Parser)]
#[command(name = "playnite-sync", about = "Sync backend for Playnite Bridge")]
struct Cli {
    /// Path to config file
    #[arg(short, long, default_value = "config.toml")]
    config: PathBuf,

    /// Run in headless mode (no system tray, no window)
    #[arg(long)]
    headless: bool,
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Initialize tracing
    tracing_subscriber::fmt()
        .with_env_filter(
            EnvFilter::try_from_default_env()
                .unwrap_or_else(|_| EnvFilter::new("playnite_sync=info,tower_http=info"))
        )
        .init();

    let cli = Cli::parse();

    // Load config
    let cfg = if cli.config.exists() {
        config::Config::load(&cli.config)?
    } else {
        tracing::warn!("Config file not found at {:?}, using defaults", cli.config);
        config::Config::default_config()
    };

    tracing::info!("Playnite Sync Backend v{}", env!("CARGO_PKG_VERSION"));
    tracing::info!("Database: {}", cfg.database.path);

    // Open database and run migrations
    let database = db::open(&cfg.db_path())?;
    db::migrations::run(&database)?;

    // Generate admin key if needed
    let admin_key = generate_or_load_admin_key(&database)?;
    tracing::info!("Admin API key: {admin_key}");

    let port = cfg.server.port;

    // Registration code
    let registration_code = generate_or_load_registration_code(&database)?;
    tracing::info!("Registration code: {registration_code}");

    // Detect Tailscale
    let network = detect_network(port);
    if network.has_tailscale {
        tracing::info!("Tailscale: {} ({})", network.tailscale_ip.as_deref().unwrap_or("?"), network.tailscale_hostname.as_deref().unwrap_or("?"));
        tracing::info!("Connect URL: {}", network.connect_url);
    } else {
        tracing::info!("Tailscale not detected — sync limited to local network");
    }

    // Build API
    let state = api::AppState {
        db: database,
        admin_key,
        registration_code,
        network,
    };

    // Determine static files directory
    let candidates = [
        cli.config.parent().map(|p| p.join("static")),
        std::env::current_exe().ok().and_then(|p| p.parent().map(|p| p.join("static"))),
        Some(PathBuf::from("static")),
        Some(PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("static")),
    ];
    let static_dir = candidates.into_iter().flatten().find(|d| d.exists());
    let static_str = static_dir.as_ref().map(|d| d.to_string_lossy().into_owned());
    let app = api::router(state, static_str.as_deref());

    // Start HTTP server on a background thread
    let addr = format!("{}:{}", cfg.server.host, port);
    let server_addr = addr.clone();
    std::thread::spawn(move || {
        let rt = tokio::runtime::Runtime::new().expect("Failed to create tokio runtime");
        rt.block_on(async move {
            let listener = tokio::net::TcpListener::bind(&server_addr).await
                .expect("Failed to bind HTTP server");
            tracing::info!("Listening on http://{server_addr}");
            axum::serve(listener, app).await.expect("HTTP server error");
        });
    });

    // Wait for server to be ready
    std::thread::sleep(std::time::Duration::from_millis(300));

    if cli.headless {
        tracing::info!("Running in headless mode. Press Ctrl+C to stop.");
        // Block forever in headless mode
        loop {
            std::thread::sleep(std::time::Duration::from_secs(3600));
        }
    } else {
        // Tray + webview window on main thread (Windows GUI requires main thread)
        tray::run(port)?;
    }

    Ok(())
}

fn generate_or_load_registration_code(db: &db::Db) -> Result<String, Box<dyn std::error::Error>> {
    let conn = db.lock().unwrap();
    let existing: Option<String> = conn.query_row(
        "SELECT value FROM _config WHERE key = 'registration_code'",
        [], |row| row.get(0),
    ).ok();

    if let Some(code) = existing {
        Ok(code)
    } else {
        use rand::Rng;
        let mut rng = rand::thread_rng();
        let chars: Vec<char> = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".chars().collect();
        let code: String = (0..4).map(|_| chars[rng.gen_range(0..chars.len())]).collect();
        let code = format!("PSR-{code}");
        conn.execute(
            "INSERT INTO _config (key, value) VALUES ('registration_code', ?1)",
            rusqlite::params![code],
        )?;
        Ok(code)
    }
}

fn detect_network(port: u16) -> NetworkInfo {
    let tailscale_ip = Command::new("tailscale").args(["ip", "-4"])
        .output().ok()
        .filter(|o| o.status.success())
        .and_then(|o| String::from_utf8(o.stdout).ok())
        .map(|s| s.trim().to_string())
        .filter(|s| !s.is_empty());

    let tailscale_hostname = Command::new("tailscale").args(["status", "--self", "--json"])
        .output().ok()
        .filter(|o| o.status.success())
        .and_then(|o| String::from_utf8(o.stdout).ok())
        .and_then(|s| {
            serde_json::from_str::<serde_json::Value>(&s).ok()
                .and_then(|v| v["Self"]["HostName"].as_str().map(|s| s.to_string()))
        });

    let local_ip = local_ip_address();
    let has_tailscale = tailscale_ip.is_some();
    let connect_url = if let Some(ref ts) = tailscale_ip {
        format!("http://{}:{}", ts, port)
    } else if let Some(ref lip) = local_ip {
        format!("http://{}:{}", lip, port)
    } else {
        format!("http://localhost:{}", port)
    };

    NetworkInfo { local_ip, tailscale_ip, tailscale_hostname, port, connect_url, has_tailscale }
}

fn local_ip_address() -> Option<String> {
    use std::net::UdpSocket;
    let socket = UdpSocket::bind("0.0.0.0:0").ok()?;
    socket.connect("8.8.8.8:80").ok()?;
    socket.local_addr().ok().map(|a| a.ip().to_string())
}

fn generate_or_load_admin_key(db: &db::Db) -> Result<String, Box<dyn std::error::Error>> {
    let conn = db.lock().unwrap();

    conn.execute_batch("
        CREATE TABLE IF NOT EXISTS _config (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
    ")?;

    let existing: Option<String> = conn.query_row(
        "SELECT value FROM _config WHERE key = 'admin_key'",
        [],
        |row| row.get(0),
    ).ok();

    if let Some(key) = existing {
        Ok(key)
    } else {
        use rand::Rng;
        let mut rng = rand::thread_rng();
        let bytes: [u8; 24] = rng.gen();
        let key = format!("pbs_admin_{}", bytes.iter().map(|b| format!("{b:02x}")).collect::<String>());
        conn.execute(
            "INSERT INTO _config (key, value) VALUES ('admin_key', ?1)",
            rusqlite::params![key],
        )?;
        Ok(key)
    }
}
