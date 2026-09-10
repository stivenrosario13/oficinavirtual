SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.agency_transition_progress',N'U') IS NULL
    THROW 50037, 'Ejecuta primero la migración 036_chat_message_deletion_transition_progress.sql.', 1;

IF OBJECT_ID(N'dbo.agency_transition_progress_evidence',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.agency_transition_progress_evidence(
        id uniqueidentifier NOT NULL CONSTRAINT PK_agency_transition_progress_evidence PRIMARY KEY DEFAULT(NEWID()),
        project_id uniqueidentifier NOT NULL,
        stage_id uniqueidentifier NOT NULL,
        progress_id uniqueidentifier NOT NULL,
        file_name nvarchar(260) NOT NULL,
        content_type varchar(80) NOT NULL,
        photo_data varbinary(max) NOT NULL,
        uploaded_by_user_id nvarchar(80) NOT NULL,
        uploaded_by_name nvarchar(160) NOT NULL,
        captured_at datetime2(3) NOT NULL CONSTRAINT DF_agency_transition_progress_evidence_captured DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_agency_transition_progress_evidence_progress FOREIGN KEY(progress_id)
            REFERENCES dbo.agency_transition_progress(id) ON DELETE CASCADE
    );

    CREATE INDEX IX_agency_transition_progress_evidence_progress
        ON dbo.agency_transition_progress_evidence(progress_id,captured_at,id);
END;

COMMIT TRANSACTION;
