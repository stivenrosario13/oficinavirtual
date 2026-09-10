-- Ejecutar una sola vez si la base ya existía antes de esta actualización.
ALTER TABLE agencies ADD COLUMN IF NOT EXISTS expected_latitude double NULL;
ALTER TABLE agencies ADD COLUMN IF NOT EXISTS expected_longitude double NULL;
ALTER TABLE agency_profiles ADD COLUMN IF NOT EXISTS distance_from_agency_meters double NULL;
ALTER TABLE agency_profiles ADD COLUMN IF NOT EXISTS structural_answers json NULL;
ALTER TABLE agency_profiles ADD COLUMN IF NOT EXISTS completion_percent tinyint unsigned NOT NULL DEFAULT 100;
ALTER TABLE agency_profiles ADD COLUMN IF NOT EXISTS employee_name varchar(160) NULL;
ALTER TABLE agency_profiles ADD COLUMN IF NOT EXISTS employee_code varchar(80) NULL;
ALTER TABLE agency_profiles ADD COLUMN IF NOT EXISTS observations text NULL;
ALTER TABLE audit_photos ADD COLUMN IF NOT EXISTS latitude double NULL;
ALTER TABLE audit_photos ADD COLUMN IF NOT EXISTS longitude double NULL;
ALTER TABLE audit_photos ADD COLUMN IF NOT EXISTS accuracy_meters double NULL;
ALTER TABLE audit_photos ADD COLUMN IF NOT EXISTS captured_at datetime(3) NULL;
