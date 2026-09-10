SET NOCOUNT ON;
DECLARE @entryTypeAdded bit=0;

IF COL_LENGTH('dbo.support_messages','deleted_at') IS NULL
  ALTER TABLE dbo.support_messages ADD deleted_at datetime2(3) NULL;
IF COL_LENGTH('dbo.support_messages','deleted_by_user_id') IS NULL
  ALTER TABLE dbo.support_messages ADD deleted_by_user_id nvarchar(80) NULL;
IF COL_LENGTH('dbo.support_messages','deleted_by_name') IS NULL
  ALTER TABLE dbo.support_messages ADD deleted_by_name nvarchar(160) NULL;

IF OBJECT_ID(N'dbo.agency_transition_progress',N'U') IS NULL
BEGIN
  CREATE TABLE dbo.agency_transition_progress(
    id uniqueidentifier NOT NULL CONSTRAINT PK_agency_transition_progress PRIMARY KEY DEFAULT NEWID(),
    project_id uniqueidentifier NOT NULL,
    stage_id uniqueidentifier NOT NULL,
    department varchar(30) NOT NULL,
    note nvarchar(2000) NOT NULL,
    progress_status varchar(20) NOT NULL,
    entry_type varchar(30) NOT NULL CONSTRAINT DF_agency_transition_progress_type DEFAULT 'ADVANCE',
    created_by_user_id nvarchar(80) NULL,
    created_by_name nvarchar(160) NOT NULL,
    created_at datetime2(3) NOT NULL CONSTRAINT DF_agency_transition_progress_created DEFAULT SYSUTCDATETIME(),
    updated_at datetime2(3) NULL,
    deleted_at datetime2(3) NULL,
    deleted_by_user_id nvarchar(80) NULL,
    deleted_by_name nvarchar(160) NULL,
    CONSTRAINT FK_agency_transition_progress_project FOREIGN KEY(project_id) REFERENCES dbo.agency_transition_projects(id) ON DELETE CASCADE,
    CONSTRAINT FK_agency_transition_progress_stage FOREIGN KEY(stage_id) REFERENCES dbo.agency_transition_stages(id),
    CONSTRAINT CK_agency_transition_progress_status CHECK(progress_status IN('IN_PROGRESS','COMPLETED','EDITED'))
  );
  CREATE INDEX IX_agency_transition_progress_stage ON dbo.agency_transition_progress(stage_id,created_at,id);
END;

IF COL_LENGTH('dbo.agency_transition_progress','entry_type') IS NULL BEGIN
  ALTER TABLE dbo.agency_transition_progress ADD entry_type varchar(30) NOT NULL CONSTRAINT DF_agency_transition_progress_type DEFAULT 'ADVANCE';
  SET @entryTypeAdded=1;
END;
IF COL_LENGTH('dbo.agency_transition_progress','updated_at') IS NULL ALTER TABLE dbo.agency_transition_progress ADD updated_at datetime2(3) NULL;
IF COL_LENGTH('dbo.agency_transition_progress','deleted_at') IS NULL ALTER TABLE dbo.agency_transition_progress ADD deleted_at datetime2(3) NULL;
IF COL_LENGTH('dbo.agency_transition_progress','deleted_by_user_id') IS NULL ALTER TABLE dbo.agency_transition_progress ADD deleted_by_user_id nvarchar(80) NULL;
IF COL_LENGTH('dbo.agency_transition_progress','deleted_by_name') IS NULL ALTER TABLE dbo.agency_transition_progress ADD deleted_by_name nvarchar(160) NULL;

IF OBJECT_ID(N'dbo.CK_agency_transition_progress_status',N'C') IS NOT NULL
  ALTER TABLE dbo.agency_transition_progress DROP CONSTRAINT CK_agency_transition_progress_status;
ALTER TABLE dbo.agency_transition_progress WITH CHECK ADD CONSTRAINT CK_agency_transition_progress_status
  CHECK(progress_status IN('IN_PROGRESS','COMPLETED','EDITED'));

IF @entryTypeAdded=1 UPDATE dbo.agency_transition_progress
SET entry_type=CASE WHEN progress_status='EDITED' THEN 'STAGE_EDIT' WHEN progress_status='COMPLETED' THEN 'STAGE_COMPLETION' ELSE 'ADVANCE' END;

INSERT INTO dbo.agency_transition_progress(project_id,stage_id,department,note,progress_status,entry_type,created_by_user_id,created_by_name,created_at)
SELECT p.id,s.id,s.department,s.notes,'EDITED','STAGE_EDIT',NULL,COALESCE(NULLIF(s.completed_by_name,''),NULLIF(p.created_by_name,''),'Registro migrado V190'),s.updated_at
FROM dbo.agency_transition_stages s
INNER JOIN dbo.agency_transition_projects p ON p.id=s.project_id
WHERE NULLIF(LTRIM(RTRIM(s.notes)),'') IS NOT NULL
  AND NOT EXISTS(SELECT 1 FROM dbo.agency_transition_progress h WHERE h.stage_id=s.id AND h.note=s.notes);

PRINT 'Eliminación segura de mensajes e historial acumulativo de avances habilitados.';
