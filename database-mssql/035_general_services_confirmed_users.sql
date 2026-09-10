SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.admin_users',N'permissions_json') IS NULL
    ALTER TABLE dbo.admin_users ADD permissions_json nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.admin_users',N'support_team') IS NULL
    ALTER TABLE dbo.admin_users ADD support_team varchar(30) NULL;

DECLARE @salt varbinary(16)=0x7BDEAB3354DED475F071FA4B58B3AB62;
DECLARE @hash varbinary(32)=0x2096941A8EDD017184CAA3E80BA9CC5097824ABE84149E3FD91C58F049B7F8C5;
DECLARE @support nvarchar(max)=N'{"canView":true,"canManage":false,"canManageUsers":false,"canManageAudits":false,"canViewDashboard":false,"canAudit":false,"canCreateTickets":true,"canAssignTickets":true,"canResolveTickets":true,"canConfigureProfile":true,"canExportAudits":false,"canViewSupportDashboard":true,"canViewAllSupportDepartments":false,"canConfigureSupport":false,"canViewSupportFindings":true,"canUseSupportChat":true,"canManageSupportChat":true,"canModerateSupportChat":false,"canDeleteSupportConversations":false,"canShareSupportConversations":true,"canDeleteSupportTickets":false,"canViewTicketNotifications":true}';
DECLARE @technician nvarchar(max)=N'{"canView":true,"canManage":false,"canManageUsers":false,"canManageAudits":false,"canViewDashboard":false,"canAudit":false,"canCreateTickets":false,"canAssignTickets":false,"canResolveTickets":true,"canConfigureProfile":true,"canExportAudits":false,"canViewSupportDashboard":true,"canViewAllSupportDepartments":false,"canConfigureSupport":false,"canViewSupportFindings":true,"canUseSupportChat":true,"canManageSupportChat":false,"canModerateSupportChat":false,"canDeleteSupportConversations":false,"canShareSupportConversations":true,"canDeleteSupportTickets":false,"canViewTicketNotifications":true}';

DECLARE @users TABLE(username nvarchar(120),display_name nvarchar(160),area nvarchar(100),is_technician bit);
INSERT INTO @users VALUES
(N'edwin.espinal',N'Edwin Espinal',N'Tecnología de Servicios Generales',0),
(N'ruth.solis',N'Ruth D. Liana Solís',N'Secretaría',0),
(N'suleika.cruz',N'Suleika de la Cruz',N'Secretaría',0),
(N'sheyla.lora',N'Sheyla Lora',N'Secretaría',0),
(N'ross.mariel',N'Ross Mariel',N'Secretaría',0),
(N'sarah.salas',N'Sarah Salas',N'Secretaría',0),
(N'edison.perez',N'Edison Pérez',N'Técnicos Electricistas',1),
(N'jhojance.feliz',N'Jhojance Feliz',N'Técnicos Electricistas',1),
(N'felix.blanco',N'Félix Blanco',N'Técnicos Electricistas',1),
(N'carlos.jimenez',N'Carlos J. Jiménez',N'Técnicos Electricistas',1),
(N'jender.mordan',N'Jender Mordan',N'Técnicos Electricistas',1),
(N'wander.cuevas',N'Wander Cuevas',N'Técnicos Electricistas',1),
(N'leonardo.ecolastico',N'Leonardo Ecolástico',N'Técnicos Servicios Generales',1),
(N'merlyn.ecolastico',N'Merlyn Javier Ecolástico',N'Técnicos Servicios Generales',1),
(N'martin.feliz',N'Martín Feliz',N'Equipo de Pintura',1),
(N'rigoberto.ramirez',N'Rigoberto Ramírez',N'Técnico Eléctrico de Plantas',1);

MERGE dbo.admin_users AS target
USING(SELECT username+N'@grupotejeda.local' email,display_name,area,is_technician FROM @users) AS source
ON LOWER(target.email)=LOWER(source.email)
WHEN MATCHED THEN UPDATE SET display_name=source.display_name,role='GENERAL_SERVICES',region=source.area,
    support_team=NULL,is_active=1,
    permissions_json=CASE WHEN source.is_technician=1 THEN @technician ELSE @support END,
    updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,must_change_password,permissions_json,support_team)
VALUES(CONVERT(char(36),NEWID()),source.email,source.display_name,'GENERAL_SERVICES',@salt,@hash,210000,1,source.area,1,
    CASE WHEN source.is_technician=1 THEN @technician ELSE @support END,NULL);

COMMIT TRANSACTION;

SELECT email,display_name,region,
       CASE WHEN JSON_VALUE(permissions_json,'$.canAssignTickets')='true' THEN N'Soporte' ELSE N'Técnico' END perfil,
       is_active,must_change_password
FROM dbo.admin_users
WHERE email IN(SELECT username+N'@grupotejeda.local' FROM @users)
ORDER BY region,display_name;
