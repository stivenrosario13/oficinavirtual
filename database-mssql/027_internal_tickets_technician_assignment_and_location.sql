SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.support_tickets',N'ticket_type') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.support_tickets ADD ticket_type varchar(15) NOT NULL
        CONSTRAINT DF_support_tickets_type DEFAULT(''SUPPORT'') WITH VALUES;';

IF COL_LENGTH(N'dbo.support_tickets',N'assigned_technician_id') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.support_tickets ADD assigned_technician_id nvarchar(80) NULL;';

IF COL_LENGTH(N'dbo.automatic_findings',N'assigned_technician_id') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.automatic_findings ADD assigned_technician_id nvarchar(80) NULL;';

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_type')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.support_tickets WITH CHECK ADD CONSTRAINT CK_support_tickets_type
        CHECK(ticket_type IN (''SUPPORT'',''INTERNAL''));';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'IX_support_tickets_type_agency_history')
    EXEC sys.sp_executesql N'CREATE INDEX IX_support_tickets_type_agency_history
        ON dbo.support_tickets(ticket_type,agency_id,status,updated_at DESC)
        INCLUDE(ticket_number,assigned_department,assigned_technician_id,subject,closed_at);';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.automatic_findings') AND name=N'IX_automatic_findings_technician')
    EXEC sys.sp_executesql N'CREATE INDEX IX_automatic_findings_technician
        ON dbo.automatic_findings(assigned_technician_id,status,updated_at DESC);';

COMMIT TRANSACTION;

EXEC sys.sp_executesql N'
    SELECT ticket_type,status,COUNT(*) AS cantidad
    FROM dbo.support_tickets
    GROUP BY ticket_type,status
    ORDER BY ticket_type,status;';
