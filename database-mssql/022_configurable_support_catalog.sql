SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.support_departments',N'U') IS NULL
CREATE TABLE dbo.support_departments(
 code varchar(40) NOT NULL PRIMARY KEY,
 name nvarchar(120) NOT NULL,
 description nvarchar(400) NULL,
 accent_color varchar(20) NOT NULL CONSTRAINT DF_support_departments_color DEFAULT('#38BDF8'),
 display_order int NOT NULL CONSTRAINT DF_support_departments_order DEFAULT(0),
 is_active bit NOT NULL CONSTRAINT DF_support_departments_active DEFAULT(1),
 created_at datetime2(3) NOT NULL CONSTRAINT DF_support_departments_created DEFAULT(SYSUTCDATETIME()),
 updated_at datetime2(3) NOT NULL CONSTRAINT DF_support_departments_updated DEFAULT(SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.support_categories',N'U') IS NULL
CREATE TABLE dbo.support_categories(
 id uniqueidentifier NOT NULL CONSTRAINT DF_support_categories_id DEFAULT(NEWID()) PRIMARY KEY,
 department_code varchar(40) NOT NULL,
 code varchar(60) NOT NULL,
 name nvarchar(160) NOT NULL,
 description nvarchar(500) NULL,
 default_priority varchar(15) NOT NULL CONSTRAINT DF_support_categories_priority DEFAULT('MEDIUM'),
 requires_detail bit NOT NULL CONSTRAINT DF_support_categories_detail DEFAULT(0),
 detail_label nvarchar(180) NULL,
 options_json nvarchar(max) NULL,
 display_order int NOT NULL CONSTRAINT DF_support_categories_order DEFAULT(0),
 is_active bit NOT NULL CONSTRAINT DF_support_categories_active DEFAULT(1),
 created_at datetime2(3) NOT NULL CONSTRAINT DF_support_categories_created DEFAULT(SYSUTCDATETIME()),
 updated_at datetime2(3) NOT NULL CONSTRAINT DF_support_categories_updated DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT FK_support_categories_department FOREIGN KEY(department_code) REFERENCES dbo.support_departments(code),
 CONSTRAINT UQ_support_categories_department_code UNIQUE(department_code,code),
 CONSTRAINT CK_support_categories_priority CHECK(default_priority IN('LOW','MEDIUM','HIGH','CRITICAL')),
 CONSTRAINT CK_support_categories_options_json CHECK(options_json IS NULL OR ISJSON(options_json)=1)
);

MERGE dbo.support_departments AS target USING(VALUES
 ('GENERAL_SERVICES',N'Servicios Generales',N'Infraestructura, energía, mantenimiento y adecuaciones físicas.','#38BDF8',10),
 ('TECHNOLOGY',N'Tecnología',N'Equipos, conectividad, redes, internet y sistemas.','#6366F1',20),
 ('HUMAN_RESOURCES',N'Recursos Humanos',N'Personal, licencias, nómina e incentivos.','#EC4899',30)
) source(code,name,description,color,sort) ON target.code=source.code
WHEN MATCHED THEN UPDATE SET name=source.name,description=source.description,accent_color=source.color,display_order=source.sort,is_active=1,updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(code,name,description,accent_color,display_order) VALUES(source.code,source.name,source.description,source.color,source.sort);

DECLARE @categories TABLE(department_code varchar(40),code varchar(60),name nvarchar(160),description nvarchar(500),priority varchar(15),requires_detail bit,detail_label nvarchar(180),options_json nvarchar(max),sort int);
INSERT INTO @categories VALUES
('GENERAL_SERVICES','GENERATOR_REQUEST',N'Requisición de planta eléctrica',N'Solicitud o reemplazo de planta eléctrica.','HIGH',1,N'Detalla capacidad, condición y necesidad',NULL,10),
('GENERAL_SERVICES','ELECTRICAL_FAILURE',N'Avería eléctrica',N'Fallas de energía, cableado o distribución.','CRITICAL',1,N'Describe la avería eléctrica',NULL,20),
('GENERAL_SERVICES','INVERTER_MAINTENANCE',N'Mantenimiento de inversor',N'Revisión preventiva o correctiva.','HIGH',1,N'Describe el estado del inversor',NULL,30),
('GENERAL_SERVICES','BATTERY_MAINTENANCE',N'Mantenimiento de batería',N'Revisión, sustitución o banco de baterías.','HIGH',1,N'Describe el estado de las baterías',NULL,40),
('GENERAL_SERVICES','PAINTING',N'Pintura',N'Mantenimiento de pintura interior o exterior.','LOW',1,N'Indica las áreas que requieren pintura',NULL,50),
('GENERAL_SERVICES','METALWORK',N'Herrería',N'Reparación de estructuras metálicas.','MEDIUM',1,N'Describe el trabajo de herrería',NULL,60),
('GENERAL_SERVICES','LIGHTING',N'Iluminación',N'Lámparas, luminarias o circuitos de iluminación.','MEDIUM',1,N'Indica el área sin iluminación',NULL,70),
('GENERAL_SERVICES','PHYSICAL_RESTRUCTURE',N'Reestructuración física / sheetrock',N'Adecuaciones, divisiones y reparaciones físicas.','HIGH',1,N'Describe la reestructuración requerida',NULL,80),
('GENERAL_SERVICES','SHUTTER_FAILURE',N'Falla en el shutter',N'Avería en cierre o persiana metálica.','HIGH',1,N'Describe la falla del shutter',NULL,90),
('GENERAL_SERVICES','OTHER',N'Otro servicio general',N'Incidencia no incluida en el catálogo.','MEDIUM',1,N'Detalla el soporte requerido',NULL,100),
('TECHNOLOGY','PRINTER_FAILURE',N'Falla en el printer',N'Impresora sin operar o con impresión defectuosa.','CRITICAL',1,N'Describe el error del printer',NULL,10),
('TECHNOLOGY','SCREEN_FAILURE',N'Falla en pantallas',N'Monitor o pantalla sin imagen o defectuosa.','HIGH',1,N'Describe la pantalla afectada',NULL,20),
('TECHNOLOGY','CONNECTIVITY_FAILURE',N'Falla de conectividad',N'Interrupción de comunicación o acceso.','CRITICAL',1,N'Describe la falla de conectividad',NULL,30),
('TECHNOLOGY','NETWORK_FAILURE',N'Falla de redes',N'Router, switch, cableado o red local.','CRITICAL',1,N'Describe la falla de red',NULL,40),
('TECHNOLOGY','INTERNET_FAILURE',N'Falla de internet',N'Servicio lento, intermitente o sin conexión.','CRITICAL',1,N'Describe el comportamiento del internet',NULL,50),
('TECHNOLOGY','SCANNER_FAILURE',N'Falla en escáner',N'Escáner no disponible o defectuoso.','HIGH',1,N'Describe el error del escáner',NULL,60),
('TECHNOLOGY','CPU_FAILURE',N'Falla CPU',N'CPU sin encender, lenta o con avería de hardware.','CRITICAL',1,N'Describe la falla de la CPU',NULL,65),
('TECHNOLOGY','SYSTEM_FAILURE',N'Falla en sistema',N'Error de aplicación o plataforma operativa.','CRITICAL',1,N'Indica sistema, mensaje y operación afectada',NULL,70),
('TECHNOLOGY','PERROS_SYSTEM_FAILURE',N'Falla en sistema Perros',N'Incidencia relacionada con el sistema Perros.','HIGH',1,N'Describe el error presentado',NULL,80),
('TECHNOLOGY','OTHER',N'Otro soporte tecnológico',N'Incidencia tecnológica no incluida.','MEDIUM',1,N'Detalla el soporte requerido',NULL,100),
('HUMAN_RESOURCES','STAFF_SHORTAGE',N'Falta de personal',N'Vacante o necesidad de cobertura operativa.','HIGH',1,N'Indica puesto, turno y motivo',NULL,10),
('HUMAN_RESOURCES','LEAVE_REQUEST',N'Licencia',N'Solicitud o seguimiento de licencia.','MEDIUM',1,N'Agrega las fechas y observaciones',N'["Médica","Embarazo / maternidad","Paternidad","Vacaciones","Estudios","Duelo","Matrimonio","Otra"]',20),
('HUMAN_RESOURCES','SALES_INCENTIVE_CLAIM',N'Reclamo de incentivo por ventas',N'Incentivo de ventas no reflejado en nómina.','HIGH',1,N'Indica período, monto esperado y nómina afectada',NULL,30),
('HUMAN_RESOURCES','PAYROLL_CLAIM',N'Reclamo de nómina',N'Diferencia o ausencia de pago.','HIGH',1,N'Detalla período y diferencia encontrada',NULL,40),
('HUMAN_RESOURCES','OTHER',N'Otro requerimiento de RR. HH.',N'Solicitud no incluida en el catálogo.','MEDIUM',1,N'Detalla el requerimiento',NULL,100);

MERGE dbo.support_categories target USING @categories source ON target.department_code=source.department_code AND target.code=source.code
WHEN MATCHED THEN UPDATE SET name=source.name,description=source.description,default_priority=source.priority,requires_detail=source.requires_detail,detail_label=source.detail_label,options_json=source.options_json,display_order=source.sort,is_active=1,updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(department_code,code,name,description,default_priority,requires_detail,detail_label,options_json,display_order) VALUES(source.department_code,source.code,source.name,source.description,source.priority,source.requires_detail,source.detail_label,source.options_json,source.sort);

UPDATE dbo.support_tickets
SET priority='CRITICAL', updated_at=SYSUTCDATETIME()
WHERE assigned_department='TECHNOLOGY' AND category='CPU_FAILURE' AND ISNULL(priority,'')<>'CRITICAL';

IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_department') ALTER TABLE dbo.support_tickets DROP CONSTRAINT CK_support_tickets_department;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_category') ALTER TABLE dbo.support_tickets DROP CONSTRAINT CK_support_tickets_category;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_role') ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_role;
ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_role CHECK(role IN('ADMINISTRATOR','VIEWER','GROUP_ADMIN','FISCALIZADOR','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'));

COMMIT TRANSACTION;
SELECT d.name,COUNT(c.id) AS categorias FROM dbo.support_departments d LEFT JOIN dbo.support_categories c ON c.department_code=d.code AND c.is_active=1 GROUP BY d.name,d.display_order ORDER BY d.display_order;
