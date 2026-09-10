-- Roles y permisos del panel administrativo — MySQL 8 / MonsterASP.NET
-- Ejecuta este archivo una vez en bases existentes. Es seguro volver a ejecutarlo.

CREATE TABLE IF NOT EXISTS admin_users (
  id char(36) NOT NULL,
  email varchar(320) NOT NULL,
  display_name varchar(160) NOT NULL,
  role enum('ADMINISTRATOR','VIEWER') NOT NULL DEFAULT 'VIEWER',
  password_salt binary(16) NOT NULL,
  password_hash binary(32) NOT NULL,
  password_iterations int unsigned NOT NULL DEFAULT 210000,
  is_active tinyint(1) NOT NULL DEFAULT 1,
  last_login_at datetime(3) NULL,
  created_at datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  UNIQUE KEY uq_admin_users_email (email),
  KEY ix_admin_users_role_active (role,is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Administrador inicial: admin@local / admin123
INSERT INTO admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active)
VALUES(UUID(),'admin@local','Administrador principal','ADMINISTRATOR',
       X'd133db154e637f90a0516bd36675ceb8',
       X'33b9c90b737dcf9614d845adf32d4166bac7badfe7b2c4cea0d7f816b8f75d9e',210000,1)
ON DUPLICATE KEY UPDATE display_name=VALUES(display_name),role='ADMINISTRATOR',is_active=1;

-- Usuario de consulta inicial: consulta@local / consulta123
INSERT INTO admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active)
VALUES(UUID(),'consulta@local','Consulta del panel','VIEWER',
       X'fb2f928c0db0c300ace956123075a129',
       X'7536093dc4781b26a0c0b72b7bdeda1677f03b605d8ae1f3013df1badbf602cb',210000,1)
ON DUPLICATE KEY UPDATE display_name=VALUES(display_name),role='VIEWER',is_active=1;

SELECT email,display_name,role,is_active FROM admin_users ORDER BY role,email;
