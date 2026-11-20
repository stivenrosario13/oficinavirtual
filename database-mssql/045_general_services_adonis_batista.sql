SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.admin_users',N'permissions_json') IS NULL ALTER TABLE dbo.admin_users ADD permissions_json nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.admin_users',N'support_team') IS NULL ALTER TABLE dbo.admin_users ADD support_team varchar(30) NULL;

DECLARE @salt varbinary(16)=0x7BDEAB3354DED475F071FA4B58B3AB62;
DECLARE @hash varbinary(32)=0x2096941A8EDD017184CAA3E80BA9CC5097824ABE84149E3FD91C58F049B7F8C5;
DECLARE @permissions nvarchar(max)=N'{"canView":true,"canManage":false,"canManageUsers":false,"canManageAudits":false,"canViewDashboard":false,"canAudit":false,"canCreateTickets":false,"canAssignTickets":false,"canResolveTickets":true,"canConfigureProfile":true,"canExportAudits":false,"canViewSupportDashboard":true,"canViewAllSupportDepartments":false,"canConfigureSupport":false,"canViewSupportFindings":true,"canUseSupportChat":true,"canManageSupportChat":false,"canModerateSupportChat":false,"canDeleteSupportConversations":false,"canShareSupportConversations":true,"canDeleteSupportTickets":false,"canViewTicketNotifications":true}';

IF EXISTS(SELECT 1 FROM dbo.admin_users WHERE LOWER(email)=N'adonis.batista@grupotejeda.local')
    UPDATE dbo.admin_users SET display_name=N'Adonis Batista',role='GENERAL_SERVICES',region=N'Técnicos Servicios Generales',support_team=NULL,is_active=1,permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE LOWER(email)=N'adonis.batista@grupotejeda.local';
ELSE
    INSERT INTO dbo.admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,must_change_password,permissions_json,support_team)
    VALUES(CONVERT(char(36),NEWID()),N'adonis.batista@grupotejeda.local',N'Adonis Batista','GENERAL_SERVICES',@salt,@hash,210000,1,N'Técnicos Servicios Generales',1,@permissions,NULL);

COMMIT TRANSACTION;

SELECT email,display_name,role,region,is_active,must_change_password FROM dbo.admin_users WHERE LOWER(email)=N'adonis.batista@grupotejeda.local';
