-- Image hashes for content-addressed artwork sync
ALTER TABLE games ADD COLUMN cover_hash TEXT;
ALTER TABLE games ADD COLUMN icon_hash TEXT;
ALTER TABLE games ADD COLUMN background_hash TEXT;
