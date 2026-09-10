SET NOCOUNT ON;

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
    CREATE INDEX IX_support_conversation_controls_conversation
        ON dbo.support_conversation_controls(conversation_id,created_at DESC);
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
