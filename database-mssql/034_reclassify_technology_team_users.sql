SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.admin_users',N'permissions_json') IS NULL
    ALTER TABLE dbo.admin_users ADD permissions_json nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.admin_users',N'support_team') IS NULL
    ALTER TABLE dbo.admin_users ADD support_team varchar(30) NULL;

DECLARE @salt varbinary(16)=0x7BDEAB3354DED475F071FA4B58B3AB62;
DECLARE @hash varbinary(32)=0x2096941A8EDD017184CAA3E80BA9CC5097824ABE84149E3FD91C58F049B7F8C5;
DECLARE @callCenter nvarchar(max)=N'{"canView":true,"canManage":false,"canManageUsers":false,"canManageAudits":false,"canViewDashboard":false,"canAudit":false,"canCreateTickets":true,"canAssignTickets":true,"canResolveTickets":true,"canConfigureProfile":true,"canExportAudits":false,"canViewSupportDashboard":true,"canViewAllSupportDepartments":false,"canConfigureSupport":false,"canViewSupportFindings":false,"canUseSupportChat":true,"canManageSupportChat":true,"canModerateSupportChat":false,"canDeleteSupportConversations":false,"canShareSupportConversations":true,"canDeleteSupportTickets":false,"canViewTicketNotifications":true}';
DECLARE @technicalFailure nvarchar(max)=N'{"canView":true,"canManage":false,"canManageUsers":false,"canManageAudits":false,"canViewDashboard":false,"canAudit":false,"canCreateTickets":true,"canAssignTickets":true,"canResolveTickets":true,"canConfigureProfile":true,"canExportAudits":false,"canViewSupportDashboard":true,"canViewAllSupportDepartments":false,"canConfigureSupport":false,"canViewSupportFindings":true,"canUseSupportChat":true,"canManageSupportChat":true,"canModerateSupportChat":false,"canDeleteSupportConversations":false,"canShareSupportConversations":true,"canDeleteSupportTickets":false,"canViewTicketNotifications":true}';
DECLARE @technician nvarchar(max)=N'{"canView":true,"canManage":false,"canManageUsers":false,"canManageAudits":false,"canViewDashboard":false,"canAudit":false,"canCreateTickets":false,"canAssignTickets":false,"canResolveTickets":true,"canConfigureProfile":true,"canExportAudits":false,"canViewSupportDashboard":true,"canViewAllSupportDepartments":false,"canConfigureSupport":false,"canViewSupportFindings":true,"canUseSupportChat":false,"canManageSupportChat":false,"canModerateSupportChat":false,"canDeleteSupportConversations":false,"canShareSupportConversations":false,"canDeleteSupportTickets":false,"canViewTicketNotifications":true}';

DECLARE @users TABLE(username nvarchar(120),display_name nvarchar(160),area nvarchar(80),support_team varchar(30));
INSERT INTO @users VALUES
(N'joel.vizcaino',N'Joel Vizcaíno',N'Tecnología','TECHNICAL_FAILURE'),
(N'eddi.bono',N'Eddi Bono',N'Tecnología','TECHNICAL_FAILURE'),
(N'franklin.calderon',N'Franklin Calderón',N'Tecnología','TECHNICAL_FAILURE'),
(N'jordaniel.ortega',N'Jordaniel Ortega',N'Tecnología','TECHNICAL_FAILURE'),
(N'yesica.mercedes',N'Yesica Mercedes',N'Tecnología','TECHNICAL_FAILURE'),
(N'rosmarlin.vittini',N'Rosmarlin Vittini',N'Soporte Técnico','TECHNICAL_FAILURE'),
(N'michael.miguel',N'Michael Miguel',N'Soporte Técnico','TECHNICAL_FAILURE'),
(N'darwin.dionel',N'Darwin Dionel',N'Soporte Técnico','TECHNICAL_FAILURE'),
(N'cristina.orosco',N'Cristina Orosco',N'Mesa de Servicio','CALL_CENTER'),
(N'milagros.nicool',N'Milagros Nicool',N'Mesa de Servicio','CALL_CENTER'),
(N'scarle.maria',N'Scarle María',N'Mesa de Servicio','CALL_CENTER'),
(N'valerin.squerli',N'Valerin Squerli',N'Mesa de Servicio','CALL_CENTER'),
(N'carlos.morel',N'Carlos Morel',N'Mesa de Servicio','CALL_CENTER'),
(N'carlina.martinez',N'Carlina Martínez',N'Mesa de Servicio','CALL_CENTER'),
(N'maxnaury.soto',N'Maxnaury Soto',N'Mesa de Servicio','CALL_CENTER'),
(N'enedraine.elisabeth',N'Enedraine Elisabeth',N'Mesa de Servicio','CALL_CENTER'),
(N'georgs.michael',N'Georgs Michael',N'Mesa de Servicio','CALL_CENTER'),
(N'lucianny.gonzalez',N'Lucianny González',N'Mesa de Servicio','CALL_CENTER'),
(N'dianna.ogando',N'Dianna Ogando',N'Mesa de Servicio','CALL_CENTER'),
(N'manauris.castro',N'Manauris Castro',N'Mensajero Técnico','TECHNICIANS'),
(N'esmerlin.mateo',N'Esmerlin Mateo',N'Mensajero Técnico','TECHNICIANS'),
(N'jeremi.mendez',N'Jeremi Méndez',N'Mensajero Técnico','TECHNICIANS'),
(N'engel.mendez',N'Engel Méndez',N'Mensajero Técnico','TECHNICIANS'),
(N'alexander.dieses',N'Alexander Dieses',N'Mensajero Técnico','TECHNICIANS'),
(N'jose.alcantara',N'José Alcántara',N'Mensajero Técnico','TECHNICIANS');

MERGE dbo.admin_users AS target
USING(SELECT username+N'@grupotejeda.local' email,display_name,area,support_team FROM @users) AS source
ON LOWER(target.email)=LOWER(source.email)
WHEN MATCHED THEN UPDATE SET display_name=source.display_name,role='TECHNOLOGY',region=source.area,
    support_team=source.support_team,is_active=1,
    permissions_json=CASE source.support_team WHEN 'CALL_CENTER' THEN @callCenter WHEN 'TECHNICIANS' THEN @technician ELSE @technicalFailure END,
    updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,must_change_password,permissions_json,support_team)
VALUES(CONVERT(char(36),NEWID()),source.email,source.display_name,'TECHNOLOGY',@salt,@hash,210000,1,source.area,1,
    CASE source.support_team WHEN 'CALL_CENTER' THEN @callCenter WHEN 'TECHNICIANS' THEN @technician ELSE @technicalFailure END,source.support_team);

COMMIT TRANSACTION;

SELECT email,display_name,region,support_team,is_active,must_change_password
FROM dbo.admin_users
WHERE email IN(SELECT username+N'@grupotejeda.local' FROM @users)
ORDER BY CASE support_team WHEN 'CALL_CENTER' THEN 1 WHEN 'TECHNICAL_FAILURE' THEN 2 ELSE 3 END,display_name;
