-- Catálogo de grupos LTK — MySQL 8 / MariaDB (MonsterASP.NET)
-- Fuente validada: LTKResultadoBrutoDetalladoApi (2) (1).xlsx
-- Resultado esperado: 169 grupos y 3,319 agencias vinculadas por el campo grupo.
--
-- Este archivo NO inserta, elimina ni modifica agencias. Debe ejecutarse después de
-- 05_CARGAR_3319_AGENCIAS_LTK.sql, que ya contiene el grupo de cada agencia.

SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS agency_groups (
  id char(36) NOT NULL,
  source_key varchar(80) NOT NULL,
  grupo varchar(150) NOT NULL,
  agency_count int unsigned NOT NULL DEFAULT 0,
  is_active tinyint(1) NOT NULL DEFAULT 1,
  created_at datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  UNIQUE KEY uq_agency_groups_source_key (source_key),
  UNIQUE KEY uq_agency_groups_name (grupo),
  KEY ix_agency_groups_active_name (is_active, grupo)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

START TRANSACTION;

-- Si el catálogo se vuelve a importar, los grupos LTK que ya no estén presentes
-- quedan inactivos; los encontrados abajo se activan nuevamente.
UPDATE agency_groups
SET is_active = 0,
    updated_at = UTC_TIMESTAMP(3)
WHERE source_key LIKE 'LTK-GROUP:%';

INSERT INTO agency_groups (
  id,
  source_key,
  grupo,
  agency_count,
  is_active
)
SELECT
  UUID(),
  CONCAT('LTK-GROUP:', SHA2(TRIM(a.grupo), 256)),
  TRIM(a.grupo),
  COUNT(*),
  1
FROM agencies a
WHERE a.grupo IS NOT NULL
  AND TRIM(a.grupo) <> ''
GROUP BY TRIM(a.grupo)
ON DUPLICATE KEY UPDATE
  source_key = VALUES(source_key),
  agency_count = VALUES(agency_count),
  is_active = 1,
  updated_at = UTC_TIMESTAMP(3);

COMMIT;

-- Verificación: con el listado entregado debe indicar OK, 169 y 3,319.
SELECT
  CASE
    WHEN COUNT(*) = 169 AND COALESCE(SUM(agency_count), 0) = 3319 THEN 'OK'
    ELSE 'REVISAR: la carga de agencias está incompleta o contiene datos adicionales'
  END AS validacion,
  COUNT(*) AS grupos_activos,
  COALESCE(SUM(agency_count), 0) AS agencias_contabilizadas
FROM agency_groups
WHERE is_active = 1
  AND source_key LIKE 'LTK-GROUP:%';

-- Detalle de los grupos importados y comparación con las agencias existentes.
SELECT
  g.grupo,
  g.agency_count AS agencias_excel,
  COUNT(a.id) AS agencias_en_mysql
FROM agency_groups g
LEFT JOIN agencies a ON TRIM(a.grupo) = g.grupo
WHERE g.is_active = 1
  AND g.source_key LIKE 'LTK-GROUP:%'
GROUP BY g.id, g.grupo, g.agency_count
ORDER BY g.grupo;
