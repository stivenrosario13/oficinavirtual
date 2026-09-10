-- Grupo Tejeda 2026
-- Ejecutar una sola vez en una base existente de MonsterASP.NET.
ALTER TABLE agency_profiles ADD COLUMN IF NOT EXISTS observations text NULL;

-- Todas las auditorías nuevas se registran como bancas de lotería desde la aplicación.
