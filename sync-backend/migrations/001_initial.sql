-- Playnite Sync Backend — Initial Schema

-- Connected Playnite instances
CREATE TABLE IF NOT EXISTS clients (
    id              TEXT PRIMARY KEY,
    name            TEXT NOT NULL,
    api_key         TEXT NOT NULL UNIQUE,
    last_seen       TEXT,
    last_sync       TEXT,
    ip_address      TEXT,
    playnite_version TEXT,
    game_count      INTEGER DEFAULT 0,
    created_at      TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Canonical game records (source of truth)
CREATE TABLE IF NOT EXISTS games (
    id                  TEXT PRIMARY KEY,
    name                TEXT NOT NULL,
    sorting_name        TEXT,
    description         TEXT,
    notes               TEXT,
    game_id             TEXT,
    source              TEXT,
    release_date        TEXT,
    community_score     INTEGER,
    critic_score        INTEGER,
    user_score          INTEGER,
    favorite            INTEGER DEFAULT 0,
    hidden              INTEGER DEFAULT 0,
    completion_status   TEXT,
    version             TEXT,
    playtime            INTEGER DEFAULT 0,
    play_count          INTEGER DEFAULT 0,
    last_activity       TEXT,
    created_at          TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at          TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Per-client installation and playtime state
CREATE TABLE IF NOT EXISTS client_games (
    client_id       TEXT NOT NULL REFERENCES clients(id),
    game_id         TEXT NOT NULL REFERENCES games(id),
    is_installed    INTEGER DEFAULT 0,
    install_dir     TEXT,
    install_size    INTEGER,
    playtime        INTEGER DEFAULT 0,
    play_count      INTEGER DEFAULT 0,
    last_activity   TEXT,
    last_synced     TEXT,
    PRIMARY KEY (client_id, game_id)
);

-- Collection items (categories, tags, genres, features, etc.)
CREATE TABLE IF NOT EXISTS collection_items (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    type    TEXT NOT NULL,
    name    TEXT NOT NULL,
    UNIQUE(type, name)
);

-- Game <-> collection membership
CREATE TABLE IF NOT EXISTS game_collections (
    game_id         TEXT NOT NULL REFERENCES games(id),
    collection_id   INTEGER NOT NULL REFERENCES collection_items(id),
    PRIMARY KEY (game_id, collection_id)
);

-- Links per game
CREATE TABLE IF NOT EXISTS game_links (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id TEXT NOT NULL REFERENCES games(id),
    name    TEXT,
    url     TEXT NOT NULL
);

-- Achievements (from SuccessStory)
CREATE TABLE IF NOT EXISTS achievements (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id         TEXT NOT NULL REFERENCES games(id),
    name            TEXT NOT NULL,
    description     TEXT,
    unlocked        INTEGER DEFAULT 0,
    date_unlocked   TEXT,
    percent         REAL,
    gamer_score     INTEGER,
    is_hidden       INTEGER DEFAULT 0,
    source          TEXT,
    updated_at      TEXT NOT NULL DEFAULT (datetime('now')),
    UNIQUE(game_id, name)
);

-- Play sessions (from GameActivity)
CREATE TABLE IF NOT EXISTS play_sessions (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id         TEXT NOT NULL REFERENCES games(id),
    client_id       TEXT REFERENCES clients(id),
    date_session    TEXT NOT NULL,
    elapsed_seconds INTEGER NOT NULL,
    action_name     TEXT,
    UNIQUE(game_id, client_id, date_session)
);

-- Sync checkpoints per client per entity type
CREATE TABLE IF NOT EXISTS sync_cursors (
    client_id       TEXT NOT NULL REFERENCES clients(id),
    entity_type     TEXT NOT NULL,
    last_sync_at    TEXT NOT NULL,
    last_sync_id    TEXT,
    PRIMARY KEY (client_id, entity_type)
);

-- Sync operation log
CREATE TABLE IF NOT EXISTS sync_log (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    client_id       TEXT NOT NULL REFERENCES clients(id),
    entity_type     TEXT NOT NULL,
    direction       TEXT NOT NULL,
    record_count    INTEGER,
    status          TEXT NOT NULL,
    error_message   TEXT,
    started_at      TEXT NOT NULL,
    completed_at    TEXT
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_games_updated ON games(updated_at);
CREATE INDEX IF NOT EXISTS idx_games_source ON games(source);
CREATE INDEX IF NOT EXISTS idx_games_source_gameid ON games(source, game_id);
CREATE INDEX IF NOT EXISTS idx_client_games_synced ON client_games(last_synced);
CREATE INDEX IF NOT EXISTS idx_achievements_game ON achievements(game_id);
CREATE INDEX IF NOT EXISTS idx_play_sessions_game ON play_sessions(game_id);
CREATE INDEX IF NOT EXISTS idx_play_sessions_client ON play_sessions(client_id);
CREATE INDEX IF NOT EXISTS idx_sync_log_client ON sync_log(client_id);
CREATE INDEX IF NOT EXISTS idx_collection_items_type ON collection_items(type);
