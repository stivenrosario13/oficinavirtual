-- Solo lectura. Ejecutar en la base del sitio publicado.
-- No elimina agencias, modifica auditorías ni cambia asignaciones de supervisores.
SET NOCOUNT ON;

-- Caso de la captura: distinguir pendientes, completadas y registros con auditoría.
SELECT a.grupo,COUNT(*) AS agencias,
    SUM(CASE WHEN a.is_active=1 THEN 1 ELSE 0 END) AS activas,
    SUM(CASE WHEN a.status='PENDING' THEN 1 ELSE 0 END) AS pendientes,
    SUM(CASE WHEN a.status='COMPLETED' THEN 1 ELSE 0 END) AS completadas,
    SUM(CASE WHEN p.agency_id IS NOT NULL THEN 1 ELSE 0 END) AS con_auditoria
FROM dbo.agencies a
LEFT JOIN (SELECT DISTINCT agency_id FROM dbo.agency_profiles) p ON p.agency_id=a.id
WHERE UPPER(a.grupo) LIKE N'%ALBERTO%PROPIAS%'
GROUP BY a.grupo ORDER BY agencias DESC;

-- Todos los registros del grupo, con su identidad e historial vinculado.
SELECT a.id,a.codigo,a.terminal,a.grupo,a.source_key,a.is_active,a.status,
    (SELECT COUNT(*) FROM dbo.agency_profiles p WHERE p.agency_id=a.id) AS auditorias,
    a.created_at,a.updated_at
FROM dbo.agencies a
WHERE UPPER(a.grupo) LIKE N'%ALBERTO%PROPIAS%'
ORDER BY a.codigo,a.grupo;

-- Códigos con más de una identidad: revisar antes de consolidar registros.
WITH duplicates AS (
    SELECT UPPER(LTRIM(RTRIM(codigo))) AS code
    FROM dbo.agencies GROUP BY UPPER(LTRIM(RTRIM(codigo))) HAVING COUNT(*)>1
)
SELECT a.id,a.codigo,a.terminal,a.grupo,a.source_key,a.is_active,a.status,
    (SELECT COUNT(*) FROM dbo.agency_profiles p WHERE p.agency_id=a.id) AS auditorias
FROM dbo.agencies a JOIN duplicates d ON UPPER(LTRIM(RTRIM(a.codigo)))=d.code
ORDER BY a.codigo,a.grupo;

-- Misma terminal con códigos distintos: candidatos, no identidades confirmadas.
SELECT UPPER(LTRIM(RTRIM(terminal))) AS terminal,COUNT(*) AS registros,
    COUNT(DISTINCT UPPER(LTRIM(RTRIM(codigo)))) AS codigos_distintos
FROM dbo.agencies
GROUP BY UPPER(LTRIM(RTRIM(terminal)))
HAVING COUNT(DISTINCT UPPER(LTRIM(RTRIM(codigo))))>1;
