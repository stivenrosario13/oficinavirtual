SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.administrative_document_folders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.administrative_document_folders (
        id uniqueidentifier NOT NULL PRIMARY KEY,
        name nvarchar(120) NOT NULL,
        parent_id uniqueidentifier NULL REFERENCES dbo.administrative_document_folders(id),
        created_by nvarchar(80) NOT NULL,
        created_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_administrative_document_folders_parent
        ON dbo.administrative_document_folders(parent_id, name);
END;
