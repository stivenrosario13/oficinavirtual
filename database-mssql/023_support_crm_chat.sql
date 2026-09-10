/* Chat CRM de soporte. La aplicación también crea estas tablas automáticamente. */
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

SELECT COUNT(*) AS conversaciones FROM dbo.support_conversations;
