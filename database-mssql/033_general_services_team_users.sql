SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_status')
    ALTER TABLE dbo.support_tickets DROP CONSTRAINT CK_support_tickets_status;
ALTER TABLE dbo.support_tickets WITH CHECK ADD CONSTRAINT CK_support_tickets_status CHECK(status IN('OPEN','IN_PROGRESS','RESOLVED','CLOSED','CANCELLED'));

IF OBJECT_ID(N'dbo.support_categories',N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.support_categories WHERE department_code='GENERAL_SERVICES' AND code='TRANSPORTATION')
    INSERT INTO dbo.support_categories(id,department_code,code,name,description,default_priority,requires_detail,detail_label,display_order,is_active)
    VALUES(NEWID(),'GENERAL_SERVICES','TRANSPORTATION',N'Transportación',N'Solicitudes de vehículo, traslado y apoyo logístico.','MEDIUM',1,N'Detalla el vehículo, ruta o traslado requerido',110,1);
IF OBJECT_ID(N'dbo.support_categories',N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.support_categories WHERE department_code='GENERAL_SERVICES' AND code='CANCELLATION_ALERT')
    INSERT INTO dbo.support_categories(id,department_code,code,name,description,default_priority,requires_detail,detail_label,display_order,is_active)
    VALUES(NEWID(),'GENERAL_SERVICES','CANCELLATION_ALERT',N'Alerta de anulación',N'Alerta crítica para registrar, asignar y dar seguimiento a una solicitud de anulación.','CRITICAL',1,N'Indica el motivo, referencia y acción requerida',120,1);

IF COL_LENGTH(N'dbo.admin_users',N'permissions_json') IS NULL
    ALTER TABLE dbo.admin_users ADD permissions_json nvarchar(max) NULL;

DECLARE @salt varbinary(16)=0x7BDEAB3354DED475F071FA4B58B3AB62;
DECLARE @hash varbinary(32)=0x2096941A8EDD017184CAA3E80BA9CC5097824ABE84149E3FD91C58F049B7F8C5;
DECLARE @support nvarchar(max)=N'{"canView":true,"canManage":false,"canManageUsers":false,"canManageAudits":false,"canViewDashboard":false,"canAudit":false,"canCreateTickets":true,"canAssignTickets":true,"canResolveTickets":true,"canConfigureProfile":true,"canExportAudits":false,"canViewSupportDashboard":true,"canViewAllSupportDepartments":false,"canConfigureSupport":false,"canViewSupportFindings":true,"canUseSupportChat":true,"canManageSupportChat":true,"canModerateSupportChat":false,"canDeleteSupportConversations":false,"canShareSupportConversations":true,"canDeleteSupportTickets":false,"canViewTicketNotifications":true}';
DECLARE @technician nvarchar(max)=N'{"canView":true,"canManage":false,"canManageUsers":false,"canManageAudits":false,"canViewDashboard":false,"canAudit":false,"canCreateTickets":false,"canAssignTickets":false,"canResolveTickets":true,"canConfigureProfile":true,"canExportAudits":false,"canViewSupportDashboard":true,"canViewAllSupportDepartments":false,"canConfigureSupport":false,"canViewSupportFindings":true,"canUseSupportChat":true,"canManageSupportChat":false,"canModerateSupportChat":false,"canDeleteSupportConversations":false,"canShareSupportConversations":true,"canDeleteSupportTickets":false,"canViewTicketNotifications":true}';

DECLARE @users TABLE(username nvarchar(120),display_name nvarchar(160),area nvarchar(80),is_technician bit);
INSERT INTO @users VALUES
(N'joel.vizcaino',N'Joel Vizcaíno',N'Tecnología',0),(N'eddi.bono',N'Eddi Bono',N'Tecnología',0),(N'franklin.calderon',N'Franklin Calderón',N'Tecnología',0),(N'jordaniel.ortega',N'Jordaniel Ortega',N'Tecnología',0),(N'yesica.mercedes',N'Yesica Mercedes',N'Tecnología',0),
(N'rosmarlin.vittini',N'Rosmarlin Vittini',N'Soporte Técnico',1),(N'michael.miguel',N'Michael Miguel',N'Soporte Técnico',1),(N'darwin.dionel',N'Darwin Dionel',N'Soporte Técnico',1),
(N'cristina.orosco',N'Cristina Orosco',N'Mesa de Servicio',0),(N'milagros.nicool',N'Milagros Nicool',N'Mesa de Servicio',0),(N'scarle.maria',N'Scarle María',N'Mesa de Servicio',0),(N'valerin.squerli',N'Valerin Squerli',N'Mesa de Servicio',0),(N'carlos.morel',N'Carlos Morel',N'Mesa de Servicio',0),(N'carlina.martinez',N'Carlina Martínez',N'Mesa de Servicio',0),(N'maxnaury.soto',N'Maxnaury Soto',N'Mesa de Servicio',0),(N'enedraine.elisabeth',N'Enedraine Elisabeth',N'Mesa de Servicio',0),(N'georgs.michael',N'Georgs Michael',N'Mesa de Servicio',0),(N'lucianny.gonzalez',N'Lucianny González',N'Mesa de Servicio',0),(N'dianna.ogando',N'Dianna Ogando',N'Mesa de Servicio',0),
(N'manauris.castro',N'Manauris Castro',N'Mensajero Técnico',1),(N'esmerlin.mateo',N'Esmerlin Mateo',N'Mensajero Técnico',1),(N'jeremi.mendez',N'Jeremi Méndez',N'Mensajero Técnico',1),(N'engel.mendez',N'Engel Méndez',N'Mensajero Técnico',1),(N'alexander.dieses',N'Alexander Dieses',N'Mensajero Técnico',1),(N'jose.alcantara',N'José Alcántara',N'Mensajero Técnico',1);

MERGE dbo.admin_users AS target
USING(SELECT username+N'@grupotejeda.local' email,display_name,area,
    CASE WHEN area=N'Mesa de Servicio' THEN 'CALL_CENTER' WHEN area=N'Mensajero Técnico' THEN 'TECHNICIANS' ELSE 'TECHNICAL_FAILURE' END support_team
    FROM @users) AS source
ON target.email=source.email
WHEN MATCHED THEN UPDATE SET display_name=source.display_name,role='TECHNOLOGY',region=source.area,support_team=source.support_team,is_active=1,
    permissions_json=CASE WHEN source.support_team='TECHNICIANS' THEN @technician ELSE @support END,updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,must_change_password,permissions_json,support_team)
VALUES(CONVERT(char(36),NEWID()),source.email,source.display_name,'TECHNOLOGY',@salt,@hash,210000,1,source.area,1,CASE WHEN source.support_team='TECHNICIANS' THEN @technician ELSE @support END,source.support_team);

COMMIT TRANSACTION;
SELECT email,display_name,region,support_team,CASE WHEN JSON_VALUE(permissions_json,'$.canAssignTickets')='true' THEN N'Soporte' ELSE N'Técnico' END perfil,is_active,must_change_password
FROM dbo.admin_users WHERE email IN(SELECT username+N'@grupotejeda.local' FROM @users) ORDER BY region,display_name;
