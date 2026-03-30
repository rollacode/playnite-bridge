-- Client connection status: pending → active/rejected
ALTER TABLE clients ADD COLUMN status TEXT NOT NULL DEFAULT 'active';
-- Existing clients stay 'active'. New connection requests start as 'pending'.
