use super::Db;
use crate::error::{AppError, AppResult};
use crate::models::{CollectionRemoval, Game, GameLink, GamesQuery, PaginatedGames, SourceCount};

/// Compute canonical key from source + gameId. Returns None if either is missing.
pub fn canonical_key(source: Option<&str>, game_id: Option<&str>) -> Option<String> {
    match (source, game_id) {
        (Some(s), Some(gid)) if !s.is_empty() && !gid.is_empty() => {
            Some(format!("{}:{}", s.to_lowercase(), gid))
        }
        _ => None,
    }
}

/// Upsert a game with conflict resolution. Returns Some(canonical_id) if synced, None if unsyncable.
pub fn upsert(db: &Db, game: &Game, client_id: &str) -> AppResult<Option<String>> {
    let canon = canonical_key(game.source.as_deref(), game.game_id.as_deref());
    if canon.is_none() {
        return Ok(None); // unsyncable — no canonical key
    }

    let conn = db.lock().unwrap();
    let tx = conn.unchecked_transaction()?;

    // Check if a game with this canonical key already exists under a different ID
    let existing_id: Option<String> = if let Some(ref ck) = canon {
        tx.query_row(
            "SELECT id FROM games WHERE canonical_key = ?1 AND id != ?2",
            rusqlite::params![ck, game.id],
            |row| row.get(0),
        ).ok()
    } else {
        None
    };

    let target_id = existing_id.as_deref().unwrap_or(&game.id);

    // Upsert the game — LWW for scalar fields, MAX for monotonic fields
    tx.execute(
        "INSERT INTO games (id, name, sorting_name, description, notes, game_id, source,
            release_date, community_score, critic_score, user_score, favorite, hidden,
            completion_status, version, playtime, play_count, last_activity,
            canonical_key, client_modified, updated_at,
            cover_hash, icon_hash, background_hash)
         VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,?10,?11,?12,?13,?14,?15,?16,?17,?18,?19,?20, datetime('now'),?21,?22,?23)
         ON CONFLICT(id) DO UPDATE SET
            name = CASE WHEN COALESCE(excluded.client_modified,'') > COALESCE(games.client_modified,'') THEN excluded.name ELSE games.name END,
            sorting_name = CASE WHEN COALESCE(excluded.client_modified,'') > COALESCE(games.client_modified,'') THEN excluded.sorting_name ELSE games.sorting_name END,
            description = CASE WHEN COALESCE(excluded.client_modified,'') > COALESCE(games.client_modified,'') THEN excluded.description ELSE games.description END,
            notes = CASE WHEN COALESCE(excluded.client_modified,'') > COALESCE(games.client_modified,'') THEN excluded.notes ELSE games.notes END,
            game_id = COALESCE(games.game_id, excluded.game_id),
            source = COALESCE(games.source, excluded.source),
            release_date = COALESCE(excluded.release_date, games.release_date),
            community_score = COALESCE(excluded.community_score, games.community_score),
            critic_score = COALESCE(excluded.critic_score, games.critic_score),
            user_score = CASE WHEN COALESCE(excluded.client_modified,'') > COALESCE(games.client_modified,'') THEN excluded.user_score ELSE games.user_score END,
            favorite = CASE WHEN COALESCE(excluded.client_modified,'') > COALESCE(games.client_modified,'') THEN excluded.favorite ELSE games.favorite END,
            hidden = CASE WHEN COALESCE(excluded.client_modified,'') > COALESCE(games.client_modified,'') THEN excluded.hidden ELSE games.hidden END,
            completion_status = CASE WHEN COALESCE(excluded.client_modified,'') > COALESCE(games.client_modified,'') THEN excluded.completion_status ELSE games.completion_status END,
            version = CASE WHEN COALESCE(excluded.client_modified,'') > COALESCE(games.client_modified,'') THEN excluded.version ELSE games.version END,
            playtime = MAX(games.playtime, excluded.playtime),
            play_count = MAX(games.play_count, excluded.play_count),
            last_activity = CASE WHEN COALESCE(excluded.last_activity,'') > COALESCE(games.last_activity,'') THEN excluded.last_activity ELSE games.last_activity END,
            canonical_key = COALESCE(excluded.canonical_key, games.canonical_key),
            client_modified = CASE WHEN COALESCE(excluded.client_modified,'') > COALESCE(games.client_modified,'') THEN excluded.client_modified ELSE games.client_modified END,
            cover_hash = COALESCE(excluded.cover_hash, games.cover_hash),
            icon_hash = COALESCE(excluded.icon_hash, games.icon_hash),
            background_hash = COALESCE(excluded.background_hash, games.background_hash),
            updated_at = datetime('now')",
        rusqlite::params![
            target_id, game.name, game.sorting_name, game.description, game.notes,
            game.game_id, game.source, game.release_date,
            game.community_score, game.critic_score, game.user_score,
            game.favorite as i32, game.hidden as i32,
            game.completion_status, game.version,
            game.playtime, game.play_count, game.last_activity,
            canon, game.updated_at, // client_modified comes from game.updated_at (which is Game.Modified)
            game.cover_hash, game.icon_hash, game.background_hash,
        ],
    )?;

    // Register alias if the pushed ID differs from canonical target
    if existing_id.is_some() {
        tx.execute(
            "INSERT OR REPLACE INTO game_aliases (client_id, playnite_guid, canonical_id)
             VALUES (?1, ?2, ?3)",
            rusqlite::params![client_id, game.id, target_id],
        )?;
    }

    // Upsert collections (UNION merge — adds, never removes here)
    upsert_collections(&tx, target_id, "genre", &game.genres)?;
    upsert_collections(&tx, target_id, "category", &game.categories)?;
    upsert_collections(&tx, target_id, "tag", &game.tags)?;
    upsert_collections(&tx, target_id, "feature", &game.features)?;
    upsert_collections(&tx, target_id, "developer", &game.developers)?;
    upsert_collections(&tx, target_id, "publisher", &game.publishers)?;
    upsert_collections(&tx, target_id, "platform", &game.platforms)?;
    upsert_collections(&tx, target_id, "series", &game.series)?;

    // Upsert links (replace)
    tx.execute("DELETE FROM game_links WHERE game_id = ?1", rusqlite::params![target_id])?;
    for link in &game.links {
        tx.execute(
            "INSERT INTO game_links (game_id, name, url) VALUES (?1, ?2, ?3)",
            rusqlite::params![target_id, link.name, link.url],
        )?;
    }

    tx.commit()?;
    Ok(Some(target_id.to_string()))
}

/// Process collection removals (tombstones)
pub fn apply_removals(db: &Db, removals: &[CollectionRemoval], client_id: &str) -> AppResult<()> {
    let conn = db.lock().unwrap();
    for removal in removals {
        let coll_type = match removal.field.as_str() {
            "categories" => "category",
            "tags" => "tag",
            "genres" => "genre",
            "features" => "feature",
            other => other,
        };
        for name in &removal.removed {
            // Insert tombstone
            conn.execute(
                "INSERT OR REPLACE INTO collection_tombstones (game_id, collection_type, collection_name, removed_by)
                 VALUES (?1, ?2, ?3, ?4)",
                rusqlite::params![removal.game_id, coll_type, name, client_id],
            )?;
            // Remove the association
            conn.execute(
                "DELETE FROM game_collections WHERE game_id = ?1 AND collection_id IN
                 (SELECT id FROM collection_items WHERE type = ?2 AND name = ?3)",
                rusqlite::params![removal.game_id, coll_type, name],
            )?;
        }
    }
    Ok(())
}

fn upsert_collections(
    conn: &rusqlite::Connection,
    game_id: &str,
    coll_type: &str,
    names: &[String],
) -> Result<(), rusqlite::Error> {
    for name in names {
        conn.execute(
            "INSERT OR IGNORE INTO collection_items (type, name) VALUES (?1, ?2)",
            rusqlite::params![coll_type, name],
        )?;
        conn.execute(
            "INSERT OR IGNORE INTO game_collections (game_id, collection_id)
             SELECT ?1, id FROM collection_items WHERE type = ?2 AND name = ?3",
            rusqlite::params![game_id, coll_type, name],
        )?;
        // Remove tombstone if re-added
        conn.execute(
            "DELETE FROM collection_tombstones WHERE game_id = ?1 AND collection_type = ?2 AND collection_name = ?3",
            rusqlite::params![game_id, coll_type, name],
        )?;
    }
    Ok(())
}

pub fn find_by_id(db: &Db, id: &str) -> AppResult<Option<Game>> {
    let conn = db.lock().unwrap();
    let mut stmt = conn.prepare(
        "SELECT id, name, sorting_name, description, notes, game_id, source,
                release_date, community_score, critic_score, user_score,
                favorite, hidden, completion_status, version,
                playtime, play_count, last_activity, created_at, updated_at,
                cover_hash, icon_hash, background_hash
         FROM games WHERE id = ?1"
    )?;

    let result = stmt.query_row(rusqlite::params![id], game_from_row);

    match result {
        Ok(mut game) => {
            load_collections(&conn, &mut game)?;
            load_links(&conn, &mut game)?;
            Ok(Some(game))
        }
        Err(rusqlite::Error::QueryReturnedNoRows) => Ok(None),
        Err(e) => Err(AppError::Database(e)),
    }
}

pub fn list(db: &Db, query: &GamesQuery) -> AppResult<PaginatedGames> {
    let conn = db.lock().unwrap();
    let limit = query.limit.unwrap_or(500).min(5000);
    let offset = query.offset.unwrap_or(0);

    let mut where_clauses = Vec::new();
    let mut params: Vec<Box<dyn rusqlite::types::ToSql>> = Vec::new();
    let mut param_idx = 1;

    if let Some(ref q) = query.q {
        where_clauses.push(format!("g.name LIKE ?{param_idx}"));
        params.push(Box::new(format!("%{q}%")));
        param_idx += 1;
    }
    if let Some(ref source) = query.source {
        where_clauses.push(format!("g.source = ?{param_idx}"));
        params.push(Box::new(source.clone()));
        param_idx += 1;
    }
    for (filter, coll_type) in [(&query.genre, "genre"), (&query.category, "category"), (&query.tag, "tag")] {
        if let Some(ref val) = filter {
            where_clauses.push(format!(
                "EXISTS (SELECT 1 FROM game_collections gc JOIN collection_items ci ON gc.collection_id = ci.id
                 WHERE gc.game_id = g.id AND ci.type = '{coll_type}' AND ci.name = ?{param_idx})"
            ));
            params.push(Box::new(val.clone()));
            param_idx += 1;
        }
    }
    if let Some(ref client_id) = query.installed_on {
        where_clauses.push(format!(
            "EXISTS (SELECT 1 FROM client_games cg WHERE cg.game_id = g.id AND cg.client_id = ?{param_idx} AND cg.is_installed = 1)"
        ));
        params.push(Box::new(client_id.clone()));
        param_idx += 1;
    }

    let where_sql = if where_clauses.is_empty() { String::new() } else { format!("WHERE {}", where_clauses.join(" AND ")) };
    let sort_col = match query.sort.as_deref() {
        Some("playtime") => "g.playtime", Some("added") => "g.created_at",
        Some("release") => "g.release_date", Some("lastplayed") => "g.last_activity",
        _ => "g.name",
    };
    let sort_dir = if query.descending.unwrap_or(false) { "DESC" } else { "ASC" };

    let count_sql = format!("SELECT COUNT(*) FROM games g {where_sql}");
    let total: i64 = {
        let mut stmt = conn.prepare(&count_sql)?;
        let param_refs: Vec<&dyn rusqlite::types::ToSql> = params.iter().map(|p| p.as_ref()).collect();
        stmt.query_row(param_refs.as_slice(), |row| row.get(0))?
    };

    let select_sql = format!(
        "SELECT g.id, g.name, g.sorting_name, g.description, g.notes, g.game_id, g.source,
                g.release_date, g.community_score, g.critic_score, g.user_score,
                g.favorite, g.hidden, g.completion_status, g.version,
                g.playtime, g.play_count, g.last_activity, g.created_at, g.updated_at,
                g.cover_hash, g.icon_hash, g.background_hash
         FROM games g {where_sql} ORDER BY {sort_col} {sort_dir} LIMIT ?{param_idx} OFFSET ?{}",
        param_idx + 1
    );
    params.push(Box::new(limit));
    params.push(Box::new(offset));

    let mut stmt = conn.prepare(&select_sql)?;
    let param_refs: Vec<&dyn rusqlite::types::ToSql> = params.iter().map(|p| p.as_ref()).collect();
    let mut games = stmt.query_map(param_refs.as_slice(), game_from_row)?
        .collect::<Result<Vec<_>, _>>()?;

    for game in &mut games {
        load_collections(&conn, game)?;
    }

    Ok(PaginatedGames { total, offset, limit, games })
}

pub fn count(db: &Db) -> AppResult<i64> {
    let conn = db.lock().unwrap();
    Ok(conn.query_row("SELECT COUNT(*) FROM games", [], |row| row.get(0))?)
}

pub fn total_playtime_hours(db: &Db) -> AppResult<f64> {
    let conn = db.lock().unwrap();
    let seconds: i64 = conn.query_row("SELECT COALESCE(SUM(playtime), 0) FROM games", [], |row| row.get(0))?;
    Ok(seconds as f64 / 3600.0)
}

pub fn sources_count(db: &Db) -> AppResult<Vec<SourceCount>> {
    let conn = db.lock().unwrap();
    let mut stmt = conn.prepare(
        "SELECT COALESCE(source, 'Unknown') as s, COUNT(*) FROM games GROUP BY s ORDER BY COUNT(*) DESC LIMIT 20"
    )?;
    let results = stmt.query_map([], |row| Ok(SourceCount { source: row.get(0)?, count: row.get(1)? }))?
        .collect::<Result<Vec<_>, _>>()?;
    Ok(results)
}

/// Collect all distinct non-null image hashes currently referenced by games.
pub fn all_image_hashes(db: &Db) -> AppResult<std::collections::HashSet<String>> {
    let conn = db.lock().unwrap();
    let mut set = std::collections::HashSet::new();
    let mut stmt = conn.prepare(
        "SELECT cover_hash FROM games WHERE cover_hash IS NOT NULL
         UNION SELECT icon_hash FROM games WHERE icon_hash IS NOT NULL
         UNION SELECT background_hash FROM games WHERE background_hash IS NOT NULL"
    )?;
    let rows = stmt.query_map([], |row| row.get::<_, String>(0))?;
    for row in rows {
        set.insert(row?);
    }
    Ok(set)
}

pub fn unsyncable_count(db: &Db) -> AppResult<i64> {
    let conn = db.lock().unwrap();
    Ok(conn.query_row("SELECT COUNT(*) FROM games WHERE canonical_key IS NULL", [], |row| row.get(0))?)
}

pub fn changed_since(db: &Db, since: &str, limit: i64) -> AppResult<(Vec<Game>, bool)> {
    let conn = db.lock().unwrap();
    let fetch_limit = limit + 1;
    let mut stmt = conn.prepare(
        "SELECT id, name, sorting_name, description, notes, game_id, source,
                release_date, community_score, critic_score, user_score,
                favorite, hidden, completion_status, version,
                playtime, play_count, last_activity, created_at, updated_at,
                cover_hash, icon_hash, background_hash
         FROM games WHERE updated_at > ?1 ORDER BY updated_at ASC LIMIT ?2"
    )?;

    let games = stmt.query_map(rusqlite::params![since, fetch_limit], game_from_row)?
        .collect::<Result<Vec<_>, _>>()?;

    let has_more = games.len() as i64 > limit;
    let mut games: Vec<Game> = games.into_iter().take(limit as usize).collect();

    for game in &mut games {
        load_collections(&conn, game)?;
        load_links(&conn, game)?;
    }

    Ok((games, has_more))
}

fn game_from_row(row: &rusqlite::Row) -> rusqlite::Result<Game> {
    let favorite_int: i32 = row.get(11)?;
    let hidden_int: i32 = row.get(12)?;
    Ok(Game {
        id: row.get(0)?, name: row.get(1)?, sorting_name: row.get(2)?,
        description: row.get(3)?, notes: row.get(4)?, game_id: row.get(5)?,
        source: row.get(6)?, release_date: row.get(7)?,
        community_score: row.get(8)?, critic_score: row.get(9)?, user_score: row.get(10)?,
        favorite: favorite_int != 0, hidden: hidden_int != 0,
        completion_status: row.get(13)?, version: row.get(14)?,
        playtime: row.get(15)?, play_count: row.get(16)?, last_activity: row.get(17)?,
        genres: Vec::new(), categories: Vec::new(), tags: Vec::new(),
        features: Vec::new(), developers: Vec::new(), publishers: Vec::new(),
        platforms: Vec::new(), series: Vec::new(), links: Vec::new(),
        cover_hash: row.get(20)?, icon_hash: row.get(21)?, background_hash: row.get(22)?,
        is_installed: None, created_at: row.get(18)?, updated_at: row.get(19)?,
    })
}

fn load_collections(conn: &rusqlite::Connection, game: &mut Game) -> Result<(), rusqlite::Error> {
    let mut stmt = conn.prepare(
        "SELECT ci.type, ci.name FROM game_collections gc
         JOIN collection_items ci ON gc.collection_id = ci.id
         WHERE gc.game_id = ?1 ORDER BY ci.type, ci.name"
    )?;
    for row in stmt.query_map(rusqlite::params![game.id], |row| Ok((row.get::<_, String>(0)?, row.get::<_, String>(1)?)))? {
        let (coll_type, name) = row?;
        match coll_type.as_str() {
            "genre" => game.genres.push(name), "category" => game.categories.push(name),
            "tag" => game.tags.push(name), "feature" => game.features.push(name),
            "developer" => game.developers.push(name), "publisher" => game.publishers.push(name),
            "platform" => game.platforms.push(name), "series" => game.series.push(name),
            _ => {}
        }
    }
    Ok(())
}

fn load_links(conn: &rusqlite::Connection, game: &mut Game) -> Result<(), rusqlite::Error> {
    let mut stmt = conn.prepare("SELECT name, url FROM game_links WHERE game_id = ?1")?;
    game.links = stmt.query_map(rusqlite::params![game.id], |row| {
        Ok(GameLink { name: row.get(0)?, url: row.get(1)? })
    })?.collect::<Result<Vec<_>, _>>()?;
    Ok(())
}
