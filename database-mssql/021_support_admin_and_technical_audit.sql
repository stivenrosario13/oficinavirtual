SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.automatic_findings',N'U') IS NULL
    THROW 50001,'Ejecuta primero la migración 018 de hallazgos automáticos.',1;
IF COL_LENGTH(N'dbo.automatic_findings',N'resolved_at') IS NULL
    ALTER TABLE dbo.automatic_findings ADD resolved_at datetime2(3) NULL;
IF COL_LENGTH(N'dbo.automatic_findings',N'resolution_minutes') IS NULL
    ALTER TABLE dbo.automatic_findings ADD resolution_minutes int NULL;
IF COL_LENGTH(N'dbo.automatic_findings',N'resolved_by_name') IS NULL
    ALTER TABLE dbo.automatic_findings ADD resolved_by_name nvarchar(160) NULL;
IF COL_LENGTH(N'dbo.automatic_findings',N'resolved_by_username') IS NULL
    ALTER TABLE dbo.automatic_findings ADD resolved_by_username nvarchar(180) NULL;

COMMIT TRANSACTION;
SELECT COUNT(*) AS hallazgos,COUNT(resolved_at) AS resueltos FROM dbo.automatic_findings;
