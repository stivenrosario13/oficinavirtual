SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.admin_users',N'permissions_json') IS NULL
    ALTER TABLE dbo.admin_users ADD permissions_json nvarchar(max) NULL;

DECLARE @email nvarchar(320)=N'servicios.generales@grupotejeda.local';
DECLARE @permissions nvarchar(max)=N'{"canView":true,"canManage":false,"canManageUsers":false,"canManageAudits":false,"canViewDashboard":false,"canAudit":false,"canCreateTickets":false,"canResolveTickets":true,"canConfigureProfile":true,"canExportAudits":false,"canViewSupportDashboard":true,"canViewAllSupportDepartments":false,"canConfigureSupport":false,"canViewSupportFindings":true,"canUseSupportChat":true,"canManageSupportChat":true,"canModerateSupportChat":true,"canDeleteSupportConversations":false,"canShareSupportConversations":true,"canDeleteSupportTickets":false,"canViewTicketNotifications":true}';

IF EXISTS(SELECT 1 FROM dbo.admin_users WHERE email=@email)
BEGIN
    UPDATE dbo.admin_users
    SET display_name=N'Encargado de Servicios Generales',
        role='GENERAL_SERVICES',is_active=1,permissions_json=@permissions,
        updated_at=SYSUTCDATETIME()
    WHERE email=@email;
END
ELSE
BEGIN
    INSERT INTO dbo.admin_users
        (id,email,display_name,role,password_salt,password_hash,password_iterations,
         is_active,must_change_password,permissions_json)
    VALUES
        (CONVERT(char(36),NEWID()),@email,N'Encargado de Servicios Generales',
         'GENERAL_SERVICES',0x7BDEAB3354DED475F071FA4B58B3AB62,
         0x2096941A8EDD017184CAA3E80BA9CC5097824ABE84149E3FD91C58F049B7F8C5,
         210000,1,1,@permissions);
END;

SELECT email,display_name,role,is_active,must_change_password
FROM dbo.admin_users WHERE email=@email;
