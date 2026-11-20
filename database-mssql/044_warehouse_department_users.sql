SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.admin_users',N'permissions_json') IS NULL
    ALTER TABLE dbo.admin_users ADD permissions_json nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.admin_users',N'support_team') IS NULL
    ALTER TABLE dbo.admin_users ADD support_team varchar(30) NULL;

IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_role')
    ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_role;
ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_role
    CHECK(role IN('ADMINISTRATOR','VIEWER','GROUP_ADMIN','FISCALIZADOR','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','WAREHOUSE'));

IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_support_team')
    ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_support_team;
ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_support_team
    CHECK(support_team IS NULL
       OR (role='TECHNOLOGY' AND support_team IN('CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS','WAREHOUSE','WORKSHOP'))
       OR (role='GENERAL_SERVICES' AND support_team='WORKSHOP')
       OR (role='WAREHOUSE' AND support_team='WAREHOUSE'));

-- Contraseña temporal institucional: 123456. El portal exige cambiarla al entrar.
DECLARE @salt varbinary(16)=0x7BDEAB3354DED475F071FA4B58B3AB62;
DECLARE @hash varbinary(32)=0x2096941A8EDD017184CAA3E80BA9CC5097824ABE84149E3FD91C58F049B7F8C5;
DECLARE @managerPermissions nvarchar(max)=N'{"canAuthorizeWarehouseRequests":true,"canLoadWarehouseInventory":true}';
DECLARE @operatorPermissions nvarchar(max)=N'{"canAuthorizeWarehouseRequests":false,"canLoadWarehouseInventory":false}';

DECLARE @users TABLE(
    email nvarchar(320),
    display_name nvarchar(160),
    position nvarchar(80),
    permissions_json nvarchar(max)
);
INSERT INTO @users(email,display_name,position,permissions_json) VALUES
(N'bryan.arias@grupotejeda.local',N'Bryan Arias',N'Gerente',@managerPermissions),
(N'diorly.abreu@grupotejeda.local',N'Diorly Abreu',N'Encargada',@managerPermissions),
(N'ana.rodriguez@grupotejeda.local',N'Ana Rodriguez',N'Operadora',@operatorPermissions),
(N'fernando.ortiz@grupotejeda.local',N'Fernando Ortiz',N'Operador',@operatorPermissions),
(N'jordan.hellys@grupotejeda.local',N'Jordan Hellys',N'Operador',@operatorPermissions),
(N'joan.matos@grupotejeda.local',N'Joan Matos',N'Operador',@operatorPermissions);

INSERT INTO dbo.admin_users
    (id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,must_change_password,permissions_json,support_team)
SELECT CONVERT(char(36),NEWID()),source.email,source.display_name,'WAREHOUSE',@salt,@hash,210000,1,
       N'Almacén · '+source.position,1,source.permissions_json,'WAREHOUSE'
FROM @users source
WHERE NOT EXISTS(SELECT 1 FROM dbo.admin_users existing WHERE LOWER(existing.email)=LOWER(source.email));

COMMIT TRANSACTION;

SELECT email,display_name,role,region,
       JSON_VALUE(permissions_json,'$.canAuthorizeWarehouseRequests') can_authorize,
       JSON_VALUE(permissions_json,'$.canLoadWarehouseInventory') can_load_inventory,
       is_active,must_change_password
FROM dbo.admin_users
WHERE email IN(SELECT email FROM @users)
ORDER BY CASE WHEN region LIKE N'%Gerente' THEN 1 WHEN region LIKE N'%Encargada' THEN 2 ELSE 3 END,display_name;
