using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

sealed partial class Database
{
    readonly List<(string Value, string Mode)> connections = [];
    readonly string? configurationError;
    static readonly System.Text.Json.JsonSerializerOptions StorageJson = new(System.Text.Json.JsonSerializerDefaults.Web);
    static readonly SemaphoreSlim SupportChatSchemaLock = new(1, 1);
    static bool supportChatSchemaReady;
    static readonly SemaphoreSlim UserPermissionSchemaLock = new(1, 1);
    static bool userPermissionSchemaReady;
    static readonly SemaphoreSlim FindingEvidenceSchemaLock = new(1, 1);
    static bool findingEvidenceSchemaReady;
    static readonly SemaphoreSlim TicketWorkflowSchemaLock = new(1,1);
    static bool ticketWorkflowSchemaReady;
    static readonly SemaphoreSlim NotificationReceiptSchemaLock = new(1,1);
    static bool notificationReceiptSchemaReady;
    static readonly SemaphoreSlim MaintenanceSchemaLock = new(1,1);
    static bool maintenanceSchemaReady;
    static readonly SemaphoreSlim AgencyTransitionEvidenceSchemaLock = new(1,1);
    static bool agencyTransitionEvidenceSchemaReady;
    static readonly SemaphoreSlim AgencyDirectorySchemaLock = new(1,1);
    static bool agencyDirectorySchemaReady;

    static async Task EnsureAgencyDirectorySchema(SqlConnection connection,CancellationToken ct)
    {
        if(agencyDirectorySchemaReady)return;
        await AgencyDirectorySchemaLock.WaitAsync(ct);
        try
        {
            if(agencyDirectorySchemaReady)return;
            const string sql="""
                IF OBJECT_ID(N'dbo.agencies',N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'dbo.agencies',N'directory_direccion') IS NULL ALTER TABLE dbo.agencies ADD directory_direccion nvarchar(300) NULL;
                    IF COL_LENGTH(N'dbo.agencies',N'directory_sector') IS NULL ALTER TABLE dbo.agencies ADD directory_sector nvarchar(120) NULL;
                    IF COL_LENGTH(N'dbo.agencies',N'directory_municipio') IS NULL ALTER TABLE dbo.agencies ADD directory_municipio nvarchar(120) NULL;
                    IF COL_LENGTH(N'dbo.agencies',N'directory_provincia') IS NULL ALTER TABLE dbo.agencies ADD directory_provincia nvarchar(120) NULL;
                END;
                """;
            await using var command=new SqlCommand(sql,connection);
            await command.ExecuteNonQueryAsync(ct);
            agencyDirectorySchemaReady=true;
        }
        finally{AgencyDirectorySchemaLock.Release();}
    }

    static async Task EnsureAgencyTransitionEvidenceSchema(SqlConnection connection,CancellationToken ct)
    {
        if(agencyTransitionEvidenceSchemaReady)return;
        await AgencyTransitionEvidenceSchemaLock.WaitAsync(ct);
        try
        {
            if(agencyTransitionEvidenceSchemaReady)return;
            const string sql="""
                IF OBJECT_ID(N'dbo.agency_transition_progress',N'U') IS NOT NULL
                   AND OBJECT_ID(N'dbo.agency_transition_progress_evidence',N'U') IS NULL
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
                        latitude decimal(9,6) NULL,
                        longitude decimal(10,6) NULL,
                        accuracy_meters decimal(8,2) NULL,
                        location_captured_at datetime2(3) NULL,
                        captured_at datetime2(3) NOT NULL CONSTRAINT DF_agency_transition_progress_evidence_captured DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT FK_agency_transition_progress_evidence_progress FOREIGN KEY(progress_id) REFERENCES dbo.agency_transition_progress(id) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_agency_transition_progress_evidence_progress ON dbo.agency_transition_progress_evidence(progress_id,captured_at,id);
                END;
                IF OBJECT_ID(N'dbo.agency_transition_progress_evidence',N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'dbo.agency_transition_progress_evidence',N'latitude') IS NULL ALTER TABLE dbo.agency_transition_progress_evidence ADD latitude decimal(9,6) NULL;
                    IF COL_LENGTH(N'dbo.agency_transition_progress_evidence',N'longitude') IS NULL ALTER TABLE dbo.agency_transition_progress_evidence ADD longitude decimal(10,6) NULL;
                    IF COL_LENGTH(N'dbo.agency_transition_progress_evidence',N'accuracy_meters') IS NULL ALTER TABLE dbo.agency_transition_progress_evidence ADD accuracy_meters decimal(8,2) NULL;
                    IF COL_LENGTH(N'dbo.agency_transition_progress_evidence',N'location_captured_at') IS NULL ALTER TABLE dbo.agency_transition_progress_evidence ADD location_captured_at datetime2(3) NULL;
                END;
                """;
            await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync(ct);agencyTransitionEvidenceSchemaReady=true;
        }
        finally{AgencyTransitionEvidenceSchemaLock.Release();}
    }

    static async Task EnsureAgencyTransitionStages(SqlConnection connection,CancellationToken ct)
    {
        const string sql="""
            IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.agency_transition_projects') AND name=N'CK_agency_transition_status' AND definition NOT LIKE '%PENDING%')
                ALTER TABLE dbo.agency_transition_projects DROP CONSTRAINT CK_agency_transition_status;
            IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.agency_transition_projects') AND name=N'CK_agency_transition_status')
                ALTER TABLE dbo.agency_transition_projects WITH CHECK ADD CONSTRAINT CK_agency_transition_status CHECK(status IN('ACTIVE','PENDING','COMPLETED','CANCELLED'));
            INSERT INTO dbo.agency_transition_stages(project_id,department,stage_order,status,started_at,completed_at,updated_at)
            SELECT p.id,v.department,v.stage_order,
              CASE
                WHEN p.status='COMPLETED' OR
                  (v.stage_order=1 AND p.current_stage IN('TECHNOLOGY','HUMAN_RESOURCES','COMPLETED')) OR
                  (v.stage_order=2 AND p.current_stage IN('HUMAN_RESOURCES','COMPLETED')) OR
                  (v.stage_order=3 AND p.current_stage='COMPLETED') THEN 'COMPLETED'
                WHEN p.current_stage=v.department THEN 'IN_PROGRESS'
                ELSE 'PENDING'
              END,
              CASE WHEN p.current_stage=v.department OR p.status='COMPLETED' OR
                (v.stage_order=1 AND p.current_stage IN('TECHNOLOGY','HUMAN_RESOURCES','COMPLETED')) OR
                (v.stage_order=2 AND p.current_stage IN('HUMAN_RESOURCES','COMPLETED')) OR
                (v.stage_order=3 AND p.current_stage='COMPLETED') THEN COALESCE(p.created_at,SYSUTCDATETIME()) ELSE NULL END,
              CASE WHEN p.status='COMPLETED' OR
                (v.stage_order=1 AND p.current_stage IN('TECHNOLOGY','HUMAN_RESOURCES','COMPLETED')) OR
                (v.stage_order=2 AND p.current_stage IN('HUMAN_RESOURCES','COMPLETED')) OR
                (v.stage_order=3 AND p.current_stage='COMPLETED') THEN COALESCE(p.completed_at,p.updated_at) ELSE NULL END,
              SYSUTCDATETIME()
            FROM dbo.agency_transition_projects p
            CROSS JOIN (VALUES('GENERAL_SERVICES',CONVERT(tinyint,1)),('TECHNOLOGY',CONVERT(tinyint,2)),('HUMAN_RESOURCES',CONVERT(tinyint,3)))v(department,stage_order)
            WHERE p.status<>'CANCELLED'
              AND NOT EXISTS(SELECT 1 FROM dbo.agency_transition_stages s WHERE s.project_id=p.id AND s.department=v.department);
            """;
        await using var command=new SqlCommand(sql,connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    static async Task<(bool Exists,bool EntryType,bool UpdatedAt,bool DeletedAt,bool DeleteAudit)> AgencyTransitionProgressCapabilities(SqlConnection connection,CancellationToken ct)
    {
        const string sql="""SELECT CASE WHEN OBJECT_ID(N'dbo.agency_transition_progress',N'U') IS NULL THEN 0 ELSE 1 END,CASE WHEN COL_LENGTH(N'dbo.agency_transition_progress',N'entry_type') IS NULL THEN 0 ELSE 1 END,CASE WHEN COL_LENGTH(N'dbo.agency_transition_progress',N'updated_at') IS NULL THEN 0 ELSE 1 END,CASE WHEN COL_LENGTH(N'dbo.agency_transition_progress',N'deleted_at') IS NULL THEN 0 ELSE 1 END,CASE WHEN COL_LENGTH(N'dbo.agency_transition_progress',N'deleted_by_user_id') IS NULL OR COL_LENGTH(N'dbo.agency_transition_progress',N'deleted_by_name') IS NULL THEN 0 ELSE 1 END;""";
        await using var command=new SqlCommand(sql,connection);await using var reader=await command.ExecuteReaderAsync(ct);await reader.ReadAsync(ct);return(reader.GetInt32(0)==1,reader.GetInt32(1)==1,reader.GetInt32(2)==1,reader.GetInt32(3)==1,reader.GetInt32(4)==1);
    }

    static async Task EnsureNotificationReceiptSchema(SqlConnection connection,CancellationToken ct)
    {
        if(notificationReceiptSchemaReady)return;
        await NotificationReceiptSchemaLock.WaitAsync(ct);
        try
        {
            if(notificationReceiptSchemaReady)return;
            const string sql="""
                IF OBJECT_ID(N'dbo.support_notification_receipts',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.support_notification_receipts(
                        user_id nvarchar(80) NOT NULL,
                        notification_key nvarchar(100) NOT NULL,
                        read_at datetime2(3) NULL,
                        dismissed_at datetime2(3) NULL,
                        updated_at datetime2(3) NOT NULL CONSTRAINT DF_support_notification_receipts_updated DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT PK_support_notification_receipts PRIMARY KEY(user_id,notification_key)
                    );
                    CREATE INDEX IX_support_notification_receipts_user ON dbo.support_notification_receipts(user_id,dismissed_at,read_at);
                END;
                """;
            await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync(ct);notificationReceiptSchemaReady=true;
        }
        finally{NotificationReceiptSchemaLock.Release();}
    }

    static async Task EnsureTicketWorkflowSchema(SqlConnection connection,CancellationToken ct)
    {
        if(ticketWorkflowSchemaReady)return;await TicketWorkflowSchemaLock.WaitAsync(ct);try{if(ticketWorkflowSchemaReady)return;const string sql="""
            IF COL_LENGTH(N'dbo.support_tickets',N'ticket_type') IS NULL ALTER TABLE dbo.support_tickets ADD ticket_type varchar(15) NOT NULL CONSTRAINT DF_support_tickets_type DEFAULT('SUPPORT') WITH VALUES;
            IF COL_LENGTH(N'dbo.support_tickets',N'assigned_technician_id') IS NULL ALTER TABLE dbo.support_tickets ADD assigned_technician_id nvarchar(80) NULL;
            IF COL_LENGTH(N'dbo.support_tickets',N'assigned_team') IS NULL ALTER TABLE dbo.support_tickets ADD assigned_team varchar(30) NULL;
            IF COL_LENGTH(N'dbo.support_tickets',N'resolved_by_user_id') IS NULL ALTER TABLE dbo.support_tickets ADD resolved_by_user_id nvarchar(80) NULL;
            IF COL_LENGTH(N'dbo.support_tickets',N'resolved_by_name') IS NULL ALTER TABLE dbo.support_tickets ADD resolved_by_name nvarchar(160) NULL;
            IF COL_LENGTH(N'dbo.support_tickets',N'resolved_at') IS NULL ALTER TABLE dbo.support_tickets ADD resolved_at datetime2(3) NULL;
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
            """;
            await using(var schemaCommand=new SqlCommand(sql,connection))await schemaCommand.ExecuteNonQueryAsync(ct);
            const string backfillSql="""
            IF COL_LENGTH(N'dbo.support_ticket_evidence',N'latitude') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD latitude float NULL;
            IF COL_LENGTH(N'dbo.support_ticket_evidence',N'longitude') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD longitude float NULL;
            IF COL_LENGTH(N'dbo.support_ticket_evidence',N'accuracy_meters') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD accuracy_meters float NULL;
            IF COL_LENGTH(N'dbo.support_ticket_evidence',N'location_captured_at') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD location_captured_at datetime2(3) NULL;
            IF COL_LENGTH(N'dbo.support_ticket_evidence',N'location_source') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD location_source varchar(30) NULL;
            IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_status' AND definition NOT LIKE '%PENDING%')
                ALTER TABLE dbo.support_tickets DROP CONSTRAINT CK_support_tickets_status;
            IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_status')
                ALTER TABLE dbo.support_tickets WITH CHECK ADD CONSTRAINT CK_support_tickets_status CHECK(status IN('OPEN','IN_PROGRESS','PENDING','RESOLVED','CLOSED','CANCELLED'));
            IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_team' AND definition NOT LIKE '%TECHNICIANS%')
                ALTER TABLE dbo.support_tickets DROP CONSTRAINT CK_support_tickets_team;
            IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_team')
                ALTER TABLE dbo.support_tickets WITH CHECK ADD CONSTRAINT CK_support_tickets_team CHECK(assigned_team IS NULL OR assigned_team IN('CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS'));
            IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'IX_support_tickets_team_assignment')
                CREATE INDEX IX_support_tickets_team_assignment ON dbo.support_tickets(assigned_department,assigned_team,status,assigned_technician_id,updated_at DESC);
            UPDATE dbo.support_tickets
            SET assigned_team=CASE
                WHEN assigned_technician_id IS NOT NULL THEN 'TECHNICIANS'
                WHEN EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=created_by_user_id AND creator.support_team='CALL_CENTER') THEN 'CALL_CENTER'
                ELSE 'TECHNICAL_FAILURE'
            END
            WHERE assigned_department='TECHNOLOGY' AND (assigned_team IS NULL OR assigned_team='');
            UPDATE t SET assigned_team='TECHNICAL_FAILURE',status=CASE WHEN t.status='OPEN' THEN 'IN_PROGRESS' ELSE t.status END,updated_at=SYSUTCDATETIME()
            FROM dbo.support_tickets t
            WHERE t.assigned_department='TECHNOLOGY' AND t.assigned_technician_id IS NULL AND ISNULL(t.assigned_team,'') NOT IN('TECHNICAL_FAILURE','CALL_CENTER','TECHNICIANS');
            UPDATE dbo.support_tickets SET status='IN_PROGRESS',updated_at=SYSUTCDATETIME() WHERE assigned_technician_id IS NOT NULL AND status='OPEN';
            UPDATE dbo.support_tickets SET resolved_at=COALESCE(closed_at,updated_at) WHERE status IN('RESOLVED','CLOSED') AND resolved_at IS NULL;
            INSERT INTO dbo.support_ticket_history(ticket_id,action,to_department,to_team,actor_user_id,actor_name,comment,created_at)
            SELECT t.id,'CREATED',t.assigned_department,t.assigned_team,t.created_by_user_id,t.created_by_name,N'Ticket existente incorporado al historial.',t.created_at
            FROM dbo.support_tickets t WHERE NOT EXISTS(SELECT 1 FROM dbo.support_ticket_history h WHERE h.ticket_id=t.id);
            INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,target_department,message,actor_user_id,actor_name,created_at)
            SELECT t.id,'CREATED',t.assigned_department,N'Se abrió el ticket.',t.created_by_user_id,t.created_by_name,t.created_at
            FROM dbo.support_tickets t WHERE NOT EXISTS(SELECT 1 FROM dbo.support_ticket_notifications n WHERE n.ticket_id=t.id AND n.event_type='CREATED' AND n.recipient_user_id IS NULL AND n.target_department=t.assigned_department);
            INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name,created_at)
            SELECT t.id,'CREATED',CONVERT(nvarchar(80),u.id),N'Se abrió un ticket en una agencia bajo tu supervisión.',t.created_by_user_id,t.created_by_name,t.created_at
            FROM dbo.support_tickets t INNER JOIN dbo.agencies a ON a.id=t.agency_id INNER JOIN dbo.admin_user_groups ug ON ug.group_name=a.grupo INNER JOIN dbo.admin_users u ON u.id=ug.user_id AND u.role='GROUP_ADMIN' AND u.is_active=1
            WHERE NOT EXISTS(SELECT 1 FROM dbo.support_ticket_notifications n WHERE n.ticket_id=t.id AND n.event_type='CREATED' AND n.recipient_user_id=CONVERT(nvarchar(80),u.id));
            INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,target_department,message,actor_user_id,actor_name,created_at)
            SELECT t.id,t.status,t.assigned_department,CASE WHEN t.status='CLOSED' THEN N'El ticket fue cerrado.' ELSE N'El ticket fue resuelto.' END,t.resolved_by_user_id,t.resolved_by_name,COALESCE(t.resolved_at,t.closed_at,t.updated_at)
            FROM dbo.support_tickets t WHERE t.status IN('RESOLVED','CLOSED') AND NOT EXISTS(SELECT 1 FROM dbo.support_ticket_notifications n WHERE n.ticket_id=t.id AND n.event_type=t.status AND n.recipient_user_id IS NULL AND n.target_department=t.assigned_department);
            INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name,created_at)
            SELECT t.id,t.status,CONVERT(nvarchar(80),u.id),CASE WHEN t.status='CLOSED' THEN N'El ticket de tu agencia fue cerrado.' ELSE N'El ticket de tu agencia fue resuelto.' END,t.resolved_by_user_id,t.resolved_by_name,COALESCE(t.resolved_at,t.closed_at,t.updated_at)
            FROM dbo.support_tickets t INNER JOIN dbo.agencies a ON a.id=t.agency_id INNER JOIN dbo.admin_user_groups ug ON ug.group_name=a.grupo INNER JOIN dbo.admin_users u ON u.id=ug.user_id AND u.role='GROUP_ADMIN' AND u.is_active=1
            WHERE t.status IN('RESOLVED','CLOSED') AND NOT EXISTS(SELECT 1 FROM dbo.support_ticket_notifications n WHERE n.ticket_id=t.id AND n.event_type=t.status AND n.recipient_user_id=CONVERT(nvarchar(80),u.id));
            """;
            await using var backfillCommand=new SqlCommand(backfillSql,connection);await backfillCommand.ExecuteNonQueryAsync(ct);ticketWorkflowSchemaReady=true;}finally{TicketWorkflowSchemaLock.Release();}
    }

    static async Task EnsureFindingEvidenceSchema(SqlConnection connection,CancellationToken ct)
    {
        if(findingEvidenceSchemaReady)return;
        await FindingEvidenceSchemaLock.WaitAsync(ct);
        try
        {
            if(findingEvidenceSchemaReady)return;
            const string sql="""
                IF COL_LENGTH(N'dbo.automatic_findings',N'diagnosis') IS NULL ALTER TABLE dbo.automatic_findings ADD diagnosis nvarchar(2000) NULL;
                IF COL_LENGTH(N'dbo.automatic_findings',N'verification') IS NULL ALTER TABLE dbo.automatic_findings ADD verification nvarchar(2000) NULL;
                IF COL_LENGTH(N'dbo.automatic_findings',N'recommendations') IS NULL ALTER TABLE dbo.automatic_findings ADD recommendations nvarchar(2000) NULL;
                IF COL_LENGTH(N'dbo.automatic_findings',N'assigned_technician_id') IS NULL ALTER TABLE dbo.automatic_findings ADD assigned_technician_id nvarchar(80) NULL;
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
                    CREATE INDEX IX_finding_resolution_evidence_finding ON dbo.finding_resolution_evidence(finding_id,captured_at,id);
                END;
                IF COL_LENGTH(N'dbo.finding_resolution_evidence',N'latitude') IS NULL ALTER TABLE dbo.finding_resolution_evidence ADD latitude float NULL;
                IF COL_LENGTH(N'dbo.finding_resolution_evidence',N'longitude') IS NULL ALTER TABLE dbo.finding_resolution_evidence ADD longitude float NULL;
                IF COL_LENGTH(N'dbo.finding_resolution_evidence',N'accuracy_meters') IS NULL ALTER TABLE dbo.finding_resolution_evidence ADD accuracy_meters float NULL;
                IF COL_LENGTH(N'dbo.finding_resolution_evidence',N'location_captured_at') IS NULL ALTER TABLE dbo.finding_resolution_evidence ADD location_captured_at datetime2(3) NULL;
                IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.automatic_findings') AND name=N'CK_automatic_findings_department' AND definition NOT LIKE '%HUMAN_RESOURCES%')
                    ALTER TABLE dbo.automatic_findings DROP CONSTRAINT CK_automatic_findings_department;
                IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.automatic_findings') AND name=N'CK_automatic_findings_department')
                    ALTER TABLE dbo.automatic_findings WITH CHECK ADD CONSTRAINT CK_automatic_findings_department CHECK(assigned_department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'));
                UPDATE dbo.automatic_findings SET
                    assigned_department='GENERAL_SERVICES',
                    assigned_technician_id=CASE WHEN status IN('OPEN','IN_PROGRESS') THEN NULL ELSE assigned_technician_id END,
                    updated_at=CASE WHEN assigned_department<>'GENERAL_SERVICES' THEN SYSUTCDATETIME() ELSE updated_at END
                WHERE finding_key IN('NO_INVERTER','NO_BATTERY','DAMAGE_FURNITURE','DAMAGE_PAINTING','DAMAGE_INVERTER','DAMAGE_BATTERIES','DAMAGE_RESTRUCTURING','DAMAGE_LIGHTING','DAMAGE_METALWORK','DAMAGE_GENERATOR_REQUEST','DAMAGE_ELECTRICAL','DAMAGE_SHUTTER','DAMAGE_GENERAL_OTHER') AND assigned_department<>'GENERAL_SERVICES';
                UPDATE dbo.automatic_findings SET
                    assigned_department='TECHNOLOGY',
                    assigned_technician_id=CASE WHEN status IN('OPEN','IN_PROGRESS') THEN NULL ELSE assigned_technician_id END,
                    updated_at=CASE WHEN assigned_department<>'TECHNOLOGY' THEN SYSUTCDATETIME() ELSE updated_at END
                WHERE finding_key IN('PRINTER_NO_MAINTENANCE','DAMAGE_WIRING','DAMAGE_SCREENS','DAMAGE_CONNECTIVITY','DAMAGE_REPORTED') AND assigned_department<>'TECHNOLOGY';
                """;
            await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync(ct);findingEvidenceSchemaReady=true;
        }
        finally{FindingEvidenceSchemaLock.Release();}
    }

    static async Task EnsureMaintenanceSchema(SqlConnection connection,CancellationToken ct)
    {
        if(maintenanceSchemaReady)return;
        await MaintenanceSchemaLock.WaitAsync(ct);
        try
        {
            if(maintenanceSchemaReady)return;
            const string columnsSql="""
                IF COL_LENGTH(N'dbo.maintenance_movements',N'operational_area') IS NULL ALTER TABLE dbo.maintenance_movements ADD operational_area varchar(20) NOT NULL CONSTRAINT DF_maintenance_operational_area DEFAULT('WORKSHOP');
                IF COL_LENGTH(N'dbo.maintenance_movements',N'document_number') IS NULL ALTER TABLE dbo.maintenance_movements ADD document_number varchar(40) NULL;
                IF COL_LENGTH(N'dbo.maintenance_movements',N'qr_token') IS NULL ALTER TABLE dbo.maintenance_movements ADD qr_token nvarchar(200) NULL;
                IF COL_LENGTH(N'dbo.maintenance_movements',N'delivered_by_name') IS NULL ALTER TABLE dbo.maintenance_movements ADD delivered_by_name nvarchar(160) NULL;
                IF COL_LENGTH(N'dbo.maintenance_movements',N'received_by_name') IS NULL ALTER TABLE dbo.maintenance_movements ADD received_by_name nvarchar(160) NULL;
                IF COL_LENGTH(N'dbo.maintenance_movements',N'destination_name') IS NULL ALTER TABLE dbo.maintenance_movements ADD destination_name nvarchar(200) NULL;
                IF COL_LENGTH(N'dbo.maintenance_movements',N'signature_data') IS NULL ALTER TABLE dbo.maintenance_movements ADD signature_data nvarchar(max) NULL;
                IF COL_LENGTH(N'dbo.maintenance_movements',N'occurred_at') IS NULL ALTER TABLE dbo.maintenance_movements ADD occurred_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_occurred_at DEFAULT(SYSUTCDATETIME());
                """;
            await using(var columnsCommand=new SqlCommand(columnsSql,connection))await columnsCommand.ExecuteNonQueryAsync(ct);
            const string dependenciesSql="""
                UPDATE dbo.maintenance_movements SET document_number=CONCAT('LEG-',REPLACE(CONVERT(varchar(36),id),'-','')) WHERE document_number IS NULL;
                UPDATE dbo.maintenance_movements SET qr_token=CONCAT('REAL-MNT|',document_number) WHERE qr_token IS NULL;
                IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'agency_id' AND is_nullable=0)
                BEGIN
                    IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'FK_maintenance_agency') ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT FK_maintenance_agency;
                    IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'IX_maintenance_agency_date') DROP INDEX IX_maintenance_agency_date ON dbo.maintenance_movements;
                    ALTER TABLE dbo.maintenance_movements ALTER COLUMN agency_id char(36) NULL;
                END;
                IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'FK_maintenance_agency') ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT FK_maintenance_agency FOREIGN KEY(agency_id) REFERENCES dbo.agencies(id);
                IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'IX_maintenance_agency_date') CREATE INDEX IX_maintenance_agency_date ON dbo.maintenance_movements(agency_id,created_at DESC);
                IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_type' AND definition NOT LIKE '%TRANSFER_TO_WORKSHOP%') ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT CK_maintenance_type;
                IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_type') ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_type CHECK(movement_type IN('ENTRY','EXIT','REQUEST','NEW_DELIVERY','DAMAGED_RETURN','TRANSFER_TO_WORKSHOP','REPLACEMENT','COMPONENT_REPLACEMENT','REPAIR','DISCHARGE'));
                IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_operational_area') ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_operational_area CHECK(operational_area IN('WORKSHOP','WAREHOUSE'));
                IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'UX_maintenance_document_number') CREATE UNIQUE INDEX UX_maintenance_document_number ON dbo.maintenance_movements(document_number) WHERE document_number IS NOT NULL;
                IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'UX_maintenance_qr_token') CREATE UNIQUE INDEX UX_maintenance_qr_token ON dbo.maintenance_movements(qr_token) WHERE qr_token IS NOT NULL;
                IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_department' AND definition NOT LIKE '%HUMAN_RESOURCES%')
                    ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT CK_maintenance_department;
                IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_department')
                    ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'));
                """;
            await using(var dependenciesCommand=new SqlCommand(dependenciesSql,connection))await dependenciesCommand.ExecuteNonQueryAsync(ct);
            const string catalogSql="""
                IF OBJECT_ID(N'dbo.maintenance_products',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.maintenance_products(
                        id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_products PRIMARY KEY DEFAULT(NEWID()),
                        department varchar(40) NOT NULL,
                        scan_code nvarchar(120) NOT NULL,
                        product_name nvarchar(160) NOT NULL,
                        component_type nvarchar(100) NULL,
                        created_by_name nvarchar(160) NOT NULL,
                        created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_products_created DEFAULT(SYSUTCDATETIME()),
                        updated_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_products_updated DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT UQ_maintenance_products_department_code UNIQUE(department,scan_code),
                        CONSTRAINT CK_maintenance_products_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'))
                    );
                    CREATE INDEX IX_maintenance_products_name ON dbo.maintenance_products(department,product_name);
                END;
                """;
            await using(var catalogCommand=new SqlCommand(catalogSql,connection))await catalogCommand.ExecuteNonQueryAsync(ct);
            const string seedCatalogSql="""
                MERGE dbo.maintenance_products AS target
                USING(
                    SELECT department,UPPER(LTRIM(RTRIM(serial_number))) scan_code,MAX(equipment_type) product_name,MAX(component_type) component_type,MAX(created_by_name) created_by_name
                    FROM dbo.maintenance_movements
                    WHERE serial_number IS NOT NULL AND LTRIM(RTRIM(serial_number))<>''
                    GROUP BY department,UPPER(LTRIM(RTRIM(serial_number)))
                ) AS source
                ON target.department=source.department AND target.scan_code=source.scan_code
                WHEN NOT MATCHED THEN INSERT(department,scan_code,product_name,component_type,created_by_name) VALUES(source.department,source.scan_code,source.product_name,source.component_type,source.created_by_name);
                """;
            await using(var seedCommand=new SqlCommand(seedCatalogSql,connection))await seedCommand.ExecuteNonQueryAsync(ct);
            const string procurementSql="""
                IF OBJECT_ID(N'dbo.maintenance_suppliers',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.maintenance_suppliers(
                        id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_suppliers PRIMARY KEY DEFAULT(NEWID()),
                        name nvarchar(160) NOT NULL,
                        tax_id nvarchar(40) NULL,
                        contact_name nvarchar(120) NULL,
                        phone nvarchar(50) NULL,
                        email nvarchar(160) NULL,
                        is_active bit NOT NULL CONSTRAINT DF_maintenance_supplier_active DEFAULT(1),
                        created_by_name nvarchar(160) NOT NULL,
                        created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_supplier_created DEFAULT(SYSUTCDATETIME()),
                        updated_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_supplier_updated DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT UQ_maintenance_supplier_name UNIQUE(name)
                    );
                END;
                IF OBJECT_ID(N'dbo.maintenance_requisitions',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.maintenance_requisitions(
                        id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_requisitions PRIMARY KEY DEFAULT(NEWID()),
                        requisition_number varchar(40) NOT NULL,
                        department varchar(40) NOT NULL,
                        product_name nvarchar(160) NOT NULL,
                        quantity_requested int NOT NULL,
                        quantity_fulfilled int NOT NULL CONSTRAINT DF_maintenance_requisition_fulfilled DEFAULT(0),
                        priority varchar(20) NOT NULL,
                        status varchar(20) NOT NULL CONSTRAINT DF_maintenance_requisition_status DEFAULT('PENDING'),
                        notes nvarchar(2000) NULL,
                        requested_by_user_id char(36) NOT NULL,
                        requested_by_name nvarchar(160) NOT NULL,
                        created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_requisition_created DEFAULT(SYSUTCDATETIME()),
                        updated_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_requisition_updated DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT UQ_maintenance_requisition_number UNIQUE(requisition_number),
                        CONSTRAINT CK_maintenance_requisition_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')),
                        CONSTRAINT CK_maintenance_requisition_quantity CHECK(quantity_requested>0 AND quantity_fulfilled>=0),
                        CONSTRAINT CK_maintenance_requisition_priority CHECK(priority IN('LOW','MEDIUM','HIGH','CRITICAL')),
                        CONSTRAINT CK_maintenance_requisition_status CHECK(status IN('PENDING','ORDERED','PARTIAL','FULFILLED','CANCELLED'))
                    );
                    CREATE INDEX IX_maintenance_requisition_status ON dbo.maintenance_requisitions(status,priority,created_at DESC);
                END;
                IF OBJECT_ID(N'dbo.maintenance_purchase_orders',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.maintenance_purchase_orders(
                        id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_purchase_orders PRIMARY KEY DEFAULT(NEWID()),
                        order_number varchar(40) NOT NULL,
                        supplier_id uniqueidentifier NOT NULL,
                        requisition_id uniqueidentifier NULL,
                        department varchar(40) NOT NULL,
                        product_name nvarchar(160) NOT NULL,
                        quantity_ordered int NOT NULL,
                        quantity_received int NOT NULL CONSTRAINT DF_maintenance_po_received DEFAULT(0),
                        unit_cost decimal(18,2) NOT NULL,
                        status varchar(20) NOT NULL CONSTRAINT DF_maintenance_po_status DEFAULT('ISSUED'),
                        expected_at datetime2(3) NULL,
                        notes nvarchar(2000) NULL,
                        created_by_name nvarchar(160) NOT NULL,
                        created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_po_created DEFAULT(SYSUTCDATETIME()),
                        updated_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_po_updated DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT UQ_maintenance_po_number UNIQUE(order_number),
                        CONSTRAINT FK_maintenance_po_supplier FOREIGN KEY(supplier_id) REFERENCES dbo.maintenance_suppliers(id),
                        CONSTRAINT FK_maintenance_po_requisition FOREIGN KEY(requisition_id) REFERENCES dbo.maintenance_requisitions(id),
                        CONSTRAINT CK_maintenance_po_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')),
                        CONSTRAINT CK_maintenance_po_quantity CHECK(quantity_ordered>0 AND quantity_received>=0 AND quantity_received<=quantity_ordered),
                        CONSTRAINT CK_maintenance_po_status CHECK(status IN('DRAFT','ISSUED','PARTIAL','RECEIVED','CANCELLED'))
                    );
                    CREATE INDEX IX_maintenance_po_status ON dbo.maintenance_purchase_orders(status,created_at DESC);
                END;
                IF OBJECT_ID(N'dbo.maintenance_returns',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.maintenance_returns(
                        id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_returns PRIMARY KEY DEFAULT(NEWID()),
                        return_number varchar(40) NOT NULL,
                        supplier_id uniqueidentifier NULL,
                        purchase_order_id uniqueidentifier NULL,
                        department varchar(40) NOT NULL,
                        product_name nvarchar(160) NOT NULL,
                        serial_number nvarchar(120) NOT NULL,
                        quantity int NOT NULL,
                        reason nvarchar(500) NOT NULL,
                        status varchar(20) NOT NULL CONSTRAINT DF_maintenance_return_status DEFAULT('REGISTERED'),
                        notes nvarchar(2000) NULL,
                        created_by_name nvarchar(160) NOT NULL,
                        created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_return_created DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT UQ_maintenance_return_number UNIQUE(return_number),
                        CONSTRAINT FK_maintenance_return_supplier FOREIGN KEY(supplier_id) REFERENCES dbo.maintenance_suppliers(id),
                        CONSTRAINT FK_maintenance_return_po FOREIGN KEY(purchase_order_id) REFERENCES dbo.maintenance_purchase_orders(id),
                        CONSTRAINT CK_maintenance_return_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')),
                        CONSTRAINT CK_maintenance_return_quantity CHECK(quantity>0),
                        CONSTRAINT CK_maintenance_return_status CHECK(status IN('REGISTERED','SENT','CREDITED','CLOSED'))
                    );
                    CREATE INDEX IX_maintenance_return_date ON dbo.maintenance_returns(created_at DESC);
                END;
                """;
            await using(var procurementCommand=new SqlCommand(procurementSql,connection))await procurementCommand.ExecuteNonQueryAsync(ct);
            maintenanceSchemaReady=true;
        }
        finally{MaintenanceSchemaLock.Release();}
    }

    static async Task EnsureUserPermissionSchema(SqlConnection connection,CancellationToken ct)
    {
        if(userPermissionSchemaReady)return;
        await UserPermissionSchemaLock.WaitAsync(ct);
        try
        {
            if(userPermissionSchemaReady)return;
            const string sql="""
                IF COL_LENGTH('dbo.admin_users','permissions_json') IS NULL ALTER TABLE dbo.admin_users ADD permissions_json nvarchar(max) NULL;
                IF COL_LENGTH('dbo.admin_users','support_team') IS NULL ALTER TABLE dbo.admin_users ADD support_team varchar(30) NULL;
                IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_support_team' AND (definition NOT LIKE '%TECHNICIANS%' OR definition NOT LIKE '%WAREHOUSE%' OR definition NOT LIKE '%WORKSHOP%' OR definition NOT LIKE '%GENERAL_SERVICES%'))
                    ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_support_team;
                IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_support_team')
                    EXEC sys.sp_executesql N'ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_support_team CHECK(support_team IS NULL OR (role=''TECHNOLOGY'' AND support_team IN(''CALL_CENTER'',''TECHNICAL_FAILURE'',''TECHNICIANS'',''WAREHOUSE'',''WORKSHOP'')) OR (role=''GENERAL_SERVICES'' AND support_team=''WORKSHOP''))';
                IF NOT EXISTS(SELECT 1 FROM dbo.admin_users WHERE email='jordy.arias@grupotejeda.local')
                    UPDATE dbo.admin_users SET email='jordy.arias@grupotejeda.local' WHERE email='yordy.arias@grupotejeda.local';
                UPDATE dbo.admin_users SET display_name=N'Jordy Arias' WHERE email='jordy.arias@grupotejeda.local' OR LOWER(display_name)=N'yordy arias';
                IF NOT EXISTS(SELECT 1 FROM dbo.admin_users WHERE email='michael.tejeda@grupotejeda.local')
                    UPDATE dbo.admin_users SET email='michael.tejeda@grupotejeda.local' WHERE email='maicol.tejeda@grupotejeda.local';
                UPDATE dbo.admin_users SET display_name=N'Michael Tejeda' WHERE email='michael.tejeda@grupotejeda.local' OR LOWER(display_name)=N'maicol tejeda';
                UPDATE dbo.admin_users SET role='TECHNOLOGY',support_team=NULL,permissions_json=NULL,is_active=1,updated_at=SYSUTCDATETIME()
                WHERE LOWER(REPLACE(email,'@grupotejeda.local','')) IN(N'joel.vizcaino',N'eddi.bono',N'yesica.mercedes',N'franklin.calderon',N'jordaniel.ortega');
                """;
            await using var command=new SqlCommand(sql,connection);
            await command.ExecuteNonQueryAsync(ct);userPermissionSchemaReady=true;
        }
        finally{UserPermissionSchemaLock.Release();}
    }
    static Dictionary<string,bool> ReadPermissions(string? json){if(string.IsNullOrWhiteSpace(json))return[];try{return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,bool>>(json,StorageJson)??[];}catch{return[];}}
    static string? WritePermissions(Dictionary<string,bool>? permissions)=>permissions is null?null:System.Text.Json.JsonSerializer.Serialize(permissions,StorageJson);

    public Database(IConfiguration configuration)
    {
        var raw = FirstValue(
            configuration.GetConnectionString("DefaultConnection"),
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"),
            configuration["MSSQL_CONNECTION_STRING"],
            Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING"));
        if (string.IsNullOrWhiteSpace(raw))
        {
            configurationError = "No se encontró la conexión. Agrega ConnectionStrings__DefaultConnection en MonsterASP y reinicia el sitio.";
            return;
        }

        try
        {
            raw = raw.Trim();
            foreach (var prefix in new[] { "ConnectionStrings__DefaultConnection=", "MSSQL_CONNECTION_STRING=" })
                if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    raw = raw[prefix.Length..].Trim();
                    break;
                }
            if (raw.Length >= 2 && ((raw[0] == '"' && raw[^1] == '"') || (raw[0] == '\'' && raw[^1] == '\''))) raw = raw[1..^1];

            var configured = new SqlConnectionStringBuilder(raw);
            if (string.IsNullOrWhiteSpace(configured.DataSource) || string.IsNullOrWhiteSpace(configured.InitialCatalog) || string.IsNullOrWhiteSpace(configured.UserID))
            {
                configurationError = "La conexión de SQL Server está incompleta: debe contener Server, Database y User Id.";
                return;
            }
            if (configured.ConnectTimeout == 0) configured.ConnectTimeout = 30;
            var internalHost = configured.DataSource.Replace(".public.databaseasp.net", ".databaseasp.net", StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(internalHost, configured.DataSource, StringComparison.OrdinalIgnoreCase))
            {
                var inside = new SqlConnectionStringBuilder(configured.ConnectionString) { DataSource = internalHost };
                connections.Add((inside.ConnectionString, "monster-internal"));
            }
            connections.Add((configured.ConnectionString, configured.DataSource.Contains(".public.databaseasp.net", StringComparison.OrdinalIgnoreCase) ? "monster-public" : "configured"));
            connections = connections.DistinctBy(item => item.Value, StringComparer.Ordinal).ToList();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"SQL Server connection configuration: {error.Message}");
            configurationError = "El formato de la conexión de SQL Server no es válido. Copia la cadena completa desde el panel de MonsterASP.";
        }
    }

    static string? FirstValue(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    static object Db(object? value) => value ?? DBNull.Value;
    static bool ReadBooleanValue(SqlDataReader reader,int index)
    {
        if(reader.IsDBNull(index))return false;
        var value=reader.GetValue(index);
        return value switch
        {
            bool flag=>flag,
            byte number=>number!=0,
            short number=>number!=0,
            int number=>number!=0,
            long number=>number!=0,
            _=>Convert.ToBoolean(value,System.Globalization.CultureInfo.InvariantCulture)
        };
    }
    static void AddScope(SqlCommand command, string userId, string role)
    {
        command.Parameters.AddWithValue("@scoped", role == "GroupAdministrator" ? 1 : 0);
        command.Parameters.AddWithValue("@userId", userId);
    }
    static bool IsGlobal(string role) => role is "Administrator" or "Viewer" or "Fiscalizador";
    static string DepartmentForRole(string role)=>role switch{"Technology"=>"TECHNOLOGY","GeneralServices"=>"GENERAL_SERVICES","HumanResources"=>"HUMAN_RESOURCES",_=>""};
    static string ClaimRole(string databaseRole) => databaseRole switch { "ADMINISTRATOR" => "Administrator", "GROUP_ADMIN" => "GroupAdministrator", "FISCALIZADOR" => "Fiscalizador", "TECHNOLOGY" => "Technology", "GENERAL_SERVICES" => "GeneralServices", "HUMAN_RESOURCES" => "HumanResources", _ => "Viewer" };
    static string? AvatarUrl(string id, bool hasAvatar) => hasAvatar ? $"/api/account/avatar/{id}" : null;
    static byte[] PasswordHash(string password, byte[] salt, int iterations) => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

    async Task<SqlConnection> Open(CancellationToken ct)
    {
        if (configurationError is not null) throw new InvalidOperationException("MSSQL_CONFIG:" + configurationError);
        Exception? last = null;
        foreach (var candidate in connections)
        {
            var connection = new SqlConnection(candidate.Value);
            try { await connection.OpenAsync(ct); return connection; }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                last = error;
                Console.Error.WriteLine($"SQL Server {candidate.Mode}: {error.Message}");
                await connection.DisposeAsync();
            }
        }
        throw last ?? new InvalidOperationException("MSSQL_CONFIG:No existe una conexión de SQL Server utilizable.");
    }

    static async Task EnsureSupportChatSchema(SqlConnection connection, CancellationToken ct)
    {
        if (supportChatSchemaReady) return;
        await SupportChatSchemaLock.WaitAsync(ct);
        try
        {
            if (supportChatSchemaReady) return;
            const string sql = """
                IF OBJECT_ID(N'dbo.support_conversations',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.support_conversations(
                        id uniqueidentifier NOT NULL CONSTRAINT DF_support_conversations_id DEFAULT(NEWID()),
                        supervisor_user_id nvarchar(80) NOT NULL,
                        supervisor_name nvarchar(160) NOT NULL,
                        supervisor_username nvarchar(180) NOT NULL,
                        assigned_department varchar(40) NOT NULL,
                        category varchar(60) NULL,
                        status varchar(25) NOT NULL CONSTRAINT DF_support_conversations_status DEFAULT('WAITING_SUPPORT'),
                        requested_agent bit NOT NULL CONSTRAINT DF_support_conversations_agent DEFAULT(0),
                        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_conversations_created DEFAULT(SYSUTCDATETIME()),
                        updated_at datetime2(3) NOT NULL CONSTRAINT DF_support_conversations_updated DEFAULT(SYSUTCDATETIME()),
                        last_message_at datetime2(3) NOT NULL CONSTRAINT DF_support_conversations_last_message DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT PK_support_conversations PRIMARY KEY(id),
                        CONSTRAINT CK_support_conversations_status CHECK(status IN('WAITING_SUPPORT','IN_PROGRESS','RESOLVED','CLOSED'))
                    );
                    CREATE INDEX IX_support_conversations_routing ON dbo.support_conversations(assigned_department,status,last_message_at DESC);
                    CREATE INDEX IX_support_conversations_supervisor ON dbo.support_conversations(supervisor_user_id,last_message_at DESC);
                END;
                IF OBJECT_ID(N'dbo.support_messages',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.support_messages(
                        id uniqueidentifier NOT NULL CONSTRAINT DF_support_messages_id DEFAULT(NEWID()),
                        conversation_id uniqueidentifier NOT NULL,
                        sender_user_id nvarchar(80) NULL,
                        sender_name nvarchar(160) NOT NULL,
                        sender_role varchar(40) NOT NULL,
                        message nvarchar(2000) NOT NULL,
                        is_bot bit NOT NULL CONSTRAINT DF_support_messages_bot DEFAULT(0),
                        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_messages_created DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT PK_support_messages PRIMARY KEY(id),
                        CONSTRAINT FK_support_messages_conversation FOREIGN KEY(conversation_id) REFERENCES dbo.support_conversations(id) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_support_messages_conversation ON dbo.support_messages(conversation_id,created_at,id);
                END;
                IF COL_LENGTH('dbo.support_conversations','is_restricted') IS NULL
                    ALTER TABLE dbo.support_conversations ADD is_restricted bit NOT NULL CONSTRAINT DF_support_conversations_restricted DEFAULT(0);
                IF COL_LENGTH('dbo.support_conversations','is_blocked') IS NULL
                    ALTER TABLE dbo.support_conversations ADD is_blocked bit NOT NULL CONSTRAINT DF_support_conversations_blocked DEFAULT(0);
                IF COL_LENGTH('dbo.support_conversations','deleted_at') IS NULL
                    ALTER TABLE dbo.support_conversations ADD deleted_at datetime2(3) NULL;
                IF OBJECT_ID(N'dbo.support_conversation_controls',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.support_conversation_controls(
                        id uniqueidentifier NOT NULL CONSTRAINT DF_support_conversation_controls_id DEFAULT(NEWID()),
                        conversation_id uniqueidentifier NOT NULL,
                        action varchar(30) NOT NULL,
                        actor_user_id nvarchar(80) NOT NULL,
                        actor_role varchar(40) NOT NULL,
                        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_conversation_controls_created DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT PK_support_conversation_controls PRIMARY KEY(id),
                        CONSTRAINT FK_support_conversation_controls_conversation FOREIGN KEY(conversation_id) REFERENCES dbo.support_conversations(id)
                    );
                    CREATE INDEX IX_support_conversation_controls_conversation ON dbo.support_conversation_controls(conversation_id,created_at DESC);
                END;
                IF OBJECT_ID(N'dbo.support_conversation_mutes',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.support_conversation_mutes(
                        conversation_id uniqueidentifier NOT NULL,
                        user_id nvarchar(80) NOT NULL,
                        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_conversation_mutes_created DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT PK_support_conversation_mutes PRIMARY KEY(conversation_id,user_id),
                        CONSTRAINT FK_support_conversation_mutes_conversation FOREIGN KEY(conversation_id) REFERENCES dbo.support_conversations(id) ON DELETE CASCADE
                    );
                END;
                IF OBJECT_ID(N'dbo.support_voice_notes',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.support_voice_notes(
                        message_id uniqueidentifier NOT NULL PRIMARY KEY,
                        audio_data varbinary(max) NOT NULL,
                        content_type varchar(100) NOT NULL,
                        file_name nvarchar(180) NULL,
                        duration_seconds int NULL,
                        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_voice_notes_created DEFAULT(SYSUTCDATETIME()),
                        CONSTRAINT FK_support_voice_notes_message FOREIGN KEY(message_id) REFERENCES dbo.support_messages(id) ON DELETE CASCADE
                    );
                END;
                IF COL_LENGTH('dbo.support_messages','deleted_at') IS NULL
                    ALTER TABLE dbo.support_messages ADD deleted_at datetime2(3) NULL;
                IF COL_LENGTH('dbo.support_messages','deleted_by_user_id') IS NULL
                    ALTER TABLE dbo.support_messages ADD deleted_by_user_id nvarchar(80) NULL;
                IF COL_LENGTH('dbo.support_messages','deleted_by_name') IS NULL
                    ALTER TABLE dbo.support_messages ADD deleted_by_name nvarchar(160) NULL;
                """;
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(ct);
            supportChatSchemaReady = true;
        }
        finally { SupportChatSchemaLock.Release(); }
    }

    public async Task<AdminIdentity?> AuthenticateUser(string email, string password, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureUserPermissionSchema(connection,ct);
        const string sql = """
            SELECT TOP (1) id,email,display_name,role,password_salt,password_hash,password_iterations,
                           must_change_password,CASE WHEN avatar_data IS NULL THEN 0 ELSE 1 END,permissions_json,support_team
            FROM dbo.admin_users
            WHERE email=@email AND is_active=1;
            """;
        string id, storedEmail, name, databaseRole;
        byte[] salt, hash;
        int iterations;
        bool mustChange, hasAvatar;string? permissionsJson,supportTeam;
        await using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@email", email);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            id = reader.GetString(0); storedEmail = reader.GetString(1); name = reader.GetString(2); databaseRole = reader.GetString(3);
            salt = (byte[])reader[4]; hash = (byte[])reader[5]; iterations = reader.GetInt32(6); mustChange = reader.GetBoolean(7); hasAvatar = reader.GetInt32(8) == 1;permissionsJson=reader.IsDBNull(9)?null:reader.GetString(9);supportTeam=reader.IsDBNull(10)?null:reader.GetString(10);
        }
        var candidate = PasswordHash(password, salt, iterations);
        var passwordMatches = candidate.Length == hash.Length && CryptographicOperations.FixedTimeEquals(candidate, hash);
        var validTemporaryAccess = mustChange && password == "123456";
        if (!passwordMatches && !validTemporaryAccess) return null;
        await using (var update = new SqlCommand("UPDATE dbo.admin_users SET last_login_at=SYSUTCDATETIME(),updated_at=SYSUTCDATETIME() WHERE id=@id;", connection))
        {
            update.Parameters.AddWithValue("@id", id);
            await update.ExecuteNonQueryAsync(ct);
        }
        return new(id, storedEmail, name, ClaimRole(databaseRole), mustChange, AvatarUrl(id, hasAvatar),ReadPermissions(permissionsJson),supportTeam);
    }

    public async Task<AdminIdentity?> Account(Guid id, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureUserPermissionSchema(connection,ct);
        const string sql = """
            SELECT email,display_name,role,must_change_password,CASE WHEN avatar_data IS NULL THEN 0 ELSE 1 END,permissions_json,support_team
            FROM dbo.admin_users WHERE id=@id AND is_active=1;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(id.ToString(), reader.GetString(0), reader.GetString(1), ClaimRole(reader.GetString(2)), reader.GetBoolean(3), AvatarUrl(id.ToString(), reader.GetInt32(4) == 1),ReadPermissions(reader.IsDBNull(5)?null:reader.GetString(5)),reader.IsDBNull(6)?null:reader.GetString(6));
    }

    public async Task<List<AdminUserRow>> AdminUsers(CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureUserPermissionSchema(connection,ct);
        const string sql = """
            SELECT id,email,display_name,role,is_active,last_login_at,created_at,region,contact,
                   CASE WHEN avatar_data IS NULL THEN 0 ELSE 1 END,must_change_password,permissions_json,support_team
            FROM dbo.admin_users ORDER BY role,display_name,email;
            """;
        var users = new List<AdminUserRow>();
        await using (var command = new SqlCommand(sql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                var id = Guid.Parse(reader.GetString(0));
                users.Add(new(id, reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4),
                    reader.IsDBNull(5) ? null : reader.GetDateTime(5), reader.GetDateTime(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
                    AvatarUrl(id.ToString(), reader.GetInt32(9) == 1), reader.GetBoolean(10), [], 0,ReadPermissions(reader.IsDBNull(11)?null:reader.GetString(11)),reader.IsDBNull(12)?null:reader.GetString(12)));
            }

        var groups = new Dictionary<Guid, List<string>>();
        var agencyCounts = new Dictionary<Guid, int>();
        const string scopeSql = """
            SELECT ug.user_id,ug.group_name,COUNT(a.id) AS agency_count
            FROM dbo.admin_user_groups ug
            LEFT JOIN dbo.agencies a ON a.grupo=ug.group_name AND a.is_active=1
            GROUP BY ug.user_id,ug.group_name
            ORDER BY ug.user_id,ug.group_name;
            """;
        await using (var command = new SqlCommand(scopeSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                var id = Guid.Parse(reader.GetString(0));
                if (!groups.TryGetValue(id, out var list)) groups[id] = list = [];
                list.Add(reader.GetString(1));
                agencyCounts[id] = agencyCounts.GetValueOrDefault(id) + reader.GetInt32(2);
            }
        return users.Select(user => user with { Groups = groups.GetValueOrDefault(user.Id) ?? [], AgencyCount = agencyCounts.GetValueOrDefault(user.Id) }).ToList();
    }

    async Task<AdminUserRow?> AdminUser(Guid id, CancellationToken ct) => (await AdminUsers(ct)).FirstOrDefault(user => user.Id == id);

    public async Task<List<string>> AllGroupNames(CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await using var command = new SqlCommand("SELECT DISTINCT grupo FROM dbo.agencies WHERE is_active=1 ORDER BY grupo;", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var groups = new List<string>();
        while (await reader.ReadAsync(ct)) groups.Add(reader.GetString(0));
        return groups;
    }

    public async Task<AdminUserRow> CreateAdminUser(AdminUserCreate body, CancellationToken ct)
    {
        var id = Guid.NewGuid(); var salt = RandomNumberGenerator.GetBytes(16); var hash = PasswordHash(body.Password!, salt, 210000);
        await using var connection = await Open(ct);
        await EnsureUserPermissionSchema(connection,ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            const string sql = """
                INSERT INTO dbo.admin_users
                  (id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,contact,must_change_password,permissions_json,support_team)
                VALUES(@id,@email,@name,@role,@salt,@hash,210000,1,@region,@contact,1,@permissions,@supportTeam);
                """;
            await using (var command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@id", id.ToString()); command.Parameters.AddWithValue("@email", body.Email!.Trim().ToLowerInvariant());
                command.Parameters.AddWithValue("@name", body.DisplayName!.Trim()); command.Parameters.AddWithValue("@role", body.Role);
                command.Parameters.AddWithValue("@salt", salt); command.Parameters.AddWithValue("@hash", hash);
                command.Parameters.AddWithValue("@region", Db(string.IsNullOrWhiteSpace(body.Region) ? null : body.Region.Trim()));
                command.Parameters.AddWithValue("@contact", Db(string.IsNullOrWhiteSpace(body.Contact) ? null : body.Contact.Trim()));
                command.Parameters.AddWithValue("@permissions",Db(WritePermissions(body.Permissions)));
                command.Parameters.AddWithValue("@supportTeam",Db((body.Role=="TECHNOLOGY"||body.Role=="GENERAL_SERVICES")&&!string.IsNullOrWhiteSpace(body.SupportTeam)?body.SupportTeam:null));
                await command.ExecuteNonQueryAsync(ct);
            }
            await ReplaceAssignments(connection, transaction, id, body.Role!, body.Region, body.Groups, ct);
            await transaction.CommitAsync(ct);
        }
        catch { await transaction.RollbackAsync(ct); throw; }
        return (await AdminUser(id, ct))!;
    }

    public async Task<AdminUserRow?> UpdateAdminUser(Guid id, AdminUserUpdate body, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureUserPermissionSchema(connection,ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var hasPassword = !string.IsNullOrEmpty(body.Password);
            var sql = hasPassword
                ? "UPDATE dbo.admin_users SET email=@email,display_name=@name,role=@role,is_active=@active,region=@region,contact=@contact,permissions_json=@permissions,support_team=@supportTeam,password_salt=@salt,password_hash=@hash,password_iterations=210000,must_change_password=1,updated_at=SYSUTCDATETIME() WHERE id=@id;"
                : "UPDATE dbo.admin_users SET email=@email,display_name=@name,role=@role,is_active=@active,region=@region,contact=@contact,permissions_json=@permissions,support_team=@supportTeam,updated_at=SYSUTCDATETIME() WHERE id=@id;";
            await using (var command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@id", id.ToString()); command.Parameters.AddWithValue("@email", body.Email!.Trim().ToLowerInvariant());
                command.Parameters.AddWithValue("@name", body.DisplayName!.Trim()); command.Parameters.AddWithValue("@role", body.Role);
                command.Parameters.AddWithValue("@active", body.IsActive); command.Parameters.AddWithValue("@region", Db(string.IsNullOrWhiteSpace(body.Region) ? null : body.Region.Trim()));
                command.Parameters.AddWithValue("@contact", Db(string.IsNullOrWhiteSpace(body.Contact) ? null : body.Contact.Trim()));
                command.Parameters.AddWithValue("@permissions",Db(WritePermissions(body.Permissions)));
                command.Parameters.AddWithValue("@supportTeam",Db((body.Role=="TECHNOLOGY"||body.Role=="GENERAL_SERVICES")&&!string.IsNullOrWhiteSpace(body.SupportTeam)?body.SupportTeam:null));
                if (hasPassword) { var salt = RandomNumberGenerator.GetBytes(16); command.Parameters.AddWithValue("@salt", salt); command.Parameters.AddWithValue("@hash", PasswordHash(body.Password!, salt, 210000)); }
                if (await command.ExecuteNonQueryAsync(ct) == 0) { await transaction.RollbackAsync(ct); return null; }
            }
            await ReplaceAssignments(connection, transaction, id, body.Role!, body.Region, body.Groups, ct);
            await transaction.CommitAsync(ct);
        }
        catch { await transaction.RollbackAsync(ct); throw; }
        return await AdminUser(id, ct);
    }

    // La eliminación de una cuenta es lógica para conservar la trazabilidad de
    // tickets, evidencias y auditorías. Se revoca el acceso y las suscripciones
    // push, pero no se destruye el historial que pertenece a la operación.
    public async Task<bool> DeleteAdminUser(Guid id, string actorEmail, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureUserPermissionSchema(connection, ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            const string beforeSql = "SELECT TOP(1) email,display_name,role FROM dbo.admin_users WHERE id=@id AND is_active=1;";
            string? email = null;
            string? displayName = null;
            string? role = null;
            await using (var before = new SqlCommand(beforeSql, connection, transaction))
            {
                before.Parameters.AddWithValue("@id", id.ToString());
                await using var reader = await before.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    await transaction.RollbackAsync(ct);
                    return false;
                }
                email = reader.GetString(0);
                displayName = reader.GetString(1);
                role = reader.GetString(2);
            }

            await using (var deactivate = new SqlCommand("UPDATE dbo.admin_users SET is_active=0,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection, transaction))
            {
                deactivate.Parameters.AddWithValue("@id", id.ToString());
                await deactivate.ExecuteNonQueryAsync(ct);
            }
            // Ningún dispositivo inactivo debe seguir recibiendo alertas push.
            await using (var push = new SqlCommand("IF OBJECT_ID(N'dbo.pwa_push_subscriptions',N'U') IS NOT NULL DELETE FROM dbo.pwa_push_subscriptions WHERE user_id=@id;", connection, transaction))
            {
                push.Parameters.AddWithValue("@id", id.ToString());
                await push.ExecuteNonQueryAsync(ct);
            }
            // La tabla de auditoría existe en instalaciones completas; el IF
            // mantiene compatible la actualización con esquemas antiguos.
            await using (var audit = new SqlCommand("IF OBJECT_ID(N'dbo.audit_log',N'U') IS NOT NULL INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data,actor_email) VALUES('admin_user',@id,'USER_DEACTIVATED',@before,@actor);", connection, transaction))
            {
                audit.Parameters.AddWithValue("@id", id.ToString());
                audit.Parameters.AddWithValue("@before", System.Text.Json.JsonSerializer.Serialize(new { email, displayName, role }));
                audit.Parameters.AddWithValue("@actor", string.IsNullOrWhiteSpace(actorEmail) ? DBNull.Value : actorEmail);
                await audit.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    static async Task ReplaceAssignments(SqlConnection connection, SqlTransaction transaction, Guid id, string role, string? region, List<string>? requested, CancellationToken ct)
    {
        await using (var delete = new SqlCommand("DELETE FROM dbo.admin_user_groups WHERE user_id=@id;", connection, transaction))
        { delete.Parameters.AddWithValue("@id", id.ToString()); await delete.ExecuteNonQueryAsync(ct); }
        if (role != "GROUP_ADMIN") return;
        foreach (var group in (requested ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            const string sql = """
                INSERT INTO dbo.admin_user_groups(user_id,group_name,region)
                SELECT @id,@group,@region
                WHERE EXISTS(SELECT 1 FROM dbo.agencies WHERE grupo=@group AND is_active=1);
                """;
            await using var insert = new SqlCommand(sql, connection, transaction);
            insert.Parameters.AddWithValue("@id", id.ToString()); insert.Parameters.AddWithValue("@group", group);
            insert.Parameters.AddWithValue("@region", Db(string.IsNullOrWhiteSpace(region) ? null : region.Trim()));
            await insert.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<AdminIdentity?> ChangeOwnPassword(Guid id, string currentPassword, string newPassword, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureUserPermissionSchema(connection,ct);
        const string selectSql = """
            SELECT email,display_name,role,password_salt,password_hash,password_iterations,
                   CASE WHEN avatar_data IS NULL THEN 0 ELSE 1 END,must_change_password,permissions_json,support_team
            FROM dbo.admin_users WHERE id=@id AND is_active=1;
            """;
        string email, name, databaseRole; byte[] salt, hash; int iterations; bool hasAvatar, mustChange;Dictionary<string,bool> permissions;string? supportTeam;
        await using (var command = new SqlCommand(selectSql, connection))
        {
            command.Parameters.AddWithValue("@id", id.ToString());
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            email = reader.GetString(0); name = reader.GetString(1); databaseRole = reader.GetString(2);
            salt = (byte[])reader[3]; hash = (byte[])reader[4]; iterations = reader.GetInt32(5); hasAvatar = reader.GetInt32(6) == 1; mustChange = reader.GetBoolean(7);permissions=ReadPermissions(reader.IsDBNull(8)?null:reader.GetString(8));supportTeam=reader.IsDBNull(9)?null:reader.GetString(9);
        }
        var candidate = PasswordHash(currentPassword, salt, iterations);
        var passwordMatches = candidate.Length == hash.Length && CryptographicOperations.FixedTimeEquals(candidate, hash);
        var validTemporaryAccess = mustChange && currentPassword == "123456";
        if (!passwordMatches && !validTemporaryAccess) return null;
        var newSalt = RandomNumberGenerator.GetBytes(16); var newHash = PasswordHash(newPassword, newSalt, 210000);
        await using (var update = new SqlCommand("UPDATE dbo.admin_users SET password_salt=@salt,password_hash=@hash,password_iterations=210000,must_change_password=0,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection))
        {
            update.Parameters.AddWithValue("@salt", newSalt); update.Parameters.AddWithValue("@hash", newHash); update.Parameters.AddWithValue("@id", id.ToString());
            await update.ExecuteNonQueryAsync(ct);
        }
        return new(id.ToString(), email, name, ClaimRole(databaseRole), false, AvatarUrl(id.ToString(), hasAvatar),permissions,supportTeam);
    }

    public async Task AttachAvatar(Guid id, byte[] data, string mime, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await using var command = new SqlCommand("UPDATE dbo.admin_users SET avatar_data=@data,avatar_content_type=@mime,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection);
        command.Parameters.AddWithValue("@data", data); command.Parameters.AddWithValue("@mime", mime); command.Parameters.AddWithValue("@id", id.ToString());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<(byte[] Data, string Mime)?> Avatar(Guid id, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await using var command = new SqlCommand("SELECT avatar_data,avatar_content_type FROM dbo.admin_users WHERE id=@id AND avatar_data IS NOT NULL;", connection);
        command.Parameters.AddWithValue("@id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ((byte[])reader[0], reader.GetString(1));
    }

    public async Task<DatabaseHealth> Health(CancellationToken ct)
    {
        if (configurationError is not null) return new(false, "MSSQL_CONFIG", configurationError, null);
        try
        {
            await using var connection = await Open(ct);
            await using (var command = new SqlCommand("SELECT 1;", connection)) await command.ExecuteScalarAsync(ct);
            const string sql = """
                SELECT CASE WHEN OBJECT_ID(N'dbo.agencies',N'U') IS NOT NULL
                              AND OBJECT_ID(N'dbo.agency_profiles',N'U') IS NOT NULL
                              AND COL_LENGTH(N'dbo.agencies',N'is_active') IS NOT NULL
                            THEN 1 ELSE 0 END;
                """;
            await using (var command = new SqlCommand(sql, connection))
                if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 0)
                    return new(false, "MSSQL_SCHEMA", "SQL Server conectó, pero falta el esquema principal o la carga de agencias.", ConnectionMode(connection.ConnectionString));
            return new(true, "OK", "Conexión de SQL Server activa.", ConnectionMode(connection.ConnectionString));
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            Console.Error.WriteLine(error); var diagnostic = DescribeConnectionError(error); return new(false, diagnostic.Code, diagnostic.Message, null);
        }
    }

    static string ConnectionMode(string value)
    {
        var builder = new SqlConnectionStringBuilder(value);
        return builder.DataSource.Contains(".public.databaseasp.net", StringComparison.OrdinalIgnoreCase) ? "monster-public" : builder.DataSource.EndsWith(".databaseasp.net", StringComparison.OrdinalIgnoreCase) ? "monster-internal" : "configured";
    }

    public static DatabaseDiagnostic DescribeConnectionError(Exception error)
    {
        if (error is InvalidOperationException invalid && invalid.Message.StartsWith("MSSQL_CONFIG:", StringComparison.Ordinal)) return new("MSSQL_CONFIG", invalid.Message["MSSQL_CONFIG:".Length..]);
        if (error is TimeoutException) return new("MSSQL_TIMEOUT", "SQL Server tardó demasiado en responder. Verifica el servidor y reinicia el sitio.");
        if (error is SqlException sql)
        {
            if (sql.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase) || sql.Message.Contains("certificado", StringComparison.OrdinalIgnoreCase)) return new("MSSQL_TLS", "SQL Server rechazó el certificado TLS. Usa Encrypt=True;TrustServerCertificate=True en la cadena de MonsterASP.");
            return sql.Number switch
            {
                18456 => new("MSSQL_AUTH", "SQL Server rechazó el usuario o la contraseña. Copia nuevamente la cadena desde MonsterASP."),
                4060 => new("MSSQL_DATABASE", "SQL Server no pudo abrir la base indicada. Verifica Database y los permisos del usuario."),
                207 or 208 => new("MSSQL_SCHEMA", "SQL Server conectó, pero falta una tabla o columna. Ejecuta los archivos T-SQL de configuración en orden."),
                -2 or 40 or 53 or 258 or 11001 => new("MSSQL_HOST", "No se pudo alcanzar SQL Server. En MonsterASP se probará primero el host interno y después el host público configurado."),
                _ => new($"MSSQL_{sql.Number}", "SQL Server rechazó la conexión o la consulta. Revisa el registro de la aplicación en MonsterASP.")
            };
        }
        return new("MSSQL_CONNECTION", "No fue posible abrir la conexión con SQL Server. Revisa las variables del sitio y reinícialo.");
    }

    public async Task<bool> CanAccessAgency(Guid id, string userId, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            SELECT COUNT(*) FROM dbo.agencies a
            WHERE a.id=@id AND a.is_active=1
              AND (@scoped=0 OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ));
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id.ToString());
        AddScope(command, userId, role);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<bool> CanAccessProfile(Guid id, string userId, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            SELECT COUNT(*) FROM dbo.agency_profiles p
            INNER JOIN dbo.agencies a ON a.id=p.agency_id
            WHERE p.id=@id
              AND (@scoped=0 OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ));
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id.ToString());
        AddScope(command, userId, role);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<List<GroupRow>> Groups(string userId, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            SELECT a.grupo,COUNT(*) AS pending FROM dbo.agencies a
            WHERE a.status='PENDING'
              AND (@scoped=0 OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ))
            GROUP BY a.grupo ORDER BY a.grupo;
            """;
        await using var command = new SqlCommand(sql, connection);
        AddScope(command, userId, role);
        await using var reader = await command.ExecuteReaderAsync(ct); var groups = new List<GroupRow>();
        while (await reader.ReadAsync(ct)) groups.Add(new(reader.GetString(0), reader.GetInt32(1)));
        return groups;
    }

    public async Task<List<GroupRow>> PanelGroups(string userId, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            SELECT a.grupo,COUNT(*) FROM dbo.agencies a
            WHERE a.status IN ('PENDING','COMPLETED','REVIEW_REQUIRED')
              AND (@scoped=0 OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ))
            GROUP BY a.grupo ORDER BY a.grupo;
            """;
        await using var command = new SqlCommand(sql, connection);
        AddScope(command, userId, role);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var groups = new List<GroupRow>();
        while (await reader.ReadAsync(ct)) groups.Add(new(reader.GetString(0), reader.GetInt32(1)));
        return groups;
    }

    public async Task<List<AgencyRow>> Agencies(string group, string userId, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            SELECT a.id,a.codigo,a.terminal,a.expected_latitude,a.expected_longitude FROM dbo.agencies a
            WHERE a.status='PENDING' AND a.grupo=@group
              AND (@scoped=0 OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ))
            ORDER BY a.terminal;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@group", group);
        AddScope(command, userId, role);
        await using var reader = await command.ExecuteReaderAsync(ct); var agencies = new List<AgencyRow>();
        while (await reader.ReadAsync(ct)) agencies.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetDouble(3), reader.IsDBNull(4) ? null : reader.GetDouble(4)));
        return agencies;
    }

    public async Task<List<DashboardAgency>> PanelAgencies(string group, string userId, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            SELECT a.codigo,a.terminal,a.grupo,a.status,
                   CASE WHEN a.status='COMPLETED' THEN 100
                        WHEN a.status='REVIEW_REQUIRED' THEN 75 ELSE 0 END
            FROM dbo.agencies a
            WHERE a.grupo=@group
              AND a.status IN ('PENDING','COMPLETED','REVIEW_REQUIRED')
              AND (@scoped=0 OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ))
            ORDER BY a.terminal;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@group", group);
        AddScope(command, userId, role);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var agencies = new List<DashboardAgency>();
        while (await reader.ReadAsync(ct))
            agencies.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), null));
        return agencies;
    }

    public async Task<DashboardRow> Dashboard(string userId, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        var groups = new List<DashboardGroup>(); var total = 0; var completed = 0; var pending = 0; var review = 0;
        var scopeSql = role == "GroupAdministrator"
            ? """
                AND EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
                )
              """
            : "";
        var groupsSql = $"""
            SELECT a.grupo,COUNT(*) AS total,
                   SUM(CASE WHEN a.status='COMPLETED' THEN 1 ELSE 0 END),
                   SUM(CASE WHEN a.status='PENDING' THEN 1 ELSE 0 END),
                   SUM(CASE WHEN a.status='REVIEW_REQUIRED' THEN 1 ELSE 0 END),
                   CAST(NULL AS datetime2) AS last_updated
            FROM dbo.agencies a
            WHERE a.status IN ('PENDING','COMPLETED','REVIEW_REQUIRED')
              {scopeSql}
            GROUP BY a.grupo ORDER BY a.grupo;
            """;
        await using (var command = new SqlCommand(groupsSql, connection))
        {
            if (role == "GroupAdministrator") command.Parameters.AddWithValue("@userId", userId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var gt = reader.GetInt32(1); var gc = Convert.ToInt32(reader.GetValue(2)); var gp = Convert.ToInt32(reader.GetValue(3)); var gr = Convert.ToInt32(reader.GetValue(4));
                groups.Add(new(reader.GetString(0), gt, gc, gp, gt == 0 ? 0 : (int)Math.Round(gc * 100d / gt), reader.IsDBNull(5) ? null : reader.GetDateTime(5)));
                total += gt; completed += gc; pending += gp; review += gr;
            }
        }
        var agencies = new List<DashboardAgency>();
        var agenciesSql = $"""
            SELECT a.codigo,a.terminal,a.grupo,a.status,
                   CASE
                     WHEN a.status='COMPLETED' THEN 100
                     WHEN a.status='REVIEW_REQUIRED' THEN 75
                     ELSE 0
                   END AS percent,
                   CAST(NULL AS datetime2) AS last_updated
            FROM dbo.agencies a
            WHERE a.status IN ('PENDING','COMPLETED','REVIEW_REQUIRED')
              {scopeSql}
            ORDER BY a.grupo,a.terminal;
            """;
        try
        {
            await using var command = new SqlCommand(agenciesSql, connection);
            if (role == "GroupAdministrator") command.Parameters.AddWithValue("@userId", userId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                agencies.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.IsDBNull(5) ? null : reader.GetDateTime(5)));
        }
        catch (SqlException error)
        {
            Console.Error.WriteLine($"Dashboard agencies optional query: {error}");
        }
        var administrators = new List<DashboardAdministrator>();
        var audits = new List<CompletedAudit>();
        var auditsSql = $"""
            SELECT TOP (200) a.codigo,a.terminal,a.grupo,p.municipio,p.provincia,p.tipo_establecimiento,
                   p.latitude,p.longitude,p.accuracy_meters,p.submitted_at,p.id,p.observations,p.employee_name,
                   COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.login'),''),
                            (SELECT TOP (1) NULLIF(u.email,'') FROM dbo.admin_users u WHERE CONVERT(nvarchar(80),u.id)=JSON_VALUE(p.submitted_by_context,'$.userId')),
                            (SELECT TOP (1) NULLIF(l.actor_email,'') FROM dbo.audit_log l WHERE l.entity_type='agency_profile' AND l.entity_id=p.id AND l.action='PROFILE_CREATED' ORDER BY l.created_at),
                            (SELECT TOP (1) NULLIF(u.email,'')
                             FROM dbo.admin_user_groups ug INNER JOIN dbo.admin_users u ON u.id=ug.user_id
                             WHERE ug.group_name=a.grupo AND u.role='GROUP_ADMIN' AND u.is_active=1
                             ORDER BY u.display_name,u.email))
            FROM dbo.agencies a INNER JOIN dbo.agency_profiles p ON p.agency_id=a.id
            WHERE 1=1
              {scopeSql}
            ORDER BY p.submitted_at DESC;
            """;
        try
        {
            await using var command = new SqlCommand(auditsSql, connection);
            if (role == "GroupAdministrator") command.Parameters.AddWithValue("@userId", userId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) audits.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetDouble(6), reader.GetDouble(7), reader.GetDouble(8), DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc), Guid.Parse(reader.GetString(10)), reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetString(13)));
        }
        catch (SqlException error)
        {
            Console.Error.WriteLine($"Dashboard audits optional query: {error}");
        }
        var percent = total == 0 ? 0 : (int)Math.Round(completed * 100d / total);
        return new(total, completed, pending, review, percent, groups.Count, groups.Count(group => group.Percent == 100), groups, agencies, administrators, audits);
    }

    public async Task<AuditDetailRow?> AuditDetail(Guid id, string userId, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        string codigo, terminal, grupo, municipio, provincia, type, employeeName, direccion, sector; string? employeeCode, observations, submittedByLogin;
        double latitude, longitude, accuracy; DateTime submittedAt; System.Text.Json.JsonElement answers;
        const string detailSql = """
            SELECT a.codigo,a.terminal,a.grupo,p.municipio,p.provincia,p.tipo_establecimiento,p.latitude,p.longitude,
                   p.accuracy_meters,p.submitted_at,p.observations,p.employee_name,p.employee_code,p.direccion,p.sector,p.structural_answers,
                   COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.login'),''),
                            (SELECT TOP (1) NULLIF(u.email,'') FROM dbo.admin_users u WHERE CONVERT(nvarchar(80),u.id)=JSON_VALUE(p.submitted_by_context,'$.userId')),
                            (SELECT TOP (1) NULLIF(l.actor_email,'') FROM dbo.audit_log l WHERE l.entity_type='agency_profile' AND l.entity_id=p.id AND l.action='PROFILE_CREATED' ORDER BY l.created_at),
                            (SELECT TOP (1) NULLIF(u.email,'')
                             FROM dbo.admin_user_groups ug INNER JOIN dbo.admin_users u ON u.id=ug.user_id
                             WHERE ug.group_name=a.grupo AND u.role='GROUP_ADMIN' AND u.is_active=1
                             ORDER BY u.display_name,u.email))
            FROM dbo.agencies a INNER JOIN dbo.agency_profiles p ON p.agency_id=a.id
            WHERE p.id=@id
              AND (@scoped=0 OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ));
            """;
        await using (var command = new SqlCommand(detailSql, connection))
        {
            command.Parameters.AddWithValue("@id", id.ToString());
            AddScope(command, userId, role);
            await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null;
            codigo = reader.GetString(0); terminal = reader.GetString(1); grupo = reader.GetString(2); municipio = reader.GetString(3); provincia = reader.GetString(4); type = reader.GetString(5);
            latitude = reader.GetDouble(6); longitude = reader.GetDouble(7); accuracy = reader.GetDouble(8); submittedAt = DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc); observations = reader.IsDBNull(10) ? null : reader.GetString(10);
            employeeName = reader.IsDBNull(11) ? "Sin nombre registrado" : reader.GetString(11); employeeCode = reader.IsDBNull(12) ? null : reader.GetString(12); direccion = reader.GetString(13); sector = reader.GetString(14);
            answers = NormalizeAnswers(reader.IsDBNull(15) ? "{}" : reader.GetString(15)); submittedByLogin = reader.IsDBNull(16) ? null : reader.GetString(16);
        }
        var photos = new List<AuditPhotoRow>();
        const string photosSql = """
            SELECT photo_type,latitude,longitude,accuracy_meters,captured_at FROM dbo.audit_photos
            WHERE profile_id=@id ORDER BY CASE photo_type WHEN 'frontal' THEN 1 WHEN 'operativa' THEN 2 WHEN 'entorno' THEN 3 WHEN 'inversor' THEN 4 WHEN 'bateria' THEN 5 ELSE 6 END;
            """;
        await using (var command = new SqlCommand(photosSql, connection))
        {
            command.Parameters.AddWithValue("@id", id.ToString()); await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) { var photoType = reader.GetString(0); photos.Add(new(photoType, $"/api/images/{id}/{photoType}", reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3), reader.GetDateTime(4))); }
        }
        return new(codigo, terminal, grupo, municipio, provincia, type, latitude, longitude, accuracy, submittedAt, id, observations, employeeName, employeeCode, direccion, sector, answers, submittedByLogin, photos);
    }

    public async Task<AuditDetailRow?> AuditDetail(Guid id, CancellationToken ct) => await AuditDetail(id, "", "Administrator", ct);

    public async Task<AuditDetailRow?> UpdateAudit(Guid id, AuditUpdate body, string actor, CancellationToken ct)
    {
        var before = await AuditDetail(id, ct); if (before is null) return null;
        await using var connection = await Open(ct); await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            string? agencyId;
            await using (var find = new SqlCommand("SELECT agency_id FROM dbo.agency_profiles WITH (UPDLOCK,ROWLOCK) WHERE id=@id;", connection, transaction)) { find.Parameters.AddWithValue("@id", id.ToString()); agencyId = (string?)await find.ExecuteScalarAsync(ct); }
            if (agencyId is null) { await transaction.RollbackAsync(ct); return null; }
            await using (var agency = new SqlCommand("UPDATE dbo.agencies SET codigo=@codigo,terminal=@terminal,grupo=@grupo,status='COMPLETED',updated_at=SYSUTCDATETIME() WHERE id=@agency;", connection, transaction))
            { agency.Parameters.AddWithValue("@codigo", body.Codigo!.Trim()); agency.Parameters.AddWithValue("@terminal", body.Terminal!.Trim()); agency.Parameters.AddWithValue("@grupo", body.Grupo!.Trim()); agency.Parameters.AddWithValue("@agency", agencyId); await agency.ExecuteNonQueryAsync(ct); }
            const string profileSql = """
                UPDATE dbo.agency_profiles SET employee_name=@employeeName,employee_code=@employeeCode,direccion=@direccion,
                    sector=@sector,municipio=@municipio,provincia=@provincia,latitude=@latitude,longitude=@longitude,
                    accuracy_meters=@accuracy,structural_answers=@answers,observations=@observations,updated_at=SYSUTCDATETIME()
                WHERE id=@id;
                """;
            await using (var profile = new SqlCommand(profileSql, connection, transaction))
            {
                profile.Parameters.AddWithValue("@employeeName", body.EmployeeName!.Trim()); profile.Parameters.AddWithValue("@employeeCode", Db(string.IsNullOrWhiteSpace(body.EmployeeCode) ? null : body.EmployeeCode.Trim()));
                profile.Parameters.AddWithValue("@direccion", body.Direccion!.Trim()); profile.Parameters.AddWithValue("@sector", body.Sector!.Trim()); profile.Parameters.AddWithValue("@municipio", body.Municipio!.Trim()); profile.Parameters.AddWithValue("@provincia", body.Provincia!.Trim());
                profile.Parameters.AddWithValue("@latitude", body.Latitude); profile.Parameters.AddWithValue("@longitude", body.Longitude); profile.Parameters.AddWithValue("@accuracy", body.Accuracy);
                profile.Parameters.AddWithValue("@answers", System.Text.Json.JsonSerializer.Serialize(body.Answers, StorageJson)); profile.Parameters.AddWithValue("@observations", Db(string.IsNullOrWhiteSpace(body.Observations) ? null : body.Observations.Trim())); profile.Parameters.AddWithValue("@id", id.ToString()); await profile.ExecuteNonQueryAsync(ct);
            }
            await using (var log = new SqlCommand("INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data,after_data,actor_email) VALUES('agency_profile',@id,'PROFILE_CORRECTED',@before,@after,@actor);", connection, transaction))
            { log.Parameters.AddWithValue("@id", id.ToString()); log.Parameters.AddWithValue("@before", System.Text.Json.JsonSerializer.Serialize(before)); log.Parameters.AddWithValue("@after", System.Text.Json.JsonSerializer.Serialize(body)); log.Parameters.AddWithValue("@actor", actor); await log.ExecuteNonQueryAsync(ct); }
            await transaction.CommitAsync(ct);
        }
        catch { await transaction.RollbackAsync(ct); throw; }
        return await AuditDetail(id, ct);
    }

    public async Task<bool> DeleteAudit(Guid id, string actor, CancellationToken ct)
    {
        var before = await AuditDetail(id, ct); if (before is null) return false;
        await using var connection = await Open(ct); await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            string? agencyId;
            await using (var find = new SqlCommand("SELECT agency_id FROM dbo.agency_profiles WITH (UPDLOCK,ROWLOCK) WHERE id=@id;", connection, transaction)) { find.Parameters.AddWithValue("@id", id.ToString()); agencyId = (string?)await find.ExecuteScalarAsync(ct); }
            if (agencyId is null) { await transaction.RollbackAsync(ct); return false; }
            await using (var delete = new SqlCommand("DELETE FROM dbo.agency_profiles WHERE id=@id;", connection, transaction)) { delete.Parameters.AddWithValue("@id", id.ToString()); await delete.ExecuteNonQueryAsync(ct); }
            await using (var reopen = new SqlCommand("UPDATE dbo.agencies SET status='PENDING',updated_at=SYSUTCDATETIME() WHERE id=@agency;", connection, transaction)) { reopen.Parameters.AddWithValue("@agency", agencyId); await reopen.ExecuteNonQueryAsync(ct); }
            await using (var log = new SqlCommand("INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data,actor_email) VALUES('agency_profile',@id,'PROFILE_DELETED_AND_AGENCY_REOPENED',@before,@actor);", connection, transaction))
            { log.Parameters.AddWithValue("@id", id.ToString()); log.Parameters.AddWithValue("@before", System.Text.Json.JsonSerializer.Serialize(before)); log.Parameters.AddWithValue("@actor", actor); await log.ExecuteNonQueryAsync(ct); }
            await transaction.CommitAsync(ct); return true;
        }
        catch { await transaction.RollbackAsync(ct); throw; }
    }

    public async Task<Guid> Submit(Guid agency, Submit body, string? submittedByUserId, string? submittedByLogin, string? submittedByName, string? ip, string? agent, CancellationToken ct)
    {
        await using var connection = await Open(ct); await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var canonicalLogin = submittedByLogin?.Trim().ToLowerInvariant();
            var canonicalName = submittedByName?.Trim();
            if (!string.IsNullOrWhiteSpace(submittedByUserId))
            {
                const string submitterSql = "SELECT TOP (1) email,display_name FROM dbo.admin_users WHERE CONVERT(nvarchar(80),id)=@userId AND is_active=1;";
                await using var submitter = new SqlCommand(submitterSql, connection, transaction);
                submitter.Parameters.AddWithValue("@userId", submittedByUserId);
                await using var submitterReader = await submitter.ExecuteReaderAsync(ct);
                if (await submitterReader.ReadAsync(ct))
                {
                    canonicalLogin = submitterReader.GetString(0).Trim().ToLowerInvariant();
                    canonicalName = submitterReader.GetString(1).Trim();
                }
            }
            string? status = null; double? expectedLatitude = null, expectedLongitude = null;
            await using (var check = new SqlCommand("SELECT status,expected_latitude,expected_longitude FROM dbo.agencies WITH (UPDLOCK,ROWLOCK) WHERE id=@id AND is_active=1;", connection, transaction))
            {
                check.Parameters.AddWithValue("@id", agency.ToString()); await using var reader = await check.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct)) { status = reader.GetString(0); expectedLatitude = reader.IsDBNull(1) ? null : reader.GetDouble(1); expectedLongitude = reader.IsDBNull(2) ? null : reader.GetDouble(2); }
            }
            if (status != "PENDING") throw new AgencyConflict();
            var id = Guid.NewGuid(); var geo = body.Geolocation!; double? distance = expectedLatitude.HasValue && expectedLongitude.HasValue ? Haversine(geo.Latitude, geo.Longitude, expectedLatitude.Value, expectedLongitude.Value) : null;
            var after = System.Text.Json.JsonSerializer.Serialize(new { id, agencyId = agency, completionPercent = 100 });
            const string sql = """
                INSERT INTO dbo.agency_profiles
                  (id,agency_id,employee_name,employee_code,direccion,sector,municipio,provincia,tipo_establecimiento,
                   tipo_establecimiento_otro,latitude,longitude,accuracy_meters,distance_from_agency_meters,
                   location_captured_at,location_source,structural_answers,observations,completion_percent,submitted_by_context)
                VALUES(@id,@agency,@employeeName,@employeeCode,@direccion,@sector,@municipio,@provincia,@tipo,@otro,@lat,@lng,
                       @accuracy,@distance,@captured,@source,@answers,@observations,100,@context);
                UPDATE dbo.agencies SET status='COMPLETED',updated_at=SYSUTCDATETIME() WHERE id=@agency;
                INSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data,actor_email) VALUES('agency_profile',@id,'PROFILE_CREATED',@after,@login);
                """;
            await using var insert = new SqlCommand(sql, connection, transaction);
            insert.Parameters.AddWithValue("@id", id.ToString()); insert.Parameters.AddWithValue("@agency", agency.ToString()); insert.Parameters.AddWithValue("@employeeName", body.EmployeeName!.Trim());
            insert.Parameters.AddWithValue("@employeeCode", Db(string.IsNullOrWhiteSpace(body.EmployeeCode) ? null : body.EmployeeCode.Trim())); insert.Parameters.AddWithValue("@direccion", body.Direccion!.Trim()); insert.Parameters.AddWithValue("@sector", body.Sector!.Trim());
            insert.Parameters.AddWithValue("@municipio", body.Municipio!.Trim()); insert.Parameters.AddWithValue("@provincia", body.Provincia!.Trim()); insert.Parameters.AddWithValue("@tipo", body.TipoEstablecimiento!); insert.Parameters.AddWithValue("@otro", DBNull.Value);
            insert.Parameters.AddWithValue("@lat", geo.Latitude); insert.Parameters.AddWithValue("@lng", geo.Longitude); insert.Parameters.AddWithValue("@accuracy", geo.AccuracyMeters); insert.Parameters.AddWithValue("@distance", Db(distance));
            insert.Parameters.AddWithValue("@captured", geo.CapturedAt!.Value.UtcDateTime); insert.Parameters.AddWithValue("@source", geo.Source!); insert.Parameters.AddWithValue("@answers", System.Text.Json.JsonSerializer.Serialize(body.Answers, StorageJson));
            insert.Parameters.AddWithValue("@observations", Db(string.IsNullOrWhiteSpace(body.Observations) ? null : body.Observations.Trim())); insert.Parameters.AddWithValue("@context", System.Text.Json.JsonSerializer.Serialize(new { userId = submittedByUserId, login = canonicalLogin, displayName = canonicalName, ip, userAgent = agent, submittedAt = DateTimeOffset.UtcNow })); insert.Parameters.AddWithValue("@after", after); insert.Parameters.AddWithValue("@login", Db(canonicalLogin));
            await insert.ExecuteNonQueryAsync(ct);

            var answers = body.Answers!;
            var findings = new List<(string Key,string Title,string Detail,string Department,string Priority)>();
            if (answers.Painted == "no") findings.Add(("NOT_PAINTED","Agencia no pintada","La agencia no cumple con la pintura requerida.","GENERAL_SERVICES","LOW"));
            if (answers.RazaSticker == "no") findings.Add(("NO_RAZA_STICKER","Falta sticker de Raza","No fue colocado el sticker de Raza.","GENERAL_SERVICES","LOW"));
            if (answers.RealSticker == "no") findings.Add(("NO_REAL_STICKER","Falta sticker de Real","No fue colocado el sticker de Real.","GENERAL_SERVICES","LOW"));
            if (answers.LotekaRemoved == "no") findings.Add(("LOTEKA_NOT_REMOVED","Publicidad de Loteka pendiente","No fue retirada toda la publicidad de Loteka.","GENERAL_SERVICES","MEDIUM"));
            if (answers.PrinterMaintained == "no") findings.Add(("PRINTER_NO_MAINTENANCE","Impresora sin mantenimiento","La impresora no recibió mantenimiento; es indispensable para imprimir tickets.","TECHNOLOGY","CRITICAL"));
            if (answers.InverterPresent == "no") findings.Add(("NO_INVERTER","Agencia sin inversor","La agencia no tiene inversor.","GENERAL_SERVICES","HIGH"));
            if (answers.BatteryPresent == "no") findings.Add(("NO_BATTERY","Agencia sin batería","La agencia no tiene batería.","GENERAL_SERVICES","HIGH"));
            var damageMap = new Dictionary<string,(string Key,string Title,string Detail,string Department,string Priority)>(StringComparer.OrdinalIgnoreCase) {
                ["WIRING"]=("DAMAGE_WIRING","Falla de cableado de datos","Se identificó una avería en el cableado tecnológico o de red.","TECHNOLOGY","HIGH"),
                ["SCREENS"]=("DAMAGE_SCREENS","Falla de pantallas","Se identificó una avería en pantallas o monitores.","TECHNOLOGY","HIGH"),
                ["CONNECTIVITY"]=("DAMAGE_CONNECTIVITY","Falla de conectividad","La conectividad impide o compromete la operación.","TECHNOLOGY","CRITICAL"),
                ["FURNITURE"]=("DAMAGE_FURNITURE","Reestructuración física / Sheetrock","Se identificó una avería histórica en el mobiliario o la infraestructura física.","GENERAL_SERVICES","MEDIUM"),
                ["GENERAL_PAINTING"]=("DAMAGE_PAINTING","Pintura","Se identificó una avería o necesidad de pintura.","GENERAL_SERVICES","LOW"),
                ["GENERAL_INVERTER"]=("DAMAGE_INVERTER","Inversor","Se identificó una avería en el inversor.","GENERAL_SERVICES","HIGH"),
                ["GENERAL_BATTERIES"]=("DAMAGE_BATTERIES","Baterías","Se identificó una avería o deterioro en las baterías.","GENERAL_SERVICES","HIGH"),
                ["GENERAL_RESTRUCTURING"]=("DAMAGE_RESTRUCTURING","Reestructuración física / Sheetrock","Se identificó una avería que requiere reestructuración física o trabajo de sheetrock.","GENERAL_SERVICES","MEDIUM"),
                ["GENERAL_LIGHTING"]=("DAMAGE_LIGHTING","Iluminación","Se identificó una falla de iluminación.","GENERAL_SERVICES","MEDIUM"),
                ["GENERAL_METALWORK"]=("DAMAGE_METALWORK","Herrería","Se identificó una avería que requiere trabajo de herrería.","GENERAL_SERVICES","MEDIUM"),
                ["GENERAL_GENERATOR_REQUEST"]=("DAMAGE_GENERATOR_REQUEST","Requisición planta","Se identificó la necesidad de requisición de una planta eléctrica.","GENERAL_SERVICES","HIGH"),
                ["GENERAL_ELECTRICAL"]=("DAMAGE_ELECTRICAL","Avería eléctrica","Se identificó una avería eléctrica.","GENERAL_SERVICES","CRITICAL"),
                ["GENERAL_SHUTTER"]=("DAMAGE_SHUTTER","Falla en el shutter","Se identificó una falla en el shutter.","GENERAL_SERVICES","HIGH"),
                ["GENERAL_OTHER"]=("DAMAGE_GENERAL_OTHER","Otro servicio general","Se identificó otra avería correspondiente a Servicios Generales.","GENERAL_SERVICES","MEDIUM")
            };
            if (answers.DamageFound == "yes") foreach (var damageType in answers.DamageTypes ?? [])
                if (damageMap.TryGetValue(damageType,out var damage)) findings.Add((damage.Key,damage.Title,damage.Detail,damage.Department,damage.Priority));

            // Automatic audit findings are intentionally stored separately from supervisor-created tickets.
            foreach (var finding in findings)
            {
                const string findingSql = """INSERT INTO dbo.automatic_findings(id,profile_id,agency_id,finding_key,title,detail,assigned_department,priority) VALUES(NEWID(),@profile,@agency,@key,@title,@detail,@department,@priority);""";
                await using var findingCommand = new SqlCommand(findingSql, connection, transaction);
                findingCommand.Parameters.AddWithValue("@profile",id.ToString()); findingCommand.Parameters.AddWithValue("@agency",agency.ToString());
                findingCommand.Parameters.AddWithValue("@key",finding.Key); findingCommand.Parameters.AddWithValue("@title",finding.Title); findingCommand.Parameters.AddWithValue("@detail",finding.Detail);
                findingCommand.Parameters.AddWithValue("@department",finding.Department); findingCommand.Parameters.AddWithValue("@priority",finding.Priority);
                await findingCommand.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct); return id;
        }
        catch { await transaction.RollbackAsync(ct); throw; }
    }

    static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6371000; var dLat = (lat2 - lat1) * Math.PI / 180; var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return Math.Round(radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)), 1);
    }

    static System.Text.Json.JsonElement NormalizeAnswers(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        static string? Read(System.Text.Json.JsonElement source, string camel, string pascal)
        {
            if (source.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            if (source.TryGetProperty(camel, out var camelValue) && camelValue.ValueKind == System.Text.Json.JsonValueKind.String) return camelValue.GetString();
            if (source.TryGetProperty(pascal, out var pascalValue) && pascalValue.ValueKind == System.Text.Json.JsonValueKind.String) return pascalValue.GetString();
            return null;
        }
        return System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, string?>
        {
            ["painted"] = Read(root, "painted", "Painted"),
            ["razaSticker"] = Read(root, "razaSticker", "RazaSticker"),
            ["realSticker"] = Read(root, "realSticker", "RealSticker"),
            ["lotekaRemoved"] = Read(root, "lotekaRemoved", "LotekaRemoved"),
            ["damageFound"] = Read(root, "damageFound", "DamageFound"),
            ["printerMaintained"] = Read(root, "printerMaintained", "PrinterMaintained"),
            ["inverterPresent"] = Read(root, "inverterPresent", "InverterPresent"),
            ["batteryPresent"] = Read(root, "batteryPresent", "BatteryPresent"),
        });
    }

    public async Task<bool> PhotoMatchesProfile(Guid id, double lat, double lng, double maximumMeters, CancellationToken ct)
    {
        await using var connection = await Open(ct); await using var command = new SqlCommand("SELECT latitude,longitude FROM dbo.agency_profiles WHERE id=@id;", connection); command.Parameters.AddWithValue("@id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) && Haversine(lat, lng, reader.GetDouble(0), reader.GetDouble(1)) <= maximumMeters;
    }

    public async Task AttachPhoto(Guid id, string type, byte[] data, string mime, double lat, double lng, double accuracy, DateTime captured, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            IF EXISTS(SELECT 1 FROM dbo.audit_photos WHERE profile_id=@id AND photo_type=@type)
                UPDATE dbo.audit_photos SET photo_data=@data,content_type=@mime,latitude=@lat,longitude=@lng,accuracy_meters=@accuracy,captured_at=@captured,created_at=SYSUTCDATETIME() WHERE profile_id=@id AND photo_type=@type;
            ELSE
                INSERT INTO dbo.audit_photos(id,profile_id,photo_type,photo_data,content_type,latitude,longitude,accuracy_meters,captured_at)
                VALUES(@photoId,@id,@type,@data,@mime,@lat,@lng,@accuracy,@captured);
            """;
        await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@photoId", Guid.NewGuid().ToString()); command.Parameters.AddWithValue("@id", id.ToString()); command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@data", data); command.Parameters.AddWithValue("@mime", mime); command.Parameters.AddWithValue("@lat", lat); command.Parameters.AddWithValue("@lng", lng); command.Parameters.AddWithValue("@accuracy", accuracy); command.Parameters.AddWithValue("@captured", captured); await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<(byte[] Data, string Mime)?> Photo(Guid id, CancellationToken ct)
    {
        await using var connection = await Open(ct); await using var command = new SqlCommand("SELECT TOP (1) photo_data,content_type FROM dbo.audit_photos WHERE profile_id=@id ORDER BY CASE photo_type WHEN 'frontal' THEN 1 WHEN 'operativa' THEN 2 WHEN 'entorno' THEN 3 WHEN 'inversor' THEN 4 WHEN 'bateria' THEN 5 ELSE 6 END;", connection); command.Parameters.AddWithValue("@id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct); if (!await reader.ReadAsync(ct)) return null; return ((byte[])reader[0], reader.GetString(1));
    }

    public async Task<(byte[] Data, string Mime)?> Photo(Guid id, string type, CancellationToken ct)
    {
        await using var connection = await Open(ct); await using var command = new SqlCommand("SELECT TOP (1) photo_data,content_type FROM dbo.audit_photos WHERE profile_id=@id AND photo_type=@type;", connection); command.Parameters.AddWithValue("@id", id.ToString()); command.Parameters.AddWithValue("@type", type);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct); if (!await reader.ReadAsync(ct)) return null; return ((byte[])reader[0], reader.GetString(1));
    }

    public async Task<List<TicketAgencyRow>> TicketAgencies(string userId, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureTicketWorkflowSchema(connection,ct);
        await EnsureAgencyDirectorySchema(connection,ct);
        const string sql = """
            SELECT a.id,a.codigo,a.terminal,a.grupo,
                   COALESCE(p.direccion,a.directory_direccion),COALESCE(p.sector,a.directory_sector),
                   COALESCE(p.municipio,a.directory_municipio),COALESCE(p.provincia,a.directory_provincia),
                   COALESCE(p.latitude,a.expected_latitude),COALESCE(p.longitude,a.expected_longitude)
            FROM dbo.agencies a
            LEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id
            WHERE a.is_active=1
              AND (@scoped=0 OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ))
            ORDER BY a.grupo,a.terminal;
            """;
        await using var command = new SqlCommand(sql, connection);
        AddScope(command, userId, role);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<TicketAgencyRow>();
        while (await reader.ReadAsync(ct)) rows.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3),reader.IsDBNull(4)?null:reader.GetString(4),reader.IsDBNull(5)?null:reader.GetString(5),reader.IsDBNull(6)?null:reader.GetString(6),reader.IsDBNull(7)?null:reader.GetString(7),reader.IsDBNull(8)?null:reader.GetDouble(8),reader.IsDBNull(9)?null:reader.GetDouble(9)));
        return rows;
    }

    public async Task<bool> UpdateAgencyDirectory(Guid id,AgencyDirectoryUpdate body,string actor,CancellationToken ct)
    {
        await using var connection=await Open(ct);
        await EnsureAgencyDirectorySchema(connection,ct);
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            Dictionary<string,object?>? before=null;
            const string readSql="""
                SELECT a.codigo,a.terminal,a.grupo,
                       COALESCE(p.direccion,a.directory_direccion),COALESCE(p.sector,a.directory_sector),
                       COALESCE(p.municipio,a.directory_municipio),COALESCE(p.provincia,a.directory_provincia),
                       COALESCE(p.latitude,a.expected_latitude),COALESCE(p.longitude,a.expected_longitude)
                FROM dbo.agencies a WITH(UPDLOCK,ROWLOCK)
                LEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id
                WHERE a.id=@id;
                """;
            await using(var read=new SqlCommand(readSql,connection,transaction))
            {
                read.Parameters.AddWithValue("@id",id.ToString());
                await using var reader=await read.ExecuteReaderAsync(ct);
                if(await reader.ReadAsync(ct))before=new()
                {
                    ["codigo"]=reader.GetString(0),["terminal"]=reader.GetString(1),["grupo"]=reader.GetString(2),
                    ["direccion"]=reader.IsDBNull(3)?null:reader.GetString(3),["sector"]=reader.IsDBNull(4)?null:reader.GetString(4),
                    ["municipio"]=reader.IsDBNull(5)?null:reader.GetString(5),["provincia"]=reader.IsDBNull(6)?null:reader.GetString(6),
                    ["latitude"]=reader.IsDBNull(7)?null:reader.GetDouble(7),["longitude"]=reader.IsDBNull(8)?null:reader.GetDouble(8)
                };
            }
            if(before is null){await transaction.RollbackAsync(ct);return false;}
            const string agencySql="""
                UPDATE dbo.agencies SET codigo=@codigo,terminal=@terminal,grupo=@grupo,
                    directory_direccion=@direccion,directory_sector=@sector,
                    directory_municipio=@municipio,directory_provincia=@provincia,
                    expected_latitude=@latitude,expected_longitude=@longitude,
                    updated_at=SYSUTCDATETIME()
                WHERE id=@id;
                """;
            await using(var agency=new SqlCommand(agencySql,connection,transaction))
            {
                agency.Parameters.AddWithValue("@id",id.ToString());agency.Parameters.AddWithValue("@codigo",body.Codigo!.Trim());agency.Parameters.AddWithValue("@terminal",body.Terminal!.Trim());agency.Parameters.AddWithValue("@grupo",body.Grupo!.Trim());
                agency.Parameters.AddWithValue("@direccion",Db(string.IsNullOrWhiteSpace(body.Direccion)?null:body.Direccion.Trim()));agency.Parameters.AddWithValue("@sector",Db(string.IsNullOrWhiteSpace(body.Sector)?null:body.Sector.Trim()));agency.Parameters.AddWithValue("@municipio",Db(string.IsNullOrWhiteSpace(body.Municipio)?null:body.Municipio.Trim()));agency.Parameters.AddWithValue("@provincia",Db(string.IsNullOrWhiteSpace(body.Provincia)?null:body.Provincia.Trim()));agency.Parameters.AddWithValue("@latitude",Db(body.Latitude));agency.Parameters.AddWithValue("@longitude",Db(body.Longitude));await agency.ExecuteNonQueryAsync(ct);
            }
            const string profileSql="""
                UPDATE dbo.agency_profiles SET
                    direccion=COALESCE(@direccion,direccion),sector=COALESCE(@sector,sector),
                    municipio=COALESCE(@municipio,municipio),provincia=COALESCE(@provincia,provincia),
                    latitude=COALESCE(@latitude,latitude),longitude=COALESCE(@longitude,longitude),
                    updated_at=SYSUTCDATETIME()
                WHERE agency_id=@id;
                """;
            await using(var profile=new SqlCommand(profileSql,connection,transaction))
            {
                profile.Parameters.AddWithValue("@id",id.ToString());profile.Parameters.AddWithValue("@direccion",Db(string.IsNullOrWhiteSpace(body.Direccion)?null:body.Direccion.Trim()));profile.Parameters.AddWithValue("@sector",Db(string.IsNullOrWhiteSpace(body.Sector)?null:body.Sector.Trim()));profile.Parameters.AddWithValue("@municipio",Db(string.IsNullOrWhiteSpace(body.Municipio)?null:body.Municipio.Trim()));profile.Parameters.AddWithValue("@provincia",Db(string.IsNullOrWhiteSpace(body.Provincia)?null:body.Provincia.Trim()));profile.Parameters.AddWithValue("@latitude",Db(body.Latitude));profile.Parameters.AddWithValue("@longitude",Db(body.Longitude));await profile.ExecuteNonQueryAsync(ct);
            }
            const string auditSql="INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data,after_data,actor_email) VALUES('agency',@id,'AGENCY_DIRECTORY_UPDATED',@before,@after,@actor);";
            await using(var audit=new SqlCommand(auditSql,connection,transaction))
            {
                audit.Parameters.AddWithValue("@id",id.ToString());audit.Parameters.AddWithValue("@before",System.Text.Json.JsonSerializer.Serialize(before));audit.Parameters.AddWithValue("@after",System.Text.Json.JsonSerializer.Serialize(body));audit.Parameters.AddWithValue("@actor",actor);await audit.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);return true;
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<AgencyDirectoryImportResult> ImportAgencyDirectory(IReadOnlyList<AgencyDirectoryImportRow> rows,string? fileName,string actor,CancellationToken ct)
    {
        await using var connection=await Open(ct);
        await EnsureAgencyDirectorySchema(connection,ct);
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var created=0;
        var duplicates=new List<string>();
        var batchId=Guid.NewGuid();
        try
        {
            foreach(var row in rows)
            {
                var codigo=row.Codigo!.Trim();
                var terminal=row.Terminal!.Trim();
                var grupo=row.Grupo!.Trim();
                const string duplicateSql="""
                    SELECT TOP(1) CONCAT(codigo,N' · ',terminal)
                    FROM dbo.agencies WITH(UPDLOCK,HOLDLOCK)
                    WHERE UPPER(LTRIM(RTRIM(codigo)))=UPPER(@codigo)
                       OR (UPPER(LTRIM(RTRIM(terminal)))=UPPER(@terminal) AND UPPER(LTRIM(RTRIM(grupo)))=UPPER(@grupo));
                    """;
                string? duplicate;
                await using(var find=new SqlCommand(duplicateSql,connection,transaction))
                {
                    find.Parameters.AddWithValue("@codigo",codigo);
                    find.Parameters.AddWithValue("@terminal",terminal);
                    find.Parameters.AddWithValue("@grupo",grupo);
                    duplicate=(string?)await find.ExecuteScalarAsync(ct);
                }
                if(duplicate is not null)
                {
                    duplicates.Add($"Fila {row.RowNumber}: {duplicate}");
                    continue;
                }

                var id=Guid.NewGuid();
                const string insertSql="""
                    INSERT INTO dbo.agencies
                        (id,source_key,codigo,terminal,grupo,region,directory_direccion,directory_sector,directory_municipio,directory_provincia,expected_latitude,expected_longitude,is_active,status,created_at,updated_at)
                    VALUES
                        (@id,@sourceKey,@codigo,@terminal,@grupo,@region,@direccion,@sector,@municipio,@provincia,@latitude,@longitude,1,'PENDING',SYSUTCDATETIME(),SYSUTCDATETIME());
                    """;
                await using(var insert=new SqlCommand(insertSql,connection,transaction))
                {
                    insert.Parameters.AddWithValue("@id",id.ToString());
                    insert.Parameters.AddWithValue("@sourceKey",$"EXCEL:{id:N}");
                    insert.Parameters.AddWithValue("@codigo",codigo);
                    insert.Parameters.AddWithValue("@terminal",terminal);
                    insert.Parameters.AddWithValue("@grupo",grupo);
                    insert.Parameters.AddWithValue("@region",Db(string.IsNullOrWhiteSpace(row.Region)?null:row.Region.Trim()));
                    insert.Parameters.AddWithValue("@direccion",Db(string.IsNullOrWhiteSpace(row.Direccion)?null:row.Direccion.Trim()));
                    insert.Parameters.AddWithValue("@sector",Db(string.IsNullOrWhiteSpace(row.Sector)?null:row.Sector.Trim()));
                    insert.Parameters.AddWithValue("@municipio",Db(string.IsNullOrWhiteSpace(row.Municipio)?null:row.Municipio.Trim()));
                    insert.Parameters.AddWithValue("@provincia",Db(string.IsNullOrWhiteSpace(row.Provincia)?null:row.Provincia.Trim()));
                    insert.Parameters.AddWithValue("@latitude",Db(row.Latitude));
                    insert.Parameters.AddWithValue("@longitude",Db(row.Longitude));
                    await insert.ExecuteNonQueryAsync(ct);
                }
                created++;
            }

            const string auditSql="""
                INSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data,actor_email)
                VALUES('agency_import',@id,'AGENCY_DIRECTORY_EXCEL_IMPORTED',@after,@actor);
                """;
            await using(var audit=new SqlCommand(auditSql,connection,transaction))
            {
                audit.Parameters.AddWithValue("@id",batchId.ToString());
                audit.Parameters.AddWithValue("@after",System.Text.Json.JsonSerializer.Serialize(new{fileName,total=rows.Count,created,skipped=duplicates.Count,duplicates=duplicates.Take(100)}));
                audit.Parameters.AddWithValue("@actor",actor);
                await audit.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return new(rows.Count,created,duplicates.Count,duplicates.Take(100).ToList());
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<SupportTicketRow>> Tickets(string userId, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureTicketWorkflowSchema(connection,ct);
        await EnsureAgencyDirectorySchema(connection,ct);
        const string sql = """
            SELECT t.id,t.ticket_number,a.codigo,a.terminal,a.grupo,t.category,t.assigned_department,t.priority,t.status,
                   t.subject,t.description,t.resolution,t.created_by_name,t.created_by_username,
                   t.created_at,t.updated_at,t.closed_at,t.is_automatic,ISNULL(t.ticket_type,'SUPPORT'),t.assigned_technician_id,u.display_name,
                   COALESCE(p.direccion,a.directory_direccion),COALESCE(p.sector,a.directory_sector),
                   COALESCE(p.municipio,a.directory_municipio),COALESCE(p.provincia,a.directory_provincia),
                   COALESCE(p.latitude,a.expected_latitude),COALESCE(p.longitude,a.expected_longitude)
            FROM dbo.support_tickets t
            INNER JOIN dbo.agencies a ON a.id=t.agency_id
            LEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id
            LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=t.assigned_technician_id
            WHERE (@scoped=0 OR t.assigned_technician_id=@userId OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ))
              AND (@department='' OR t.assigned_department=@department)
              AND (@ownOnly=0 OR t.created_by_user_id=@userId OR t.assigned_technician_id=@userId)
            ORDER BY CASE t.status WHEN 'OPEN' THEN 1 WHEN 'IN_PROGRESS' THEN 2 WHEN 'PENDING' THEN 3 WHEN 'RESOLVED' THEN 4 ELSE 5 END,
                     CASE t.priority WHEN 'CRITICAL' THEN 1 WHEN 'HIGH' THEN 2 WHEN 'MEDIUM' THEN 3 ELSE 4 END,
                     t.created_at DESC;
            """;
        await using var command = new SqlCommand(sql, connection);
        AddScope(command, userId, role);
        command.Parameters.AddWithValue("@department",DepartmentForRole(role));
        command.Parameters.AddWithValue("@ownOnly", role == "GroupAdministrator" ? 1 : 0);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<SupportTicketRow>();
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.GetDateTime(14), reader.GetDateTime(15), reader.IsDBNull(16) ? null : reader.GetDateTime(16), reader.GetBoolean(17),reader.GetString(18),reader.IsDBNull(19)?null:reader.GetString(19),reader.IsDBNull(20)?null:reader.GetString(20),reader.IsDBNull(21)?null:reader.GetString(21),reader.IsDBNull(22)?null:reader.GetString(22),reader.IsDBNull(23)?null:reader.GetString(23),reader.IsDBNull(24)?null:reader.GetString(24),reader.IsDBNull(25)?null:reader.GetDouble(25),reader.IsDBNull(26)?null:reader.GetDouble(26)));
        return rows;
    }

    public async Task<List<SupportTicketRow>> Tickets(string userId,string role,bool canAssignTickets,string? supportTeam,string? userEmail,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureTicketWorkflowSchema(connection,ct);
        const string sql="""
            DECLARE @visible TABLE(id uniqueidentifier NOT NULL PRIMARY KEY);
            INSERT INTO @visible(id)
            SELECT t.id
            FROM dbo.support_tickets t
            INNER JOIN dbo.agencies a ON a.id=t.agency_id
            OUTER APPLY (SELECT CASE WHEN
                t.assigned_technician_id=@userId
                OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))
                OR LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LTRIM(RTRIM(@userEmail)))
                OR EXISTS(SELECT 1 FROM dbo.admin_users assigned_user WHERE CONVERT(nvarchar(80),assigned_user.id)=LTRIM(RTRIM(t.assigned_technician_id)) AND LOWER(LTRIM(RTRIM(assigned_user.email)))=LOWER(LTRIM(RTRIM(@userEmail))))
                THEN 1 ELSE 0 END AS is_current) assignment
            -- Las expresiones históricas se conservan como referencia de compatibilidad:
            -- @groupSupervisor=1 AND (t.assigned_technician_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)))
            -- @supportTeam='TECHNICIANS' AND (t.assigned_technician_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)))
            WHERE
              @administrator=1
              OR (@groupSupervisor=1 AND (
                    assignment.is_current=1
                    OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo)
                 ))
              OR (@supportDepartment<>'' AND (
                    (@supportTeam='CALL_CENTER' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER')
                    OR (@supportTeam='TECHNICIANS' AND assignment.is_current=1)
                    OR (@supportTeam='TECHNICAL_FAILURE' AND (
                        assignment.is_current=1
                        OR (@canAssign=1 AND t.assigned_department='TECHNOLOGY' AND t.assigned_team IN('TECHNICAL_FAILURE','TECHNICIANS'))
                    ))
                    OR (@supportTeam NOT IN('CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS') AND (
                        assignment.is_current=1
                        OR (@canAssign=1 AND (
                            (t.assigned_department=@supportDepartment AND (@supportTeam='' OR t.assigned_team=@supportTeam))
                            OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares s WHERE s.ticket_id=t.id AND s.department_code=@supportDepartment)
                        ))
                    ))
                 ));

            SELECT t.id,t.ticket_number,a.codigo,a.terminal,a.grupo,t.category,t.assigned_department,t.priority,t.status,
                   t.subject,t.description,t.resolution,t.created_by_name,t.created_by_username,
                   t.created_at,t.updated_at,t.closed_at,t.is_automatic,ISNULL(t.ticket_type,'SUPPORT'),t.assigned_technician_id,u.display_name,
                   COALESCE(p.direccion,a.directory_direccion),COALESCE(p.sector,a.directory_sector),
                   COALESCE(p.municipio,a.directory_municipio),COALESCE(p.provincia,a.directory_provincia),
                   COALESCE(p.latitude,a.expected_latitude),COALESCE(p.longitude,a.expected_longitude),t.assigned_team,
                   COALESCE(t.resolved_by_name,ru.display_name,CASE WHEN t.status IN('RESOLVED','CLOSED') THEN u.display_name END)
            FROM @visible v
            INNER JOIN dbo.support_tickets t ON t.id=v.id
            INNER JOIN dbo.agencies a ON a.id=t.agency_id
            LEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id
            LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=t.assigned_technician_id
            LEFT JOIN dbo.admin_users ru ON CONVERT(nvarchar(80),ru.id)=t.resolved_by_user_id
            ORDER BY CASE t.status WHEN 'OPEN' THEN 1 WHEN 'IN_PROGRESS' THEN 2 WHEN 'PENDING' THEN 3 WHEN 'RESOLVED' THEN 4 ELSE 5 END,
                     CASE t.priority WHEN 'CRITICAL' THEN 1 WHEN 'HIGH' THEN 2 WHEN 'MEDIUM' THEN 3 ELSE 4 END,t.created_at DESC;

            SELECT h.ticket_id,h.id,h.action,h.from_department,h.to_department,h.from_team,h.to_team,h.actor_name,h.comment,h.created_at
            FROM dbo.support_ticket_history h INNER JOIN @visible v ON v.id=h.ticket_id ORDER BY h.created_at,h.id;
            SELECT s.ticket_id,s.department_code FROM dbo.support_ticket_shares s INNER JOIN @visible v ON v.id=s.ticket_id ORDER BY s.department_code;
            SELECT e.ticket_id,e.id,e.file_name,e.content_type,e.captured_at,e.latitude,e.longitude,e.accuracy_meters,e.location_captured_at FROM dbo.support_ticket_evidence e INNER JOIN @visible v ON v.id=e.ticket_id ORDER BY e.captured_at,e.id;
            """;
        await using var command=new SqlCommand(sql,connection);
        command.Parameters.AddWithValue("@administrator",role=="Administrator"?1:0);
        command.Parameters.AddWithValue("@groupSupervisor",role=="GroupAdministrator"?1:0);
        command.Parameters.AddWithValue("@supportDepartment",DepartmentForRole(role));
        command.Parameters.AddWithValue("@supportTeam",supportTeam??"");
        command.Parameters.AddWithValue("@userEmail",userEmail??"");
        command.Parameters.AddWithValue("@canAssign",canAssignTickets?1:0);
        command.Parameters.AddWithValue("@userId",userId);
        await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<SupportTicketRow>();
        while(await reader.ReadAsync(ct))rows.Add(new(reader.GetGuid(0),reader.GetInt64(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.GetString(8),reader.GetString(9),reader.GetString(10),reader.IsDBNull(11)?null:reader.GetString(11),reader.GetString(12),reader.GetString(13),reader.GetDateTime(14),reader.GetDateTime(15),reader.IsDBNull(16)?null:reader.GetDateTime(16),ReadBooleanValue(reader,17),reader.GetString(18),reader.IsDBNull(19)?null:reader.GetString(19),reader.IsDBNull(20)?null:reader.GetString(20),reader.IsDBNull(21)?null:reader.GetString(21),reader.IsDBNull(22)?null:reader.GetString(22),reader.IsDBNull(23)?null:reader.GetString(23),reader.IsDBNull(24)?null:reader.GetString(24),reader.IsDBNull(25)?null:reader.GetDouble(25),reader.IsDBNull(26)?null:reader.GetDouble(26),reader.IsDBNull(27)?null:reader.GetString(27),[],[],[],reader.IsDBNull(28)?null:reader.GetString(28)));
        var history=new Dictionary<Guid,List<TicketHistoryRow>>();var shares=new Dictionary<Guid,List<string>>();var evidence=new Dictionary<Guid,List<TicketEvidenceRow>>();
        if(await reader.NextResultAsync(ct))while(await reader.ReadAsync(ct))
        {
            var ticketId=reader.GetGuid(0);if(!history.TryGetValue(ticketId,out var list))history[ticketId]=list=[];
            list.Add(new(reader.GetGuid(1),reader.GetString(2),reader.IsDBNull(3)?null:reader.GetString(3),reader.IsDBNull(4)?null:reader.GetString(4),reader.IsDBNull(5)?null:reader.GetString(5),reader.IsDBNull(6)?null:reader.GetString(6),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetString(8),reader.GetDateTime(9)));
        }
        if(await reader.NextResultAsync(ct))while(await reader.ReadAsync(ct)){var ticketId=reader.GetGuid(0);if(!shares.TryGetValue(ticketId,out var list))shares[ticketId]=list=[];list.Add(reader.GetString(1));}
        if(await reader.NextResultAsync(ct))while(await reader.ReadAsync(ct))
        {
            var ticketId=reader.GetGuid(0);if(!evidence.TryGetValue(ticketId,out var list))evidence[ticketId]=list=[];var evidenceId=reader.GetGuid(1);
            list.Add(new(evidenceId,reader.GetString(2),reader.GetString(3),reader.GetDateTime(4),$"/api/tickets/evidence/{evidenceId}",reader.IsDBNull(5)?null:reader.GetDouble(5),reader.IsDBNull(6)?null:reader.GetDouble(6),reader.IsDBNull(7)?null:reader.GetDouble(7),reader.IsDBNull(8)?null:reader.GetDateTime(8)));
        }
        return rows.Select(row=>row with{History=history.GetValueOrDefault(row.Id)??[],SharedWithDepartments=shares.GetValueOrDefault(row.Id)??[],Evidence=evidence.GetValueOrDefault(row.Id)??[],IsAssignedToCurrentUser=string.Equals(row.AssignedTechnicianId?.Trim(),userId.Trim(),StringComparison.OrdinalIgnoreCase)||string.Equals(row.AssignedTechnicianId?.Trim(),userEmail?.Trim(),StringComparison.OrdinalIgnoreCase)}).ToList();
    }

    public async Task<SupportTicketRow?> CreateTicket(TicketCreate body, string userId, string role, string displayName, string username, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureTicketWorkflowSchema(connection,ct);
        const string accessSql = """
            SELECT COUNT(*) FROM dbo.agencies a
            WHERE a.id=@agency AND a.is_active=1
              AND (@scoped=0 OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo));
            """;
        await using (var access = new SqlCommand(accessSql, connection))
        {
            access.Parameters.AddWithValue("@agency", body.AgencyId!.Value.ToString()); AddScope(access, userId, role);
            if (Convert.ToInt32(await access.ExecuteScalarAsync(ct)) == 0) return null;
        }
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO dbo.support_tickets(id,agency_id,category,assigned_department,priority,subject,description,created_by_user_id,created_by_name,created_by_username,ticket_type,assigned_technician_id)
            VALUES(@id,@agency,@category,@department,@priority,@subject,@description,@userId,@name,@username,@ticketType,@technician);
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id); command.Parameters.AddWithValue("@agency", body.AgencyId.Value.ToString());
        command.Parameters.AddWithValue("@category", body.Category!); command.Parameters.AddWithValue("@department", body.AssignedDepartment!); command.Parameters.AddWithValue("@priority", body.AssignedDepartment == "TECHNOLOGY" && body.Category == "CPU_FAILURE" ? "CRITICAL" : body.Priority!);
        command.Parameters.AddWithValue("@subject", body.Subject!.Trim()); command.Parameters.AddWithValue("@description", body.Description!.Trim());
        command.Parameters.AddWithValue("@userId", userId); command.Parameters.AddWithValue("@name", displayName); command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@ticketType",body.TicketType=="INTERNAL"?"INTERNAL":"SUPPORT");command.Parameters.AddWithValue("@technician",Db(body.AssignedTechnicianId));
        await command.ExecuteNonQueryAsync(ct);
        return (await Tickets(userId, role, ct)).FirstOrDefault(ticket => ticket.Id == id);
    }

    static string DatabaseRoleForDepartment(string department)=>department switch{"TECHNOLOGY"=>"TECHNOLOGY","GENERAL_SERVICES"=>"GENERAL_SERVICES","HUMAN_RESOURCES"=>"HUMAN_RESOURCES",_=>""};

    static async Task<bool> ValidTicketAssignee(SqlConnection connection,SqlTransaction transaction,string? technicianId,string department,string? team,string ticketType,string agencyGroup,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(technicianId))return true;
        const string sql="""
            SELECT COUNT(*) FROM dbo.admin_users u
            WHERE CONVERT(nvarchar(80),u.id)=@technician AND u.is_active=1 AND (
                (u.role=@departmentRole AND (
                    (@department='TECHNOLOGY' AND (
                        (@team='CALL_CENTER' AND u.support_team='CALL_CENTER')
                        OR (@team='TECHNICAL_FAILURE' AND u.support_team='TECHNICAL_FAILURE')
                        OR (@team='TECHNICAL_FAILURE' AND (u.support_team='TECHNICIANS' OR (ISNULL(u.support_team,'')='' AND JSON_VALUE(u.permissions_json,'$.canResolveTickets')='true' AND ISNULL(JSON_VALUE(u.permissions_json,'$.canAssignTickets'),'false')<>'true')))
                        OR (@team='TECHNICIANS' AND (u.support_team='TECHNICIANS' OR u.support_team IS NULL))
                    ))
                    OR (@department<>'TECHNOLOGY' AND (u.support_team='TECHNICIANS' OR (
                        JSON_VALUE(u.permissions_json,'$.canResolveTickets')='true'
                        AND ISNULL(JSON_VALUE(u.permissions_json,'$.canAssignTickets'),'false')<>'true'
                    )))
                ))
                OR u.role='GROUP_ADMIN'
            );
            """;
        await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@technician",technicianId);command.Parameters.AddWithValue("@department",department);command.Parameters.AddWithValue("@departmentRole",DatabaseRoleForDepartment(department));command.Parameters.AddWithValue("@team",team??"");command.Parameters.AddWithValue("@ticketType",ticketType);command.Parameters.AddWithValue("@agencyGroup",agencyGroup);return Convert.ToInt32(await command.ExecuteScalarAsync(ct))>0;
    }

    static async Task AddTicketHistory(SqlConnection connection,SqlTransaction transaction,Guid ticketId,string action,string? fromDepartment,string? toDepartment,string? fromTeam,string? toTeam,string userId,string actorName,string? comment,CancellationToken ct)
    {
        const string sql="INSERT INTO dbo.support_ticket_history(ticket_id,action,from_department,to_department,from_team,to_team,actor_user_id,actor_name,comment) VALUES(@ticket,@action,@fromDepartment,@toDepartment,@fromTeam,@toTeam,@userId,@actor,@comment);";
        await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@ticket",ticketId);command.Parameters.AddWithValue("@action",action);command.Parameters.AddWithValue("@fromDepartment",Db(fromDepartment));command.Parameters.AddWithValue("@toDepartment",Db(toDepartment));command.Parameters.AddWithValue("@fromTeam",Db(fromTeam));command.Parameters.AddWithValue("@toTeam",Db(toTeam));command.Parameters.AddWithValue("@userId",Db(string.IsNullOrWhiteSpace(userId)?null:userId));command.Parameters.AddWithValue("@actor",actorName);command.Parameters.AddWithValue("@comment",Db(string.IsNullOrWhiteSpace(comment)?null:comment.Trim()));await command.ExecuteNonQueryAsync(ct);
    }

    static async Task AddTicketNotifications(SqlConnection connection,SqlTransaction transaction,Guid ticketId,string eventType,string? recipientUserId,string? targetDepartment,string agencyGroup,string userId,string actorName,string message,bool notifySupervisors,CancellationToken ct)
    {
        const string sql="""
            DECLARE @occurredAt datetime2(3)=SYSUTCDATETIME();
            IF @targetDepartment<>''
                INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,target_department,message,actor_user_id,actor_name,created_at)
                VALUES(@ticket,@event,@targetDepartment,@message,@userId,@actor,@occurredAt);
            IF @recipient<>''
                INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name,created_at)
                VALUES(@ticket,@event,@recipient,@message,@userId,@actor,@occurredAt);
            IF @notifySupervisors=1
                INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name,created_at)
                SELECT @ticket,@event,CONVERT(nvarchar(80),u.id),@message,@userId,@actor,@occurredAt
                FROM dbo.admin_users u
                WHERE u.is_active=1 AND u.role='GROUP_ADMIN'
                  AND EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=u.id AND ug.group_name=@agencyGroup)
                  AND (@recipient='' OR CONVERT(nvarchar(80),u.id)<>@recipient);
            """;
        await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@ticket",ticketId);command.Parameters.AddWithValue("@event",eventType);command.Parameters.AddWithValue("@recipient",recipientUserId??"");command.Parameters.AddWithValue("@targetDepartment",targetDepartment??"");command.Parameters.AddWithValue("@agencyGroup",agencyGroup);command.Parameters.AddWithValue("@userId",Db(string.IsNullOrWhiteSpace(userId)?null:userId));command.Parameters.AddWithValue("@actor",actorName);command.Parameters.AddWithValue("@message",message);command.Parameters.AddWithValue("@notifySupervisors",notifySupervisors?1:0);await command.ExecuteNonQueryAsync(ct);
    }

    static async Task AddTechnologyResolutionNotifications(SqlConnection connection,SqlTransaction transaction,Guid ticketId,string eventType,string userId,string actorName,string message,CancellationToken ct)
    {
        const string sql="""
            INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name)
            SELECT @ticket,@event,CONVERT(nvarchar(80),u.id),@message,@userId,@actor
            FROM dbo.admin_users u
            INNER JOIN dbo.support_tickets t ON t.id=@ticket
            WHERE u.is_active=1 AND u.role='TECHNOLOGY'
              AND (u.support_team IS NULL OR u.support_team=t.assigned_team)
              AND CONVERT(nvarchar(80),u.id)<>@userId;
            """;
        await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@ticket",ticketId);command.Parameters.AddWithValue("@event",eventType);command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@actor",actorName);command.Parameters.AddWithValue("@message",message);await command.ExecuteNonQueryAsync(ct);
    }

    static async Task<bool> CanWorkTicket(SqlConnection connection,SqlTransaction transaction,Guid ticketId,string userId,string role,string? supportTeam,bool canAssignTickets,CancellationToken ct)
    {
        const string sql="""
            SELECT COUNT(*)
            FROM dbo.support_tickets t INNER JOIN dbo.agencies a ON a.id=t.agency_id
            OUTER APPLY (SELECT CASE WHEN
                t.assigned_technician_id=@userId
                OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))
                OR EXISTS(SELECT 1 FROM dbo.admin_users actor_account WHERE CONVERT(nvarchar(80),actor_account.id)=@userId AND LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LTRIM(RTRIM(actor_account.email))))
                THEN 1 ELSE 0 END AS is_current) assignment
            -- Compatibilidad con asignaciones históricas:
            -- t.assigned_technician_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))
            WHERE t.id=@ticket AND (
                @administrator=1
                OR (@groupSupervisor=1 AND assignment.is_current=1)
                OR (@supportDepartment<>'' AND (
                    (@supportTeam='CALL_CENTER' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER')
                    OR (@supportTeam='TECHNICAL_FAILURE' AND (
                        assignment.is_current=1
                        OR (@canAssign=1 AND t.assigned_department='TECHNOLOGY' AND t.assigned_team IN('TECHNICAL_FAILURE','TECHNICIANS'))
                    ))
                    OR (@supportTeam NOT IN('CALL_CENTER','TECHNICAL_FAILURE') AND (
                        assignment.is_current=1
                        OR (@canAssign=1 AND (
                            (t.assigned_department=@supportDepartment AND (@supportTeam='' OR t.assigned_team=@supportTeam))
                            OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares s WHERE s.ticket_id=t.id AND s.department_code=@supportDepartment)
                        ))
                    ))
                ))
            );
            """;
        await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@ticket",ticketId);command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@administrator",role=="Administrator"?1:0);command.Parameters.AddWithValue("@groupSupervisor",role=="GroupAdministrator"?1:0);command.Parameters.AddWithValue("@supportDepartment",DepartmentForRole(role));command.Parameters.AddWithValue("@supportTeam",supportTeam??"");command.Parameters.AddWithValue("@canAssign",canAssignTickets?1:0);return Convert.ToInt32(await command.ExecuteScalarAsync(ct))>0;
    }

    static async Task<bool> CanAdministerTicket(SqlConnection connection,SqlTransaction transaction,Guid ticketId,string role,string? supportTeam,bool canAssignTickets,CancellationToken ct)
    {
        const string sql="""
            SELECT COUNT(*) FROM dbo.support_tickets t
            WHERE t.id=@ticket AND (
                @administrator=1
                OR (@canAssign=1 AND @department<>'' AND t.assigned_department=@department
                    AND (@supportTeam='' OR t.assigned_team=@supportTeam OR (@supportTeam='TECHNICAL_FAILURE' AND t.assigned_team='TECHNICIANS')))
            );
            """;
        await using var command=new SqlCommand(sql,connection,transaction);
        command.Parameters.AddWithValue("@ticket",ticketId);
        command.Parameters.AddWithValue("@administrator",role=="Administrator"?1:0);
        command.Parameters.AddWithValue("@canAssign",canAssignTickets?1:0);
        command.Parameters.AddWithValue("@department",DepartmentForRole(role));
        command.Parameters.AddWithValue("@supportTeam",supportTeam??"");
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct))>0;
    }

    public async Task<SupportTicketRow?> CreateTicket(TicketCreate body,string userId,string role,string displayName,string username,string? supportTeam,bool canAssignTickets,IReadOnlyList<TicketEvidenceUpload> creationEvidence,string? supervisorEscalationReason,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureTicketWorkflowSchema(connection,ct);await EnsureUserPermissionSchema(connection,ct);
        var ticketType=body.TicketType=="INTERNAL"?"INTERNAL":"SUPPORT";var department=body.AssignedDepartment!.Trim().ToUpperInvariant();var team=department=="TECHNOLOGY"?body.AssignedTeam?.Trim().ToUpperInvariant():null;var priority=department=="TECHNOLOGY"&&string.Equals(body.Category?.Trim(),"CPU_FAILURE",StringComparison.OrdinalIgnoreCase)?"CRITICAL":body.Priority!.Trim().ToUpperInvariant();if(department=="TECHNOLOGY"&&team is null)team="TECHNICAL_FAILURE";
        if(supportTeam=="CALL_CENTER"){if(ticketType!="INTERNAL"||department!="TECHNOLOGY")return null;team="CALL_CENTER";}
        else if(supportTeam=="TECHNICAL_FAILURE"&&department=="TECHNOLOGY")team="TECHNICAL_FAILURE";
        else if(role is "Technology" or "GeneralServices" or "HumanResources"&&department!=DepartmentForRole(role))return null;
        if(team is not (null or "CALL_CENTER" or "TECHNICAL_FAILURE" or "TECHNICIANS"))return null;
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            string agencyGroup;
            const string accessSql="""
                SELECT TOP(1)a.grupo FROM dbo.agencies a
                WHERE a.id=@agency AND a.is_active=1
                  AND (@scoped=0 OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo));
                """;
            await using(var access=new SqlCommand(accessSql,connection,transaction)){access.Parameters.AddWithValue("@agency",body.AgencyId!.Value.ToString());access.Parameters.AddWithValue("@scoped",role=="GroupAdministrator"?1:0);access.Parameters.AddWithValue("@userId",userId);agencyGroup=(await access.ExecuteScalarAsync(ct)) as string??"";if(string.IsNullOrWhiteSpace(agencyGroup)){await transaction.RollbackAsync(ct);return null;}}
            await using(var catalog=new SqlCommand("SELECT COUNT(*) FROM dbo.support_departments d WHERE d.code=@department AND d.is_active=1 AND EXISTS(SELECT 1 FROM dbo.support_categories c WHERE c.department_code=d.code AND c.code=@category AND c.is_active=1);",connection,transaction)){catalog.Parameters.AddWithValue("@department",department);catalog.Parameters.AddWithValue("@category",body.Category!);if(Convert.ToInt32(await catalog.ExecuteScalarAsync(ct))==0){await transaction.RollbackAsync(ct);return null;}}
            if(body.AllowDuplicate!=true)
            {
                const string duplicateSql="""
                    SELECT TOP(1)t.id,t.ticket_number,t.status,t.subject
                    FROM dbo.support_tickets t WITH(UPDLOCK,HOLDLOCK)
                    WHERE t.agency_id=@agency
                      AND t.assigned_department=@department
                      AND t.category=@category
                      AND ISNULL(t.ticket_type,'SUPPORT')=@ticketType
                      AND t.status NOT IN('RESOLVED','CLOSED','CANCELLED')
                    ORDER BY t.created_at,t.ticket_number;
                    """;
                await using var duplicateCommand=new SqlCommand(duplicateSql,connection,transaction);
                duplicateCommand.Parameters.AddWithValue("@agency",body.AgencyId!.Value.ToString());
                duplicateCommand.Parameters.AddWithValue("@department",department);
                duplicateCommand.Parameters.AddWithValue("@category",body.Category!);
                duplicateCommand.Parameters.AddWithValue("@ticketType",ticketType);
                await using var duplicateReader=await duplicateCommand.ExecuteReaderAsync(ct);
                if(await duplicateReader.ReadAsync(ct))
                {
                    var duplicate=new TicketDuplicateException(duplicateReader.GetGuid(0),duplicateReader.GetInt64(1),duplicateReader.GetString(2),duplicateReader.GetString(3));
                    await duplicateReader.DisposeAsync();throw duplicate;
                }
            }
            // Los tickets internos creados por Call Center quedan asignados al
            // usuario que los registra. Así la respuesta nunca queda vacía y el
            // caso aparece inmediatamente en su bandeja personal. El responsable
            // del área receptora se elige únicamente al escalarlo.
            var requestedTechnicianId=supportTeam=="CALL_CENTER"?userId:role=="GroupAdministrator"?null:body.AssignedTechnicianId;
            if(!string.IsNullOrWhiteSpace(requestedTechnicianId)&&!canAssignTickets){await transaction.RollbackAsync(ct);return null;}
            if(!await ValidTicketAssignee(connection,transaction,requestedTechnicianId,department,team,ticketType,agencyGroup,ct)){await transaction.RollbackAsync(ct);return null;}
            var id=Guid.NewGuid();const string insert="""
                INSERT INTO dbo.support_tickets(id,agency_id,category,assigned_department,assigned_team,priority,status,subject,description,created_by_user_id,created_by_name,created_by_username,ticket_type,assigned_technician_id)
                VALUES(@id,@agency,@category,@department,@team,@priority,CASE WHEN @technician IS NULL THEN 'OPEN' ELSE 'IN_PROGRESS' END,@subject,@description,@userId,@name,@username,@ticketType,@technician);
                """;
            await using(var command=new SqlCommand(insert,connection,transaction)){command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@agency",body.AgencyId.Value.ToString());command.Parameters.AddWithValue("@category",body.Category!);command.Parameters.AddWithValue("@department",department);command.Parameters.AddWithValue("@team",Db(team));command.Parameters.AddWithValue("@priority",priority);command.Parameters.AddWithValue("@subject",body.Subject!.Trim());command.Parameters.AddWithValue("@description",body.Description!.Trim());command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@name",displayName);command.Parameters.AddWithValue("@username",username);command.Parameters.AddWithValue("@ticketType",ticketType);command.Parameters.AddWithValue("@technician",Db(string.IsNullOrWhiteSpace(requestedTechnicianId)?null:requestedTechnicianId));await command.ExecuteNonQueryAsync(ct);}
            await AddTicketHistory(connection,transaction,id,"CREATED",null,department,null,team,userId,displayName,null,ct);
            foreach(var item in creationEvidence)
            {
                await using var evidenceCommand=new SqlCommand("INSERT INTO dbo.support_ticket_evidence(ticket_id,file_name,content_type,photo_data,uploaded_by_user_id,uploaded_by_name) VALUES(@ticket,@fileName,@contentType,@data,@userId,@actor);",connection,transaction);
                evidenceCommand.Parameters.AddWithValue("@ticket",id);evidenceCommand.Parameters.AddWithValue("@fileName",item.FileName);evidenceCommand.Parameters.AddWithValue("@contentType",item.ContentType);evidenceCommand.Parameters.Add("@data",SqlDbType.VarBinary,-1).Value=item.Data;evidenceCommand.Parameters.AddWithValue("@userId",userId);evidenceCommand.Parameters.AddWithValue("@actor",displayName);await evidenceCommand.ExecuteNonQueryAsync(ct);
            }
            if(role=="GroupAdministrator"&&!string.IsNullOrWhiteSpace(supervisorEscalationReason))await AddTicketHistory(connection,transaction,id,"SUPERVISOR_ESCALATED",null,department,null,team,userId,displayName,supervisorEscalationReason.Trim(),ct);
            await AddTicketNotifications(connection,transaction,id,"CREATED",null,department,agencyGroup,userId,displayName,$"Se abrió un ticket {(ticketType=="INTERNAL"?"interno":"de soporte")} en {department}.",true,ct);
            if(!string.IsNullOrWhiteSpace(requestedTechnicianId))await AddTicketNotifications(connection,transaction,id,"ASSIGNED",requestedTechnicianId,null,agencyGroup,userId,displayName,"Se te asignó un nuevo ticket.",false,ct);
            await transaction.CommitAsync(ct);return (await Tickets(userId,role,true,supportTeam,username,ct)).FirstOrDefault(ticket=>ticket.Id==id);
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<bool> UpdateTicket(Guid id, TicketUpdate body, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureTicketWorkflowSchema(connection,ct);
        const string sql = """
            UPDATE dbo.support_tickets SET status=CASE WHEN @technician IS NOT NULL AND @status='OPEN' THEN 'IN_PROGRESS' ELSE @status END,priority=CASE WHEN assigned_department='TECHNOLOGY' AND category='CPU_FAILURE' THEN 'CRITICAL' ELSE @priority END,resolution=@resolution,assigned_technician_id=@technician,
                updated_at=SYSUTCDATETIME(),closed_at=CASE WHEN @status='CLOSED' THEN COALESCE(closed_at,SYSUTCDATETIME()) ELSE NULL END
            WHERE id=@id AND (@department='' OR assigned_department=@department);
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id); command.Parameters.AddWithValue("@status", body.Status!); command.Parameters.AddWithValue("@priority", body.Priority!);
        command.Parameters.AddWithValue("@resolution", Db(string.IsNullOrWhiteSpace(body.Resolution) ? null : body.Resolution.Trim()));
        command.Parameters.AddWithValue("@technician",Db(body.AssignedTechnicianId));
        command.Parameters.AddWithValue("@department",DepartmentForRole(role));
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    sealed record TicketWorkflowState(string AgencyGroup,string Department,string? Team,string TicketType,string Status,string Priority,string? TechnicianId,string CreatedByUserId,double? AgencyLatitude,double? AgencyLongitude);

    static async Task<TicketWorkflowState?> LoadTicketState(SqlConnection connection,SqlTransaction transaction,Guid id,CancellationToken ct)
    {
        const string sql="""
            SELECT a.grupo,t.assigned_department,t.assigned_team,ISNULL(t.ticket_type,'SUPPORT'),t.status,t.priority,t.assigned_technician_id,t.created_by_user_id,
                   COALESCE(profile_location.latitude,a.expected_latitude),COALESCE(profile_location.longitude,a.expected_longitude)
            FROM dbo.support_tickets t WITH(UPDLOCK,ROWLOCK)
            INNER JOIN dbo.agencies a ON a.id=t.agency_id
            OUTER APPLY(SELECT TOP(1)p.latitude,p.longitude FROM dbo.agency_profiles p WHERE p.agency_id=a.id ORDER BY p.submitted_at DESC,p.id DESC) profile_location
            WHERE t.id=@id;
            """;
        await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@id",id);await using var reader=await command.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))return null;return new(reader.GetString(0),reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.IsDBNull(6)?null:reader.GetString(6),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetDouble(8),reader.IsDBNull(9)?null:reader.GetDouble(9));
    }

    public async Task<TicketActionResult> UpdateTicket(Guid id,TicketUpdate body,string userId,string role,string actorName,string? supportTeam,bool canAssignTickets,CancellationToken ct)
    {
        if(body.Status is "RESOLVED" or "CLOSED")return new(false,"VALIDATION","Para resolver o cerrar el ticket debes adjuntar evidencia en la acción de resolución.");
        await using var connection=await Open(ct);await EnsureTicketWorkflowSchema(connection,ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            var state=await LoadTicketState(connection,transaction,id,ct);if(state is null){await transaction.RollbackAsync(ct);return new(false,"NOT_FOUND","Ticket no encontrado.");}
            if(state.Status is "RESOLVED" or "CLOSED" or "CANCELLED"){await transaction.RollbackAsync(ct);return new(false,"CONFLICT","Un ticket finalizado no puede reabrirse desde esta acción.");}
            var requestedTechnician=string.IsNullOrWhiteSpace(body.AssignedTechnicianId)?null:body.AssignedTechnicianId;
            var requestedTeam=string.IsNullOrWhiteSpace(body.AssignedTeam)?state.Team:body.AssignedTeam.Trim().ToUpperInvariant();
            if(state.Department!="TECHNOLOGY") requestedTeam=null;
            if(requestedTeam is not (null or "CALL_CENTER" or "TECHNICAL_FAILURE" or "TECHNICIANS")){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","El equipo de Tecnología no es válido.");}
            var changesResponsible=!string.Equals(state.TechnicianId,requestedTechnician,StringComparison.OrdinalIgnoreCase);
            if(supportTeam=="CALL_CENTER"&&changesResponsible){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Call Center solo puede compartir el ticket con otros departamentos; no puede asignar responsables.");}
            var administrativeAction=body.Status=="CANCELLED"||changesResponsible;
            if(administrativeAction)
            {
                if(!await CanAdministerTicket(connection,transaction,id,role,supportTeam,canAssignTickets,ct)){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Solo el administrador o el soporte del área propietaria puede anular el ticket o cambiar su responsable.");}
            }
            else if(!await CanWorkTicket(connection,transaction,id,userId,role,supportTeam,canAssignTickets,ct)){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","No tienes permiso para actualizar este ticket.");}
            if(!await ValidTicketAssignee(connection,transaction,body.AssignedTechnicianId,state.Department,requestedTeam,state.TicketType,state.AgencyGroup,ct)){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","El técnico no pertenece al departamento o a la agencia del ticket.");}
            var terminal=body.Status is "RESOLVED" or "CLOSED" or "CANCELLED";const string sql="""
                UPDATE dbo.support_tickets SET status=CASE WHEN @technician IS NOT NULL AND @status='OPEN' THEN 'IN_PROGRESS' ELSE @status END,priority=@priority,resolution=@resolution,assigned_technician_id=@technician,assigned_team=@team,
                    resolved_by_user_id=CASE WHEN @terminal=1 THEN @userId ELSE resolved_by_user_id END,
                    resolved_by_name=CASE WHEN @terminal=1 THEN @actor ELSE resolved_by_name END,
                    resolved_at=CASE WHEN @terminal=1 THEN COALESCE(resolved_at,SYSUTCDATETIME()) ELSE NULL END,
                    updated_at=SYSUTCDATETIME(),closed_at=CASE WHEN @status IN('CLOSED','CANCELLED') THEN COALESCE(closed_at,SYSUTCDATETIME()) ELSE NULL END
                WHERE id=@id;
                """;
            await using(var command=new SqlCommand(sql,connection,transaction)){command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@status",body.Status!);command.Parameters.AddWithValue("@priority",body.Priority!);command.Parameters.AddWithValue("@resolution",Db(string.IsNullOrWhiteSpace(body.Resolution)?null:body.Resolution.Trim()));command.Parameters.AddWithValue("@technician",Db(string.IsNullOrWhiteSpace(body.AssignedTechnicianId)?null:body.AssignedTechnicianId));command.Parameters.AddWithValue("@team",Db(requestedTeam));command.Parameters.AddWithValue("@terminal",terminal?1:0);command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@actor",actorName);await command.ExecuteNonQueryAsync(ct);}
            var action=terminal?body.Status!:changesResponsible?(requestedTechnician is null?"UNASSIGNED":state.TechnicianId is null?"ASSIGNED":"REASSIGNED"):"STATUS_CHANGED";await AddTicketHistory(connection,transaction,id,action,state.Department,state.Department,state.Team,requestedTeam,userId,actorName,body.Resolution,ct);
            if(changesResponsible&&requestedTechnician is not null)await AddTicketNotifications(connection,transaction,id,state.TechnicianId is null?"ASSIGNED":"REASSIGNED",requestedTechnician,null,state.AgencyGroup,userId,actorName,state.TechnicianId is null?"Se te asignó un ticket de soporte.":"Se te reasignó un ticket de soporte.",false,ct);
            if(!terminal&&body.Status=="PENDING"&&state.Status!="PENDING")await AddTicketNotifications(connection,transaction,id,"PENDING",requestedTechnician,state.Department,state.AgencyGroup,userId,actorName,$"El ticket quedó pendiente: {body.Resolution}",false,ct);
            if(!terminal&&body.Status=="IN_PROGRESS"&&state.Status=="PENDING")await AddTicketNotifications(connection,transaction,id,"UPDATED",requestedTechnician,state.Department,state.AgencyGroup,userId,actorName,"El ticket fue reanudado y volvió a estar en proceso.",false,ct);
            if(terminal)await AddTicketNotifications(connection,transaction,id,body.Status!,null,state.Department,state.AgencyGroup,userId,actorName,$"El ticket fue {(body.Status=="CLOSED"?"cerrado":body.Status=="CANCELLED"?"anulado":"resuelto")} por {actorName}.",true,ct);
            await transaction.CommitAsync(ct);return new(true,"OK",null);
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<TicketActionResult> RouteTicket(Guid id,TicketRoute body,string userId,string role,string actorName,string? supportTeam,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureTicketWorkflowSchema(connection,ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            var state=await LoadTicketState(connection,transaction,id,ct);if(state is null){await transaction.RollbackAsync(ct);return new(false,"NOT_FOUND","Ticket no encontrado.");}
            if(state.Status is "RESOLVED" or "CLOSED" or "CANCELLED"){await transaction.RollbackAsync(ct);return new(false,"CONFLICT","No se puede reasignar un ticket finalizado.");}
            var department=body.AssignedDepartment!.Trim().ToUpperInvariant();await using(var catalog=new SqlCommand("SELECT COUNT(*) FROM dbo.support_departments WHERE code=@department AND is_active=1;",connection,transaction)){catalog.Parameters.AddWithValue("@department",department);if(Convert.ToInt32(await catalog.ExecuteScalarAsync(ct))==0){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","El departamento de destino no está activo.");}}
            var team=department=="TECHNOLOGY"?(body.AssignedTeam?.Trim().ToUpperInvariant()??(department==state.Department?state.Team:"TECHNICAL_FAILURE")):null;if(team is not (null or "CALL_CENTER" or "TECHNICAL_FAILURE" or "TECHNICIANS")){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","El equipo de Tecnología no es válido.");}
            var callCenterEscalation=supportTeam=="CALL_CENTER"&&state.TicketType=="INTERNAL"&&state.Department=="TECHNOLOGY"&&department=="TECHNOLOGY"&&team=="TECHNICAL_FAILURE"&&string.IsNullOrWhiteSpace(body.AssignedTechnicianId);
            if(supportTeam=="CALL_CENTER"&&!callCenterEscalation){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Call Center solo puede compartir tickets internos escalándolos a Avería Técnica.");}
            if(role=="Technology"&&supportTeam is "TECHNICIANS" or "WAREHOUSE" or "WORKSHOP"){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Los técnicos no pueden asignar ni transferir tickets.");}
            var requestedTechnician=string.IsNullOrWhiteSpace(body.AssignedTechnicianId)?null:body.AssignedTechnicianId;
            var changesResponsible=!string.Equals(state.TechnicianId,requestedTechnician,StringComparison.OrdinalIgnoreCase);
            var changesDepartment=!string.Equals(state.Department,department,StringComparison.OrdinalIgnoreCase);
            if(state.TechnicianId is not null&&changesDepartment){await transaction.RollbackAsync(ct);return new(false,"CONFLICT","Primero deja el ticket sin responsable antes de transferirlo a otro departamento.");}
            if(changesResponsible&&!callCenterEscalation)
            {
                if(!await CanAdministerTicket(connection,transaction,id,role,supportTeam,true,ct)){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Solo el administrador o el soporte del área propietaria puede cambiar el responsable.");}
            }
            else if(!callCenterEscalation&&!await CanWorkTicket(connection,transaction,id,userId,role,supportTeam,true,ct)){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","No tienes permiso para reasignar este ticket.");}
            if(supportTeam=="CALL_CENTER"&&state.TicketType!="INTERNAL"){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Call Center solo puede escalar tickets internos.");}
            if(!await ValidTicketAssignee(connection,transaction,body.AssignedTechnicianId,department,team,state.TicketType,state.AgencyGroup,ct)){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","El técnico seleccionado no pertenece al destino o no supervisa esta agencia.");}
            const string update="UPDATE dbo.support_tickets SET assigned_department=@department,assigned_team=@team,assigned_technician_id=@technician,status=CASE WHEN @technician IS NULL THEN 'OPEN' ELSE 'IN_PROGRESS' END,updated_at=SYSUTCDATETIME() WHERE id=@id;";
            await using(var command=new SqlCommand(update,connection,transaction)){command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@department",department);command.Parameters.AddWithValue("@team",Db(team));command.Parameters.AddWithValue("@technician",Db(string.IsNullOrWhiteSpace(body.AssignedTechnicianId)?null:body.AssignedTechnicianId));await command.ExecuteNonQueryAsync(ct);}
            var assigned=requestedTechnician is not null;var escalated=!assigned&&(state.Department!=department||state.Team!=team);var action=escalated?"ESCALATED":changesResponsible?(assigned?(state.TechnicianId is null?"ASSIGNED":"REASSIGNED"):"UNASSIGNED"):"ROUTED";await AddTicketHistory(connection,transaction,id,action,state.Department,department,state.Team,team,userId,actorName,body.Comment,ct);
            await AddTicketNotifications(connection,transaction,id,action,requestedTechnician,department,state.AgencyGroup,userId,actorName,assigned?(state.TechnicianId is null?"Se te asignó un ticket de soporte.":"Se te reasignó un ticket de soporte."):$"El ticket fue enviado a {department}{(team is null?"":$" / {team}")}.",!assigned,ct);
            await transaction.CommitAsync(ct);return new(true,"OK",null);
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<TicketActionResult> ShareTicket(Guid id,TicketShare body,string userId,string role,string actorName,string? supportTeam,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureTicketWorkflowSchema(connection,ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            var state=await LoadTicketState(connection,transaction,id,ct);if(state is null){await transaction.RollbackAsync(ct);return new(false,"NOT_FOUND","Ticket no encontrado.");}if(state.Status is "RESOLVED" or "CLOSED" or "CANCELLED"){await transaction.RollbackAsync(ct);return new(false,"CONFLICT","No se puede compartir un ticket finalizado.");}
            if(role=="Technology"&&supportTeam is "TECHNICIANS" or "WAREHOUSE" or "WORKSHOP"){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Los técnicos no pueden compartir ni transferir tickets.");}
            if(!await CanWorkTicket(connection,transaction,id,userId,role,supportTeam,true,ct)){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","No tienes permiso para compartir este ticket.");}
            var departments=(body.Departments??[]).Where(value=>!string.IsNullOrWhiteSpace(value)).Select(value=>value.Trim().ToUpperInvariant()).Where(value=>value!=state.Department).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if(departments.Count>0){const string validSql="SELECT code FROM dbo.support_departments WHERE is_active=1;";var valid=new HashSet<string>(StringComparer.OrdinalIgnoreCase);await using(var command=new SqlCommand(validSql,connection,transaction))await using(var reader=await command.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))valid.Add(reader.GetString(0));if(departments.Any(value=>!valid.Contains(value))){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","Uno de los departamentos seleccionados no está activo.");}}
            await using(var delete=new SqlCommand("DELETE FROM dbo.support_ticket_shares WHERE ticket_id=@ticket;",connection,transaction)){delete.Parameters.AddWithValue("@ticket",id);await delete.ExecuteNonQueryAsync(ct);}
            foreach(var department in departments){await using var insert=new SqlCommand("INSERT INTO dbo.support_ticket_shares(ticket_id,department_code,shared_by_user_id,shared_by_name) VALUES(@ticket,@department,@userId,@actor);",connection,transaction);insert.Parameters.AddWithValue("@ticket",id);insert.Parameters.AddWithValue("@department",department);insert.Parameters.AddWithValue("@userId",userId);insert.Parameters.AddWithValue("@actor",actorName);await insert.ExecuteNonQueryAsync(ct);await AddTicketNotifications(connection,transaction,id,"ESCALATED",null,department,state.AgencyGroup,userId,actorName,$"{actorName} compartió un ticket con {department}.",false,ct);}
            await AddTicketHistory(connection,transaction,id,"SHARED",state.Department,state.Department,state.Team,state.Team,userId,actorName,string.IsNullOrWhiteSpace(body.Comment)?(departments.Count==0?"Se retiraron los departamentos compartidos.":$"Compartido con: {string.Join(", ",departments)}"):body.Comment,ct);
            await transaction.CommitAsync(ct);return new(true,"OK",null);
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<TicketActionResult> ResolveTicket(Guid id,string status,string resolution,List<TicketEvidenceUpload> evidence,string userId,string role,string actorName,string? supportTeam,bool canAssignTickets,bool canResolveTickets,CancellationToken ct)
    {
        if(role!="GroupAdministrator"&&!canResolveTickets)return new(false,"FORBIDDEN","Tu perfil no tiene permiso para resolver tickets.");
        await using var connection=await Open(ct);await EnsureTicketWorkflowSchema(connection,ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            var state=await LoadTicketState(connection,transaction,id,ct);if(state is null){await transaction.RollbackAsync(ct);return new(false,"NOT_FOUND","Ticket no encontrado.");}if(state.Status is "RESOLVED" or "CLOSED" or "CANCELLED"){await transaction.RollbackAsync(ct);return new(false,"CONFLICT","Este ticket ya fue finalizado.");}
            if(state.TechnicianId==userId&&evidence.Any(item=>item.Latitude is null||item.Longitude is null||item.AccuracyMeters is null)){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","La evidencia del técnico asignado debe incluir la ubicación GPS capturada en el lugar.");}
            if(!await CanWorkTicket(connection,transaction,id,userId,role,supportTeam,canAssignTickets,ct)){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","El ticket no está asignado a tu usuario o departamento.");}
            if(role=="GroupAdministrator")
            {
                if(!string.Equals(state.TechnicianId,userId,StringComparison.OrdinalIgnoreCase)){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","El supervisor solo puede resolver tickets asignados directamente a su cuenta.");}
                if(state.AgencyLatitude is null||state.AgencyLongitude is null){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","La agencia no tiene una ubicación verificada. Completa primero su Auditoría de campo.");}
                var now=DateTime.UtcNow;
                if(evidence.Any(item=>item.Latitude is null||item.Longitude is null||item.AccuracyMeters is null||item.LocationCapturedAt is null||item.AccuracyMeters<=0||item.AccuracyMeters>50||item.LocationCapturedAt<now.AddMinutes(-10)||item.LocationCapturedAt>now.AddMinutes(2))){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","La resolución del supervisor requiere GPS reciente, hora de captura y precisión máxima de 50 metros.");}
                var supervisorLocation=evidence[0];var distance=Haversine(supervisorLocation.Latitude!.Value,supervisorLocation.Longitude!.Value,state.AgencyLatitude.Value,state.AgencyLongitude.Value);
                if(distance>75){await transaction.RollbackAsync(ct);return new(false,"VALIDATION",$"Debes resolver el ticket desde la agencia. La evidencia se capturó a {Math.Round(distance)} metros del punto verificado.");}
            }
            var assignedTechnologyTechnician=false;
            if(!string.IsNullOrWhiteSpace(state.TechnicianId))
            {
                await using var assignee=new SqlCommand("SELECT COUNT(*) FROM dbo.admin_users WHERE CONVERT(nvarchar(80),id)=@id AND role='TECHNOLOGY' AND ISNULL(support_team,'TECHNICIANS') NOT IN('CALL_CENTER','TECHNICAL_FAILURE');",connection,transaction);
                assignee.Parameters.AddWithValue("@id",state.TechnicianId);
                assignedTechnologyTechnician=Convert.ToInt32(await assignee.ExecuteScalarAsync(ct))>0;
            }
            if(assignedTechnologyTechnician&&evidence.Any(item=>item.Latitude is null||item.Longitude is null||item.AccuracyMeters is null||item.LocationCapturedAt is null)){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","Los casos asignados a técnicos de Tecnología requieren fotografías tomadas con GPS, coordenadas y precisión.");}
            const string update="""
                UPDATE dbo.support_tickets SET status=@status,resolution=@resolution,resolved_by_user_id=@userId,resolved_by_name=@actor,
                    resolved_at=SYSUTCDATETIME(),closed_at=CASE WHEN @status='CLOSED' THEN SYSUTCDATETIME() ELSE NULL END,updated_at=SYSUTCDATETIME()
                WHERE id=@id;
                """;
            await using(var command=new SqlCommand(update,connection,transaction)){command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@status",status);command.Parameters.AddWithValue("@resolution",resolution);command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@actor",actorName);await command.ExecuteNonQueryAsync(ct);}
            foreach(var item in evidence){await using var insert=new SqlCommand("INSERT INTO dbo.support_ticket_evidence(ticket_id,file_name,content_type,photo_data,uploaded_by_user_id,uploaded_by_name,latitude,longitude,accuracy_meters,location_captured_at,location_source) VALUES(@ticket,@fileName,@contentType,@data,@userId,@actor,@latitude,@longitude,@accuracy,@locationCapturedAt,@locationSource);",connection,transaction);insert.Parameters.AddWithValue("@ticket",id);insert.Parameters.AddWithValue("@fileName",item.FileName);insert.Parameters.AddWithValue("@contentType",item.ContentType);insert.Parameters.Add("@data",SqlDbType.VarBinary,-1).Value=item.Data;insert.Parameters.AddWithValue("@userId",userId);insert.Parameters.AddWithValue("@actor",actorName);insert.Parameters.AddWithValue("@latitude",Db(item.Latitude));insert.Parameters.AddWithValue("@longitude",Db(item.Longitude));insert.Parameters.AddWithValue("@accuracy",Db(item.AccuracyMeters));insert.Parameters.AddWithValue("@locationCapturedAt",Db(item.LocationCapturedAt));insert.Parameters.AddWithValue("@locationSource",Db(item.Latitude is null?null:"DEVICE_GPS"));await insert.ExecuteNonQueryAsync(ct);}
            var resolutionMessage=$"El ticket fue {(status=="CLOSED"?"cerrado":"resuelto")} por {actorName}.";
            await AddTicketHistory(connection,transaction,id,status,state.Department,state.Department,state.Team,state.Team,userId,actorName,resolution,ct);await AddTicketNotifications(connection,transaction,id,status,null,state.Department,state.AgencyGroup,userId,actorName,resolutionMessage,true,ct);
            if(state.Department=="TECHNOLOGY")await AddTechnologyResolutionNotifications(connection,transaction,id,status,userId,actorName,resolutionMessage,ct);
            await transaction.CommitAsync(ct);return new(true,"OK",null);
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<(byte[] Data,string Mime,string FileName)?> TicketEvidencePhoto(Guid evidenceId,string userId,string role,bool canAssignTickets,string? supportTeam,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureTicketWorkflowSchema(connection,ct);const string sql="""
            SELECT TOP(1)e.photo_data,e.content_type,e.file_name
            FROM dbo.support_ticket_evidence e
            INNER JOIN dbo.support_tickets t ON t.id=e.ticket_id INNER JOIN dbo.agencies a ON a.id=t.agency_id
            OUTER APPLY (SELECT CASE WHEN
                t.assigned_technician_id=@userId
                OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))
                OR EXISTS(SELECT 1 FROM dbo.admin_users actor_account WHERE CONVERT(nvarchar(80),actor_account.id)=@userId AND LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LTRIM(RTRIM(actor_account.email))))
                THEN 1 ELSE 0 END AS is_current) assignment
            WHERE e.id=@evidence AND (
                @administrator=1
                OR (@groupSupervisor=1 AND (
                    assignment.is_current=1
                    OR (t.created_by_user_id=@userId AND EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo))
                ))
                OR (@department<>'' AND (
                    (@supportTeam='CALL_CENTER' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER')
                    OR (@supportTeam<>'CALL_CENTER' AND (
                        assignment.is_current=1
                        OR (@canAssign=1 AND (
                            (t.assigned_department=@department AND (@supportTeam='' OR t.assigned_team IS NULL OR t.assigned_team=@supportTeam))
                            OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares s WHERE s.ticket_id=t.id AND s.department_code=@department)
                        ))
                    ))
                ))
            );
            """;await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@evidence",evidenceId);command.Parameters.AddWithValue("@administrator",role=="Administrator"?1:0);command.Parameters.AddWithValue("@groupSupervisor",role=="GroupAdministrator"?1:0);command.Parameters.AddWithValue("@department",DepartmentForRole(role));command.Parameters.AddWithValue("@supportTeam",supportTeam??"");command.Parameters.AddWithValue("@canAssign",canAssignTickets?1:0);command.Parameters.AddWithValue("@userId",userId);await using var reader=await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess,ct);if(!await reader.ReadAsync(ct))return null;return((byte[])reader[0],reader.GetString(1),reader.GetString(2));
    }

    public async Task<List<SupportTechnicianRow>> SupportTechnicians(CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureUserPermissionSchema(connection,ct);const string sql="""
            SELECT CONVERT(nvarchar(80),u.id),u.display_name,
                   CASE u.role WHEN 'TECHNOLOGY' THEN 'TECHNOLOGY' WHEN 'GENERAL_SERVICES' THEN 'GENERAL_SERVICES' WHEN 'HUMAN_RESOURCES' THEN 'HUMAN_RESOURCES' ELSE '' END,
                   u.role,COALESCE(g.names,N''),u.support_team,
                   CASE WHEN u.role<>'GROUP_ADMIN' AND (
                       u.support_team='TECHNICIANS' OR (ISNULL(u.support_team,'') NOT IN('WAREHOUSE','WORKSHOP') AND
                           JSON_VALUE(u.permissions_json,'$.canResolveTickets')='true'
                           AND ISNULL(JSON_VALUE(u.permissions_json,'$.canAssignTickets'),'false')<>'true'
                       )
                   ) THEN 1 ELSE 0 END
            FROM dbo.admin_users u
            OUTER APPLY(SELECT STRING_AGG(ug.group_name,N'|') names FROM dbo.admin_user_groups ug WHERE ug.user_id=u.id)g
            WHERE u.is_active=1 AND u.role IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','GROUP_ADMIN')
            ORDER BY CASE WHEN u.role='GROUP_ADMIN' THEN 2 ELSE 1 END,u.display_name;
            """;
        await using var command=new SqlCommand(sql,connection);await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<SupportTechnicianRow>();
        while(await reader.ReadAsync(ct)){var databaseRole=reader.GetString(3);rows.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),ClaimRole(databaseRole),reader.GetString(4).Split('|',StringSplitOptions.RemoveEmptyEntries).ToList(),databaseRole=="GROUP_ADMIN",reader.IsDBNull(5)?null:reader.GetString(5),reader.GetInt32(6)==1));}return rows;
    }

    public async Task<bool> DeleteTicket(Guid id,CancellationToken ct)
    {
        await using var connection=await Open(ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        await using(var audit=new SqlCommand("INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data) SELECT 'support_ticket',CONVERT(char(36),id),'TICKET_DELETED',CONCAT(N'{\"ticketNumber\":',ticket_number,N',\"subject\":\"',STRING_ESCAPE(subject,'json'),N'\"}') FROM dbo.support_tickets WHERE id=@id;",connection,transaction)){audit.Parameters.AddWithValue("@id",id);await audit.ExecuteNonQueryAsync(ct);}
        await using var command=new SqlCommand("DELETE FROM dbo.support_tickets WHERE id=@id;",connection,transaction);command.Parameters.AddWithValue("@id",id);var deleted=await command.ExecuteNonQueryAsync(ct)==1;
        if(deleted)await transaction.CommitAsync(ct);else await transaction.RollbackAsync(ct);return deleted;
    }

    public async Task<int> TicketAlerts(string userId, string role, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            SELECT COUNT(*) FROM dbo.support_tickets t INNER JOIN dbo.agencies a ON a.id=t.agency_id
            WHERE t.status IN ('OPEN','IN_PROGRESS','PENDING')
              AND (@department='' OR t.assigned_department=@department)
              AND (@ownOnly=0 OR t.created_by_user_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)))
              AND (@scoped=0 OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)) OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo));
            """;
        await using var command = new SqlCommand(sql, connection); AddScope(command,userId,role);
        command.Parameters.AddWithValue("@department",DepartmentForRole(role));
        command.Parameters.AddWithValue("@ownOnly", role == "GroupAdministrator" ? 1 : 0);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    public async Task<List<TicketNotificationRow>> ResolvedTicketNotifications(string userId, string role, CancellationToken ct)
    {
        if (role is not ("GroupAdministrator" or "Administrator" or "Technology" or "GeneralServices" or "HumanResources")) return [];
        await using var connection = await Open(ct);
        const string sql = """
            SELECT TOP (100) t.id,t.ticket_number,a.codigo,a.terminal,a.grupo,t.assigned_department,
                   t.subject,t.resolution,t.status,t.updated_at
            FROM dbo.support_tickets t
            INNER JOIN dbo.agencies a ON a.id=t.agency_id
            WHERE (@allProcesses=1 OR @supportDepartment<>'' OR t.created_by_user_id=@userId OR (@supervisor=1 AND LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))))
              AND (@allProcesses=1
                   OR (@supportDepartment<>'' AND t.assigned_department=@supportDepartment AND t.status='OPEN')
                   OR (@supervisor=1 AND t.status IN ('IN_PROGRESS','PENDING','RESOLVED','CLOSED','CANCELLED')))
              AND (@allProcesses=1 OR @supportDepartment<>'' OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ))
            ORDER BY t.updated_at DESC,t.ticket_number DESC;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@allProcesses", role == "Administrator" ? 1 : 0);
        command.Parameters.AddWithValue("@supervisor", role == "GroupAdministrator" ? 1 : 0);
        command.Parameters.AddWithValue("@supportDepartment", DepartmentForRole(role));
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<TicketNotificationRow>();
        while (await reader.ReadAsync(ct)) rows.Add(new(
            reader.GetGuid(0),reader.GetInt64(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),
            reader.GetString(5),reader.GetString(6),reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetString(8),reader.GetDateTime(9)));
        return rows;
    }

    public async Task<int> TicketAlerts(string userId,string role,bool canAssignTickets,string? supportTeam,string? userEmail,CancellationToken ct)
        =>(await Tickets(userId,role,canAssignTickets,supportTeam,userEmail,ct)).Count(ticket=>ticket.Status is "OPEN" or "IN_PROGRESS" or "PENDING");

    public async Task<List<TicketNotificationRow>> ResolvedTicketNotifications(string userId,string role,bool canAssignTickets,string? supportTeam,CancellationToken ct)
    {
        if(role is not ("GroupAdministrator" or "Administrator" or "Viewer" or "Fiscalizador" or "Technology" or "GeneralServices" or "HumanResources"))return[];
        await using var connection=await Open(ct);await EnsureTicketWorkflowSchema(connection,ct);await EnsureNotificationReceiptSchema(connection,ct);const string sql="""
            DECLARE @firstNotificationLoad bit=CASE WHEN EXISTS(SELECT 1 FROM dbo.support_notification_receipts WHERE user_id=@userId AND notification_key=N'SYSTEM:TICKET_INITIALIZED') THEN 0 ELSE 1 END;
            MERGE dbo.support_notification_receipts WITH(HOLDLOCK) AS target USING(SELECT @userId user_id,N'SYSTEM:TICKET_INITIALIZED' notification_key) source ON target.user_id=source.user_id AND target.notification_key=source.notification_key WHEN NOT MATCHED THEN INSERT(user_id,notification_key,read_at) VALUES(source.user_id,source.notification_key,SYSUTCDATETIME());
            SELECT TOP(100)t.id,t.ticket_number,a.codigo,a.terminal,a.grupo,t.assigned_department,t.subject,t.resolution,t.status,
                   n.created_at,n.event_type,n.message,n.actor_name,n.id,ISNULL(t.ticket_type,'SUPPORT'),CONVERT(bit,CASE WHEN @firstNotificationLoad=1 OR receipt.read_at IS NOT NULL THEN 1 ELSE 0 END)
            FROM dbo.support_ticket_notifications n
            INNER JOIN dbo.support_tickets t ON t.id=n.ticket_id INNER JOIN dbo.agencies a ON a.id=t.agency_id
            LEFT JOIN dbo.support_notification_receipts receipt ON receipt.user_id=@userId AND receipt.notification_key=CONCAT(N'TICKET_EVENT:',CONVERT(nvarchar(36),n.id))
            WHERE (n.recipient_user_id=@userId
               OR (@administrator=1 AND (n.recipient_user_id IS NULL OR n.event_type='ASSIGNED'))
               OR (@groupSupervisor=1 AND n.recipient_user_id=@userId)
               OR (@department<>'' AND (n.recipient_user_id=@userId OR (@supportTeam<>'TECHNICIANS' AND @canAssign=1 AND n.target_department=@department))))
              AND (@supportTeam='' OR n.recipient_user_id=@userId OR (
                    (@supportTeam='CALL_CENTER' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER')
                    OR (@supportTeam<>'CALL_CENTER' AND (
                        (t.assigned_department='TECHNOLOGY' AND (t.assigned_team IS NULL OR t.assigned_team=@supportTeam))
                        OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares shared WHERE shared.ticket_id=t.id AND shared.department_code='TECHNOLOGY')
                    ))
              )) AND receipt.dismissed_at IS NULL
            ORDER BY n.created_at DESC,n.id DESC;
            """;await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@administrator",role=="Administrator"?1:0);command.Parameters.AddWithValue("@groupSupervisor",role=="GroupAdministrator"?1:0);command.Parameters.AddWithValue("@department",DepartmentForRole(role));command.Parameters.AddWithValue("@canAssign",canAssignTickets?1:0);command.Parameters.AddWithValue("@supportTeam",supportTeam??"");command.Parameters.AddWithValue("@userId",userId);await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<TicketNotificationRow>();while(await reader.ReadAsync(ct))rows.Add(new(reader.GetGuid(0),reader.GetInt64(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.IsDBNull(7)?null:reader.GetString(7),reader.GetString(8),reader.GetDateTime(9),reader.GetString(10),reader.IsDBNull(11)?null:reader.GetString(11),reader.IsDBNull(12)?null:reader.GetString(12),reader.GetGuid(13),reader.GetString(14),reader.GetBoolean(15)));return rows;
    }

    public async Task SavePushSubscription(string userId,string endpoint,string p256dh,string auth,CancellationToken ct)
    {
        await using var connection=await Open(ct);
        const string sql="""
        IF OBJECT_ID(N'dbo.pwa_push_subscriptions',N'U') IS NULL CREATE TABLE dbo.pwa_push_subscriptions(id uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,user_id nvarchar(80) NOT NULL,endpoint nvarchar(2048) NOT NULL UNIQUE,p256dh nvarchar(512) NOT NULL,auth_key nvarchar(512) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
        MERGE dbo.pwa_push_subscriptions AS t USING(SELECT @endpoint endpoint) s ON t.endpoint=s.endpoint WHEN MATCHED THEN UPDATE SET user_id=@userId,p256dh=@p256dh,auth_key=@auth,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(user_id,endpoint,p256dh,auth_key) VALUES(@userId,@endpoint,@p256dh,@auth);
        """;
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@endpoint",endpoint);command.Parameters.AddWithValue("@p256dh",p256dh);command.Parameters.AddWithValue("@auth",auth);await command.ExecuteNonQueryAsync(ct);
    }
    public async Task<List<(string Endpoint,string P256dh,string Auth)>> PushSubscriptions(string userId,CancellationToken ct){await using var connection=await Open(ct);await using var command=new SqlCommand("SELECT endpoint,p256dh,auth_key FROM dbo.pwa_push_subscriptions WHERE user_id=@userId;",connection);command.Parameters.AddWithValue("@userId",userId);await using var reader=await command.ExecuteReaderAsync(ct);var result=new List<(string,string,string)>();while(await reader.ReadAsync(ct))result.Add((reader.GetString(0),reader.GetString(1),reader.GetString(2)));return result;}

    public async Task<List<PushDeliveryRow>> PendingPushDeliveries(CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureNotificationReceiptSchema(connection,ct);
        const string sql="""
        IF OBJECT_ID(N'dbo.pwa_push_subscriptions',N'U') IS NULL CREATE TABLE dbo.pwa_push_subscriptions(id uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,user_id nvarchar(80) NOT NULL,endpoint nvarchar(2048) NOT NULL UNIQUE,p256dh nvarchar(512) NOT NULL,auth_key nvarchar(512) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
        IF OBJECT_ID(N'dbo.pwa_push_deliveries',N'U') IS NULL CREATE TABLE dbo.pwa_push_deliveries(notification_id uniqueidentifier NOT NULL,endpoint nvarchar(2048) NOT NULL,delivered_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CONSTRAINT PK_pwa_push_deliveries PRIMARY KEY(notification_id,endpoint));
        SELECT TOP(100)n.id,s.endpoint,s.p256dh,s.auth_key,t.ticket_number,t.subject,n.message,t.id,t.assigned_department,ISNULL(t.ticket_type,'SUPPORT'),t.status,n.created_at
        FROM dbo.support_ticket_notifications n
        INNER JOIN dbo.support_tickets t ON t.id=n.ticket_id
        INNER JOIN dbo.pwa_push_subscriptions s ON 1=1
        INNER JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=s.user_id AND u.is_active=1
        WHERE (n.recipient_user_id=s.user_id OR (n.recipient_user_id IS NULL AND n.target_department IS NOT NULL
          AND u.role=CASE n.target_department WHEN 'TECHNOLOGY' THEN 'TECHNOLOGY' WHEN 'GENERAL_SERVICES' THEN 'GENERAL_SERVICES' WHEN 'HUMAN_RESOURCES' THEN 'HUMAN_RESOURCES' ELSE '' END
          AND (n.target_department<>'TECHNOLOGY' OR u.support_team IS NULL OR u.support_team=t.assigned_team)
          AND (ISNULL(u.support_team,'')<>'TECHNICIANS' OR t.assigned_technician_id=s.user_id)))
          AND NOT EXISTS(SELECT 1 FROM dbo.pwa_push_deliveries d WHERE d.notification_id=n.id AND d.endpoint=s.endpoint)
          AND n.created_at>=s.created_at
          AND NOT EXISTS(SELECT 1 FROM dbo.support_notification_receipts receipt WHERE receipt.user_id=s.user_id AND receipt.notification_key=CONCAT(N'TICKET_EVENT:',CONVERT(nvarchar(36),n.id)) AND (receipt.read_at IS NOT NULL OR receipt.dismissed_at IS NOT NULL))
        ORDER BY n.created_at;
        """;
        await using var command=new SqlCommand(sql,connection);await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<PushDeliveryRow>();while(await reader.ReadAsync(ct))rows.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetInt64(4),reader.GetString(5),reader.IsDBNull(6)?null:reader.GetString(6),reader.GetGuid(7),reader.GetString(8),reader.GetString(9),reader.GetString(10),reader.GetDateTime(11)));return rows;
    }

    public async Task<List<ChatPushDeliveryRow>> PendingChatPushDeliveries(CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);await EnsureNotificationReceiptSchema(connection,ct);
        const string sql="""
        IF OBJECT_ID(N'dbo.pwa_push_subscriptions',N'U') IS NULL CREATE TABLE dbo.pwa_push_subscriptions(id uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,user_id nvarchar(80) NOT NULL,endpoint nvarchar(2048) NOT NULL UNIQUE,p256dh nvarchar(512) NOT NULL,auth_key nvarchar(512) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
        IF OBJECT_ID(N'dbo.pwa_push_deliveries',N'U') IS NULL CREATE TABLE dbo.pwa_push_deliveries(notification_id uniqueidentifier NOT NULL,endpoint nvarchar(2048) NOT NULL,delivered_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CONSTRAINT PK_pwa_push_deliveries PRIMARY KEY(notification_id,endpoint));
        IF OBJECT_ID(N'dbo.pwa_push_chat_state',N'U') IS NULL CREATE TABLE dbo.pwa_push_chat_state(endpoint nvarchar(2048) NOT NULL PRIMARY KEY,initialized_at datetime2 NOT NULL);
        INSERT INTO dbo.pwa_push_chat_state(endpoint,initialized_at)
        SELECT s.endpoint,DATEADD(SECOND,-30,SYSUTCDATETIME()) FROM dbo.pwa_push_subscriptions s WHERE NOT EXISTS(SELECT 1 FROM dbo.pwa_push_chat_state state WHERE state.endpoint=s.endpoint);
        SELECT TOP(100)m.id,s.endpoint,s.p256dh,s.auth_key,c.id,c.assigned_department,m.sender_name,m.message,m.created_at
        FROM dbo.support_messages m
        INNER JOIN dbo.support_conversations c ON c.id=m.conversation_id
        INNER JOIN dbo.pwa_push_subscriptions s ON 1=1
        INNER JOIN dbo.pwa_push_chat_state push_state ON push_state.endpoint=s.endpoint AND m.created_at>=push_state.initialized_at
        INNER JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=s.user_id AND u.is_active=1
        WHERE m.is_bot=0 AND m.deleted_at IS NULL AND c.deleted_at IS NULL
          AND ISNULL(m.sender_user_id,N'')<>s.user_id
          AND NOT EXISTS(SELECT 1 FROM dbo.support_conversation_mutes cm WHERE cm.conversation_id=c.id AND cm.user_id=s.user_id)
          AND ((c.category LIKE 'DIRECT:%' AND (u.role='ADMINISTRATOR' OR c.supervisor_user_id=s.user_id OR SUBSTRING(c.category,8,80)=s.user_id))
            OR (ISNULL(c.category,'') NOT LIKE 'DIRECT:%' AND (u.role='ADMINISTRATOR' OR (u.role='GROUP_ADMIN' AND c.supervisor_user_id=s.user_id)
              OR (c.assigned_department='TECHNOLOGY' AND u.role='TECHNOLOGY')
              OR (c.assigned_department='GENERAL_SERVICES' AND u.role='GENERAL_SERVICES')
              OR (c.assigned_department='HUMAN_RESOURCES' AND u.role='HUMAN_RESOURCES'))))
          AND NOT EXISTS(SELECT 1 FROM dbo.pwa_push_deliveries d WHERE d.notification_id=m.id AND d.endpoint=s.endpoint)
          AND m.created_at>=s.created_at
          AND NOT EXISTS(SELECT 1 FROM dbo.support_notification_receipts receipt WHERE receipt.user_id=s.user_id AND receipt.notification_key=CONCAT(N'CHAT_MESSAGE:',CONVERT(nvarchar(36),m.id)) AND (receipt.read_at IS NOT NULL OR receipt.dismissed_at IS NOT NULL))
        ORDER BY m.created_at;
        """;
        await using var command=new SqlCommand(sql,connection);await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<ChatPushDeliveryRow>();while(await reader.ReadAsync(ct))rows.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetGuid(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.GetDateTime(8)));return rows;
    }

    public async Task MarkPushDelivered(Guid notificationId,string endpoint,CancellationToken ct){await using var connection=await Open(ct);await using var command=new SqlCommand("IF OBJECT_ID(N'dbo.pwa_push_deliveries',N'U') IS NULL CREATE TABLE dbo.pwa_push_deliveries(notification_id uniqueidentifier NOT NULL,endpoint nvarchar(2048) NOT NULL,delivered_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CONSTRAINT PK_pwa_push_deliveries PRIMARY KEY(notification_id,endpoint)); IF NOT EXISTS(SELECT 1 FROM dbo.pwa_push_deliveries WHERE notification_id=@id AND endpoint=@endpoint) INSERT INTO dbo.pwa_push_deliveries(notification_id,endpoint) VALUES(@id,@endpoint);",connection);command.Parameters.AddWithValue("@id",notificationId);command.Parameters.AddWithValue("@endpoint",endpoint);await command.ExecuteNonQueryAsync(ct);}

    public async Task DeletePushSubscription(string endpoint,CancellationToken ct){await using var connection=await Open(ct);await using var command=new SqlCommand("DELETE FROM dbo.pwa_push_subscriptions WHERE endpoint=@endpoint; IF OBJECT_ID(N'dbo.pwa_push_chat_state',N'U') IS NOT NULL DELETE FROM dbo.pwa_push_chat_state WHERE endpoint=@endpoint;",connection);command.Parameters.AddWithValue("@endpoint",endpoint);await command.ExecuteNonQueryAsync(ct);}

    public async Task<TicketStatisticsResult> TicketStatistics(string userId,string role,bool canAssignTickets,string? supportTeam,DateTime? from,DateTime? to,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureTicketWorkflowSchema(connection,ct);var fromDate=(from??DateTime.UtcNow.AddMonths(-12)).Date;var toDate=(to??DateTime.UtcNow).Date.AddDays(1);if(toDate<=fromDate)toDate=fromDate.AddDays(1);if((toDate-fromDate).TotalDays>1096)fromDate=toDate.AddYears(-3);
        const string sql="""
            DECLARE @visible TABLE(id uniqueidentifier NOT NULL PRIMARY KEY);
            INSERT INTO @visible(id)
            SELECT t.id FROM dbo.support_tickets t INNER JOIN dbo.agencies a ON a.id=t.agency_id
            WHERE t.created_at<@to AND (
                @administrator=1
                OR (@groupSupervisor=1 AND (
                    LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))
                    OR (t.created_by_user_id=@userId AND EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo))
                ))
                    OR (@department<>'' AND (LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)) OR (@supportTeam<>'TECHNICIANS' AND @canAssign=1 AND (
                    (@supportTeam='CALL_CENTER' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER')
                    OR (@supportTeam='TECHNICAL_FAILURE' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='TECHNICAL_FAILURE')
                    OR (@supportTeam NOT IN('CALL_CENTER','TECHNICAL_FAILURE') AND (
                        (t.assigned_department=@department AND (@supportTeam='' OR t.assigned_team=@supportTeam))
                        OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares s WHERE s.ticket_id=t.id AND s.department_code=@department)
                    ))
                ))))
            );
            SELECT SUM(CASE WHEN t.status IN('OPEN','IN_PROGRESS','PENDING') THEN 1 ELSE 0 END),SUM(CASE WHEN t.status IN('RESOLVED','CLOSED') AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)>=@from THEN 1 ELSE 0 END)
            FROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id;
            SELECT COALESCE(t.resolved_by_user_id,t.assigned_technician_id),COALESCE(NULLIF(t.resolved_by_name,N''),u.display_name,N'Sin técnico'),t.assigned_department,
                   SUM(CASE WHEN t.status='RESOLVED' THEN 1 ELSE 0 END),SUM(CASE WHEN t.status='CLOSED' THEN 1 ELSE 0 END),COUNT(*),
                   CAST(AVG(CAST(DATEDIFF(MINUTE,t.created_at,COALESCE(t.resolved_at,t.closed_at,t.updated_at)) AS float))/60.0 AS float)
            FROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=COALESCE(t.resolved_by_user_id,t.assigned_technician_id)
            WHERE t.status IN('RESOLVED','CLOSED') AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)>=@from AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)<@to
            GROUP BY COALESCE(t.resolved_by_user_id,t.assigned_technician_id),COALESCE(NULLIF(t.resolved_by_name,N''),u.display_name,N'Sin técnico'),t.assigned_department ORDER BY COUNT(*) DESC;
            SELECT t.assigned_department,t.category,SUM(CASE WHEN t.status='RESOLVED' THEN 1 ELSE 0 END),SUM(CASE WHEN t.status='CLOSED' THEN 1 ELSE 0 END),COUNT(*),
                   CAST(AVG(CAST(DATEDIFF(MINUTE,t.created_at,COALESCE(t.resolved_at,t.closed_at,t.updated_at)) AS float))/60.0 AS float)
            FROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id
            WHERE t.status IN('RESOLVED','CLOSED') AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)>=@from AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)<@to
            GROUP BY t.assigned_department,t.category ORDER BY COUNT(*) DESC;
            SELECT t.assigned_department,SUM(CASE WHEN t.status='OPEN' THEN 1 ELSE 0 END),SUM(CASE WHEN t.status IN('IN_PROGRESS','PENDING') THEN 1 ELSE 0 END),SUM(CASE WHEN t.status='RESOLVED' THEN 1 ELSE 0 END),SUM(CASE WHEN t.status='CLOSED' THEN 1 ELSE 0 END),COUNT(*),
                   COALESCE(CAST(AVG(CASE WHEN t.status IN('RESOLVED','CLOSED') THEN CAST(DATEDIFF(MINUTE,t.created_at,COALESCE(t.resolved_at,t.closed_at,t.updated_at)) AS float) END)/60.0 AS float),0)
            FROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id GROUP BY t.assigned_department ORDER BY t.assigned_department;
            SELECT CONVERT(date,COALESCE(t.resolved_at,t.closed_at,t.updated_at)),t.assigned_department,COUNT(*)
            FROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id
            WHERE t.status IN('RESOLVED','CLOSED') AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)>=@from AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)<@to
            GROUP BY CONVERT(date,COALESCE(t.resolved_at,t.closed_at,t.updated_at)),t.assigned_department ORDER BY 1,2;
            """;
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@administrator",role=="Administrator"?1:0);command.Parameters.AddWithValue("@groupSupervisor",role=="GroupAdministrator"?1:0);command.Parameters.AddWithValue("@department",DepartmentForRole(role));command.Parameters.AddWithValue("@canAssign",canAssignTickets?1:0);command.Parameters.AddWithValue("@supportTeam",supportTeam??"");command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@from",fromDate);command.Parameters.AddWithValue("@to",toDate);await using var reader=await command.ExecuteReaderAsync(ct);var active=0;var resolved=0;if(await reader.ReadAsync(ct)){active=reader.IsDBNull(0)?0:reader.GetInt32(0);resolved=reader.IsDBNull(1)?0:reader.GetInt32(1);}var technicians=new List<TicketTechnicianStatistic>();var categories=new List<TicketCategoryStatistic>();var departments=new List<TicketDepartmentStatistic>();var history=new List<TicketHistoryPoint>();
        if(await reader.NextResultAsync(ct))while(await reader.ReadAsync(ct))technicians.Add(new(reader.IsDBNull(0)?null:reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetInt32(3),reader.GetInt32(4),reader.GetInt32(5),reader.IsDBNull(6)?0:Convert.ToDouble(reader.GetValue(6),System.Globalization.CultureInfo.InvariantCulture)));
        if(await reader.NextResultAsync(ct))while(await reader.ReadAsync(ct))categories.Add(new(reader.GetString(0),reader.GetString(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetInt32(4),reader.IsDBNull(5)?0:Convert.ToDouble(reader.GetValue(5),System.Globalization.CultureInfo.InvariantCulture)));
        if(await reader.NextResultAsync(ct))while(await reader.ReadAsync(ct))departments.Add(new(reader.GetString(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetInt32(4),reader.GetInt32(5),reader.IsDBNull(6)?0:Convert.ToDouble(reader.GetValue(6),System.Globalization.CultureInfo.InvariantCulture)));
        if(await reader.NextResultAsync(ct))while(await reader.ReadAsync(ct))history.Add(new(reader.GetDateTime(0),reader.GetString(1),reader.GetInt32(2)));return new(fromDate,toDate.AddDays(-1),active,resolved,technicians,categories,departments,history);
    }

    public async Task<List<SupportContactRow>> SupportContacts(string userId,CancellationToken ct)
    {
        await using var connection=await Open(ct);
        const string sql="""
            SELECT u.id,u.display_name,u.email,u.role,u.contact,
                   CASE WHEN u.avatar_data IS NULL THEN 0 ELSE 1 END,
                   COALESCE(g.names,N'')
            FROM dbo.admin_users u
            OUTER APPLY(SELECT STRING_AGG(ug.group_name,N'|') names FROM dbo.admin_user_groups ug WHERE ug.user_id=u.id)g
            WHERE u.is_active=1 AND CONVERT(nvarchar(80),u.id)<>@userId
              AND u.role IN('ADMINISTRATOR','GROUP_ADMIN','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')
            ORDER BY CASE u.role WHEN 'GROUP_ADMIN' THEN 1 ELSE 2 END,u.display_name;
            """;
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@userId",userId);
        await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<SupportContactRow>();
        while(await reader.ReadAsync(ct)){var id=reader.GetString(0);rows.Add(new(id,reader.GetString(1),reader.GetString(2),ClaimRole(reader.GetString(3)),reader.IsDBNull(4)?null:reader.GetString(4),ReadBooleanValue(reader,5)?$"/api/account/avatar/{id}":null,reader.GetString(6).Split('|',StringSplitOptions.RemoveEmptyEntries).ToList()));}
        return rows;
    }

    public async Task<List<SupportConversationRow>> SupportConversations(string userId,string role,CancellationToken ct)
    {
        if (role is not ("Administrator" or "GroupAdministrator" or "Technology" or "GeneralServices" or "HumanResources")) return [];
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);
        const string sql="""
            SELECT c.id,peer.contact_user_id,
                   COALESCE(u.display_name,c.supervisor_name),COALESCE(u.email,c.supervisor_username),u.contact,
                   CASE WHEN u.avatar_data IS NULL THEN 0 ELSE 1 END,COALESCE(user_groups.names,N''),
                   c.assigned_department,c.category,c.status,c.requested_agent,c.created_at,c.updated_at,c.last_message_at,COALESCE(last_message.message,N''),
                   CASE WHEN EXISTS(SELECT 1 FROM dbo.support_conversation_mutes cm WHERE cm.conversation_id=c.id AND cm.user_id=@userId) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                   c.is_restricted,c.is_blocked
            FROM dbo.support_conversations c
            OUTER APPLY(SELECT CASE WHEN c.category LIKE 'DIRECT:%' AND c.supervisor_user_id=@userId THEN SUBSTRING(c.category,8,80) ELSE c.supervisor_user_id END contact_user_id)peer
            LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=peer.contact_user_id
            OUTER APPLY(SELECT TOP(1)m.message FROM dbo.support_messages m WHERE m.conversation_id=c.id AND m.deleted_at IS NULL ORDER BY m.created_at DESC,m.id DESC)last_message
            OUTER APPLY(SELECT STRING_AGG(ug.group_name,N'|') names FROM dbo.admin_user_groups ug WHERE CONVERT(nvarchar(80),ug.user_id)=peer.contact_user_id)user_groups
            WHERE c.deleted_at IS NULL AND (
                (c.category LIKE 'DIRECT:%' AND (@admin=1 OR c.supervisor_user_id=@userId OR SUBSTRING(c.category,8,80)=@userId))
                OR
                (ISNULL(c.category,'') NOT LIKE 'DIRECT:%' AND (@all=1 OR (@own=1 AND c.supervisor_user_id=@userId) OR (@department<>'' AND c.assigned_department=@department)))
            )
            ORDER BY CASE c.status WHEN 'WAITING_SUPPORT' THEN 1 WHEN 'IN_PROGRESS' THEN 2 WHEN 'RESOLVED' THEN 3 ELSE 4 END,c.last_message_at DESC;
            """;
        await using var command=new SqlCommand(sql,connection);
        command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);command.Parameters.AddWithValue("@all",role=="Administrator"?1:0);command.Parameters.AddWithValue("@own",role=="GroupAdministrator"?1:0);
        command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@department",DepartmentForRole(role));
        await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<SupportConversationRow>();
        while(await reader.ReadAsync(ct)){var supervisorId=reader.GetString(1);rows.Add(new(reader.GetGuid(0),supervisorId,reader.GetString(2),reader.GetString(3),reader.IsDBNull(4)?null:reader.GetString(4),ReadBooleanValue(reader,5)?$"/api/account/avatar/{supervisorId}":null,reader.GetString(6).Split('|',StringSplitOptions.RemoveEmptyEntries).ToList(),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetString(8),reader.GetString(9),ReadBooleanValue(reader,10),reader.GetDateTime(11),reader.GetDateTime(12),reader.GetDateTime(13),reader.GetString(14),ReadBooleanValue(reader,15),ReadBooleanValue(reader,16),ReadBooleanValue(reader,17)));}
        return rows;
    }

    public async Task<List<SupportMessageRow>> SupportMessages(Guid conversationId,string userId,string role,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);
        if(!await CanAccessSupportConversation(connection,conversationId,userId,role,ct))return [];
        const string sql="""SELECT m.id,m.conversation_id,m.sender_user_id,m.sender_name,m.sender_role,CASE WHEN u.avatar_data IS NULL THEN 0 ELSE 1 END,CASE WHEN m.deleted_at IS NULL THEN m.message ELSE N'Mensaje eliminado' END,m.is_bot,m.created_at,CASE WHEN v.message_id IS NULL OR m.deleted_at IS NOT NULL THEN 0 ELSE 1 END,CONVERT(bit,CASE WHEN m.deleted_at IS NULL THEN 0 ELSE 1 END) FROM dbo.support_messages m LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=m.sender_user_id LEFT JOIN dbo.support_voice_notes v ON v.message_id=m.id WHERE m.conversation_id=@id ORDER BY m.created_at,m.id;""";
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@id",conversationId);
        await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<SupportMessageRow>();
        while(await reader.ReadAsync(ct)){var senderId=reader.IsDBNull(2)?null:reader.GetString(2);var messageId=reader.GetGuid(0);rows.Add(new(messageId,reader.GetGuid(1),senderId,reader.GetString(3),reader.GetString(4),senderId is not null&&ReadBooleanValue(reader,5)?$"/api/account/avatar/{senderId}":null,reader.GetString(6),ReadBooleanValue(reader,7),reader.GetDateTime(8),ReadBooleanValue(reader,9)?$"/api/support/chat/voice/{messageId}":null,ReadBooleanValue(reader,10)));}
        return rows;
    }

    public async Task<List<SupportChatNotificationRow>> SupportChatNotifications(string userId,string role,CancellationToken ct)
    {
        if (role is not ("Administrator" or "GroupAdministrator" or "Technology" or "GeneralServices" or "HumanResources")) return [];
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);await EnsureNotificationReceiptSchema(connection,ct);
        const string sql="""
            DECLARE @firstNotificationLoad bit=CASE WHEN EXISTS(SELECT 1 FROM dbo.support_notification_receipts WHERE user_id=@userId AND notification_key=N'SYSTEM:CHAT_INITIALIZED') THEN 0 ELSE 1 END;
            MERGE dbo.support_notification_receipts WITH(HOLDLOCK) AS target USING(SELECT @userId user_id,N'SYSTEM:CHAT_INITIALIZED' notification_key) source ON target.user_id=source.user_id AND target.notification_key=source.notification_key WHEN NOT MATCHED THEN INSERT(user_id,notification_key,read_at) VALUES(source.user_id,source.notification_key,SYSUTCDATETIME());
            SELECT TOP (100) m.id,c.id,c.assigned_department,c.supervisor_name,m.sender_name,m.sender_role,m.message,m.created_at,CONVERT(bit,CASE WHEN @firstNotificationLoad=1 OR receipt.read_at IS NOT NULL THEN 1 ELSE 0 END)
            FROM dbo.support_messages m
            INNER JOIN dbo.support_conversations c ON c.id=m.conversation_id
            LEFT JOIN dbo.support_notification_receipts receipt ON receipt.user_id=@userId AND receipt.notification_key=CONCAT(N'CHAT_MESSAGE:',CONVERT(nvarchar(36),m.id))
            WHERE m.is_bot=0
              AND m.deleted_at IS NULL
              AND ISNULL(m.sender_user_id,N'')<>@userId
              AND c.deleted_at IS NULL
              AND receipt.dismissed_at IS NULL
              AND NOT EXISTS(SELECT 1 FROM dbo.support_conversation_mutes cm WHERE cm.conversation_id=c.id AND cm.user_id=@userId)
              AND ((c.category LIKE 'DIRECT:%' AND (@admin=1 OR c.supervisor_user_id=@userId OR SUBSTRING(c.category,8,80)=@userId))
                   OR (ISNULL(c.category,'') NOT LIKE 'DIRECT:%' AND (@all=1 OR (@own=1 AND c.supervisor_user_id=@userId) OR (@department<>'' AND c.assigned_department=@department))))
            ORDER BY m.created_at DESC,m.id DESC;
            """;
        await using var command=new SqlCommand(sql,connection);
        command.Parameters.AddWithValue("@userId",userId);
        command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);
        command.Parameters.AddWithValue("@all",role=="Administrator"?1:0);
        command.Parameters.AddWithValue("@own",role=="GroupAdministrator"?1:0);
        command.Parameters.AddWithValue("@department",DepartmentForRole(role));
        await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<SupportChatNotificationRow>();
        while(await reader.ReadAsync(ct))rows.Add(new(reader.GetGuid(0),reader.GetGuid(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetDateTime(7),reader.GetBoolean(8)));
        return rows;
    }

    public async Task SetNotificationState(string userId,IEnumerable<string> keys,bool dismiss,CancellationToken ct)
    {
        var safeKeys=keys.Where(key=>!string.IsNullOrWhiteSpace(key)&&key.Length<=100&&(key.StartsWith("TICKET_EVENT:",StringComparison.Ordinal)||key.StartsWith("CHAT_MESSAGE:",StringComparison.Ordinal))).Distinct(StringComparer.Ordinal).Take(200).ToList();
        if(safeKeys.Count==0)return;
        await using var connection=await Open(ct);await EnsureNotificationReceiptSchema(connection,ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            const string sql="""
                MERGE dbo.support_notification_receipts AS target
                USING(SELECT @userId user_id,@key notification_key) AS source
                ON target.user_id=source.user_id AND target.notification_key=source.notification_key
                WHEN MATCHED THEN UPDATE SET read_at=COALESCE(target.read_at,SYSUTCDATETIME()),dismissed_at=CASE WHEN @dismiss=1 THEN COALESCE(target.dismissed_at,SYSUTCDATETIME()) ELSE target.dismissed_at END,updated_at=SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT(user_id,notification_key,read_at,dismissed_at) VALUES(@userId,@key,SYSUTCDATETIME(),CASE WHEN @dismiss=1 THEN SYSUTCDATETIME() ELSE NULL END);
                """;
            foreach(var key in safeKeys){await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@key",key);command.Parameters.AddWithValue("@dismiss",dismiss?1:0);await command.ExecuteNonQueryAsync(ct);}
            await transaction.CommitAsync(ct);
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<SupportConversationRow?> CreateSupportConversation(SupportConversationCreate body,string userId,string role,string displayName,string username,CancellationToken ct)
    {
        if(role!="GroupAdministrator")return null;
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);
        string? departmentName=null,categoryName=null;
        const string catalogSql="""
            SELECT d.name,(SELECT TOP(1)c.name FROM dbo.support_categories c WHERE c.department_code=d.code AND c.code=@category AND c.is_active=1)
            FROM dbo.support_departments d WHERE d.code=@department AND d.is_active=1;
            """;
        await using(var catalog=new SqlCommand(catalogSql,connection)){catalog.Parameters.AddWithValue("@department",body.Department!);catalog.Parameters.AddWithValue("@category",Db(string.IsNullOrWhiteSpace(body.Category)?null:body.Category));await using var reader=await catalog.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))return null;departmentName=reader.GetString(0);categoryName=reader.IsDBNull(1)?null:reader.GetString(1);}
        var id=Guid.NewGuid();await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        try{
            const string insertConversation="INSERT INTO dbo.support_conversations(id,supervisor_user_id,supervisor_name,supervisor_username,assigned_department,category,requested_agent) VALUES(@id,@userId,@name,@username,@department,@category,@agent);";
            await using(var command=new SqlCommand(insertConversation,connection,transaction)){command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@name",displayName);command.Parameters.AddWithValue("@username",username);command.Parameters.AddWithValue("@department",body.Department!);command.Parameters.AddWithValue("@category",Db(string.IsNullOrWhiteSpace(body.Category)?null:body.Category));command.Parameters.AddWithValue("@agent",body.RequestedAgent);await command.ExecuteNonQueryAsync(ct);}
            var botMessage=body.RequestedAgent?$"Te comuniqué con un personal de {departmentName}. Describe la avería y el equipo continuará contigo por este chat.":$"Seleccionaste {categoryName??departmentName}. Describe qué está ocurriendo y enviaré la solicitud al departamento correspondiente.";
            await using(var message=new SqlCommand("INSERT INTO dbo.support_messages(conversation_id,sender_name,sender_role,message,is_bot) VALUES(@id,N'Asistente de soporte','BOT',@message,1);",connection,transaction)){message.Parameters.AddWithValue("@id",id);message.Parameters.AddWithValue("@message",botMessage);await message.ExecuteNonQueryAsync(ct);}
            await transaction.CommitAsync(ct);
        }catch{await transaction.RollbackAsync(ct);throw;}
        return (await SupportConversations(userId,role,ct)).FirstOrDefault(item=>item.Id==id);
    }

    public async Task<SupportConversationRow?> CreateDirectSupportConversation(string targetUserId,string userId,string role,string displayName,string username,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);
        string targetId,targetName,targetUsername,targetRole;
        await using(var target=new SqlCommand("SELECT id,display_name,email,role FROM dbo.admin_users WHERE CONVERT(nvarchar(80),id)=@target AND is_active=1 AND role IN('ADMINISTRATOR','GROUP_ADMIN','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES');",connection))
        {
            target.Parameters.AddWithValue("@target",targetUserId.Trim());await using var reader=await target.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))return null;
            targetId=reader.GetString(0);targetName=reader.GetString(1);targetUsername=reader.GetString(2);targetRole=ClaimRole(reader.GetString(3));
        }
        var category=$"DIRECT:{userId}";var department=DepartmentForRole(role);if(string.IsNullOrWhiteSpace(department))department=DepartmentForRole(targetRole);if(string.IsNullOrWhiteSpace(department))department="TECHNOLOGY";
        await using(var existing=new SqlCommand("SELECT TOP(1) id FROM dbo.support_conversations WHERE supervisor_user_id=@target AND category=@category AND deleted_at IS NULL ORDER BY updated_at DESC;",connection))
        {
            existing.Parameters.AddWithValue("@target",targetId);existing.Parameters.AddWithValue("@category",category);var found=await existing.ExecuteScalarAsync(ct);
            if(found is Guid existingId)return (await SupportConversations(userId,role,ct)).FirstOrDefault(item=>item.Id==existingId);
        }
        var id=Guid.NewGuid();await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            const string insert="INSERT INTO dbo.support_conversations(id,supervisor_user_id,supervisor_name,supervisor_username,assigned_department,category,requested_agent,status) VALUES(@id,@target,@name,@username,@department,@category,1,'IN_PROGRESS');";
            await using(var command=new SqlCommand(insert,connection,transaction)){command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@target",targetId);command.Parameters.AddWithValue("@name",targetName);command.Parameters.AddWithValue("@username",targetUsername);command.Parameters.AddWithValue("@department",department);command.Parameters.AddWithValue("@category",category);await command.ExecuteNonQueryAsync(ct);}
            await transaction.CommitAsync(ct);
        }
        catch{await transaction.RollbackAsync(ct);throw;}
        return (await SupportConversations(userId,role,ct)).FirstOrDefault(item=>item.Id==id);
    }

    public async Task<bool> AddSupportMessage(Guid conversationId,string message,string userId,string role,string displayName,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);
        if(!await CanAccessSupportConversation(connection,conversationId,userId,role,ct))return false;
        await using(var state=new SqlCommand("SELECT COUNT(*) FROM dbo.support_conversations WHERE id=@id AND deleted_at IS NULL AND is_blocked=0 AND (@supervisor=0 OR is_restricted=0);",connection)){state.Parameters.AddWithValue("@id",conversationId);state.Parameters.AddWithValue("@supervisor",role=="GroupAdministrator"?1:0);if(Convert.ToInt32(await state.ExecuteScalarAsync(ct))==0)return false;}
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        try{
            await using(var insert=new SqlCommand("INSERT INTO dbo.support_messages(conversation_id,sender_user_id,sender_name,sender_role,message,is_bot) VALUES(@id,@userId,@name,@role,@message,0);",connection,transaction)){insert.Parameters.AddWithValue("@id",conversationId);insert.Parameters.AddWithValue("@userId",userId);insert.Parameters.AddWithValue("@name",displayName);insert.Parameters.AddWithValue("@role",role);insert.Parameters.AddWithValue("@message",message);await insert.ExecuteNonQueryAsync(ct);}
            var status=role=="GroupAdministrator"?"WAITING_SUPPORT":"IN_PROGRESS";
            await using(var update=new SqlCommand("UPDATE dbo.support_conversations SET status=CASE WHEN status IN('RESOLVED','CLOSED') THEN status ELSE @status END,updated_at=SYSUTCDATETIME(),last_message_at=SYSUTCDATETIME() WHERE id=@id;",connection,transaction)){update.Parameters.AddWithValue("@id",conversationId);update.Parameters.AddWithValue("@status",status);await update.ExecuteNonQueryAsync(ct);}
            await transaction.CommitAsync(ct);return true;
        }catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<bool> AddSupportVoiceNote(Guid conversationId,byte[] audio,string contentType,string? fileName,int? durationSeconds,string userId,string role,string displayName,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);
        if(!await CanAccessSupportConversation(connection,conversationId,userId,role,ct))return false;
        await using(var state=new SqlCommand("SELECT COUNT(*) FROM dbo.support_conversations WHERE id=@id AND deleted_at IS NULL AND is_blocked=0 AND (@supervisor=0 OR is_restricted=0);",connection)){state.Parameters.AddWithValue("@id",conversationId);state.Parameters.AddWithValue("@supervisor",role=="GroupAdministrator"?1:0);if(Convert.ToInt32(await state.ExecuteScalarAsync(ct))==0)return false;}
        var messageId=Guid.NewGuid();await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        try{
            await using(var message=new SqlCommand("INSERT INTO dbo.support_messages(id,conversation_id,sender_user_id,sender_name,sender_role,message,is_bot) VALUES(@messageId,@id,@userId,@name,@role,N'Nota de voz',0);",connection,transaction)){message.Parameters.AddWithValue("@messageId",messageId);message.Parameters.AddWithValue("@id",conversationId);message.Parameters.AddWithValue("@userId",userId);message.Parameters.AddWithValue("@name",displayName);message.Parameters.AddWithValue("@role",role);await message.ExecuteNonQueryAsync(ct);}
            await using(var voice=new SqlCommand("INSERT INTO dbo.support_voice_notes(message_id,audio_data,content_type,file_name,duration_seconds) VALUES(@messageId,@audio,@contentType,@fileName,@duration);",connection,transaction)){voice.Parameters.AddWithValue("@messageId",messageId);voice.Parameters.Add("@audio",System.Data.SqlDbType.VarBinary,-1).Value=audio;voice.Parameters.AddWithValue("@contentType",contentType);voice.Parameters.AddWithValue("@fileName",Db(fileName));voice.Parameters.AddWithValue("@duration",Db(durationSeconds));await voice.ExecuteNonQueryAsync(ct);}
            await using(var update=new SqlCommand("UPDATE dbo.support_conversations SET updated_at=SYSUTCDATETIME(),last_message_at=SYSUTCDATETIME() WHERE id=@id;",connection,transaction)){update.Parameters.AddWithValue("@id",conversationId);await update.ExecuteNonQueryAsync(ct);}
            await transaction.CommitAsync(ct);return true;
        }catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<bool> DeleteSupportMessage(Guid messageId,string userId,string role,string actorName,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);
        Guid conversationId;
        await using(var find=new SqlCommand("SELECT conversation_id FROM dbo.support_messages WHERE id=@id AND deleted_at IS NULL AND is_bot=0 AND (sender_user_id=@userId OR @admin=1);",connection))
        {
            find.Parameters.AddWithValue("@id",messageId);find.Parameters.AddWithValue("@userId",userId);find.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);
            var value=await find.ExecuteScalarAsync(ct);if(value is not Guid found)return false;conversationId=found;
        }
        if(!await CanAccessSupportConversation(connection,conversationId,userId,role,ct))return false;
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using(var update=new SqlCommand("UPDATE dbo.support_messages SET deleted_at=SYSUTCDATETIME(),deleted_by_user_id=@userId,deleted_by_name=@actor WHERE id=@id AND deleted_at IS NULL;",connection,transaction)){update.Parameters.AddWithValue("@id",messageId);update.Parameters.AddWithValue("@userId",userId);update.Parameters.AddWithValue("@actor",actorName);if(await update.ExecuteNonQueryAsync(ct)!=1){await transaction.RollbackAsync(ct);return false;}}
            await using(var refresh=new SqlCommand("UPDATE dbo.support_conversations SET updated_at=SYSUTCDATETIME(),last_message_at=COALESCE((SELECT MAX(created_at) FROM dbo.support_messages WHERE conversation_id=@conversationId AND deleted_at IS NULL),created_at) WHERE id=@conversationId;",connection,transaction)){refresh.Parameters.AddWithValue("@conversationId",conversationId);await refresh.ExecuteNonQueryAsync(ct);}
            await transaction.CommitAsync(ct);return true;
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<(byte[] Audio,string ContentType,string FileName)?> SupportVoiceNote(Guid messageId,string userId,string role,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);
        Guid conversationId;await using(var find=new SqlCommand("SELECT m.conversation_id FROM dbo.support_messages m INNER JOIN dbo.support_voice_notes v ON v.message_id=m.id WHERE m.id=@id AND m.deleted_at IS NULL;",connection)){find.Parameters.AddWithValue("@id",messageId);var value=await find.ExecuteScalarAsync(ct);if(value is not Guid id)return null;conversationId=id;}
        if(!await CanAccessSupportConversation(connection,conversationId,userId,role,ct))return null;
        await using var command=new SqlCommand("SELECT audio_data,content_type,COALESCE(file_name,N'nota-de-voz.webm') FROM dbo.support_voice_notes WHERE message_id=@id;",connection);command.Parameters.AddWithValue("@id",messageId);await using var reader=await command.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))return null;return ((byte[])reader[0],reader.GetString(1),reader.GetString(2));
    }

    public async Task<bool> CanDeleteDirectConversation(Guid conversationId,string userId,string role,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);
        const string sql="SELECT COUNT(*) FROM dbo.support_conversations WHERE id=@id AND deleted_at IS NULL AND category LIKE 'DIRECT:%' AND (@admin=1 OR supervisor_user_id=@userId OR SUBSTRING(category,8,80)=@userId);";
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@id",conversationId);command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);command.Parameters.AddWithValue("@userId",userId);return Convert.ToInt32(await command.ExecuteScalarAsync(ct))>0;
    }

    public async Task<bool> UpdateSupportConversationStatus(Guid conversationId,string status,string userId,string role,CancellationToken ct)
    {
        if(role is not ("Administrator" or "Technology" or "GeneralServices" or "HumanResources"))return false;
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);
        if(!await CanAccessSupportConversation(connection,conversationId,userId,role,ct))return false;
        await using var command=new SqlCommand("UPDATE dbo.support_conversations SET status=@status,updated_at=SYSUTCDATETIME() WHERE id=@id;",connection);command.Parameters.AddWithValue("@id",conversationId);command.Parameters.AddWithValue("@status",status);return await command.ExecuteNonQueryAsync(ct)==1;
    }

    public async Task<bool> UpdateSupportConversationControl(Guid conversationId,string action,string userId,string role,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);
        if(!await CanAccessSupportConversation(connection,conversationId,userId,role,ct))return false;
        var sql=action switch{
            "MUTE"=>"IF NOT EXISTS(SELECT 1 FROM dbo.support_conversation_mutes WHERE conversation_id=@id AND user_id=@userId) INSERT INTO dbo.support_conversation_mutes(conversation_id,user_id) VALUES(@id,@userId); UPDATE dbo.support_conversations SET updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;",
            "UNMUTE"=>"DELETE FROM dbo.support_conversation_mutes WHERE conversation_id=@id AND user_id=@userId; UPDATE dbo.support_conversations SET updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;",
            "RESTRICT"=>"UPDATE dbo.support_conversations SET is_restricted=1,updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;",
            "UNRESTRICT"=>"UPDATE dbo.support_conversations SET is_restricted=0,updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;",
            "BLOCK"=>"UPDATE dbo.support_conversations SET is_blocked=1,status='CLOSED',updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;",
            "UNBLOCK"=>"UPDATE dbo.support_conversations SET is_blocked=0,status='WAITING_SUPPORT',updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;",
            "DELETE"=>"UPDATE dbo.support_conversations SET deleted_at=SYSUTCDATETIME(),updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;",
            _=>null};
        if(sql is null)return false;
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        try{
            await using(var update=new SqlCommand(sql,connection,transaction)){update.Parameters.AddWithValue("@id",conversationId);update.Parameters.AddWithValue("@userId",userId);if(await update.ExecuteNonQueryAsync(ct)<1){await transaction.RollbackAsync(ct);return false;}}
            await using(var audit=new SqlCommand("INSERT INTO dbo.support_conversation_controls(conversation_id,action,actor_user_id,actor_role) VALUES(@id,@action,@userId,@role);",connection,transaction)){audit.Parameters.AddWithValue("@id",conversationId);audit.Parameters.AddWithValue("@action",action);audit.Parameters.AddWithValue("@userId",userId);audit.Parameters.AddWithValue("@role",role);await audit.ExecuteNonQueryAsync(ct);}
            await transaction.CommitAsync(ct);return true;
        }catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<string?> SupportPresenceContext(Guid conversationId,string userId,string role,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureSupportChatSchema(connection,ct);if(!await CanAccessSupportConversation(connection,conversationId,userId,role,ct))return null;await using var command=new SqlCommand("SELECT assigned_department FROM dbo.support_conversations WHERE id=@id;",connection);command.Parameters.AddWithValue("@id",conversationId);return (string?)await command.ExecuteScalarAsync(ct);
    }

    public async Task<AccountContactProfileRow?> ContactProfile(Guid id,CancellationToken ct)
    {
        await using var connection=await Open(ct);const string sql="""SELECT u.id,u.email,u.display_name,u.role,u.contact,CASE WHEN u.avatar_data IS NULL THEN 0 ELSE 1 END,COALESCE(g.names,N'') FROM dbo.admin_users u OUTER APPLY(SELECT STRING_AGG(ug.group_name,N'|') names FROM dbo.admin_user_groups ug WHERE ug.user_id=u.id)g WHERE u.id=@id AND u.is_active=1;""";await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@id",id.ToString());await using var reader=await command.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))return null;return new(Guid.Parse(reader.GetString(0)),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.IsDBNull(4)?null:reader.GetString(4),ReadBooleanValue(reader,5)?$"/api/account/avatar/{id}":null,reader.GetString(6).Split('|',StringSplitOptions.RemoveEmptyEntries).ToList());
    }

    public async Task UpdateOwnContact(Guid id,string? contact,CancellationToken ct){await using var connection=await Open(ct);await using var command=new SqlCommand("UPDATE dbo.admin_users SET contact=@contact,updated_at=SYSUTCDATETIME() WHERE id=@id;",connection);command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@contact",Db(string.IsNullOrWhiteSpace(contact)?null:contact.Trim()));await command.ExecuteNonQueryAsync(ct);}

    static async Task<bool> CanAccessSupportConversation(SqlConnection connection,Guid conversationId,string userId,string role,CancellationToken ct)
    {
        const string sql="SELECT COUNT(*) FROM dbo.support_conversations WHERE id=@id AND deleted_at IS NULL AND ((category LIKE 'DIRECT:%' AND (@admin=1 OR supervisor_user_id=@userId OR SUBSTRING(category,8,80)=@userId)) OR (ISNULL(category,'') NOT LIKE 'DIRECT:%' AND (@all=1 OR (@own=1 AND supervisor_user_id=@userId) OR (@department<>'' AND assigned_department=@department))));";
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@id",conversationId);command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);command.Parameters.AddWithValue("@all",role=="Administrator"?1:0);command.Parameters.AddWithValue("@own",role=="GroupAdministrator"?1:0);command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@department",DepartmentForRole(role));return Convert.ToInt32(await command.ExecuteScalarAsync(ct))>0;
    }

    public async Task<List<AutomaticFindingRow>> AutomaticFindings(string userId, string role,string? supportTeam, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureFindingEvidenceSchema(connection,ct);
        const string sql = """
            SELECT af.id,p.id,a.codigo,a.terminal,a.grupo,p.submitted_at,af.finding_key,af.title,af.detail,af.assigned_department,af.priority,af.status,
                   af.diagnosis,af.resolution,af.verification,af.recommendations,af.resolved_at,af.resolution_minutes,af.resolved_by_name,
                   (SELECT e.id AS [id],e.file_name AS [fileName],e.content_type AS [contentType],e.captured_at AS [capturedAt],e.latitude AS [latitude],e.longitude AS [longitude],e.accuracy_meters AS [accuracyMeters]
                    FROM dbo.finding_resolution_evidence e WHERE e.finding_id=af.id ORDER BY e.captured_at,e.id FOR JSON PATH) AS evidence_json,
                   af.assigned_technician_id,u.display_name,p.direccion,p.sector,p.municipio,p.provincia,p.latitude,p.longitude
            FROM dbo.automatic_findings af
            INNER JOIN dbo.agency_profiles p ON p.id=af.profile_id
            INNER JOIN dbo.agencies a ON a.id=af.agency_id
            LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=af.assigned_technician_id
            WHERE (@department='' OR af.assigned_department=@department)
              AND (@technicianOnly=0 OR af.assigned_technician_id=@userId)
              AND (@scoped=0 OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo))
            ORDER BY af.title,a.grupo,a.terminal;
            """;
        await using var command = new SqlCommand(sql, connection); AddScope(command,userId,role);
        command.Parameters.AddWithValue("@department",DepartmentForRole(role));
        command.Parameters.AddWithValue("@technicianOnly",role=="Technology"&&supportTeam is "TECHNICIANS" or "WAREHOUSE" or "WORKSHOP"?1:0);
        await using var reader = await command.ExecuteReaderAsync(ct); var rows = new List<AutomaticFindingRow>();
        while(await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0),Guid.Parse(reader.GetString(1)),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetDateTime(5),reader.GetString(6),reader.GetString(7),reader.GetString(8),reader.GetString(9),reader.GetString(10),reader.GetString(11),reader.IsDBNull(12)?null:reader.GetString(12),reader.IsDBNull(13)?null:reader.GetString(13),reader.IsDBNull(14)?null:reader.GetString(14),reader.IsDBNull(15)?null:reader.GetString(15),reader.IsDBNull(16)?null:reader.GetDateTime(16),reader.IsDBNull(17)?null:reader.GetInt32(17),reader.IsDBNull(18)?null:reader.GetString(18),System.Text.Json.JsonSerializer.Deserialize<List<FindingEvidenceRow>>(reader.IsDBNull(19)?"[]":reader.GetString(19),StorageJson)??[],reader.IsDBNull(20)?null:reader.GetString(20),reader.IsDBNull(21)?null:reader.GetString(21),reader.IsDBNull(22)?null:reader.GetString(22),reader.IsDBNull(23)?null:reader.GetString(23),reader.IsDBNull(24)?null:reader.GetString(24),reader.IsDBNull(25)?null:reader.GetString(25),reader.IsDBNull(26)?null:reader.GetDouble(26),reader.IsDBNull(27)?null:reader.GetDouble(27)));
        return rows;
    }

    public async Task<bool> AssignAutomaticFinding(Guid id,string? technicianId,string role,string? supportTeam,CancellationToken ct)
    {
        if(role=="Technology"&&supportTeam is "TECHNICIANS" or "WAREHOUSE" or "WORKSHOP")return false;
        await using var connection=await Open(ct);await EnsureFindingEvidenceSchema(connection,ct);const string sql="UPDATE dbo.automatic_findings SET assigned_technician_id=@technician,updated_at=SYSUTCDATETIME() WHERE id=@id AND (@department='' OR assigned_department=@department);";await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@technician",Db(technicianId));command.Parameters.AddWithValue("@department",DepartmentForRole(role));return await command.ExecuteNonQueryAsync(ct)==1;
    }

    public async Task<bool> ResolveAutomaticFinding(Guid id,string diagnosis,string resolution,string verification,string recommendations,string userId,string role,string? supportTeam,string resolvedByName,string resolvedByUsername,List<FindingEvidenceUpload> evidence,CancellationToken ct)
    {
        await using var connection=await Open(ct);
        await EnsureFindingEvidenceSchema(connection,ct);
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        await using(var gpsRequired=new SqlCommand("SELECT COUNT(*) FROM dbo.automatic_findings af INNER JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=af.assigned_technician_id WHERE af.id=@id AND u.role='TECHNOLOGY' AND ISNULL(u.support_team,'TECHNICIANS') NOT IN('CALL_CENTER','TECHNICAL_FAILURE');",connection,transaction))
        {
            gpsRequired.Parameters.AddWithValue("@id",id);
            if(Convert.ToInt32(await gpsRequired.ExecuteScalarAsync(ct))>0&&evidence.Any(photo=>photo.Latitude is null||photo.Longitude is null||photo.AccuracyMeters is null||photo.LocationCapturedAt is null)){await transaction.RollbackAsync(ct);return false;}
        }
        const string sql="""
            UPDATE dbo.automatic_findings SET status='RESOLVED',diagnosis=@diagnosis,resolution=@resolution,verification=@verification,recommendations=@recommendations,
              resolved_at=COALESCE(resolved_at,SYSUTCDATETIME()),
              resolution_minutes=COALESCE(resolution_minutes,DATEDIFF(MINUTE,created_at,SYSUTCDATETIME())),
              resolved_by_name=@resolvedByName,resolved_by_username=@resolvedByUsername,
              updated_at=SYSUTCDATETIME(),closed_at=COALESCE(closed_at,SYSUTCDATETIME())
            WHERE id=@id AND (@department='' OR assigned_department=@department) AND (@technicianOnly=0 OR assigned_technician_id=@userId) AND status NOT IN ('RESOLVED','CLOSED');
            """;
        await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@diagnosis",diagnosis);command.Parameters.AddWithValue("@resolution",resolution);command.Parameters.AddWithValue("@verification",verification);command.Parameters.AddWithValue("@recommendations",Db(string.IsNullOrWhiteSpace(recommendations)?null:recommendations));
        command.Parameters.AddWithValue("@department",DepartmentForRole(role));
        command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@technicianOnly",role=="Technology"&&supportTeam is "TECHNICIANS" or "WAREHOUSE" or "WORKSHOP"?1:0);
        command.Parameters.AddWithValue("@resolvedByName",resolvedByName);command.Parameters.AddWithValue("@resolvedByUsername",resolvedByUsername);
        if(await command.ExecuteNonQueryAsync(ct)!=1){await transaction.RollbackAsync(ct);return false;}
        foreach(var photo in evidence){await using var insertPhoto=new SqlCommand("INSERT INTO dbo.finding_resolution_evidence(id,finding_id,file_name,content_type,photo_data,uploaded_by_name,latitude,longitude,accuracy_meters,location_captured_at) VALUES(NEWID(),@finding,@fileName,@contentType,@data,@actor,@latitude,@longitude,@accuracy,@locationCapturedAt);",connection,transaction);insertPhoto.Parameters.AddWithValue("@finding",id);insertPhoto.Parameters.AddWithValue("@fileName",photo.FileName);insertPhoto.Parameters.AddWithValue("@contentType",photo.ContentType);insertPhoto.Parameters.AddWithValue("@data",photo.Data);insertPhoto.Parameters.AddWithValue("@actor",resolvedByName);insertPhoto.Parameters.AddWithValue("@latitude",Db(photo.Latitude));insertPhoto.Parameters.AddWithValue("@longitude",Db(photo.Longitude));insertPhoto.Parameters.AddWithValue("@accuracy",Db(photo.AccuracyMeters));insertPhoto.Parameters.AddWithValue("@locationCapturedAt",Db(photo.LocationCapturedAt));await insertPhoto.ExecuteNonQueryAsync(ct);}
        const string answersSql="""
            UPDATE p SET structural_answers=CASE af.finding_key
              WHEN 'NO_INVERTER' THEN JSON_MODIFY(p.structural_answers,'$.inverterPresent','yes')
              WHEN 'NO_BATTERY' THEN JSON_MODIFY(p.structural_answers,'$.batteryPresent','yes')
              WHEN 'PRINTER_NO_MAINTENANCE' THEN JSON_MODIFY(p.structural_answers,'$.printerMaintained','yes')
              WHEN 'NOT_PAINTED' THEN JSON_MODIFY(p.structural_answers,'$.painted','yes')
              WHEN 'NO_RAZA_STICKER' THEN JSON_MODIFY(p.structural_answers,'$.razaSticker','yes')
              WHEN 'NO_REAL_STICKER' THEN JSON_MODIFY(p.structural_answers,'$.realSticker','yes')
              WHEN 'LOTEKA_NOT_REMOVED' THEN JSON_MODIFY(p.structural_answers,'$.lotekaRemoved','yes')
              ELSE p.structural_answers END
            FROM dbo.agency_profiles p INNER JOIN dbo.automatic_findings af ON af.profile_id=p.id
            WHERE af.id=@id;

            UPDATE p SET structural_answers=JSON_MODIFY(p.structural_answers,'$.damageFound','no')
            FROM dbo.agency_profiles p INNER JOIN dbo.automatic_findings af ON af.profile_id=p.id
            WHERE af.id=@id AND af.finding_key LIKE 'DAMAGE[_]%'
              AND NOT EXISTS(SELECT 1 FROM dbo.automatic_findings pending
                WHERE pending.profile_id=af.profile_id AND pending.finding_key LIKE 'DAMAGE[_]%'
                  AND pending.status NOT IN ('RESOLVED','CLOSED'));

            INSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data)
            SELECT 'automatic_finding',CONVERT(nvarchar(80),af.id),'FINDING_RESOLVED',
              CONCAT(N'{"findingKey":"',af.finding_key,N'","resolutionMinutes":',COALESCE(CONVERT(varchar(20),af.resolution_minutes),'0'),N',"evidenceCount":',@evidenceCount,N'}')
            FROM dbo.automatic_findings af WHERE af.id=@id;
            """;
        await using var answersCommand=new SqlCommand(answersSql,connection,transaction);answersCommand.Parameters.AddWithValue("@id",id);answersCommand.Parameters.AddWithValue("@evidenceCount",evidence.Count);
        await answersCommand.ExecuteNonQueryAsync(ct);await transaction.CommitAsync(ct);return true;
    }

    public async Task<(byte[] Data,string Mime)?> FindingEvidencePhoto(Guid evidenceId,string role,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureFindingEvidenceSchema(connection,ct);
        const string sql="""SELECT e.photo_data,e.content_type FROM dbo.finding_resolution_evidence e INNER JOIN dbo.automatic_findings af ON af.id=e.finding_id WHERE e.id=@id AND (@department='' OR af.assigned_department=@department);""";
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@id",evidenceId);command.Parameters.AddWithValue("@department",DepartmentForRole(role));await using var reader=await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess,ct);return await reader.ReadAsync(ct)?((byte[])reader[0],reader.GetString(1)):null;
    }

    public async Task<List<SupportDepartmentRow>> SupportCatalog(CancellationToken ct)
    {
        await using var connection=await Open(ct);var departments=new List<SupportDepartmentRow>();
        const string cpuCategorySql="""IF OBJECT_ID(N'dbo.support_categories',N'U') IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.support_departments WHERE code='TECHNOLOGY') MERGE dbo.support_categories AS target USING(SELECT 'TECHNOLOGY' department_code,'CPU_FAILURE' code,N'Falla CPU' name,N'CPU sin encender, lenta o con avería de hardware.' description,'CRITICAL' default_priority,CAST(1 AS bit) requires_detail,N'Describe la falla de la CPU' detail_label,CAST(NULL AS nvarchar(max)) options_json,65 display_order,CAST(1 AS bit) is_active) AS source ON target.department_code=source.department_code AND target.code=source.code WHEN MATCHED THEN UPDATE SET name=source.name,description=source.description,default_priority=source.default_priority,requires_detail=source.requires_detail,detail_label=source.detail_label,options_json=source.options_json,display_order=source.display_order,is_active=source.is_active,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(department_code,code,name,description,default_priority,requires_detail,detail_label,options_json,display_order,is_active) VALUES(source.department_code,source.code,source.name,source.description,source.default_priority,source.requires_detail,source.detail_label,source.options_json,source.display_order,source.is_active);""";
        await using(var cpuCategoryCommand=new SqlCommand(cpuCategorySql,connection))await cpuCategoryCommand.ExecuteNonQueryAsync(ct);
        const string cpuTicketPrioritySql="""IF OBJECT_ID(N'dbo.support_tickets',N'U') IS NOT NULL UPDATE dbo.support_tickets SET priority='CRITICAL',updated_at=SYSUTCDATETIME() WHERE assigned_department='TECHNOLOGY' AND category='CPU_FAILURE' AND ISNULL(priority,'')<>'CRITICAL';""";
        await using(var cpuTicketPriorityCommand=new SqlCommand(cpuTicketPrioritySql,connection))await cpuTicketPriorityCommand.ExecuteNonQueryAsync(ct);
        const string sql="""SELECT d.code,d.name,d.description,d.accent_color,d.display_order,d.is_active,c.id,c.code,c.name,c.description,c.default_priority,c.requires_detail,c.detail_label,c.options_json,c.display_order,c.is_active FROM dbo.support_departments d LEFT JOIN dbo.support_categories c ON c.department_code=d.code WHERE d.is_active=1 ORDER BY d.display_order,c.display_order,c.name;""";
        await using var command=new SqlCommand(sql,connection);await using var reader=await command.ExecuteReaderAsync(ct);
        var map=new Dictionary<string,SupportDepartmentRow>();
        while(await reader.ReadAsync(ct)){var code=reader.GetString(0);if(!map.TryGetValue(code,out var department)){department=new(code,reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.GetString(3),reader.GetInt32(4),reader.GetBoolean(5),[]);map[code]=department;departments.Add(department);}if(!reader.IsDBNull(6))department.Categories.Add(new(reader.GetGuid(6),reader.GetString(7),reader.GetString(8),reader.IsDBNull(9)?null:reader.GetString(9),reader.GetString(10),reader.GetBoolean(11),reader.IsDBNull(12)?null:reader.GetString(12),reader.IsDBNull(13)?null:System.Text.Json.JsonSerializer.Deserialize<List<string>>(reader.GetString(13)),reader.GetInt32(14),reader.GetBoolean(15)));}
        return departments;
    }

    public async Task UpsertSupportDepartment(SupportDepartmentInput body,CancellationToken ct){await using var connection=await Open(ct);const string sql="""UPDATE dbo.support_departments SET name=@name,description=@description,accent_color=@color,display_order=@sort,is_active=@active,updated_at=SYSUTCDATETIME() WHERE code=@code;IF @@ROWCOUNT=0 INSERT INTO dbo.support_departments(code,name,description,accent_color,display_order,is_active) VALUES(@code,@name,@description,@color,@sort,@active);""";await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@code",body.Code!);command.Parameters.AddWithValue("@name",body.Name!);command.Parameters.AddWithValue("@description",Db(body.Description));command.Parameters.AddWithValue("@color",body.AccentColor??"#38BDF8");command.Parameters.AddWithValue("@sort",body.DisplayOrder);command.Parameters.AddWithValue("@active",body.IsActive);await command.ExecuteNonQueryAsync(ct);}
    public async Task UpsertSupportCategory(Guid? id,SupportCategoryInput body,CancellationToken ct){await using var connection=await Open(ct);var categoryId=id??Guid.NewGuid();const string sql="""UPDATE dbo.support_categories SET department_code=@department,code=@code,name=@name,description=@description,default_priority=@priority,requires_detail=@requires,detail_label=@label,options_json=@options,display_order=@sort,is_active=@active,updated_at=SYSUTCDATETIME() WHERE id=@id;IF @@ROWCOUNT=0 INSERT INTO dbo.support_categories(id,department_code,code,name,description,default_priority,requires_detail,detail_label,options_json,display_order,is_active) VALUES(@id,@department,@code,@name,@description,@priority,@requires,@label,@options,@sort,@active);""";await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@id",categoryId);command.Parameters.AddWithValue("@department",body.DepartmentCode!);command.Parameters.AddWithValue("@code",body.Code!);command.Parameters.AddWithValue("@name",body.Name!);command.Parameters.AddWithValue("@description",Db(body.Description));command.Parameters.AddWithValue("@priority",body.DefaultPriority!);command.Parameters.AddWithValue("@requires",body.RequiresDetail);command.Parameters.AddWithValue("@label",Db(body.DetailLabel));command.Parameters.AddWithValue("@options",Db(body.Options is null?null:System.Text.Json.JsonSerializer.Serialize(body.Options)));command.Parameters.AddWithValue("@sort",body.DisplayOrder);command.Parameters.AddWithValue("@active",body.IsActive);await command.ExecuteNonQueryAsync(ct);}
    public async Task<bool> DeleteSupportCategory(Guid id,CancellationToken ct){await using var connection=await Open(ct);await using var command=new SqlCommand("DELETE FROM dbo.support_categories WHERE id=@id;",connection);command.Parameters.AddWithValue("@id",id);return await command.ExecuteNonQueryAsync(ct)==1;}

    public async Task<List<AgencyTransitionRow>?> AgencyTransitions(string role,CancellationToken ct)
    {
        if(role is not ("Administrator" or "Technology" or "GeneralServices" or "HumanResources"))return null;
        await using var connection=await Open(ct);await EnsureAgencyTransitionStages(connection,ct);await EnsureAgencyTransitionEvidenceSchema(connection,ct);var progressCapabilities=await AgencyTransitionProgressCapabilities(connection,ct);var projects=new List<AgencyTransitionRow>();var map=new Dictionary<Guid,AgencyTransitionRow>();var stageMap=new Dictionary<Guid,AgencyTransitionStageRow>();var progressMap=new Dictionary<Guid,AgencyTransitionProgressRow>();
        const string sql="""SELECT p.id,p.agency_id,a.codigo,a.terminal,a.grupo,p.project_type,p.status,p.current_stage,p.notes,p.created_by_name,p.created_at,p.updated_at,p.completed_at FROM dbo.agency_transition_projects p INNER JOIN dbo.agencies a ON a.id=p.agency_id WHERE p.status<>'CANCELLED' ORDER BY CASE p.status WHEN 'ACTIVE' THEN 0 WHEN 'PENDING' THEN 1 ELSE 2 END,p.updated_at DESC;""";
        await using(var command=new SqlCommand(sql,connection))await using(var reader=await command.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct)){var row=new AgencyTransitionRow(reader.GetGuid(0),Guid.Parse(reader.GetString(1)),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetString(8),reader.GetString(9),reader.GetDateTime(10),reader.GetDateTime(11),reader.IsDBNull(12)?null:reader.GetDateTime(12),[]);projects.Add(row);map[row.Id]=row;}
        if(projects.Count==0)return projects;
        const string stageSql="""SELECT s.id,s.project_id,s.department,s.stage_order,s.status,s.notes,s.started_at,s.completed_at,s.completed_by_name,s.updated_at FROM dbo.agency_transition_stages s INNER JOIN dbo.agency_transition_projects p ON p.id=s.project_id WHERE p.status<>'CANCELLED' ORDER BY s.project_id,s.stage_order;""";
        await using(var command=new SqlCommand(stageSql,connection))await using(var reader=await command.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct)){if(map.TryGetValue(reader.GetGuid(1),out var project)){var stage=new AgencyTransitionStageRow(reader.GetGuid(0),reader.GetString(2),reader.GetByte(3),reader.GetString(4),reader.IsDBNull(5)?null:reader.GetString(5),reader.IsDBNull(6)?null:reader.GetDateTime(6),reader.IsDBNull(7)?null:reader.GetDateTime(7),reader.IsDBNull(8)?null:reader.GetString(8),reader.GetDateTime(9),[]);project.Stages.Add(stage);stageMap[stage.Id]=stage;}}
        if(progressCapabilities.Exists)try
        {
            var entryType=progressCapabilities.EntryType?"h.entry_type":"CASE WHEN h.note LIKE '__ASSIGNMENT__%' THEN 'ASSIGNMENT' WHEN h.progress_status='EDITED' THEN 'STAGE_EDIT' WHEN h.progress_status='COMPLETED' THEN 'STAGE_COMPLETION' ELSE 'ADVANCE' END";var updatedAt=progressCapabilities.UpdatedAt?"h.updated_at":"CAST(NULL AS datetime2(3))";var visible=progressCapabilities.DeletedAt?"AND h.deleted_at IS NULL":"";var progressSql=$"SELECT h.id,h.stage_id,h.note,h.progress_status,{entryType},h.created_by_name,h.created_at,{updatedAt} FROM dbo.agency_transition_progress h INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE p.status<>'CANCELLED' {visible} ORDER BY h.created_at,h.id;";
            await using(var command=new SqlCommand(progressSql,connection))await using(var reader=await command.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct)){if(stageMap.TryGetValue(reader.GetGuid(1),out var stage)){var progress=new AgencyTransitionProgressRow(reader.GetGuid(0),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetDateTime(6),reader.IsDBNull(7)?null:reader.GetDateTime(7),[]);stage.Progress.Add(progress);progressMap[progress.Id]=progress;}}
            if(progressMap.Count>0){const string evidenceSql="SELECT e.id,e.progress_id,e.file_name,e.content_type,e.captured_at,e.latitude,e.longitude,e.accuracy_meters,e.location_captured_at,e.uploaded_by_name FROM dbo.agency_transition_progress_evidence e INNER JOIN dbo.agency_transition_projects p ON p.id=e.project_id WHERE p.status<>'CANCELLED' ORDER BY e.captured_at,e.id;";await using var evidenceCommand=new SqlCommand(evidenceSql,connection);await using var evidenceReader=await evidenceCommand.ExecuteReaderAsync(ct);while(await evidenceReader.ReadAsync(ct)){if(progressMap.TryGetValue(evidenceReader.GetGuid(1),out var progress))progress.Evidence.Add(new(evidenceReader.GetGuid(0),evidenceReader.GetString(2),evidenceReader.GetString(3),evidenceReader.GetDateTime(4),$"/api/agency-transitions/evidence/{evidenceReader.GetGuid(0)}",evidenceReader.IsDBNull(5)?null:Convert.ToDouble(evidenceReader.GetDecimal(5)),evidenceReader.IsDBNull(6)?null:Convert.ToDouble(evidenceReader.GetDecimal(6)),evidenceReader.IsDBNull(7)?null:Convert.ToDouble(evidenceReader.GetDecimal(7)),evidenceReader.IsDBNull(8)?null:evidenceReader.GetDateTime(8),evidenceReader.IsDBNull(9)?null:evidenceReader.GetString(9)));}}
        }
        catch(SqlException)
        {
            // La estructura principal se devuelve completa; una incompatibilidad del historial no vuelve a ocultar las etapas.
        }
        return projects;
    }

    public async Task<TicketActionResult> CreateAgencyTransition(AgencyTransitionCreate body,string userId,string role,string actorName,CancellationToken ct)
    {
        if(role is not ("Administrator" or "Technology" or "GeneralServices" or "HumanResources"))return new(false,"FORBIDDEN","Tu cuenta no puede crear procesos de construcción.");
        await using var connection=await Open(ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);var id=Guid.NewGuid();
        await using(var exists=new SqlCommand("SELECT COUNT(*) FROM dbo.agency_transition_projects WITH(UPDLOCK,HOLDLOCK) WHERE agency_id=@agency AND status IN('ACTIVE','PENDING');",connection,transaction)){exists.Parameters.AddWithValue("@agency",body.AgencyId!.Value.ToString());if(Convert.ToInt32(await exists.ExecuteScalarAsync(ct))>0){await transaction.RollbackAsync(ct);return new(false,"CONFLICT","Esta agencia ya tiene un proceso activo o pendiente.");}}
        const string insert="""INSERT INTO dbo.agency_transition_projects(id,agency_id,project_type,status,current_stage,notes,created_by_user_id,created_by_name) SELECT @id,a.id,@type,'ACTIVE','GENERAL_SERVICES',@notes,@userId,@actor FROM dbo.agencies a WHERE a.id=@agency;""";
        await using(var command=new SqlCommand(insert,connection,transaction)){command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@agency",body.AgencyId!.Value.ToString());command.Parameters.AddWithValue("@type",body.ProjectType!);command.Parameters.AddWithValue("@notes",Db(body.Notes));command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@actor",actorName);if(await command.ExecuteNonQueryAsync(ct)!=1){await transaction.RollbackAsync(ct);return new(false,"NOT_FOUND","Agencia no encontrada.");}}
        const string stages="""INSERT INTO dbo.agency_transition_stages(project_id,department,stage_order,status,started_at) VALUES(@id,'GENERAL_SERVICES',1,'IN_PROGRESS',SYSUTCDATETIME()),(@id,'TECHNOLOGY',2,'PENDING',NULL),(@id,'HUMAN_RESOURCES',3,'PENDING',NULL);""";
        await using(var command=new SqlCommand(stages,connection,transaction)){command.Parameters.AddWithValue("@id",id);await command.ExecuteNonQueryAsync(ct);}await transaction.CommitAsync(ct);return new(true,"OK",null);
    }

    public async Task<TicketActionResult> UpdateAgencyTransitionStatus(Guid id,AgencyTransitionStatusUpdate body,string userId,string role,string actorName,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureAgencyTransitionStages(connection,ct);var progressCapabilities=await AgencyTransitionProgressCapabilities(connection,ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            string? currentStatus=null;string? currentDepartment=null;Guid? stageId=null;
            const string find="""SELECT p.status,p.current_stage,s.id FROM dbo.agency_transition_projects p WITH(UPDLOCK,HOLDLOCK) LEFT JOIN dbo.agency_transition_stages s ON s.project_id=p.id AND s.department=p.current_stage WHERE p.id=@id AND p.status IN('ACTIVE','PENDING');""";
            await using(var command=new SqlCommand(find,connection,transaction)){command.Parameters.AddWithValue("@id",id);await using var reader=await command.ExecuteReaderAsync(ct);if(await reader.ReadAsync(ct)){currentStatus=reader.GetString(0);currentDepartment=reader.GetString(1);stageId=reader.IsDBNull(2)?null:reader.GetGuid(2);}}
            if(currentStatus is null||currentDepartment is null){await transaction.RollbackAsync(ct);return new(false,"NOT_FOUND","Proceso no encontrado o ya finalizado.");}
            var ownerDepartment=DepartmentForRole(role);if(role!="Administrator"&&ownerDepartment!=currentDepartment){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Solo el administrador o el departamento de la etapa actual puede cambiar el estado del proceso.");}
            if(currentStatus==body.Status){await transaction.CommitAsync(ct);return new(true,"OK",null);}
            await using(var update=new SqlCommand("UPDATE dbo.agency_transition_projects SET status=@status,updated_at=SYSUTCDATETIME() WHERE id=@id;",connection,transaction)){update.Parameters.AddWithValue("@id",id);update.Parameters.AddWithValue("@status",body.Status!);await update.ExecuteNonQueryAsync(ct);}
            if(progressCapabilities.Exists&&stageId.HasValue)
            {
                var note=body.Status=="PENDING"?$"Proceso puesto en pendiente: {body.Reason!.Trim()}":"Proceso reanudado.";var typeColumn=progressCapabilities.EntryType?",entry_type":"";var typeValue=progressCapabilities.EntryType?",'STAGE_EDIT'":"";var updatedColumn=progressCapabilities.UpdatedAt?",updated_at":"";var updatedValue=progressCapabilities.UpdatedAt?",SYSUTCDATETIME()":"";
                await using var history=new SqlCommand($"INSERT INTO dbo.agency_transition_progress(project_id,stage_id,department,note,progress_status{typeColumn},created_by_user_id,created_by_name{updatedColumn}) VALUES(@project,@stage,@department,@note,'EDITED'{typeValue},@userId,@actor{updatedValue});",connection,transaction);history.Parameters.AddWithValue("@project",id);history.Parameters.AddWithValue("@stage",stageId.Value);history.Parameters.AddWithValue("@department",currentDepartment);history.Parameters.AddWithValue("@note",note);history.Parameters.AddWithValue("@userId",userId);history.Parameters.AddWithValue("@actor",actorName);await history.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);return new(true,"OK",null);
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<TicketActionResult> UpdateAgencyTransitionStage(Guid id,AgencyTransitionStageUpdate body,string userId,string role,string actorName,CancellationToken ct)
    {
        var department=DepartmentForRole(role);if(role!="Administrator"&&string.IsNullOrEmpty(department))return new(false,"FORBIDDEN","Tu cuenta no pertenece a uno de los departamentos del proceso.");
        await using var connection=await Open(ct);var progressCapabilities=await AgencyTransitionProgressCapabilities(connection,ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);string? current=null;
        await using(var find=new SqlCommand("SELECT current_stage FROM dbo.agency_transition_projects WITH(UPDLOCK,HOLDLOCK) WHERE id=@id AND status='ACTIVE';",connection,transaction)){find.Parameters.AddWithValue("@id",id);current=(string?)await find.ExecuteScalarAsync(ct);}if(current is null){await transaction.RollbackAsync(ct);return new(false,"NOT_FOUND","Proceso no encontrado o ya finalizado.");}
        if(role!="Administrator"&&department!=current){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN",$"La etapa actual corresponde a {current}.");}
        if(current=="COMPLETED"){await transaction.RollbackAsync(ct);return new(false,"CONFLICT","El proceso ya está completado.");}
        var status=body.Status!;var normalizedNote=string.IsNullOrWhiteSpace(body.Notes)?(status=="COMPLETED"?"Etapa completada.":null):body.Notes.Trim();if(status=="IN_PROGRESS"&&normalizedNote is null){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","Describe el registro antes de guardarlo.");}if(progressCapabilities.Exists){var typeFilter=progressCapabilities.EntryType?"AND entry_type='ADVANCE'":"AND note NOT LIKE '__ASSIGNMENT__%'";var visibleFilter=progressCapabilities.DeletedAt?"AND deleted_at IS NULL":"";await using var pending=new SqlCommand($"SELECT COUNT(*) FROM dbo.agency_transition_progress WHERE project_id=@id AND department=@department {typeFilter} AND progress_status='IN_PROGRESS' {visibleFilter};",connection,transaction);pending.Parameters.AddWithValue("@id",id);pending.Parameters.AddWithValue("@department",current);if(Convert.ToInt32(await pending.ExecuteScalarAsync(ct))>0){await transaction.RollbackAsync(ct);return new(false,"CONFLICT",status=="IN_PROGRESS"?"Completa el registro actual y adjunta sus evidencias antes de crear otro registro.":"Completa o elimina los registros pendientes antes de cerrar la etapa del departamento.");}}const string update="""UPDATE dbo.agency_transition_stages SET status=@status,notes=COALESCE(@notes,notes),started_at=COALESCE(started_at,SYSUTCDATETIME()),completed_at=CASE WHEN @status='COMPLETED' THEN SYSUTCDATETIME() ELSE NULL END,completed_by_user_id=CASE WHEN @status='COMPLETED' THEN @userId ELSE NULL END,completed_by_name=CASE WHEN @status='COMPLETED' THEN @actor ELSE NULL END,updated_at=SYSUTCDATETIME() WHERE project_id=@id AND department=@department;""";
        Guid stageId;await using(var command=new SqlCommand(update+" SELECT id FROM dbo.agency_transition_stages WHERE project_id=@id AND department=@department;",connection,transaction)){command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@department",current);command.Parameters.AddWithValue("@status",status);command.Parameters.AddWithValue("@notes",Db(normalizedNote));command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@actor",actorName);var value=await command.ExecuteScalarAsync(ct);if(value is not Guid found){await transaction.RollbackAsync(ct);return new(false,"NOT_FOUND","Etapa no encontrada.");}stageId=found;}
        if(progressCapabilities.Exists){var columns=progressCapabilities.EntryType?",entry_type":"";var values=progressCapabilities.EntryType?",@entryType":"";await using var history=new SqlCommand($"INSERT INTO dbo.agency_transition_progress(project_id,stage_id,department,note,progress_status{columns},created_by_user_id,created_by_name) VALUES(@project,@stage,@department,@note,@status{values},@userId,@actor);",connection,transaction);history.Parameters.AddWithValue("@project",id);history.Parameters.AddWithValue("@stage",stageId);history.Parameters.AddWithValue("@department",current);history.Parameters.AddWithValue("@note",normalizedNote!);history.Parameters.AddWithValue("@status",status);if(progressCapabilities.EntryType)history.Parameters.AddWithValue("@entryType",status=="COMPLETED"?"STAGE_COMPLETION":"ADVANCE");history.Parameters.AddWithValue("@userId",userId);history.Parameters.AddWithValue("@actor",actorName);await history.ExecuteNonQueryAsync(ct);}
        if(status=="COMPLETED"){var next=current switch{"GENERAL_SERVICES"=>"TECHNOLOGY","TECHNOLOGY"=>"HUMAN_RESOURCES",_=>"COMPLETED"};await using(var project=new SqlCommand("UPDATE dbo.agency_transition_projects SET current_stage=@next,status=CASE WHEN @next='COMPLETED' THEN 'COMPLETED' ELSE 'ACTIVE' END,completed_at=CASE WHEN @next='COMPLETED' THEN SYSUTCDATETIME() ELSE NULL END,updated_at=SYSUTCDATETIME() WHERE id=@id;",connection,transaction)){project.Parameters.AddWithValue("@id",id);project.Parameters.AddWithValue("@next",next);await project.ExecuteNonQueryAsync(ct);}if(next!="COMPLETED")await using(var start=new SqlCommand("UPDATE dbo.agency_transition_stages SET status='IN_PROGRESS',started_at=COALESCE(started_at,SYSUTCDATETIME()),updated_at=SYSUTCDATETIME() WHERE project_id=@id AND department=@next;",connection,transaction)){start.Parameters.AddWithValue("@id",id);start.Parameters.AddWithValue("@next",next);await start.ExecuteNonQueryAsync(ct);}}
        await transaction.CommitAsync(ct);return new(true,"OK",null);
    }

    public async Task<TicketActionResult> EditAgencyTransitionStage(Guid projectId,Guid stageId,AgencyTransitionStageEdit body,string userId,string role,string actorName,CancellationToken ct)
    {
        var department=DepartmentForRole(role);var normalizedNote=string.IsNullOrWhiteSpace(body.Notes)?null:body.Notes.Trim();await using var connection=await Open(ct);var progressCapabilities=await AgencyTransitionProgressCapabilities(connection,ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        const string updateSql="""UPDATE s SET notes=@notes,updated_at=SYSUTCDATETIME() FROM dbo.agency_transition_stages s INNER JOIN dbo.agency_transition_projects p ON p.id=s.project_id WHERE s.id=@stageId AND p.id=@projectId AND p.status<>'CANCELLED' AND (@admin=1 OR s.department=@department); SELECT @@ROWCOUNT;""";
        await using(var command=new SqlCommand(updateSql,connection,transaction)){command.Parameters.AddWithValue("@notes",Db(normalizedNote));command.Parameters.AddWithValue("@stageId",stageId);command.Parameters.AddWithValue("@projectId",projectId);command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);command.Parameters.AddWithValue("@department",department);var changed=Convert.ToInt32(await command.ExecuteScalarAsync(ct));if(changed!=1){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","No tienes permiso para editar esta etapa.");}}
        if(progressCapabilities.Exists&&progressCapabilities.EntryType){const string historySql="INSERT INTO dbo.agency_transition_progress(project_id,stage_id,department,note,progress_status,entry_type,created_by_user_id,created_by_name) SELECT @projectId,s.id,s.department,@historyNote,'EDITED','STAGE_EDIT',@userId,@actor FROM dbo.agency_transition_stages s WHERE s.id=@stageId AND s.project_id=@projectId;";await using var command=new SqlCommand(historySql,connection,transaction);command.Parameters.AddWithValue("@projectId",projectId);command.Parameters.AddWithValue("@stageId",stageId);command.Parameters.AddWithValue("@historyNote",normalizedNote??"Se eliminó la descripción de la etapa.");command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@actor",actorName);await command.ExecuteNonQueryAsync(ct);}
        await using(var projectCommand=new SqlCommand("UPDATE dbo.agency_transition_projects SET updated_at=SYSUTCDATETIME() WHERE id=@projectId;",connection,transaction)){projectCommand.Parameters.AddWithValue("@projectId",projectId);await projectCommand.ExecuteNonQueryAsync(ct);}
        await transaction.CommitAsync(ct);return new(true,"OK",null);
    }

    public async Task<TicketActionResult> AssignAgencyTransitionProgress(Guid projectId,Guid stageId,Guid progressId,AgencyTransitionAssignmentUpdate body,string userId,string role,string actorName,CancellationToken ct)
    {
        var ownerDepartment=DepartmentForRole(role);await using var connection=await Open(ct);var capabilities=await AgencyTransitionProgressCapabilities(connection,ct);if(!capabilities.Exists)return new(false,"CONFLICT","El historial de avances debe estar disponible para guardar responsables.");await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var targetTypeFilter=capabilities.EntryType?"AND h.entry_type='ADVANCE'":"AND h.note NOT LIKE '__ASSIGNMENT__%'";var targetVisibleFilter=capabilities.DeletedAt?"AND h.deleted_at IS NULL":"";string? stageDepartment;await using(var stageCommand=new SqlCommand($"SELECT s.department FROM dbo.agency_transition_stages s INNER JOIN dbo.agency_transition_projects p ON p.id=s.project_id INNER JOIN dbo.agency_transition_progress h ON h.project_id=p.id AND h.stage_id=s.id WHERE s.id=@stageId AND p.id=@projectId AND h.id=@progressId AND p.status<>'CANCELLED' {targetTypeFilter} {targetVisibleFilter};",connection,transaction)){stageCommand.Parameters.AddWithValue("@stageId",stageId);stageCommand.Parameters.AddWithValue("@projectId",projectId);stageCommand.Parameters.AddWithValue("@progressId",progressId);stageDepartment=(string?)await stageCommand.ExecuteScalarAsync(ct);}if(stageDepartment is null){await transaction.RollbackAsync(ct);return new(false,"NOT_FOUND","Registro de trabajo no encontrado.");}if(role!="Administrator"&&ownerDepartment!=stageDepartment){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Solo el departamento propietario puede asignar el responsable de este registro.");}
        var assignmentType=body.AssignmentType!;string? assignedUserId=null;string assignedName;
        if(assignmentType=="TECHNICIAN")
        {
            if(string.IsNullOrWhiteSpace(body.TechnicianUserId)){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","Selecciona un técnico del departamento.");}
            const string technicianSql="""
                SELECT TOP(1)CONVERT(nvarchar(80),u.id),u.display_name
                FROM dbo.admin_users u
                WHERE CONVERT(nvarchar(80),u.id)=@technicianId
                  AND u.is_active=1
                  AND u.role=@department
                  AND u.role<>'GROUP_ADMIN'
                  AND (u.support_team='TECHNICIANS' OR (
                      JSON_VALUE(u.permissions_json,'$.canResolveTickets')='true'
                      AND ISNULL(JSON_VALUE(u.permissions_json,'$.canAssignTickets'),'false')<>'true'
                  ));
                """;
            await using var technicianCommand=new SqlCommand(technicianSql,connection,transaction);technicianCommand.Parameters.AddWithValue("@technicianId",body.TechnicianUserId.Trim());technicianCommand.Parameters.AddWithValue("@department",stageDepartment);await using var reader=await technicianCommand.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct)){await reader.DisposeAsync();await transaction.RollbackAsync(ct);return new(false,"VALIDATION","El usuario seleccionado no es un técnico activo de este departamento.");}assignedUserId=reader.GetString(0);assignedName=reader.GetString(1);await reader.DisposeAsync();
        }
        else
        {
            assignedName=body.ContractorName?.Trim()??"";if(assignedName.Length is <2 or >160){await transaction.RollbackAsync(ct);return new(false,"VALIDATION","Escribe el nombre completo del contratista.");}
        }
        var assignmentJson="__ASSIGNMENT__"+System.Text.Json.JsonSerializer.Serialize(new{assignmentType,assignedUserId,assignedName,targetProgressId=progressId});var typeColumn=capabilities.EntryType?",entry_type":"";var typeValue=capabilities.EntryType?",'ASSIGNMENT'":"";var updatedColumn=capabilities.UpdatedAt?",updated_at":"";var updatedValue=capabilities.UpdatedAt?",SYSUTCDATETIME()":"";await using(var history=new SqlCommand($"INSERT INTO dbo.agency_transition_progress(project_id,stage_id,department,note,progress_status{typeColumn},created_by_user_id,created_by_name{updatedColumn}) VALUES(@projectId,@stageId,@department,@note,'IN_PROGRESS'{typeValue},@userId,@actor{updatedValue});",connection,transaction)){history.Parameters.AddWithValue("@projectId",projectId);history.Parameters.AddWithValue("@stageId",stageId);history.Parameters.AddWithValue("@department",stageDepartment);history.Parameters.AddWithValue("@note",assignmentJson);history.Parameters.AddWithValue("@userId",userId);history.Parameters.AddWithValue("@actor",actorName);await history.ExecuteNonQueryAsync(ct);}await using(var projectCommand=new SqlCommand("UPDATE dbo.agency_transition_projects SET updated_at=SYSUTCDATETIME() WHERE id=@projectId;",connection,transaction)){projectCommand.Parameters.AddWithValue("@projectId",projectId);await projectCommand.ExecuteNonQueryAsync(ct);}await transaction.CommitAsync(ct);return new(true,"OK",null);
    }

    public async Task<TicketActionResult> UpdateAgencyTransitionProgress(Guid projectId,Guid stageId,Guid progressId,AgencyTransitionProgressUpdate body,string userId,string role,string actorName,CancellationToken ct)
    {
        var department=DepartmentForRole(role);var note=string.IsNullOrWhiteSpace(body.Note)?null:body.Note.Trim();await using var connection=await Open(ct);var capabilities=await AgencyTransitionProgressCapabilities(connection,ct);if(!capabilities.Exists)return new(false,"NOT_FOUND","El historial de avances todavía no está disponible.");await EnsureAgencyTransitionEvidenceSchema(connection,ct);var updated=capabilities.UpdatedAt?",updated_at=SYSUTCDATETIME()":"";var typeFilter=capabilities.EntryType?"AND h.entry_type='ADVANCE'":"";var visibleFilter=capabilities.DeletedAt?"AND h.deleted_at IS NULL":"";
        if(body.Status=="COMPLETED")
        {
            const string evidenceCountSql="SELECT COUNT(*) FROM dbo.agency_transition_progress_evidence e INNER JOIN dbo.agency_transition_progress h ON h.id=e.progress_id INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id WHERE e.progress_id=@progressId AND e.project_id=@projectId AND e.stage_id=@stageId AND (@admin=1 OR s.department=@department);";
            await using var evidenceCount=new SqlCommand(evidenceCountSql,connection);evidenceCount.Parameters.AddWithValue("@progressId",progressId);evidenceCount.Parameters.AddWithValue("@projectId",projectId);evidenceCount.Parameters.AddWithValue("@stageId",stageId);evidenceCount.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);evidenceCount.Parameters.AddWithValue("@department",department);var count=Convert.ToInt32(await evidenceCount.ExecuteScalarAsync(ct));if(count is <1 or >4)return new(false,"CONFLICT","Sube entre 1 y 4 fotografías de evidencia antes de completar este registro.");
        }
        var sql=$"UPDATE h SET note=COALESCE(@note,h.note),progress_status=COALESCE(@status,h.progress_status){updated} FROM dbo.agency_transition_progress h INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE h.id=@progressId AND h.project_id=@projectId AND h.stage_id=@stageId AND p.status<>'CANCELLED' {typeFilter} {visibleFilter} AND (@admin=1 OR s.department=@department);";
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@note",Db(note));command.Parameters.AddWithValue("@status",Db(body.Status));command.Parameters.AddWithValue("@progressId",progressId);command.Parameters.AddWithValue("@projectId",projectId);command.Parameters.AddWithValue("@stageId",stageId);command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);command.Parameters.AddWithValue("@department",department);var changed=await command.ExecuteNonQueryAsync(ct);return changed==1?new(true,"OK",null):new(false,"FORBIDDEN","Solo el departamento propietario puede modificar este registro.");
    }

    public async Task<TicketActionResult> AddAgencyTransitionEvidence(Guid projectId,Guid stageId,Guid progressId,List<AgencyTransitionEvidenceUpload> files,string userId,string role,string actorName,CancellationToken ct)
    {
        if(files.Count is <1 or >4)return new(false,"VALIDATION","Selecciona entre 1 y 4 fotografías.");var department=DepartmentForRole(role);await using var connection=await Open(ct);await EnsureAgencyTransitionEvidenceSchema(connection,ct);var capabilities=await AgencyTransitionProgressCapabilities(connection,ct);var typeFilter=capabilities.EntryType?"AND h.entry_type='ADVANCE'":"";var visibleFilter=capabilities.DeletedAt?"AND h.deleted_at IS NULL":"";await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var targetSql=$"SELECT h.progress_status FROM dbo.agency_transition_progress h WITH(UPDLOCK,HOLDLOCK) INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE h.id=@progressId AND h.project_id=@projectId AND h.stage_id=@stageId AND p.status<>'CANCELLED' {typeFilter} {visibleFilter} AND (@admin=1 OR s.department=@department);";string? status;await using(var target=new SqlCommand(targetSql,connection,transaction)){target.Parameters.AddWithValue("@progressId",progressId);target.Parameters.AddWithValue("@projectId",projectId);target.Parameters.AddWithValue("@stageId",stageId);target.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);target.Parameters.AddWithValue("@department",department);status=(string?)await target.ExecuteScalarAsync(ct);}if(status is null){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Solo el departamento propietario puede agregar evidencias a este registro.");}if(status=="COMPLETED"){await transaction.RollbackAsync(ct);return new(false,"CONFLICT","El registro está completado y sus evidencias ya no pueden modificarse.");}
        await using(var countCommand=new SqlCommand("SELECT COUNT(*) FROM dbo.agency_transition_progress_evidence WITH(UPDLOCK,HOLDLOCK) WHERE progress_id=@progressId;",connection,transaction)){countCommand.Parameters.AddWithValue("@progressId",progressId);var existing=Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct));if(existing+files.Count>4){await transaction.RollbackAsync(ct);return new(false,"CONFLICT",$"Este registro admite un máximo de 4 fotografías. Puedes agregar {Math.Max(0,4-existing)} más.");}}
        const string insert="INSERT INTO dbo.agency_transition_progress_evidence(id,project_id,stage_id,progress_id,file_name,content_type,photo_data,uploaded_by_user_id,uploaded_by_name,latitude,longitude,accuracy_meters,location_captured_at) VALUES(@id,@projectId,@stageId,@progressId,@fileName,@contentType,@data,@userId,@actor,@latitude,@longitude,@accuracy,@locationCapturedAt);";foreach(var file in files){await using var command=new SqlCommand(insert,connection,transaction);command.Parameters.AddWithValue("@id",Guid.NewGuid());command.Parameters.AddWithValue("@projectId",projectId);command.Parameters.AddWithValue("@stageId",stageId);command.Parameters.AddWithValue("@progressId",progressId);command.Parameters.AddWithValue("@fileName",file.FileName);command.Parameters.AddWithValue("@contentType",file.ContentType);command.Parameters.Add("@data",SqlDbType.VarBinary,-1).Value=file.Data;command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@actor",actorName);command.Parameters.AddWithValue("@latitude",Db(file.Latitude));command.Parameters.AddWithValue("@longitude",Db(file.Longitude));command.Parameters.AddWithValue("@accuracy",Db(file.AccuracyMeters));command.Parameters.AddWithValue("@locationCapturedAt",Db(file.LocationCapturedAt));await command.ExecuteNonQueryAsync(ct);}await using(var projectCommand=new SqlCommand("UPDATE dbo.agency_transition_projects SET updated_at=SYSUTCDATETIME() WHERE id=@projectId;",connection,transaction)){projectCommand.Parameters.AddWithValue("@projectId",projectId);await projectCommand.ExecuteNonQueryAsync(ct);}await transaction.CommitAsync(ct);return new(true,"OK",null);
    }

    public async Task<TicketActionResult> DeleteAgencyTransitionEvidence(Guid projectId,Guid stageId,Guid progressId,Guid evidenceId,string role,CancellationToken ct)
    {
        var department=DepartmentForRole(role);await using var connection=await Open(ct);await EnsureAgencyTransitionEvidenceSchema(connection,ct);const string sql="DELETE e FROM dbo.agency_transition_progress_evidence e INNER JOIN dbo.agency_transition_progress h ON h.id=e.progress_id INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE e.id=@evidenceId AND e.progress_id=@progressId AND e.project_id=@projectId AND e.stage_id=@stageId AND h.progress_status<>'COMPLETED' AND p.status<>'CANCELLED' AND (@admin=1 OR s.department=@department);";await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@evidenceId",evidenceId);command.Parameters.AddWithValue("@progressId",progressId);command.Parameters.AddWithValue("@projectId",projectId);command.Parameters.AddWithValue("@stageId",stageId);command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);command.Parameters.AddWithValue("@department",department);return await command.ExecuteNonQueryAsync(ct)==1?new(true,"OK",null):new(false,"FORBIDDEN","La evidencia no existe, el registro está completado o no tienes permiso para eliminarla.");
    }

    public async Task<(byte[] Data,string Mime)?> AgencyTransitionEvidencePhoto(Guid evidenceId,string role,CancellationToken ct)
    {
        if(role is not ("Administrator" or "Technology" or "GeneralServices" or "HumanResources"))return null;await using var connection=await Open(ct);await EnsureAgencyTransitionEvidenceSchema(connection,ct);const string sql="SELECT photo_data,content_type FROM dbo.agency_transition_progress_evidence WHERE id=@id;";await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@id",evidenceId);await using var reader=await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess,ct);return await reader.ReadAsync(ct)?((byte[])reader[0],reader.GetString(1)):null;
    }

    public async Task<TicketActionResult> DeleteAgencyTransitionProgress(Guid projectId,Guid stageId,Guid progressId,string userId,string role,string actorName,CancellationToken ct)
    {
        var department=DepartmentForRole(role);await using var connection=await Open(ct);var capabilities=await AgencyTransitionProgressCapabilities(connection,ct);if(!capabilities.Exists)return new(false,"NOT_FOUND","El historial de avances todavía no está disponible.");var typeFilter=capabilities.EntryType?"AND h.entry_type='ADVANCE'":"";var visibleFilter=capabilities.DeletedAt?"AND h.deleted_at IS NULL":"";var sql=capabilities.DeletedAt&&capabilities.DeleteAudit?$"UPDATE h SET deleted_at=SYSUTCDATETIME(),deleted_by_user_id=@userId,deleted_by_name=@actor{(capabilities.UpdatedAt?",updated_at=SYSUTCDATETIME()":"")} FROM dbo.agency_transition_progress h INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE h.id=@progressId AND h.project_id=@projectId AND h.stage_id=@stageId AND p.status<>'CANCELLED' {typeFilter} {visibleFilter} AND (@admin=1 OR s.department=@department);":$"DELETE h FROM dbo.agency_transition_progress h INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE h.id=@progressId AND h.project_id=@projectId AND h.stage_id=@stageId AND p.status<>'CANCELLED' {typeFilter} AND (@admin=1 OR s.department=@department);";await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@actor",actorName);command.Parameters.AddWithValue("@progressId",progressId);command.Parameters.AddWithValue("@projectId",projectId);command.Parameters.AddWithValue("@stageId",stageId);command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);command.Parameters.AddWithValue("@department",department);var changed=await command.ExecuteNonQueryAsync(ct);return changed==1?new(true,"OK",null):new(false,"FORBIDDEN","Solo el departamento propietario puede eliminar este registro.");
    }

    public async Task<TicketActionResult> DeleteAgencyTransitionStageContent(Guid projectId,Guid stageId,string userId,string role,string actorName,CancellationToken ct)
    {
        var department=DepartmentForRole(role);await using var connection=await Open(ct);var capabilities=await AgencyTransitionProgressCapabilities(connection,ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);const string stageSql="""UPDATE s SET notes=NULL,updated_at=SYSUTCDATETIME() FROM dbo.agency_transition_stages s INNER JOIN dbo.agency_transition_projects p ON p.id=s.project_id WHERE s.id=@stageId AND p.id=@projectId AND p.status<>'CANCELLED' AND (@admin=1 OR s.department=@department); SELECT @@ROWCOUNT;""";await using(var command=new SqlCommand(stageSql,connection,transaction)){command.Parameters.AddWithValue("@stageId",stageId);command.Parameters.AddWithValue("@projectId",projectId);command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);command.Parameters.AddWithValue("@department",department);if(Convert.ToInt32(await command.ExecuteScalarAsync(ct))!=1){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Solo el departamento propietario puede eliminar el contenido de esta etapa.");}}if(capabilities.Exists){var typeFilter=capabilities.EntryType?"AND entry_type<>'STAGE_COMPLETION'":"";var progressSql=capabilities.DeletedAt&&capabilities.DeleteAudit?$"UPDATE dbo.agency_transition_progress SET deleted_at=SYSUTCDATETIME(),deleted_by_user_id=@userId,deleted_by_name=@actor{(capabilities.UpdatedAt?",updated_at=SYSUTCDATETIME()":"")} WHERE project_id=@projectId AND stage_id=@stageId {typeFilter} AND deleted_at IS NULL;":$"DELETE FROM dbo.agency_transition_progress WHERE project_id=@projectId AND stage_id=@stageId {typeFilter};";await using var progressCommand=new SqlCommand(progressSql,connection,transaction);progressCommand.Parameters.AddWithValue("@userId",userId);progressCommand.Parameters.AddWithValue("@actor",actorName);progressCommand.Parameters.AddWithValue("@projectId",projectId);progressCommand.Parameters.AddWithValue("@stageId",stageId);await progressCommand.ExecuteNonQueryAsync(ct);}await transaction.CommitAsync(ct);return new(true,"OK",null);
    }

    public async Task<TicketActionResult> DeleteAgencyTransition(Guid id,string role,CancellationToken ct)
    {
        if(role!="Administrator")return new(false,"FORBIDDEN","Solo administración puede eliminar un proceso.");
        await using var connection=await Open(ct);await using var command=new SqlCommand("UPDATE dbo.agency_transition_projects SET status='CANCELLED',updated_at=SYSUTCDATETIME() WHERE id=@id AND status<>'CANCELLED';",connection);command.Parameters.AddWithValue("@id",id);return await command.ExecuteNonQueryAsync(ct)==1?new(true,"OK",null):new(false,"NOT_FOUND","Proceso no encontrado.");
    }

    public async Task<List<MaintenanceMovementRow>?> MaintenanceMovements(string userId,string role,string? supportTeam,CancellationToken ct)
    {
        var department=DepartmentForRole(role);if(role!="Administrator"&&department is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES"))return null;
        await using var connection=await Open(ct);await EnsureMaintenanceSchema(connection,ct);const string sql="""SELECT m.id,m.agency_id,COALESCE(a.codigo,'ALMACEN'),COALESCE(a.terminal,'Inventario central'),COALESCE(a.grupo,'Oficina principal'),m.ticket_id,m.department,m.technician_user_id,m.technician_name,m.movement_type,m.equipment_type,m.component_type,m.failure_cause,m.serial_number,m.quantity,m.notes,m.created_by_name,m.created_at,m.operational_area,m.document_number,m.qr_token,m.delivered_by_name,m.received_by_name,m.destination_name,m.signature_data,m.occurred_at,CAST(NULL AS nvarchar(256)) received_by_login FROM dbo.maintenance_movements m LEFT JOIN dbo.agencies a ON a.id=m.agency_id WHERE (@department='' OR m.department=@department) AND (@area='' OR m.operational_area=@area OR (@area='WORKSHOP' AND m.movement_type='TRANSFER_TO_WORKSHOP')) AND (@technicianOnly=0 OR m.technician_user_id=@userId) ORDER BY m.occurred_at DESC,m.created_at DESC;""";
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@department",role=="Administrator"?"":supportTeam=="WAREHOUSE"?"TECHNOLOGY":department);command.Parameters.AddWithValue("@area",supportTeam=="WAREHOUSE"?"WAREHOUSE":supportTeam=="WORKSHOP"?"WORKSHOP":"");command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@technicianOnly",role=="Technology"&&supportTeam=="TECHNICIANS"?1:0);await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<MaintenanceMovementRow>();while(await reader.ReadAsync(ct))rows.Add(new(reader.GetGuid(0),reader.IsDBNull(1)?null:Guid.Parse(reader.GetString(1)),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.IsDBNull(5)?null:reader.GetGuid(5),reader.GetString(6),reader.IsDBNull(7)?null:reader.GetString(7),reader.GetString(8),reader.GetString(9),reader.GetString(10),reader.IsDBNull(11)?null:reader.GetString(11),reader.GetString(12),reader.IsDBNull(13)?null:reader.GetString(13),reader.GetInt32(14),reader.IsDBNull(15)?null:reader.GetString(15),reader.GetString(16),reader.GetDateTime(17),reader.GetString(18),reader.GetString(19),reader.GetString(20),reader.IsDBNull(21)?null:reader.GetString(21),reader.IsDBNull(22)?null:reader.GetString(22),reader.IsDBNull(23)?null:reader.GetString(23),reader.IsDBNull(24)?null:reader.GetString(24),reader.GetDateTime(25),reader.IsDBNull(26)?null:reader.GetString(26)));return rows;
    }

    public async Task<List<MaintenanceProductRow>> MaintenanceProducts(string role,string? supportTeam,CancellationToken ct)
    {
        var department=DepartmentForRole(role);await using var connection=await Open(ct);await EnsureMaintenanceSchema(connection,ct);const string sql="""SELECT id,department,scan_code,product_name,component_type,updated_at FROM dbo.maintenance_products WHERE (@department='' OR department=@department) ORDER BY product_name,scan_code;""";await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@department",role=="Administrator"?"":supportTeam=="WAREHOUSE"?"TECHNOLOGY":department);await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<MaintenanceProductRow>();while(await reader.ReadAsync(ct))rows.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.IsDBNull(4)?null:reader.GetString(4),reader.GetDateTime(5)));return rows;
    }

    public async Task<TicketActionResult> CreateMaintenanceMovement(MaintenanceMovementCreate body,string userId,string role,string? supportTeam,string actorName,CancellationToken ct)
    {
        var ownDepartment=DepartmentForRole(role);var requestedDepartment=(body.Department??"").Trim().ToUpperInvariant();var warehouse=supportTeam=="WAREHOUSE";if(warehouse&&requestedDepartment!="TECHNOLOGY")return new(false,"FORBIDDEN","El Almacén gestiona únicamente inventario tecnológico.");var department=role=="Administrator"?requestedDepartment:warehouse?"TECHNOLOGY":ownDepartment;if(department is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES"))return new(false,"FORBIDDEN","Selecciona Tecnología, Servicios Generales o Recursos Humanos para registrar el mantenimiento.");if(role!="Administrator"&&!warehouse&&!string.IsNullOrWhiteSpace(body.Department)&&!string.Equals(body.Department,ownDepartment,StringComparison.OrdinalIgnoreCase))return new(false,"FORBIDDEN","Solo puedes registrar mantenimientos de tu departamento.");var requestedArea=(body.OperationalArea??"").Trim().ToUpperInvariant();if(warehouse&&requestedArea!="WAREHOUSE")return new(false,"FORBIDDEN","La cuenta de Almacén solo puede registrar operaciones de Almacén.");if(supportTeam=="WORKSHOP"&&requestedArea!="WORKSHOP")return new(false,"FORBIDDEN","La cuenta de Taller solo puede registrar operaciones de Taller.");
        var movementType=body.MovementType!.Trim().ToUpperInvariant();var entersWorkshop=requestedArea=="WORKSHOP"||movementType=="TRANSFER_TO_WORKSHOP";var equipment=body.EquipmentType!.Trim();var serialNumber=string.IsNullOrWhiteSpace(body.SerialNumber)?null:body.SerialNumber.Trim().ToUpperInvariant();
        if(entersWorkshop&&department=="HUMAN_RESOURCES")return new(false,"VALIDATION","Recursos Humanos no forma parte de Taller; registra sus movimientos en Almacén.");
        if(entersWorkshop&&department=="GENERAL_SERVICES"&&!equipment.Contains("INVERSOR",StringComparison.OrdinalIgnoreCase)&&!equipment.Contains("INVERTER",StringComparison.OrdinalIgnoreCase))return new(false,"VALIDATION","El Taller de Servicios Generales recibe únicamente inversores.");
        var technicianOnly=role=="Technology"&&supportTeam=="TECHNICIANS";
        await using var connection=await Open(ct);await EnsureMaintenanceSchema(connection,ct);
        if(movementType is ("REQUEST" or "EXIT" or "NEW_DELIVERY"))
        {
            await using var recipientCheck=new SqlCommand("SELECT COUNT(*) FROM dbo.admin_users WHERE CONVERT(nvarchar(80),id)=@recipient AND is_active=1 AND ((role='TECHNOLOGY' AND (support_team IS NULL OR support_team IN('CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS'))) OR role='GROUP_ADMIN');",connection);recipientCheck.Parameters.AddWithValue("@recipient",body.TechnicianUserId??"");if(Convert.ToInt32(await recipientCheck.ExecuteScalarAsync(ct))!=1)return new(false,"FORBIDDEN","El destinatario debe pertenecer a Tecnología o ser supervisor activo.");
        }
        const string sql="""INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at) SELECT @id,@agency,@ticket,@department,@technicianId,@technicianName,@movement,@equipment,@component,@cause,@serial,@quantity,@notes,@userId,@actor,@area,@document,@qr,@delivered,@received,@destination,@signature,@occurred WHERE @agency IS NULL OR EXISTS(SELECT 1 FROM dbo.agencies WHERE id=@agency);""";
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@id",Guid.NewGuid());command.Parameters.Add("@agency",SqlDbType.Char,36).Value=body.AgencyId is null?DBNull.Value:body.AgencyId.Value.ToString();command.Parameters.AddWithValue("@ticket",body.TicketId is null?DBNull.Value:body.TicketId.Value);command.Parameters.AddWithValue("@department",department);command.Parameters.AddWithValue("@technicianId",Db(technicianOnly?userId:body.TechnicianUserId));command.Parameters.AddWithValue("@technicianName",technicianOnly?actorName:string.IsNullOrWhiteSpace(body.TechnicianName)?actorName:body.TechnicianName.Trim());command.Parameters.AddWithValue("@movement",body.MovementType!.Trim().ToUpperInvariant());command.Parameters.AddWithValue("@equipment",body.EquipmentType!.Trim());command.Parameters.AddWithValue("@component",Db(string.IsNullOrWhiteSpace(body.ComponentType)?null:body.ComponentType.Trim()));command.Parameters.AddWithValue("@cause",body.FailureCause!.Trim());command.Parameters.AddWithValue("@serial",Db(serialNumber));command.Parameters.AddWithValue("@quantity",body.Quantity);command.Parameters.AddWithValue("@notes",Db(string.IsNullOrWhiteSpace(body.Notes)?null:body.Notes.Trim()));command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@actor",actorName);command.Parameters.AddWithValue("@area",body.OperationalArea!.Trim().ToUpperInvariant());command.Parameters.AddWithValue("@document",body.DocumentNumber!.Trim().ToUpperInvariant());command.Parameters.AddWithValue("@qr",string.IsNullOrWhiteSpace(body.QrToken)?$"REAL-MNT|{body.DocumentNumber!.Trim().ToUpperInvariant()}":body.QrToken.Trim());command.Parameters.AddWithValue("@delivered",Db(string.IsNullOrWhiteSpace(body.DeliveredByName)?null:body.DeliveredByName.Trim()));command.Parameters.AddWithValue("@received",Db(string.IsNullOrWhiteSpace(body.ReceivedByName)?null:body.ReceivedByName.Trim()));command.Parameters.AddWithValue("@destination",Db(string.IsNullOrWhiteSpace(body.DestinationName)?null:body.DestinationName.Trim()));command.Parameters.AddWithValue("@signature",Db(string.IsNullOrWhiteSpace(body.SignatureData)?null:body.SignatureData));command.Parameters.AddWithValue("@occurred",body.OccurredAt??DateTime.UtcNow);int changed;try{changed=await command.ExecuteNonQueryAsync(ct);}catch(SqlException error) when(error.Number is 2601 or 2627){return new(false,"CONFLICT","Ese número de formulario o código QR ya fue registrado.");}if(changed!=1)return new(false,"NOT_FOUND","Agencia no encontrada.");
        const string catalogSql="""MERGE dbo.maintenance_products AS target USING(SELECT @department department,@scan scan_code,@product product_name,@component component_type,@actor created_by_name) AS source ON target.department=source.department AND target.scan_code=source.scan_code WHEN MATCHED THEN UPDATE SET product_name=source.product_name,component_type=source.component_type,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(department,scan_code,product_name,component_type,created_by_name) VALUES(source.department,source.scan_code,source.product_name,source.component_type,source.created_by_name);""";
        if(serialNumber is not null)try{await using var catalogCommand=new SqlCommand(catalogSql,connection);catalogCommand.Parameters.AddWithValue("@department",department);catalogCommand.Parameters.AddWithValue("@scan",serialNumber);catalogCommand.Parameters.AddWithValue("@product",body.EquipmentType!.Trim());catalogCommand.Parameters.AddWithValue("@component",Db(string.IsNullOrWhiteSpace(body.ComponentType)?null:body.ComponentType.Trim()));catalogCommand.Parameters.AddWithValue("@actor",actorName);await catalogCommand.ExecuteNonQueryAsync(ct);}catch(SqlException error){Console.Error.WriteLine($"Maintenance product catalog: {error.Message}");}
        return new(true,"OK",null);
    }

    public async Task<MaintenanceProcurementSnapshot?> MaintenanceProcurement(string role,string? supportTeam,CancellationToken ct)
    {
        var department=DepartmentForRole(role);if(role!="Administrator"&&supportTeam is not ("WAREHOUSE" or "WORKSHOP")&&department is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES"))return null;
        await using var connection=await Open(ct);await EnsureMaintenanceSchema(connection,ct);var all=role=="Administrator"||supportTeam=="WORKSHOP";var warehouse=supportTeam=="WAREHOUSE";
        const string sql="""
        SELECT id,name,tax_id,contact_name,phone,email,is_active,created_at FROM dbo.maintenance_suppliers WHERE is_active=1 ORDER BY name;
        SELECT id,requisition_number,department,product_name,quantity_requested,quantity_fulfilled,priority,status,requested_by_name,created_at,updated_at,notes FROM dbo.maintenance_requisitions WHERE (@all=1 OR (@warehouse=1 AND department IN('TECHNOLOGY','GENERAL_SERVICES')) OR department=@department) ORDER BY CASE priority WHEN 'CRITICAL' THEN 1 WHEN 'HIGH' THEN 2 WHEN 'MEDIUM' THEN 3 ELSE 4 END,created_at DESC;
        SELECT p.id,p.order_number,p.supplier_id,s.name,p.requisition_id,p.department,p.product_name,p.quantity_ordered,p.quantity_received,p.unit_cost,p.status,p.expected_at,p.created_by_name,p.created_at,p.updated_at,p.notes FROM dbo.maintenance_purchase_orders p JOIN dbo.maintenance_suppliers s ON s.id=p.supplier_id WHERE (@all=1 OR (@warehouse=1 AND p.department IN('TECHNOLOGY','GENERAL_SERVICES')) OR p.department=@department) ORDER BY p.created_at DESC;
        SELECT r.id,r.return_number,r.supplier_id,s.name,r.purchase_order_id,r.department,r.product_name,r.serial_number,r.quantity,r.reason,r.status,r.created_by_name,r.created_at,r.notes FROM dbo.maintenance_returns r LEFT JOIN dbo.maintenance_suppliers s ON s.id=r.supplier_id WHERE (@all=1 OR (@warehouse=1 AND r.department IN('TECHNOLOGY','GENERAL_SERVICES')) OR r.department=@department) ORDER BY r.created_at DESC;
        """;
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@all",all?1:0);command.Parameters.AddWithValue("@warehouse",warehouse?1:0);command.Parameters.AddWithValue("@department",department);await using var reader=await command.ExecuteReaderAsync(ct);
        var suppliers=new List<MaintenanceSupplierRow>();while(await reader.ReadAsync(ct))suppliers.Add(new(reader.GetGuid(0),reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.IsDBNull(3)?null:reader.GetString(3),reader.IsDBNull(4)?null:reader.GetString(4),reader.IsDBNull(5)?null:reader.GetString(5),reader.GetBoolean(6),reader.GetDateTime(7)));
        await reader.NextResultAsync(ct);var requisitions=new List<MaintenanceRequisitionRow>();while(await reader.ReadAsync(ct))requisitions.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetInt32(4),reader.GetInt32(5),reader.GetString(6),reader.GetString(7),reader.GetString(8),reader.GetDateTime(9),reader.GetDateTime(10),reader.IsDBNull(11)?null:reader.GetString(11)));
        await reader.NextResultAsync(ct);var orders=new List<MaintenancePurchaseOrderRow>();while(await reader.ReadAsync(ct))orders.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetGuid(2),reader.GetString(3),reader.IsDBNull(4)?null:reader.GetGuid(4),reader.GetString(5),reader.GetString(6),reader.GetInt32(7),reader.GetInt32(8),reader.GetDecimal(9),reader.GetString(10),reader.IsDBNull(11)?null:reader.GetDateTime(11),reader.GetString(12),reader.GetDateTime(13),reader.GetDateTime(14),reader.IsDBNull(15)?null:reader.GetString(15)));
        await reader.NextResultAsync(ct);var returns=new List<MaintenanceReturnRow>();while(await reader.ReadAsync(ct))returns.Add(new(reader.GetGuid(0),reader.GetString(1),reader.IsDBNull(2)?null:reader.GetGuid(2),reader.IsDBNull(3)?null:reader.GetString(3),reader.IsDBNull(4)?null:reader.GetGuid(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.GetInt32(8),reader.GetString(9),reader.GetString(10),reader.GetString(11),reader.GetDateTime(12),reader.IsDBNull(13)?null:reader.GetString(13)));
        return new(suppliers,requisitions,orders,returns);
    }

    public async Task<TicketActionResult> CreateMaintenanceSupplier(MaintenanceSupplierCreate body,string role,string? supportTeam,string actorName,CancellationToken ct)
    {
        if(role!="Administrator"&&supportTeam!="WAREHOUSE"&&!(role=="Technology"&&supportTeam is null))return new(false,"FORBIDDEN","Solo Almacén o Administración de Tecnología puede registrar proveedores.");await using var connection=await Open(ct);await EnsureMaintenanceSchema(connection,ct);
        const string sql="""MERGE dbo.maintenance_suppliers AS target USING(SELECT @name name) AS source ON target.name=source.name WHEN MATCHED THEN UPDATE SET tax_id=@tax,contact_name=@contact,phone=@phone,email=@email,is_active=1,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(name,tax_id,contact_name,phone,email,created_by_name) VALUES(@name,@tax,@contact,@phone,@email,@actor);""";
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@name",body.Name!.Trim());command.Parameters.AddWithValue("@tax",Db(body.TaxId?.Trim()));command.Parameters.AddWithValue("@contact",Db(body.ContactName?.Trim()));command.Parameters.AddWithValue("@phone",Db(body.Phone?.Trim()));command.Parameters.AddWithValue("@email",Db(body.Email?.Trim()));command.Parameters.AddWithValue("@actor",actorName);await command.ExecuteNonQueryAsync(ct);return new(true,"OK",null);
    }

    public async Task<TicketActionResult> CreateMaintenanceRequisition(MaintenanceRequisitionCreate body,string userId,string role,string? supportTeam,string actorName,CancellationToken ct)
    {
        var ownDepartment=DepartmentForRole(role);var requested=body.Department!.Trim().ToUpperInvariant();var warehouse=supportTeam=="WAREHOUSE";if(warehouse&&requested is not ("TECHNOLOGY" or "GENERAL_SERVICES"))return new(false,"FORBIDDEN","El almacén solo organiza requerimientos de Tecnología y Servicios Generales.");var department=role=="Administrator"||warehouse?requested:ownDepartment;if(department is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES"))return new(false,"FORBIDDEN","Selecciona un departamento válido.");if(role!="Administrator"&&!warehouse&&requested!=ownDepartment)return new(false,"FORBIDDEN","Solo puedes solicitar artículos para tu departamento.");await using var connection=await Open(ct);await EnsureDocumentSchema(connection,ct);var number=$"REQ-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..25].ToUpperInvariant();
        const string sql="""INSERT INTO dbo.maintenance_requisitions(id,requisition_number,department,product_name,quantity_requested,priority,notes,requested_by_user_id,requested_by_name) VALUES(NEWID(),@number,@department,@product,@quantity,@priority,@notes,@userId,@actor);""";await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@number",number);command.Parameters.AddWithValue("@department",department);command.Parameters.AddWithValue("@product",body.ProductName!.Trim());command.Parameters.AddWithValue("@quantity",body.Quantity);command.Parameters.AddWithValue("@priority",body.Priority!);command.Parameters.AddWithValue("@notes",Db(body.Notes?.Trim()));command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@actor",actorName);await command.ExecuteNonQueryAsync(ct);return new(true,"OK",null);
    }

    public async Task<TicketActionResult> CreateMaintenancePurchaseOrder(MaintenancePurchaseOrderCreate body,string role,string? supportTeam,string actorName,CancellationToken ct)
    {
        var technologyManager=role=="Technology"&&supportTeam is null;var warehouse=supportTeam=="WAREHOUSE";if(role!="Administrator"&&!warehouse&&!technologyManager)return new(false,"FORBIDDEN","Solo Almacén o Administración de Tecnología puede emitir órdenes de compra.");var department=body.Department!.Trim().ToUpperInvariant();if(department is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES")||(technologyManager&&department!="TECHNOLOGY")||(warehouse&&department is not ("TECHNOLOGY" or "GENERAL_SERVICES")))return new(false,"FORBIDDEN","Solo puedes gestionar órdenes de Tecnología o Servicios Generales desde Almacén.");await using var connection=await Open(ct);await EnsureDocumentSchema(connection,ct);var number=$"OC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            const string validationSql="""SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.maintenance_suppliers WHERE id=@supplier AND is_active=1) AND (@requisition IS NULL OR EXISTS(SELECT 1 FROM dbo.maintenance_requisitions WHERE id=@requisition AND department=@department AND status IN('APPROVED','ORDERED','PARTIAL'))) THEN 1 ELSE 0 END;""";
            await using(var validation=new SqlCommand(validationSql,connection,transaction))
            {
                validation.Parameters.AddWithValue("@supplier",body.SupplierId);
                validation.Parameters.AddWithValue("@requisition",body.RequisitionId is null?DBNull.Value:body.RequisitionId.Value);
                validation.Parameters.AddWithValue("@department",department);
                if(Convert.ToInt32(await validation.ExecuteScalarAsync(ct))!=1)
                {
                    await transaction.RollbackAsync(ct);
                    return new(false,"NOT_FOUND","Proveedor o requisición no disponible.");
                }
            }

            const string sql="""INSERT INTO dbo.maintenance_purchase_orders(id,order_number,supplier_id,requisition_id,department,product_name,quantity_ordered,unit_cost,status,expected_at,notes,created_by_name) VALUES(NEWID(),@number,@supplier,@requisition,@department,@product,@quantity,@cost,'ISSUED',@expected,@notes,@actor); UPDATE dbo.maintenance_requisitions SET status='ORDERED',updated_at=SYSUTCDATETIME() WHERE id=@requisition AND status='APPROVED';""";
            await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@number",number);command.Parameters.AddWithValue("@supplier",body.SupplierId);command.Parameters.AddWithValue("@requisition",body.RequisitionId is null?DBNull.Value:body.RequisitionId.Value);command.Parameters.AddWithValue("@department",department);command.Parameters.AddWithValue("@product",body.ProductName!.Trim());command.Parameters.AddWithValue("@quantity",body.Quantity);command.Parameters.AddWithValue("@cost",body.UnitCost);command.Parameters.AddWithValue("@expected",body.ExpectedAt is null?DBNull.Value:body.ExpectedAt.Value);command.Parameters.AddWithValue("@notes",Db(body.Notes?.Trim()));command.Parameters.AddWithValue("@actor",actorName);await command.ExecuteNonQueryAsync(ct);await transaction.CommitAsync(ct);return new(true,"OK",null);
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<TicketActionResult> ReceiveMaintenancePurchaseOrder(Guid id,MaintenancePurchaseOrderReceive body,string userId,string role,string? supportTeam,string actorName,CancellationToken ct)
    {
        var technologyManager=role=="Technology"&&supportTeam is null;var warehouse=supportTeam=="WAREHOUSE";if(role!="Administrator"&&!warehouse&&!technologyManager)return new(false,"FORBIDDEN","Solo Almacén o Administración de Tecnología puede recibir mercancía.");await using var connection=await Open(ct);await EnsureMaintenanceSchema(connection,ct);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        try{string department,product,supplier;int ordered,received;Guid? requisition;await using(var select=new SqlCommand("SELECT p.department,p.product_name,p.quantity_ordered,p.quantity_received,p.requisition_id,s.name FROM dbo.maintenance_purchase_orders p JOIN dbo.maintenance_suppliers s ON s.id=p.supplier_id WHERE p.id=@id AND p.status IN('ISSUED','PARTIAL') AND (@technologyManager=0 OR p.department='TECHNOLOGY') AND (@warehouse=0 OR p.department IN('TECHNOLOGY','GENERAL_SERVICES'));",connection,transaction)){select.Parameters.AddWithValue("@id",id);select.Parameters.AddWithValue("@technologyManager",technologyManager?1:0);select.Parameters.AddWithValue("@warehouse",warehouse?1:0);await using var reader=await select.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct)){await reader.DisposeAsync();await transaction.RollbackAsync(ct);return new(false,"NOT_FOUND","La orden no existe, ya fue recibida o no pertenece al alcance del almacén.");}department=reader.GetString(0);product=reader.GetString(1);ordered=reader.GetInt32(2);received=reader.GetInt32(3);requisition=reader.IsDBNull(4)?null:reader.GetGuid(4);supplier=reader.GetString(5);}if(body.Quantity>ordered-received){await transaction.RollbackAsync(ct);return new(false,"CONFLICT",$"Solo quedan {ordered-received} unidades pendientes en la orden.");}var newReceived=received+body.Quantity;var status=newReceived==ordered?"RECEIVED":"PARTIAL";var document=$"REC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..25].ToUpperInvariant();
            const string sql="""UPDATE dbo.maintenance_purchase_orders SET quantity_received=@received,status=@status,updated_at=SYSUTCDATETIME() WHERE id=@id; UPDATE dbo.maintenance_requisitions SET quantity_fulfilled=CASE WHEN quantity_fulfilled+@quantity>quantity_requested THEN quantity_requested ELSE quantity_fulfilled+@quantity END,status=CASE WHEN quantity_fulfilled+@quantity>=quantity_requested THEN 'FULFILLED' ELSE 'PARTIAL' END,updated_at=SYSUTCDATETIME() WHERE id=@requisition; INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at) VALUES(NEWID(),NULL,NULL,@department,NULL,@actor,'ENTRY',@product,NULL,'Recepción física contra orden de compra',@serial,@quantity,@notes,@userId,@actor,'WAREHOUSE',@document,CONCAT('REAL-MNT|',@document),@supplier,@receiver,'Almacén central',NULL,SYSUTCDATETIME());""";await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@received",newReceived);command.Parameters.AddWithValue("@status",status);command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@quantity",body.Quantity);command.Parameters.AddWithValue("@requisition",requisition is null?DBNull.Value:requisition.Value);command.Parameters.AddWithValue("@department",department);command.Parameters.AddWithValue("@actor",actorName);command.Parameters.AddWithValue("@product",product);command.Parameters.AddWithValue("@serial",body.SerialNumber!.Trim().ToUpperInvariant());command.Parameters.AddWithValue("@notes",Db(body.Notes?.Trim()));command.Parameters.AddWithValue("@userId",userId);command.Parameters.AddWithValue("@document",document);command.Parameters.AddWithValue("@supplier",supplier);command.Parameters.AddWithValue("@receiver",string.IsNullOrWhiteSpace(body.ReceivedByName)?actorName:body.ReceivedByName.Trim());await command.ExecuteNonQueryAsync(ct);await transaction.CommitAsync(ct);return new(true,"OK",null);}catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<TicketActionResult> CreateMaintenanceReturn(MaintenanceReturnCreate body,string userId,string role,string? supportTeam,string actorName,CancellationToken ct)
    {
        var technologyManager=role=="Technology"&&supportTeam is null;var warehouse=supportTeam=="WAREHOUSE";if(role!="Administrator"&&!warehouse&&!technologyManager)return new(false,"FORBIDDEN","Solo Almacén o Administración de Tecnología puede registrar devoluciones.");var department=body.Department!.Trim().ToUpperInvariant();if(department is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES")||(technologyManager&&department!="TECHNOLOGY")||(warehouse&&department is not ("TECHNOLOGY" or "GENERAL_SERVICES")))return new(false,"FORBIDDEN","Solo puedes gestionar devoluciones de Tecnología o Servicios Generales desde Almacén.");await using var connection=await Open(ct);await EnsureMaintenanceSchema(connection,ct);var number=$"DEV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..25].ToUpperInvariant();await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        try{const string sql="""INSERT INTO dbo.maintenance_returns(id,return_number,supplier_id,purchase_order_id,department,product_name,serial_number,quantity,reason,notes,created_by_name) VALUES(NEWID(),@number,@supplier,@order,@department,@product,@serial,@quantity,@reason,@notes,@actor); INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at) VALUES(NEWID(),NULL,NULL,@department,NULL,@actor,'EXIT',@product,NULL,@reason,@serial,@quantity,@notes,@userId,@actor,'WAREHOUSE',@number,CONCAT('REAL-MNT|',@number),@actor,COALESCE((SELECT name FROM dbo.maintenance_suppliers WHERE id=@supplier),'Proveedor / devolución'),'Devolución a proveedor',NULL,SYSUTCDATETIME());""";await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@number",number);command.Parameters.AddWithValue("@supplier",body.SupplierId is null?DBNull.Value:body.SupplierId.Value);command.Parameters.AddWithValue("@order",body.PurchaseOrderId is null?DBNull.Value:body.PurchaseOrderId.Value);command.Parameters.AddWithValue("@department",department);command.Parameters.AddWithValue("@product",body.ProductName!.Trim());command.Parameters.AddWithValue("@serial",body.SerialNumber!.Trim().ToUpperInvariant());command.Parameters.AddWithValue("@quantity",body.Quantity);command.Parameters.AddWithValue("@reason",body.Reason!.Trim());command.Parameters.AddWithValue("@notes",Db(body.Notes?.Trim()));command.Parameters.AddWithValue("@actor",actorName);command.Parameters.AddWithValue("@userId",userId);await command.ExecuteNonQueryAsync(ct);await transaction.CommitAsync(ct);return new(true,"OK",null);}catch(SqlException error) when(error.Number==547){await transaction.RollbackAsync(ct);return new(false,"NOT_FOUND","La orden o el proveedor seleccionado ya no existe.");}catch{await transaction.RollbackAsync(ct);throw;}
    }
}
