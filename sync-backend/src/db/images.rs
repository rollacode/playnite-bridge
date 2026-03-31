use std::path::{Path, PathBuf};
use crate::error::{AppError, AppResult};

/// Compute the storage path for an image: `data_dir/images/{first2chars}/{hash}.bin`
fn image_path(data_dir: &Path, hash: &str) -> PathBuf {
    let prefix = if hash.len() >= 2 { &hash[..2] } else { hash };
    data_dir.join("images").join(prefix).join(format!("{hash}.bin"))
}

/// Store image bytes on disk, content-addressed by hash.
pub fn store(data_dir: &Path, hash: &str, bytes: &[u8]) -> AppResult<()> {
    let path = image_path(data_dir, hash);
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent)
            .map_err(|e| AppError::Internal(format!("Failed to create image dir: {e}")))?;
    }
    std::fs::write(&path, bytes)
        .map_err(|e| AppError::Internal(format!("Failed to write image {hash}: {e}")))?;
    Ok(())
}

/// Check if an image with this hash exists on disk.
pub fn exists(data_dir: &Path, hash: &str) -> bool {
    image_path(data_dir, hash).exists()
}

/// Load image bytes from disk.
pub fn load(data_dir: &Path, hash: &str) -> AppResult<Vec<u8>> {
    let path = image_path(data_dir, hash);
    std::fs::read(&path)
        .map_err(|e| AppError::NotFound(format!("Image {hash} not found: {e}")))
}

/// Remove images from disk that are not referenced by any game in the DB.
/// Returns count of files removed.
pub fn remove_unused(data_dir: &Path, db: &super::Db) -> AppResult<usize> {
    let referenced = super::games::all_image_hashes(db)?;
    let images_dir = data_dir.join("images");
    if !images_dir.exists() {
        return Ok(0);
    }

    let mut removed = 0;
    for prefix_entry in std::fs::read_dir(&images_dir).map_err(|e| AppError::Internal(e.to_string()))? {
        let prefix_entry = prefix_entry.map_err(|e| AppError::Internal(e.to_string()))?;
        if !prefix_entry.path().is_dir() {
            continue;
        }
        for file_entry in std::fs::read_dir(prefix_entry.path()).map_err(|e| AppError::Internal(e.to_string()))? {
            let file_entry = file_entry.map_err(|e| AppError::Internal(e.to_string()))?;
            let file_name = file_entry.file_name();
            let name = file_name.to_string_lossy();
            // Extract hash from filename (strip .bin extension)
            if let Some(hash) = name.strip_suffix(".bin") {
                if !referenced.contains(hash) {
                    if std::fs::remove_file(file_entry.path()).is_ok() {
                        removed += 1;
                    }
                }
            }
        }
    }

    Ok(removed)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    fn temp_dir() -> PathBuf {
        static COUNTER: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(0);
        let n = COUNTER.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        let dir = std::env::temp_dir().join(format!("ps_test_{}_{}_{}", std::process::id(), n,
            std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).unwrap().subsec_nanos()));
        std::fs::create_dir_all(dir.join("images")).unwrap();
        dir
    }

    #[test]
    fn test_store_and_load() {
        let dir = temp_dir();
        let hash = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        let data = b"fake image data";

        store(&dir, hash, data).unwrap();
        assert!(exists(&dir, hash));

        let loaded = load(&dir, hash).unwrap();
        assert_eq!(loaded, data);

        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn test_exists_returns_false_for_missing() {
        let dir = temp_dir();
        assert!(!exists(&dir, "nonexistent_hash"));
        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn test_load_missing_returns_error() {
        let dir = temp_dir();
        let result = load(&dir, "nonexistent_hash");
        assert!(result.is_err());
        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn test_image_path_structure() {
        let dir = PathBuf::from("/data");
        let hash = "ab3f7c1234567890";
        let path = image_path(&dir, hash);
        assert_eq!(path, PathBuf::from("/data/images/ab/ab3f7c1234567890.bin"));
    }

    #[test]
    fn test_store_creates_prefix_dirs() {
        let dir = temp_dir();
        let hash = "ff0011223344556677889900aabbccddeeff0011223344556677889900aabbcc";
        store(&dir, hash, b"test").unwrap();
        assert!(dir.join("images/ff").exists());
        assert!(exists(&dir, hash));
        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn test_overwrite_existing() {
        let dir = temp_dir();
        let hash = "1111111111111111111111111111111111111111111111111111111111111111";
        store(&dir, hash, b"version1").unwrap();
        store(&dir, hash, b"version2").unwrap();
        let loaded = load(&dir, hash).unwrap();
        assert_eq!(loaded, b"version2");
        std::fs::remove_dir_all(&dir).ok();
    }
}
