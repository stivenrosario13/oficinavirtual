SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.admin_users',N'support_team') IS NULL
    ALTER TABLE dbo.admin_users ADD support_team varchar(30) NULL;

IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_support_team')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_support_team
        CHECK(support_team IS NULL OR (role=''TECHNOLOGY'' AND support_team IN(''CALL_CENTER'',''TECHNICAL_FAILURE'')));';

IF COL_LENGTH(N'dbo.support_tickets',N'ticket_type') IS NULL
    ALTER TABLE dbo.support_tickets ADD ticket_type varchar(15) NOT NULL
        CONSTRAINT DF_support_tickets_type DEFAULT('SUPPORT') WITH VALUES;
IF COL_LENGTH(N'dbo.support_tickets',N'assigned_technician_id') IS NULL
    ALTER TABLE dbo.support_tickets ADD assigned_technician_id nvarchar(80) NULL;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_type')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.support_tickets WITH CHECK ADD CONSTRAINT CK_support_tickets_type
        CHECK(ticket_type IN(''SUPPORT'',''INTERNAL''));';

IF COL_LENGTH(N'dbo.support_tickets',N'assigned_team') IS NULL
    ALTER TABLE dbo.support_tickets ADD assigned_team varchar(30) NULL;
IF COL_LENGTH(N'dbo.support_tickets',N'resolved_by_user_id') IS NULL
    ALTER TABLE dbo.support_tickets ADD resolved_by_user_id nvarchar(80) NULL;
IF COL_LENGTH(N'dbo.support_tickets',N'resolved_by_name') IS NULL
    ALTER TABLE dbo.support_tickets ADD resolved_by_name nvarchar(160) NULL;
IF COL_LENGTH(N'dbo.support_tickets',N'resolved_at') IS NULL
    ALTER TABLE dbo.support_tickets ADD resolved_at datetime2(3) NULL;

IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_team')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.support_tickets WITH CHECK ADD CONSTRAINT CK_support_tickets_team
        CHECK(assigned_team IS NULL OR assigned_team IN(''CALL_CENTER'',''TECHNICAL_FAILURE''));';

IF OBJECT_ID(N'dbo.support_ticket_shares',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.support_ticket_shares(
        ticket_id uniqueidentifier NOT NULL,
        department_code varchar(40) NOT NULL,
        shared_by_user_id nvarchar(80) NOT NULL,
        shared_by_name nvarchar(160) NOT NULL,
        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_ticket_shares_created DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_support_ticket_shares PRIMARY KEY(ticket_id,department_code),
        CONSTRAINT FK_support_ticket_shares_ticket FOREIGN KEY(ticket_id) REFERENCES dbo.support_tickets(id) ON DELETE CASCADE
    );
    CREATE INDEX IX_support_ticket_shares_department ON dbo.support_ticket_shares(department_code,created_at DESC);
END;

IF OBJECT_ID(N'dbo.support_ticket_history',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.support_ticket_history(
        id uniqueidentifier NOT NULL CONSTRAINT DF_support_ticket_history_id DEFAULT(NEWID()),
        ticket_id uniqueidentifier NOT NULL,
        action varchar(40) NOT NULL,
        from_department varchar(40) NULL,
        to_department varchar(40) NULL,
        from_team varchar(30) NULL,
        to_team varchar(30) NULL,
        actor_user_id nvarchar(80) NULL,
        actor_name nvarchar(160) NOT NULL,
        comment nvarchar(1000) NULL,
        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_ticket_history_created DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_support_ticket_history PRIMARY KEY(id),
        CONSTRAINT FK_support_ticket_history_ticket FOREIGN KEY(ticket_id) REFERENCES dbo.support_tickets(id) ON DELETE CASCADE
    );
    CREATE INDEX IX_support_ticket_history_ticket ON dbo.support_ticket_history(ticket_id,created_at,id);
END;

IF OBJECT_ID(N'dbo.support_ticket_evidence',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.support_ticket_evidence(
        id uniqueidentifier NOT NULL CONSTRAINT DF_support_ticket_evidence_id DEFAULT(NEWID()),
        ticket_id uniqueidentifier NOT NULL,
        file_name nvarchar(260) NOT NULL,
        content_type varchar(80) NOT NULL,
        photo_data varbinary(max) NOT NULL,
        uploaded_by_user_id nvarchar(80) NOT NULL,
        uploaded_by_name nvarchar(160) NOT NULL,
        captured_at datetime2(3) NOT NULL CONSTRAINT DF_support_ticket_evidence_created DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_support_ticket_evidence PRIMARY KEY(id),
        CONSTRAINT FK_support_ticket_evidence_ticket FOREIGN KEY(ticket_id) REFERENCES dbo.support_tickets(id) ON DELETE CASCADE
    );
    CREATE INDEX IX_support_ticket_evidence_ticket ON dbo.support_ticket_evidence(ticket_id,captured_at,id);
END;

IF OBJECT_ID(N'dbo.support_ticket_notifications',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.support_ticket_notifications(
        id uniqueidentifier NOT NULL CONSTRAINT DF_support_ticket_notifications_id DEFAULT(NEWID()),
        ticket_id uniqueidentifier NOT NULL,
        event_type varchar(30) NOT NULL,
        recipient_user_id nvarchar(80) NULL,
        target_department varchar(40) NULL,
        message nvarchar(500) NULL,
        actor_user_id nvarchar(80) NULL,
        actor_name nvarchar(160) NULL,
        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_ticket_notifications_created DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_support_ticket_notifications PRIMARY KEY(id),
        CONSTRAINT FK_support_ticket_notifications_ticket FOREIGN KEY(ticket_id) REFERENCES dbo.support_tickets(id) ON DELETE CASCADE
    );
    CREATE INDEX IX_support_ticket_notifications_recipient ON dbo.support_ticket_notifications(recipient_user_id,created_at DESC);
    CREATE INDEX IX_support_ticket_notifications_department ON dbo.support_ticket_notifications(target_department,created_at DESC);
END;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'IX_support_tickets_team_assignment')
    EXEC sys.sp_executesql N'CREATE INDEX IX_support_tickets_team_assignment
        ON dbo.support_tickets(assigned_department,assigned_team,status,assigned_technician_id,updated_at DESC);';

EXEC sys.sp_executesql N'
    UPDATE dbo.support_tickets
    SET assigned_team=CASE WHEN ticket_type=''INTERNAL'' THEN ''CALL_CENTER'' ELSE ''TECHNICAL_FAILURE'' END
    WHERE assigned_department=''TECHNOLOGY'' AND assigned_team IS NULL;

    UPDATE dbo.support_tickets
    SET resolved_at=COALESCE(closed_at,updated_at)
    WHERE status IN(''RESOLVED'',''CLOSED'') AND resolved_at IS NULL;

    INSERT INTO dbo.support_ticket_history(ticket_id,action,to_department,to_team,actor_user_id,actor_name,comment,created_at)
    SELECT t.id,''CREATED'',t.assigned_department,t.assigned_team,t.created_by_user_id,t.created_by_name,
           N''Ticket existente incorporado al historial.'',t.created_at
    FROM dbo.support_tickets t
    WHERE NOT EXISTS(SELECT 1 FROM dbo.support_ticket_history h WHERE h.ticket_id=t.id);

    INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,target_department,message,actor_user_id,actor_name,created_at)
    SELECT t.id,''CREATED'',t.assigned_department,N''Se abrió el ticket.'',t.created_by_user_id,t.created_by_name,t.created_at
    FROM dbo.support_tickets t
    WHERE NOT EXISTS(
        SELECT 1 FROM dbo.support_ticket_notifications n
        WHERE n.ticket_id=t.id AND n.event_type=''CREATED'' AND n.recipient_user_id IS NULL AND n.target_department=t.assigned_department
    );

    INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name,created_at)
    SELECT t.id,''CREATED'',CONVERT(nvarchar(80),u.id),N''Se abrió un ticket en una agencia bajo tu supervisión.'',t.created_by_user_id,t.created_by_name,t.created_at
    FROM dbo.support_tickets t
    INNER JOIN dbo.agencies a ON a.id=t.agency_id
    INNER JOIN dbo.admin_user_groups ug ON ug.group_name=a.grupo
    INNER JOIN dbo.admin_users u ON u.id=ug.user_id AND u.role=''GROUP_ADMIN'' AND u.is_active=1
    WHERE NOT EXISTS(
        SELECT 1 FROM dbo.support_ticket_notifications n
        WHERE n.ticket_id=t.id AND n.event_type=''CREATED'' AND n.recipient_user_id=CONVERT(nvarchar(80),u.id)
    );

    INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,target_department,message,actor_user_id,actor_name,created_at)
    SELECT t.id,t.status,t.assigned_department,
           CASE WHEN t.status=''CLOSED'' THEN N''El ticket fue cerrado.'' ELSE N''El ticket fue resuelto.'' END,
           t.resolved_by_user_id,t.resolved_by_name,COALESCE(t.resolved_at,t.closed_at,t.updated_at)
    FROM dbo.support_tickets t
    WHERE t.status IN(''RESOLVED'',''CLOSED'') AND NOT EXISTS(
        SELECT 1 FROM dbo.support_ticket_notifications n
        WHERE n.ticket_id=t.id AND n.event_type=t.status AND n.recipient_user_id IS NULL AND n.target_department=t.assigned_department
    );

    INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name,created_at)
    SELECT t.id,t.status,CONVERT(nvarchar(80),u.id),
           CASE WHEN t.status=''CLOSED'' THEN N''El ticket de tu agencia fue cerrado.'' ELSE N''El ticket de tu agencia fue resuelto.'' END,
           t.resolved_by_user_id,t.resolved_by_name,COALESCE(t.resolved_at,t.closed_at,t.updated_at)
    FROM dbo.support_tickets t
    INNER JOIN dbo.agencies a ON a.id=t.agency_id
    INNER JOIN dbo.admin_user_groups ug ON ug.group_name=a.grupo
    INNER JOIN dbo.admin_users u ON u.id=ug.user_id AND u.role=''GROUP_ADMIN'' AND u.is_active=1
    WHERE t.status IN(''RESOLVED'',''CLOSED'') AND NOT EXISTS(
        SELECT 1 FROM dbo.support_ticket_notifications n
        WHERE n.ticket_id=t.id AND n.event_type=t.status AND n.recipient_user_id=CONVERT(nvarchar(80),u.id)
    );';

COMMIT TRANSACTION;

EXEC sys.sp_executesql N'
    SELECT assigned_department,assigned_team,status,COUNT(*) AS tickets
    FROM dbo.support_tickets
    GROUP BY assigned_department,assigned_team,status
    ORDER BY assigned_department,assigned_team,status;';
