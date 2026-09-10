SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_role')
    ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_role;
ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_role
CHECK (role IN ('ADMINISTRATOR','VIEWER','GROUP_ADMIN','FISCALIZADOR','TECHNOLOGY','GENERAL_SERVICES'));

IF OBJECT_ID(N'dbo.support_tickets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.support_tickets (
        id uniqueidentifier NOT NULL CONSTRAINT DF_support_tickets_id DEFAULT (NEWID()),
        ticket_number bigint IDENTITY(1000,1) NOT NULL,
        agency_id char(36) NOT NULL,
        category varchar(30) NOT NULL,
        assigned_department varchar(25) NOT NULL,
        priority varchar(15) NOT NULL,
        status varchar(20) NOT NULL CONSTRAINT DF_support_tickets_status DEFAULT ('OPEN'),
        subject nvarchar(180) NOT NULL,
        description nvarchar(2000) NOT NULL,
        resolution nvarchar(2000) NULL,
        created_by_user_id nvarchar(80) NOT NULL,
        created_by_name nvarchar(160) NOT NULL,
        created_by_username nvarchar(180) NOT NULL,
        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_tickets_created DEFAULT (SYSUTCDATETIME()),
        updated_at datetime2(3) NOT NULL CONSTRAINT DF_support_tickets_updated DEFAULT (SYSUTCDATETIME()),
        closed_at datetime2(3) NULL,
        CONSTRAINT PK_support_tickets PRIMARY KEY (id),
        CONSTRAINT UQ_support_tickets_number UNIQUE (ticket_number),
        CONSTRAINT FK_support_tickets_agency FOREIGN KEY (agency_id) REFERENCES dbo.agencies(id),
        CONSTRAINT CK_support_tickets_category CHECK (category IN ('EQUIPMENT','CONNECTIVITY','INFRASTRUCTURE','PRINTER','SECURITY','OTHER')),
        CONSTRAINT CK_support_tickets_department CHECK (assigned_department IN ('TECHNOLOGY','GENERAL_SERVICES')),
        CONSTRAINT CK_support_tickets_priority CHECK (priority IN ('LOW','MEDIUM','HIGH','CRITICAL')),
        CONSTRAINT CK_support_tickets_status CHECK (status IN ('OPEN','IN_PROGRESS','RESOLVED','CLOSED'))
    );
END;

IF COL_LENGTH(N'dbo.support_tickets',N'assigned_department') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.support_tickets ADD assigned_department varchar(25) NOT NULL
        CONSTRAINT DF_support_tickets_department DEFAULT(''TECHNOLOGY'') WITH VALUES;';

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_department')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.support_tickets WITH CHECK ADD CONSTRAINT CK_support_tickets_department
    CHECK (assigned_department IN (''TECHNOLOGY'',''GENERAL_SERVICES''));';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'IX_support_tickets_department_status')
    EXEC sys.sp_executesql N'CREATE INDEX IX_support_tickets_department_status ON dbo.support_tickets(assigned_department,status,priority,created_at DESC);';

COMMIT TRANSACTION;
SELECT role,COUNT(*) AS usuarios FROM dbo.admin_users GROUP BY role ORDER BY role;
EXEC sys.sp_executesql N'SELECT assigned_department,status,COUNT(*) AS tickets FROM dbo.support_tickets GROUP BY assigned_department,status;';
