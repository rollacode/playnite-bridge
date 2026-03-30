use super::Db;
use crate::error::{AppError, AppResult};
use crate::models::{Client, ClientSummary};

fn client_from_row(row: &rusqlite::Row) -> rusqlite::Result<Client> {
    Ok(Client {
        id: row.get(0)?,
        name: row.get(1)?,
        api_key: row.get(2)?,
        status: row.get(3)?,
        last_seen: row.get(4)?,
        last_sync: row.get(5)?,
        ip_address: row.get(6)?,
        playnite_version: row.get(7)?,
        game_count: row.get(8)?,
        created_at: row.get(9)?,
    })
}

const CLIENT_COLS: &str = "id, name, api_key, status, last_seen, last_sync, ip_address, playnite_version, game_count, created_at";

pub fn insert(db: &Db, client: &Client) -> AppResult<()> {
    let conn = db.lock().unwrap();
    conn.execute(
        "INSERT INTO clients (id, name, api_key, status, ip_address, playnite_version, created_at)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)",
        rusqlite::params![
            client.id, client.name, client.api_key, client.status,
            client.ip_address, client.playnite_version, client.created_at,
        ],
    )?;
    Ok(())
}

pub fn find_by_api_key(db: &Db, api_key: &str) -> AppResult<Option<Client>> {
    let conn = db.lock().unwrap();
    let sql = format!("SELECT {CLIENT_COLS} FROM clients WHERE api_key = ?1 AND status = 'active'");
    let result = conn.prepare(&sql)?.query_row(rusqlite::params![api_key], client_from_row);
    match result {
        Ok(c) => Ok(Some(c)),
        Err(rusqlite::Error::QueryReturnedNoRows) => Ok(None),
        Err(e) => Err(AppError::Database(e)),
    }
}

pub fn find_by_id(db: &Db, id: &str) -> AppResult<Option<Client>> {
    let conn = db.lock().unwrap();
    let sql = format!("SELECT {CLIENT_COLS} FROM clients WHERE id = ?1");
    let result = conn.prepare(&sql)?.query_row(rusqlite::params![id], client_from_row);
    match result {
        Ok(c) => Ok(Some(c)),
        Err(rusqlite::Error::QueryReturnedNoRows) => Ok(None),
        Err(e) => Err(AppError::Database(e)),
    }
}

pub fn list(db: &Db) -> AppResult<Vec<Client>> {
    let conn = db.lock().unwrap();
    let sql = format!("SELECT {CLIENT_COLS} FROM clients ORDER BY created_at DESC");
    let mut stmt = conn.prepare(&sql)?;
    let clients = stmt.query_map([], client_from_row)?.collect::<Result<Vec<_>, _>>()?;
    Ok(clients)
}

pub fn list_summaries(db: &Db) -> AppResult<Vec<ClientSummary>> {
    let conn = db.lock().unwrap();
    let mut stmt = conn.prepare(
        "SELECT id, name, game_count, last_seen, last_sync, status FROM clients WHERE status = 'active' ORDER BY last_seen DESC"
    )?;
    let summaries = stmt.query_map([], |row| {
        Ok(ClientSummary {
            id: row.get(0)?,
            name: row.get(1)?,
            game_count: row.get(2)?,
            last_seen: row.get(3)?,
            last_sync: row.get(4)?,
        })
    })?.collect::<Result<Vec<_>, _>>()?;
    Ok(summaries)
}

pub fn list_pending(db: &Db) -> AppResult<Vec<Client>> {
    let conn = db.lock().unwrap();
    let sql = format!("SELECT {CLIENT_COLS} FROM clients WHERE status = 'pending' ORDER BY created_at DESC");
    let mut stmt = conn.prepare(&sql)?;
    let clients = stmt.query_map([], client_from_row)?.collect::<Result<Vec<_>, _>>()?;
    Ok(clients)
}

pub fn approve(db: &Db, client_id: &str) -> AppResult<String> {
    let conn = db.lock().unwrap();
    // Generate api_key
    use rand::Rng;
    let mut rng = rand::thread_rng();
    let bytes: [u8; 24] = rng.gen();
    let api_key = format!("pbs_{}", bytes.iter().map(|b| format!("{b:02x}")).collect::<String>());

    let changed = conn.execute(
        "UPDATE clients SET status = 'active', api_key = ?2 WHERE id = ?1 AND status = 'pending'",
        rusqlite::params![client_id, api_key],
    )?;
    if changed == 0 {
        return Err(AppError::NotFound("Pending client not found".into()));
    }
    Ok(api_key)
}

pub fn reject(db: &Db, client_id: &str) -> AppResult<()> {
    let conn = db.lock().unwrap();
    conn.execute(
        "UPDATE clients SET status = 'rejected' WHERE id = ?1 AND status = 'pending'",
        rusqlite::params![client_id],
    )?;
    Ok(())
}

pub fn update_last_seen(db: &Db, client_id: &str, ip: Option<&str>) -> AppResult<()> {
    let conn = db.lock().unwrap();
    conn.execute(
        "UPDATE clients SET last_seen = datetime('now'), ip_address = COALESCE(?2, ip_address) WHERE id = ?1",
        rusqlite::params![client_id, ip],
    )?;
    Ok(())
}

pub fn delete(db: &Db, client_id: &str) -> AppResult<bool> {
    let conn = db.lock().unwrap();
    let changed = conn.execute("DELETE FROM clients WHERE id = ?1", rusqlite::params![client_id])?;
    Ok(changed > 0)
}

pub fn pending_count(db: &Db) -> AppResult<i64> {
    let conn = db.lock().unwrap();
    Ok(conn.query_row("SELECT COUNT(*) FROM clients WHERE status = 'pending'", [], |row| row.get(0))?)
}

pub fn cleanup_stale_pending(db: &Db) -> AppResult<usize> {
    let conn = db.lock().unwrap();
    let deleted = conn.execute(
        "DELETE FROM clients WHERE status = 'pending' AND created_at < datetime('now', '-1 day')",
        [],
    )?;
    Ok(deleted)
}
