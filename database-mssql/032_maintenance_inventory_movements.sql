SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.maintenance_movements',N'U') IS NULL
BEGIN
  CREATE TABLE dbo.maintenance_movements(
    id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_movements PRIMARY KEY DEFAULT NEWID(),
    agency_id char(36) NOT NULL,
    ticket_id uniqueidentifier NULL,
    department varchar(40) NOT NULL,
    technician_user_id nvarchar(80) NULL,
    technician_name nvarchar(160) NOT NULL,
    movement_type varchar(30) NOT NULL,
    equipment_type nvarchar(100) NOT NULL,
    component_type nvarchar(100) NULL,
    failure_cause nvarchar(200) NOT NULL,
    serial_number nvarchar(120) NULL,
    quantity int NOT NULL CONSTRAINT DF_maintenance_quantity DEFAULT 1,
    notes nvarchar(2000) NULL,
    created_by_user_id nvarchar(80) NULL,
    created_by_name nvarchar(160) NOT NULL,
    created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_maintenance_agency FOREIGN KEY(agency_id) REFERENCES dbo.agencies(id),
    CONSTRAINT FK_maintenance_ticket FOREIGN KEY(ticket_id) REFERENCES dbo.support_tickets(id),
    CONSTRAINT CK_maintenance_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')),
    CONSTRAINT CK_maintenance_type CHECK(movement_type IN('ENTRY','EXIT','REPLACEMENT','COMPONENT_REPLACEMENT','REPAIR')),
    CONSTRAINT CK_maintenance_quantity CHECK(quantity BETWEEN 1 AND 1000)
  );
  CREATE INDEX IX_maintenance_department_date ON dbo.maintenance_movements(department,created_at DESC);
  CREATE INDEX IX_maintenance_technician_date ON dbo.maintenance_movements(technician_user_id,created_at DESC);
  CREATE INDEX IX_maintenance_agency_date ON dbo.maintenance_movements(agency_id,created_at DESC);
END;

PRINT 'Control de mantenimiento, equipos y componentes habilitado.';
