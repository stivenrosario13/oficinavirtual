/*
  GRUPO TEJEDA - SQL SERVER 2025
  Diagnóstico y reparación segura de administrador -> grupo -> agencias.
  Ejecutar en la base db60703 de MonsterASP. No es un script para MySQL.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.admin_users', N'U') IS NULL
    THROW 51001, 'Falta dbo.admin_users. Ejecuta primero 001_schema.sql.', 1;
IF OBJECT_ID(N'dbo.admin_user_groups', N'U') IS NULL
    THROW 51002, 'Falta dbo.admin_user_groups. Ejecuta primero 001_schema.sql.', 1;
IF OBJECT_ID(N'dbo.agencies', N'U') IS NULL
    THROW 51003, 'Falta dbo.agencies. Importa primero las agencias de SQL Server.', 1;

/* Columnas mínimas que utiliza la aplicación. */
IF COL_LENGTH(N'dbo.agencies', N'is_active') IS NULL
    ALTER TABLE dbo.agencies ADD is_active bit NOT NULL
        CONSTRAINT DF_agencies_active_repair DEFAULT (1) WITH VALUES;
IF COL_LENGTH(N'dbo.agencies', N'status') IS NULL
    ALTER TABLE dbo.agencies ADD status varchar(30) NOT NULL
        CONSTRAINT DF_agencies_status_repair DEFAULT ('PENDING') WITH VALUES;
IF COL_LENGTH(N'dbo.agencies', N'expected_latitude') IS NULL
    ALTER TABLE dbo.agencies ADD expected_latitude float NULL;
IF COL_LENGTH(N'dbo.agencies', N'expected_longitude') IS NULL
    ALTER TABLE dbo.agencies ADD expected_longitude float NULL;

BEGIN TRANSACTION;

/* Quita espacios invisibles en los extremos sin cambiar el nombre del grupo. */
UPDATE dbo.agencies
SET grupo = LTRIM(RTRIM(REPLACE(grupo, NCHAR(160), N' ')))
WHERE grupo <> LTRIM(RTRIM(REPLACE(grupo, NCHAR(160), N' ')));

UPDATE dbo.admin_user_groups
SET group_name = LTRIM(RTRIM(REPLACE(group_name, NCHAR(160), N' ')))
WHERE group_name <> LTRIM(RTRIM(REPLACE(group_name, NCHAR(160), N' ')));

/* Corrige asignaciones que solo difieren en mayúsculas, acentos o espacios. */
;WITH AgencyNames AS (
    SELECT grupo, COUNT(*) AS agency_count
    FROM dbo.agencies
    GROUP BY grupo
),
Matches AS (
    SELECT ug.user_id, ug.group_name AS old_name, MIN(a.grupo) AS exact_name
    FROM dbo.admin_user_groups ug
    INNER JOIN AgencyNames a
      ON LTRIM(RTRIM(ug.group_name)) COLLATE Latin1_General_100_CI_AI
       = LTRIM(RTRIM(a.grupo)) COLLATE Latin1_General_100_CI_AI
    GROUP BY ug.user_id, ug.group_name
    HAVING COUNT(*) = 1
)
UPDATE ug
SET group_name = m.exact_name
FROM dbo.admin_user_groups ug
INNER JOIN Matches m
  ON m.user_id = ug.user_id AND m.old_name = ug.group_name
WHERE ug.group_name <> m.exact_name
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.admin_user_groups duplicate
      WHERE duplicate.user_id = ug.user_id
        AND duplicate.group_name = m.exact_name
  );

COMMIT TRANSACTION;

/* RESULTADO 1: administradores y total de agencias que realmente pueden ver. */
SELECT
    u.email AS usuario,
    u.display_name AS nombre_administrador,
    u.role AS rol,
    u.is_active AS activo,
    COUNT(DISTINCT ug.group_name) AS grupos_asignados,
    COUNT(a.id) AS agencias_asignadas
FROM dbo.admin_users u
LEFT JOIN dbo.admin_user_groups ug ON ug.user_id = u.id
LEFT JOIN dbo.agencies a
  ON a.grupo COLLATE Latin1_General_100_CI_AI
   = ug.group_name COLLATE Latin1_General_100_CI_AI
WHERE u.role = 'GROUP_ADMIN'
GROUP BY u.email, u.display_name, u.role, u.is_active
ORDER BY u.display_name;

/* RESULTADO 2: columnas separadas por administrador, grupo y estado. */
SELECT
    u.email AS usuario,
    u.display_name AS nombre,
    ug.region AS region,
    ug.group_name AS grupo_asignado,
    COUNT(a.id) AS agencias_totales,
    SUM(CASE WHEN a.status = 'PENDING' THEN 1 ELSE 0 END) AS pendientes,
    SUM(CASE WHEN a.status = 'COMPLETED' THEN 1 ELSE 0 END) AS completadas,
    SUM(CASE WHEN a.status = 'REVIEW_REQUIRED' THEN 1 ELSE 0 END) AS en_revision
FROM dbo.admin_users u
INNER JOIN dbo.admin_user_groups ug ON ug.user_id = u.id
LEFT JOIN dbo.agencies a
  ON a.grupo COLLATE Latin1_General_100_CI_AI
   = ug.group_name COLLATE Latin1_General_100_CI_AI
WHERE u.role = 'GROUP_ADMIN'
GROUP BY u.email, u.display_name, ug.region, ug.group_name
ORDER BY u.display_name, ug.group_name;

/* RESULTADO 3: asignaciones que no encuentran ninguna agencia. */
SELECT
    u.email AS usuario,
    u.display_name AS nombre,
    ug.group_name AS grupo_sin_agencias
FROM dbo.admin_user_groups ug
INNER JOIN dbo.admin_users u ON u.id = ug.user_id
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.agencies a
    WHERE a.grupo COLLATE Latin1_General_100_CI_AI
        = ug.group_name COLLATE Latin1_General_100_CI_AI
)
ORDER BY u.display_name, ug.group_name;

/* RESULTADO 4: comprobación específica solicitada para Steven Rosario. */
SELECT
    u.email AS usuario,
    ug.group_name AS grupo,
    a.codigo,
    a.terminal,
    a.status,
    a.is_active
FROM dbo.admin_users u
INNER JOIN dbo.admin_user_groups ug ON ug.user_id = u.id
INNER JOIN dbo.agencies a
  ON a.grupo COLLATE Latin1_General_100_CI_AI
   = ug.group_name COLLATE Latin1_General_100_CI_AI
WHERE u.email IN (
    N'steven.rosario',
    N'steven.rosario@grupotejeda.local'
)
ORDER BY ug.group_name, a.terminal;
