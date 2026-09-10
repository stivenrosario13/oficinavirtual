SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.maintenance_movements',N'operational_area') IS NULL ALTER TABLE dbo.maintenance_movements ADD operational_area varchar(20) NOT NULL CONSTRAINT DF_maintenance_operational_area DEFAULT('WORKSHOP');
IF COL_LENGTH(N'dbo.maintenance_movements',N'document_number') IS NULL ALTER TABLE dbo.maintenance_movements ADD document_number varchar(40) NULL;
IF COL_LENGTH(N'dbo.maintenance_movements',N'qr_token') IS NULL ALTER TABLE dbo.maintenance_movements ADD qr_token nvarchar(200) NULL;
IF COL_LENGTH(N'dbo.maintenance_movements',N'delivered_by_name') IS NULL ALTER TABLE dbo.maintenance_movements ADD delivered_by_name nvarchar(160) NULL;
IF COL_LENGTH(N'dbo.maintenance_movements',N'received_by_name') IS NULL ALTER TABLE dbo.maintenance_movements ADD received_by_name nvarchar(160) NULL;
IF COL_LENGTH(N'dbo.maintenance_movements',N'destination_name') IS NULL ALTER TABLE dbo.maintenance_movements ADD destination_name nvarchar(200) NULL;
IF COL_LENGTH(N'dbo.maintenance_movements',N'signature_data') IS NULL ALTER TABLE dbo.maintenance_movements ADD signature_data nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.maintenance_movements',N'occurred_at') IS NULL ALTER TABLE dbo.maintenance_movements ADD occurred_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_occurred_at DEFAULT(SYSUTCDATETIME());
GO

UPDATE dbo.maintenance_movements SET document_number=CONCAT('LEG-',REPLACE(CONVERT(varchar(36),id),'-','')) WHERE document_number IS NULL;
UPDATE dbo.maintenance_movements SET qr_token=CONCAT('REAL-MNT|',document_number) WHERE qr_token IS NULL;
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'agency_id' AND is_nullable=0)
BEGIN
  IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'FK_maintenance_agency') ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT FK_maintenance_agency;
  IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'IX_maintenance_agency_date') DROP INDEX IX_maintenance_agency_date ON dbo.maintenance_movements;
  ALTER TABLE dbo.maintenance_movements ALTER COLUMN agency_id char(36) NULL;
END;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'FK_maintenance_agency') ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT FK_maintenance_agency FOREIGN KEY(agency_id) REFERENCES dbo.agencies(id);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'IX_maintenance_agency_date') CREATE INDEX IX_maintenance_agency_date ON dbo.maintenance_movements(agency_id,created_at DESC);
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_type' AND definition NOT LIKE '%TRANSFER_TO_WORKSHOP%') ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT CK_maintenance_type;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_type') ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_type CHECK(movement_type IN('ENTRY','EXIT','REQUEST','NEW_DELIVERY','DAMAGED_RETURN','TRANSFER_TO_WORKSHOP','REPLACEMENT','COMPONENT_REPLACEMENT','REPAIR','DISCHARGE'));
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_operational_area') ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_operational_area CHECK(operational_area IN('WORKSHOP','WAREHOUSE'));
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'UX_maintenance_document_number') CREATE UNIQUE INDEX UX_maintenance_document_number ON dbo.maintenance_movements(document_number) WHERE document_number IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'UX_maintenance_qr_token') CREATE UNIQUE INDEX UX_maintenance_qr_token ON dbo.maintenance_movements(qr_token) WHERE qr_token IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.maintenance_products',N'U') IS NULL
BEGIN
  CREATE TABLE dbo.maintenance_products(
    id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_products PRIMARY KEY DEFAULT(NEWID()),
    department varchar(40) NOT NULL,
    scan_code nvarchar(120) NOT NULL,
    product_name nvarchar(160) NOT NULL,
    component_type nvarchar(100) NULL,
    created_by_name nvarchar(160) NOT NULL,
    created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_products_created DEFAULT(SYSUTCDATETIME()),
    updated_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_products_updated DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT UQ_maintenance_products_department_code UNIQUE(department,scan_code),
    CONSTRAINT CK_maintenance_products_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'))
  );
  CREATE INDEX IX_maintenance_products_name ON dbo.maintenance_products(department,product_name);
END;
GO

MERGE dbo.maintenance_products AS target
USING(
  SELECT department,UPPER(LTRIM(RTRIM(serial_number))) scan_code,MAX(equipment_type) product_name,MAX(component_type) component_type,MAX(created_by_name) created_by_name
  FROM dbo.maintenance_movements
  WHERE serial_number IS NOT NULL AND LTRIM(RTRIM(serial_number))<>''
  GROUP BY department,UPPER(LTRIM(RTRIM(serial_number)))
) AS source
ON target.department=source.department AND target.scan_code=source.scan_code
WHEN NOT MATCHED THEN INSERT(department,scan_code,product_name,component_type,created_by_name) VALUES(source.department,source.scan_code,source.product_name,source.component_type,source.created_by_name);
GO

PRINT 'Taller, Almacen, formularios QR y firma digital habilitados.';
