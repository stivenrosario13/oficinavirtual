SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

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
    CREATE INDEX IX_support_notification_receipts_user
        ON dbo.support_notification_receipts(user_id,dismissed_at,read_at);
END;

COMMIT TRANSACTION;
