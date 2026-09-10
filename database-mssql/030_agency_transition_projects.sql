SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.agency_transition_projects',N'U') IS NULL
BEGIN
  CREATE TABLE dbo.agency_transition_projects(
    id uniqueidentifier NOT NULL CONSTRAINT PK_agency_transition_projects PRIMARY KEY DEFAULT NEWID(),
    agency_id char(36) NOT NULL,
    project_type varchar(20) NOT NULL,
    status varchar(20) NOT NULL CONSTRAINT DF_agency_transition_status DEFAULT 'ACTIVE',
    current_stage varchar(30) NOT NULL CONSTRAINT DF_agency_transition_stage DEFAULT 'GENERAL_SERVICES',
    notes nvarchar(2000) NULL,
    created_by_user_id nvarchar(80) NULL,
    created_by_name nvarchar(160) NOT NULL,
    created_at datetime2(3) NOT NULL CONSTRAINT DF_agency_transition_created DEFAULT SYSUTCDATETIME(),
    updated_at datetime2(3) NOT NULL CONSTRAINT DF_agency_transition_updated DEFAULT SYSUTCDATETIME(),
    completed_at datetime2(3) NULL,
    CONSTRAINT FK_agency_transition_agency FOREIGN KEY(agency_id) REFERENCES dbo.agencies(id),
    CONSTRAINT CK_agency_transition_type CHECK(project_type IN('CONSTRUCTION','RESTRUCTURING')),
    CONSTRAINT CK_agency_transition_status CHECK(status IN('ACTIVE','PENDING','COMPLETED','CANCELLED')),
    CONSTRAINT CK_agency_transition_stage CHECK(current_stage IN('GENERAL_SERVICES','TECHNOLOGY','HUMAN_RESOURCES','COMPLETED'))
  );
  CREATE INDEX IX_agency_transition_status ON dbo.agency_transition_projects(status,current_stage,updated_at DESC);
END;

IF OBJECT_ID(N'dbo.agency_transition_stages',N'U') IS NULL
BEGIN
  CREATE TABLE dbo.agency_transition_stages(
    id uniqueidentifier NOT NULL CONSTRAINT PK_agency_transition_stages PRIMARY KEY DEFAULT NEWID(),
    project_id uniqueidentifier NOT NULL,
    department varchar(30) NOT NULL,
    stage_order tinyint NOT NULL,
    status varchar(20) NOT NULL CONSTRAINT DF_agency_transition_stage_status DEFAULT 'PENDING',
    notes nvarchar(2000) NULL,
    started_at datetime2(3) NULL,
    completed_at datetime2(3) NULL,
    completed_by_user_id nvarchar(80) NULL,
    completed_by_name nvarchar(160) NULL,
    updated_at datetime2(3) NOT NULL CONSTRAINT DF_agency_transition_stage_updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_agency_transition_stage_project FOREIGN KEY(project_id) REFERENCES dbo.agency_transition_projects(id) ON DELETE CASCADE,
    CONSTRAINT UQ_agency_transition_stage UNIQUE(project_id,department),
    CONSTRAINT CK_agency_transition_stage_department CHECK(department IN('GENERAL_SERVICES','TECHNOLOGY','HUMAN_RESOURCES')),
    CONSTRAINT CK_agency_transition_stage_work_status CHECK(status IN('PENDING','IN_PROGRESS','COMPLETED'))
  );
END;

PRINT 'Flujo compartido de agencias en construcción y reestructuración habilitado.';
