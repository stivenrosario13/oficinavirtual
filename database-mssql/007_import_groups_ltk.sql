-- Catálogo actualizado de grupos LTK - SQL Server 2025 / MonsterASP.NET
-- Resultado esperado: 161 grupos y 3122 agencias activas.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
UPDATE dbo.agency_groups SET is_active=0,updated_at=SYSUTCDATETIME() WHERE source_key LIKE N'LTK-GROUP:%';
;WITH source_groups AS (
 SELECT CONCAT(N'LTK-GROUP:',LTRIM(RTRIM(grupo))) AS source_key,LTRIM(RTRIM(grupo)) AS grupo,COUNT(*) AS agency_count
 FROM dbo.agencies WHERE is_active=1 AND source_key LIKE N'LTK:%' AND LTRIM(RTRIM(grupo))<>N'' GROUP BY LTRIM(RTRIM(grupo))
)
MERGE dbo.agency_groups WITH(HOLDLOCK) AS target USING source_groups AS source ON target.grupo=source.grupo
WHEN MATCHED THEN UPDATE SET source_key=source.source_key,agency_count=source.agency_count,is_active=1,updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(id,source_key,grupo,agency_count,is_active) VALUES(CONVERT(char(36),NEWID()),source.source_key,source.grupo,source.agency_count,1);
COMMIT TRANSACTION;
SELECT CASE WHEN COUNT(*)=161 AND COALESCE(SUM(agency_count),0)=3122 THEN N'OK' ELSE N'REVISAR' END AS validacion,COUNT(*) AS grupos_activos,COALESCE(SUM(agency_count),0) AS agencias_contabilizadas FROM dbo.agency_groups WHERE is_active=1 AND source_key LIKE N'LTK-GROUP:%';
