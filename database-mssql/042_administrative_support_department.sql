SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.support_departments WITH (UPDLOCK, HOLDLOCK) WHERE code = 'ADMINISTRATIVE')
    INSERT INTO dbo.support_departments (code, name, description, accent_color, display_order, is_active)
    VALUES ('ADMINISTRATIVE', N'Departamento Administrativo', N'Solicitudes y gestiones administrativas.', '#0F766E', 40, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.support_categories WITH (UPDLOCK, HOLDLOCK) WHERE department_code = 'ADMINISTRATIVE' AND code = 'ADMINISTRATIVE_REQUEST')
    INSERT INTO dbo.support_categories (department_code, code, name, description, default_priority, requires_detail, detail_label, display_order, is_active)
    VALUES ('ADMINISTRATIVE', 'ADMINISTRATIVE_REQUEST', N'Solicitud administrativa', N'Gestión o requerimiento dirigido al Departamento Administrativo.', 'MEDIUM', 1, N'Describe la gestión administrativa requerida', 10, 1);

COMMIT TRANSACTION;
