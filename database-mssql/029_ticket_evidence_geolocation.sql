SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.support_ticket_evidence',N'latitude') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD latitude float NULL;
IF COL_LENGTH(N'dbo.support_ticket_evidence',N'longitude') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD longitude float NULL;
IF COL_LENGTH(N'dbo.support_ticket_evidence',N'accuracy_meters') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD accuracy_meters float NULL;
IF COL_LENGTH(N'dbo.support_ticket_evidence',N'location_captured_at') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD location_captured_at datetime2(3) NULL;
IF COL_LENGTH(N'dbo.support_ticket_evidence',N'location_source') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD location_source varchar(30) NULL;

COMMIT TRANSACTION;
