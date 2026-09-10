-- Registro de Agencias - SQL Server 2025 / MonsterASP.NET
-- Ejecuta este archivo dentro de la base asignada al sitio.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.agencies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.agencies (
        id char(36) NOT NULL,
        source_key nvarchar(200) NOT NULL,
        codigo nvarchar(100) NOT NULL,
        terminal nvarchar(300) NOT NULL,
        grupo nvarchar(150) NOT NULL,
        region nvarchar(120) NULL,
        expected_latitude float NULL,
        expected_longitude float NULL,
        is_active bit NOT NULL CONSTRAINT DF_agencies_active DEFAULT (1),
        status varchar(30) NOT NULL CONSTRAINT DF_agencies_status DEFAULT ('PENDING'),
        created_at datetime2(3) NOT NULL CONSTRAINT DF_agencies_created DEFAULT (SYSUTCDATETIME()),
        updated_at datetime2(3) NOT NULL CONSTRAINT DF_agencies_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_agencies PRIMARY KEY (id),
        CONSTRAINT UQ_agencies_source_key UNIQUE (source_key),
        CONSTRAINT CK_agencies_status CHECK (status IN ('PENDING','COMPLETED','REVIEW_REQUIRED'))
    );
END;

-- Compatibilidad con instalaciones anteriores a la carga regional.
-- Se usa SQL dinámico para que SQL Server reconozca las columnas nuevas en esta misma ejecución.
IF COL_LENGTH(N'dbo.agencies',N'region') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.agencies ADD region nvarchar(120) NULL;';
IF COL_LENGTH(N'dbo.agencies',N'is_active') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.agencies ADD is_active bit NOT NULL CONSTRAINT DF_agencies_active_upgrade DEFAULT(1) WITH VALUES;';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.agencies') AND name=N'IX_agencies_group_status')
    CREATE INDEX IX_agencies_group_status ON dbo.agencies(grupo,status);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.agencies') AND name=N'IX_agencies_active_group_status')
    EXEC sys.sp_executesql N'CREATE INDEX IX_agencies_active_group_status ON dbo.agencies(is_active,grupo,status);';

IF OBJECT_ID(N'dbo.agency_profiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.agency_profiles (
        id char(36) NOT NULL,
        agency_id char(36) NOT NULL,
        employee_name nvarchar(160) NOT NULL,
        employee_code nvarchar(80) NULL,
        direccion nvarchar(300) NOT NULL,
        sector nvarchar(120) NOT NULL,
        municipio nvarchar(120) NOT NULL,
        provincia nvarchar(120) NOT NULL,
        tipo_establecimiento nvarchar(100) NOT NULL,
        tipo_establecimiento_otro nvarchar(120) NULL,
        latitude float NOT NULL,
        longitude float NOT NULL,
        accuracy_meters float NOT NULL,
        distance_from_agency_meters float NULL,
        location_captured_at datetime2(3) NOT NULL,
        location_source varchar(40) NOT NULL,
        structural_answers nvarchar(max) NOT NULL,
        observations nvarchar(1000) NULL,
        completion_percent tinyint NOT NULL CONSTRAINT DF_profiles_completion DEFAULT (100),
        photo_data varbinary(max) NULL,
        photo_content_type varchar(100) NULL,
        submitted_at datetime2(3) NOT NULL CONSTRAINT DF_profiles_submitted DEFAULT (SYSUTCDATETIME()),
        updated_at datetime2(3) NOT NULL CONSTRAINT DF_profiles_updated DEFAULT (SYSUTCDATETIME()),
        submitted_by_context nvarchar(max) NULL,
        CONSTRAINT PK_agency_profiles PRIMARY KEY (id),
        CONSTRAINT UQ_profiles_agency UNIQUE (agency_id),
        CONSTRAINT FK_profiles_agency FOREIGN KEY (agency_id) REFERENCES dbo.agencies(id),
        CONSTRAINT CK_profiles_lat CHECK (latitude BETWEEN -90 AND 90),
        CONSTRAINT CK_profiles_lng CHECK (longitude BETWEEN -180 AND 180),
        CONSTRAINT CK_profiles_zero CHECK (NOT (latitude=0 AND longitude=0)),
        CONSTRAINT CK_profiles_accuracy CHECK (accuracy_meters>0),
        CONSTRAINT CK_profiles_completion CHECK (completion_percent BETWEEN 0 AND 100),
        CONSTRAINT CK_profiles_other CHECK (tipo_establecimiento<>N'Otro' OR ISNULL(LEN(LTRIM(RTRIM(tipo_establecimiento_otro))),0)>0),
        CONSTRAINT CK_profiles_answers_json CHECK (ISJSON(structural_answers)=1),
        CONSTRAINT CK_profiles_context_json CHECK (submitted_by_context IS NULL OR ISJSON(submitted_by_context)=1)
    );
END;

IF OBJECT_ID(N'dbo.audit_log', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.audit_log (
        id bigint IDENTITY(1,1) NOT NULL,
        entity_type varchar(50) NOT NULL,
        entity_id char(36) NOT NULL,
        action varchar(80) NOT NULL,
        before_data nvarchar(max) NULL,
        after_data nvarchar(max) NULL,
        actor_email nvarchar(320) NULL,
        created_at datetime2(3) NOT NULL CONSTRAINT DF_audit_log_created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_audit_log PRIMARY KEY (id),
        CONSTRAINT CK_audit_log_before_json CHECK (before_data IS NULL OR ISJSON(before_data)=1),
        CONSTRAINT CK_audit_log_after_json CHECK (after_data IS NULL OR ISJSON(after_data)=1)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.audit_log') AND name=N'IX_audit_entity')
    CREATE INDEX IX_audit_entity ON dbo.audit_log(entity_type,entity_id,created_at DESC);

IF OBJECT_ID(N'dbo.admin_users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.admin_users (
        id char(36) NOT NULL,
        email nvarchar(320) NOT NULL,
        display_name nvarchar(160) NOT NULL,
        role varchar(20) NOT NULL CONSTRAINT DF_admin_users_role DEFAULT ('VIEWER'),
        password_salt binary(16) NOT NULL,
        password_hash binary(32) NOT NULL,
        password_iterations int NOT NULL CONSTRAINT DF_admin_users_iterations DEFAULT (210000),
        is_active bit NOT NULL CONSTRAINT DF_admin_users_active DEFAULT (1),
        last_login_at datetime2(3) NULL,
        created_at datetime2(3) NOT NULL CONSTRAINT DF_admin_users_created DEFAULT (SYSUTCDATETIME()),
        updated_at datetime2(3) NOT NULL CONSTRAINT DF_admin_users_updated DEFAULT (SYSUTCDATETIME()),
        region nvarchar(120) NULL,
        contact nvarchar(80) NULL,
        avatar_data varbinary(max) NULL,
        avatar_content_type varchar(100) NULL,
        must_change_password bit NOT NULL CONSTRAINT DF_admin_users_must_change DEFAULT (0),
        CONSTRAINT PK_admin_users PRIMARY KEY (id),
        CONSTRAINT UQ_admin_users_email UNIQUE (email),
        CONSTRAINT CK_admin_users_role CHECK (role IN ('ADMINISTRATOR','VIEWER','GROUP_ADMIN','FISCALIZADOR','TECHNOLOGY','GENERAL_SERVICES')),
        CONSTRAINT CK_admin_users_iterations CHECK (password_iterations>0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'IX_admin_users_role_active')
    CREATE INDEX IX_admin_users_role_active ON dbo.admin_users(role,is_active);

IF OBJECT_ID(N'dbo.admin_user_groups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.admin_user_groups (
        user_id char(36) NOT NULL,
        group_name nvarchar(150) NOT NULL,
        region nvarchar(120) NULL,
        assigned_at datetime2(3) NOT NULL CONSTRAINT DF_admin_user_groups_assigned DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_admin_user_groups PRIMARY KEY (user_id,group_name),
        CONSTRAINT FK_admin_user_groups_user FOREIGN KEY (user_id) REFERENCES dbo.admin_users(id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.admin_user_groups') AND name=N'IX_admin_user_groups_group')
    CREATE INDEX IX_admin_user_groups_group ON dbo.admin_user_groups(group_name,user_id);

IF OBJECT_ID(N'dbo.audit_photos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.audit_photos (
        id char(36) NOT NULL,
        profile_id char(36) NOT NULL,
        photo_type varchar(20) NOT NULL,
        photo_data varbinary(max) NOT NULL,
        content_type varchar(100) NOT NULL,
        latitude float NOT NULL,
        longitude float NOT NULL,
        accuracy_meters float NOT NULL,
        captured_at datetime2(3) NOT NULL,
        created_at datetime2(3) NOT NULL CONSTRAINT DF_audit_photos_created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_audit_photos PRIMARY KEY (id),
        CONSTRAINT UQ_photo_profile_type UNIQUE (profile_id,photo_type),
        CONSTRAINT FK_photos_profile FOREIGN KEY (profile_id) REFERENCES dbo.agency_profiles(id) ON DELETE CASCADE,
        CONSTRAINT CK_photos_type CHECK (photo_type IN ('frontal','operativa','entorno','inversor','bateria')),
        CONSTRAINT CK_photos_lat CHECK (latitude BETWEEN -90 AND 90),
        CONSTRAINT CK_photos_lng CHECK (longitude BETWEEN -180 AND 180),
        CONSTRAINT CK_photos_accuracy CHECK (accuracy_meters>0)
    );
END;

IF OBJECT_ID(N'dbo.audit_photos',N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.audit_photos') AND name=N'CK_photos_type')
        ALTER TABLE dbo.audit_photos DROP CONSTRAINT CK_photos_type;
    ALTER TABLE dbo.audit_photos WITH CHECK ADD CONSTRAINT CK_photos_type CHECK (photo_type IN ('frontal','operativa','entorno','inversor','bateria'));
END;

IF OBJECT_ID(N'dbo.agency_groups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.agency_groups (
        id char(36) NOT NULL,
        source_key nvarchar(200) NOT NULL,
        grupo nvarchar(150) NOT NULL,
        agency_count int NOT NULL CONSTRAINT DF_agency_groups_count DEFAULT (0),
        is_active bit NOT NULL CONSTRAINT DF_agency_groups_active DEFAULT (1),
        created_at datetime2(3) NOT NULL CONSTRAINT DF_agency_groups_created DEFAULT (SYSUTCDATETIME()),
        updated_at datetime2(3) NOT NULL CONSTRAINT DF_agency_groups_updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_agency_groups PRIMARY KEY (id),
        CONSTRAINT UQ_agency_groups_source_key UNIQUE (source_key),
        CONSTRAINT UQ_agency_groups_name UNIQUE (grupo),
        CONSTRAINT CK_agency_groups_count CHECK (agency_count>=0)
    );
END;

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
        source_profile_id char(36) NULL,
        is_automatic bit NOT NULL CONSTRAINT DF_support_tickets_automatic DEFAULT(0),
        CONSTRAINT PK_support_tickets PRIMARY KEY (id),
        CONSTRAINT UQ_support_tickets_number UNIQUE (ticket_number),
        CONSTRAINT FK_support_tickets_agency FOREIGN KEY (agency_id) REFERENCES dbo.agencies(id),
        CONSTRAINT CK_support_tickets_category CHECK (category IN ('EQUIPMENT','CONNECTIVITY','INFRASTRUCTURE','PRINTER','SECURITY','OTHER')),
        CONSTRAINT CK_support_tickets_department CHECK (assigned_department IN ('TECHNOLOGY','GENERAL_SERVICES')),
        CONSTRAINT CK_support_tickets_priority CHECK (priority IN ('LOW','MEDIUM','HIGH','CRITICAL')),
        CONSTRAINT CK_support_tickets_status CHECK (status IN ('OPEN','IN_PROGRESS','RESOLVED','CLOSED'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'IX_support_tickets_agency_status')
    CREATE INDEX IX_support_tickets_agency_status ON dbo.support_tickets(agency_id,status,created_at DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'UX_support_tickets_profile_department')
    CREATE UNIQUE INDEX UX_support_tickets_profile_department ON dbo.support_tickets(source_profile_id,assigned_department) WHERE source_profile_id IS NOT NULL;

IF OBJECT_ID(N'dbo.automatic_findings',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.automatic_findings (
        id uniqueidentifier NOT NULL CONSTRAINT DF_automatic_findings_id DEFAULT(NEWID()),
        profile_id char(36) NOT NULL,
        agency_id char(36) NOT NULL,
        finding_key varchar(40) NOT NULL,
        title nvarchar(180) NOT NULL,
        detail nvarchar(1000) NOT NULL,
        assigned_department varchar(25) NOT NULL,
        priority varchar(15) NOT NULL,
        status varchar(20) NOT NULL CONSTRAINT DF_automatic_findings_status DEFAULT('OPEN'),
        resolution nvarchar(2000) NULL,
        resolved_at datetime2(3) NULL,
        resolution_minutes int NULL,
        resolved_by_name nvarchar(160) NULL,
        resolved_by_username nvarchar(180) NULL,
        created_at datetime2(3) NOT NULL CONSTRAINT DF_automatic_findings_created DEFAULT(SYSUTCDATETIME()),
        updated_at datetime2(3) NOT NULL CONSTRAINT DF_automatic_findings_updated DEFAULT(SYSUTCDATETIME()),
        closed_at datetime2(3) NULL,
        CONSTRAINT PK_automatic_findings PRIMARY KEY(id),
        CONSTRAINT UQ_automatic_findings_profile_key UNIQUE(profile_id,finding_key),
        CONSTRAINT FK_automatic_findings_profile FOREIGN KEY(profile_id) REFERENCES dbo.agency_profiles(id) ON DELETE CASCADE,
        CONSTRAINT FK_automatic_findings_agency FOREIGN KEY(agency_id) REFERENCES dbo.agencies(id),
        CONSTRAINT CK_automatic_findings_department CHECK(assigned_department IN ('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')),
        CONSTRAINT CK_automatic_findings_priority CHECK(priority IN ('LOW','MEDIUM','HIGH','CRITICAL')),
        CONSTRAINT CK_automatic_findings_status CHECK(status IN ('OPEN','IN_PROGRESS','RESOLVED','CLOSED'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.agency_groups') AND name=N'IX_agency_groups_active_name')
    CREATE INDEX IX_agency_groups_active_name ON dbo.agency_groups(is_active,grupo);

SELECT N'OK' AS resultado,
       (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME IN
          ('agencies','agency_profiles','audit_log','admin_users','admin_user_groups','audit_photos','agency_groups')) AS tablas_creadas;
