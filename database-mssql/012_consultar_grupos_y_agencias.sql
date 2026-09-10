/*
  CONSULTA DE GRUPOS Y AGENCIAS
  SQL Server 2022/2025 - MonsterASP

  Este archivo no modifica datos. Solo realiza consultas.
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.agencies', N'U') IS NULL
BEGIN
    THROW 50001, 'No existe la tabla dbo.agencies. Verifica la base de datos seleccionada.', 1;
END;

/* 1. Resumen general */
SELECT
    COUNT(DISTINCT NULLIF(LTRIM(RTRIM(grupo)), N'')) AS grupos_totales,
    COUNT(*) AS agencias_totales,
    SUM(CASE WHEN status = 'PENDING' THEN 1 ELSE 0 END) AS agencias_pendientes,
    SUM(CASE WHEN status = 'COMPLETED' THEN 1 ELSE 0 END) AS agencias_completadas,
    SUM(CASE WHEN status = 'REVIEW_REQUIRED' THEN 1 ELSE 0 END) AS agencias_en_revision
FROM dbo.agencies;

/* 2. Todos los grupos y cantidad de agencias por estado */
SELECT
    LTRIM(RTRIM(grupo)) AS grupo,
    COUNT(*) AS agencias_totales,
    SUM(CASE WHEN status = 'PENDING' THEN 1 ELSE 0 END) AS pendientes,
    SUM(CASE WHEN status = 'COMPLETED' THEN 1 ELSE 0 END) AS completadas,
    SUM(CASE WHEN status = 'REVIEW_REQUIRED' THEN 1 ELSE 0 END) AS en_revision,
    CAST(
        ROUND(
            100.0 * SUM(CASE WHEN status = 'COMPLETED' THEN 1 ELSE 0 END)
            / NULLIF(COUNT(*), 0),
            0
        ) AS int
    ) AS porcentaje_avance
FROM dbo.agencies
WHERE NULLIF(LTRIM(RTRIM(grupo)), N'') IS NOT NULL
GROUP BY LTRIM(RTRIM(grupo))
ORDER BY grupo;

/* 3. Detalle de todas las agencias y su grupo */
SELECT
    codigo,
    terminal AS agencia,
    LTRIM(RTRIM(grupo)) AS grupo,
    status AS estado,
    expected_latitude AS latitud_esperada,
    expected_longitude AS longitud_esperada,
    created_at AS fecha_creacion,
    updated_at AS ultima_actualizacion
FROM dbo.agencies
ORDER BY grupo, agencia, codigo;

/* 4. Detectar agencias sin grupo */
SELECT
    codigo,
    terminal AS agencia,
    grupo,
    status AS estado
FROM dbo.agencies
WHERE NULLIF(LTRIM(RTRIM(grupo)), N'') IS NULL
ORDER BY terminal;

