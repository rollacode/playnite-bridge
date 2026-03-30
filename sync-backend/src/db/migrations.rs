use super::Db;

const MIGRATION_001: &str = include_str!("../../migrations/001_initial.sql");
const MIGRATION_002: &str = include_str!("../../migrations/002_sync_v2.sql");
const MIGRATION_003: &str = include_str!("../../migrations/003_client_status.sql");

pub fn run(db: &Db) -> Result<(), rusqlite::Error> {
    let conn = db.lock().unwrap();

    conn.execute_batch("
        CREATE TABLE IF NOT EXISTS _migrations (
            id      INTEGER PRIMARY KEY,
            name    TEXT NOT NULL,
            applied TEXT NOT NULL DEFAULT (datetime('now'))
        );
    ")?;

    let migrations: &[(i64, &str, &str)] = &[
        (1, "001_initial", MIGRATION_001),
        (2, "002_sync_v2", MIGRATION_002),
        (3, "003_client_status", MIGRATION_003),
    ];

    for (id, name, sql) in migrations {
        let applied: bool = conn.query_row(
            "SELECT COUNT(*) > 0 FROM _migrations WHERE id = ?1",
            [id],
            |row| row.get(0),
        )?;

        if !applied {
            conn.execute_batch(sql)?;
            conn.execute(
                "INSERT INTO _migrations (id, name) VALUES (?1, ?2)",
                rusqlite::params![id, name],
            )?;
            tracing::info!("Applied migration {name}");
        }
    }

    Ok(())
}
