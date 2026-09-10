SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.admin_users')
      AND name = N'CK_admin_users_role'
)
    ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_role;

ALTER TABLE dbo.admin_users WITH CHECK
ADD CONSTRAINT CK_admin_users_role
CHECK (role IN ('ADMINISTRATOR','VIEWER','GROUP_ADMIN','FISCALIZADOR'));

ALTER TABLE dbo.admin_users CHECK CONSTRAINT CK_admin_users_role;

COMMIT TRANSACTION;

SELECT role, COUNT(*) AS usuarios
FROM dbo.admin_users
GROUP BY role
ORDER BY role;
