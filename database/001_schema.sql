-- Registro de Agencias — MySQL 8 / MonsterASP.NET
-- Selecciona primero la base creada desde el panel de MonsterASP.NET.

CREATE TABLE IF NOT EXISTS agencies (
  id char(36) NOT NULL,
  source_key varchar(200) NOT NULL,
  codigo varchar(100) NOT NULL,
  terminal varchar(200) NOT NULL,
  grupo varchar(150) NOT NULL,
  expected_latitude double NULL,
  expected_longitude double NULL,
  status enum('PENDING','COMPLETED','REVIEW_REQUIRED') NOT NULL DEFAULT 'PENDING',
  created_at datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id), UNIQUE KEY uq_agencies_source_key (source_key),
  KEY ix_agencies_group_status (grupo,status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS agency_profiles (
  id char(36) NOT NULL, agency_id char(36) NOT NULL,
  employee_name varchar(160) NOT NULL, employee_code varchar(80) NULL,
  direccion varchar(300) NOT NULL, sector varchar(120) NOT NULL,
  municipio varchar(120) NOT NULL, provincia varchar(120) NOT NULL,
  tipo_establecimiento varchar(100) NOT NULL, tipo_establecimiento_otro varchar(120) NULL,
  latitude double NOT NULL, longitude double NOT NULL, accuracy_meters double NOT NULL,
  distance_from_agency_meters double NULL,
  location_captured_at datetime(3) NOT NULL, location_source varchar(40) NOT NULL,
  structural_answers json NOT NULL,
  observations text NULL,
  completion_percent tinyint unsigned NOT NULL DEFAULT 100,
  photo_data longblob NULL, photo_content_type varchar(100) NULL,
  submitted_at datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  submitted_by_context json NULL,
  PRIMARY KEY (id), UNIQUE KEY uq_profiles_agency (agency_id),
  CONSTRAINT fk_profiles_agency FOREIGN KEY (agency_id) REFERENCES agencies(id),
  CONSTRAINT ck_profiles_lat CHECK(latitude BETWEEN -90 AND 90),
  CONSTRAINT ck_profiles_lng CHECK(longitude BETWEEN -180 AND 180),
  CONSTRAINT ck_profiles_zero CHECK(NOT(latitude=0 AND longitude=0)),
  CONSTRAINT ck_profiles_accuracy CHECK(accuracy_meters>0),
  CONSTRAINT ck_profiles_completion CHECK(completion_percent BETWEEN 0 AND 100),
  CONSTRAINT ck_profiles_other CHECK(tipo_establecimiento<>'Otro' OR CHAR_LENGTH(TRIM(tipo_establecimiento_otro))>0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS audit_log (
  id bigint unsigned NOT NULL AUTO_INCREMENT,
  entity_type varchar(50) NOT NULL, entity_id char(36) NOT NULL,
  action varchar(80) NOT NULL, before_data json NULL, after_data json NULL,
  actor_email varchar(320) NULL, created_at datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id), KEY ix_audit_entity (entity_type,entity_id,created_at DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

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

CREATE TABLE IF NOT EXISTS audit_photos (
  id char(36) NOT NULL, profile_id char(36) NOT NULL,
  photo_type enum('frontal','operativa','entorno') NOT NULL,
  photo_data longblob NOT NULL, content_type varchar(100) NOT NULL,
  latitude double NOT NULL, longitude double NOT NULL, accuracy_meters double NOT NULL,
  captured_at datetime(3) NOT NULL,
  created_at datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY(id), UNIQUE KEY uq_photo_profile_type(profile_id,photo_type),
  CONSTRAINT fk_photos_profile FOREIGN KEY(profile_id) REFERENCES agency_profiles(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Ejemplo:
-- INSERT INTO agencies(id,source_key,codigo,terminal,grupo)
-- VALUES(UUID(),'AG-001','001','Agencia Central','Grupo A');
