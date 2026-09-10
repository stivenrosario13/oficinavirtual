SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.automatic_findings',N'U') IS NULL
    THROW 50001,'Ejecuta primero la migración 018_separate_automatic_findings.sql.',1;

IF COL_LENGTH(N'dbo.automatic_findings',N'diagnosis') IS NULL
    ALTER TABLE dbo.automatic_findings ADD diagnosis nvarchar(2000) NULL;
IF COL_LENGTH(N'dbo.automatic_findings',N'verification') IS NULL
    ALTER TABLE dbo.automatic_findings ADD verification nvarchar(2000) NULL;
IF COL_LENGTH(N'dbo.automatic_findings',N'recommendations') IS NULL
    ALTER TABLE dbo.automatic_findings ADD recommendations nvarchar(2000) NULL;

IF OBJECT_ID(N'dbo.finding_resolution_evidence',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.finding_resolution_evidence(
        id uniqueidentifier NOT NULL CONSTRAINT DF_finding_resolution_evidence_id DEFAULT(NEWID()),
        finding_id uniqueidentifier NOT NULL,
        file_name nvarchar(260) NOT NULL,
        content_type varchar(80) NOT NULL,
        photo_data varbinary(max) NOT NULL,
        uploaded_by_name nvarchar(160) NOT NULL,
        captured_at datetime2(3) NOT NULL CONSTRAINT DF_finding_resolution_evidence_date DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_finding_resolution_evidence PRIMARY KEY(id),
        CONSTRAINT FK_finding_resolution_evidence_finding FOREIGN KEY(finding_id) REFERENCES dbo.automatic_findings(id) ON DELETE CASCADE
    );
    CREATE INDEX IX_finding_resolution_evidence_finding
        ON dbo.finding_resolution_evidence(finding_id,captured_at,id);
END;

COMMIT TRANSACTION;

SELECT COUNT(*) AS evidencias_registradas FROM dbo.finding_resolution_evidence;
