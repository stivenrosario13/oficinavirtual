-- MonsterASP: selecciona primero la base creada desde el panel (por ejemplo db64413).
-- El hosting no permite CREATE DATABASE ni USE con un nombre diferente.
IF OBJECT_ID(N'dbo.licenses',N'U') IS NULL
CREATE TABLE dbo.licenses(
 id BIGINT IDENTITY(1,1) PRIMARY KEY,
 license_key VARCHAR(100) NOT NULL UNIQUE,
 client_name NVARCHAR(150) NOT NULL,
 domain VARCHAR(255) NOT NULL,
 installation_id VARCHAR(128) NULL,
 status VARCHAR(20) NOT NULL CONSTRAINT DF_licenses_status DEFAULT 'ACTIVE',
 maintenance_message NVARCHAR(500) NULL,
 expires_at DATETIME2(3) NULL,
 last_check_at DATETIME2(3) NULL,
 last_ip VARCHAR(45) NULL,
 created_at DATETIME2(3) NOT NULL CONSTRAINT DF_licenses_created DEFAULT SYSUTCDATETIME(),
 updated_at DATETIME2(3) NOT NULL CONSTRAINT DF_licenses_updated DEFAULT SYSUTCDATETIME(),
 CONSTRAINT CK_licenses_status CHECK(status IN('ACTIVE','MAINTENANCE','SUSPENDED','EXPIRED','REVOKED'))
);
GO
IF OBJECT_ID(N'dbo.license_logs',N'U') IS NULL
CREATE TABLE dbo.license_logs(
 id BIGINT IDENTITY(1,1) PRIMARY KEY,
 license_id BIGINT NULL REFERENCES dbo.licenses(id) ON DELETE SET NULL,
 event_type VARCHAR(50) NOT NULL,
 installation_id VARCHAR(128) NULL,
 domain VARCHAR(255) NULL,
 ip VARCHAR(45) NULL,
 details NVARCHAR(MAX) NULL,
 created_at DATETIME2(3) NOT NULL CONSTRAINT DF_license_logs_created DEFAULT SYSUTCDATETIME()
);
GO
CREATE OR ALTER TRIGGER dbo.TR_licenses_updated ON dbo.licenses AFTER UPDATE AS UPDATE l SET updated_at=SYSUTCDATETIME() FROM dbo.licenses l JOIN inserted i ON i.id=l.id;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_license_logs_created') CREATE INDEX IX_license_logs_created ON dbo.license_logs(created_at DESC);
GO
