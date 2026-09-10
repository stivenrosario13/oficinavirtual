SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.automatic_findings',N'U') IS NULL
    THROW 50001,'Ejecuta primero la migración 018_separate_automatic_findings.sql.',1;

IF COL_LENGTH(N'dbo.automatic_findings',N'resolved_at') IS NULL
    ALTER TABLE dbo.automatic_findings ADD resolved_at datetime2(3) NULL;

IF COL_LENGTH(N'dbo.automatic_findings',N'resolution_minutes') IS NULL
    ALTER TABLE dbo.automatic_findings ADD resolution_minutes int NULL;

COMMIT TRANSACTION;
SELECT COUNT(*) AS hallazgos FROM dbo.automatic_findings;
