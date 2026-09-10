-- Roles y permisos iniciales - SQL Server 2025 / MonsterASP.NET
-- Administrador: admin@local / admin123
-- Solo lectura: consulta@local / consulta123

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM dbo.admin_users WHERE email=N'admin@local')
BEGIN
    UPDATE dbo.admin_users
    SET display_name=N'Administrador principal',role='ADMINISTRATOR',is_active=1,updated_at=SYSUTCDATETIME()
    WHERE email=N'admin@local';
END
ELSE
BEGIN
    INSERT INTO dbo.admin_users
      (id,email,display_name,role,password_salt,password_hash,password_iterations,is_active)
    VALUES
      (CONVERT(char(36),NEWID()),N'admin@local',N'Administrador principal','ADMINISTRATOR',
       0xd133db154e637f90a0516bd36675ceb8,
       0x33b9c90b737dcf9614d845adf32d4166bac7badfe7b2c4cea0d7f816b8f75d9e,210000,1);
END;

IF EXISTS (SELECT 1 FROM dbo.admin_users WHERE email=N'consulta@local')
BEGIN
    UPDATE dbo.admin_users
    SET display_name=N'Consulta del panel',role='VIEWER',is_active=1,updated_at=SYSUTCDATETIME()
    WHERE email=N'consulta@local';
END
ELSE
BEGIN
    INSERT INTO dbo.admin_users
      (id,email,display_name,role,password_salt,password_hash,password_iterations,is_active)
    VALUES
      (CONVERT(char(36),NEWID()),N'consulta@local',N'Consulta del panel','VIEWER',
       0xfb2f928c0db0c300ace956123075a129,
       0x7536093dc4781b26a0c0b72b7bdeda1677f03b605d8ae1f3013df1badbf602cb,210000,1);
END;

COMMIT TRANSACTION;

SELECT email,display_name,role,is_active FROM dbo.admin_users ORDER BY role,email;
