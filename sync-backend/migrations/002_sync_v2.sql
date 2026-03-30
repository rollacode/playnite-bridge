-- Canonical game identity (source:gameId)
ALTER TABLE games ADD COLUMN canonical_key TEXT;
ALTER TABLE games ADD COLUMN client_modified TEXT;

-- Playnite GUID → canonical game mapping per client
CREATE TABLE IF NOT EXISTS game_aliases (
    client_id       TEXT NOT NULL REFERENCES clients(id),
    playnite_guid   TEXT NOT NULL,
    canonical_id    TEXT NOT NULL REFERENCES games(id),
    PRIMARY KEY (client_id, playnite_guid)
);

-- Collection deletion tracking (tombstones)
CREATE TABLE IF NOT EXISTS collection_tombstones (
    game_id         TEXT NOT NULL,
    collection_type TEXT NOT NULL,
    collection_name TEXT NOT NULL,
    removed_by      TEXT NOT NULL REFERENCES clients(id),
    removed_at      TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (game_id, collection_type, collection_name)
);

-- Server → client command queue
CREATE TABLE IF NOT EXISTS client_commands (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    client_id       TEXT NOT NULL REFERENCES clients(id),
    command         TEXT NOT NULL,
    payload         TEXT,
    created_at      TEXT NOT NULL DEFAULT (datetime('now')),
    acknowledged_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_client_commands_pending
    ON client_commands(client_id) WHERE acknowledged_at IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS idx_games_canonical
    ON games(canonical_key) WHERE canonical_key IS NOT NULL;

ALTER TABLE client_games ADD COLUMN client_modified TEXT;
