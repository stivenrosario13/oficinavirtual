SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_support_team')
  ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_support_team;
ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_support_team
  CHECK(support_team IS NULL OR (role='TECHNOLOGY' AND support_team IN('CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS')));

IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_team')
  ALTER TABLE dbo.support_tickets DROP CONSTRAINT CK_support_tickets_team;
ALTER TABLE dbo.support_tickets WITH CHECK ADD CONSTRAINT CK_support_tickets_team
  CHECK(assigned_team IS NULL OR assigned_team IN('CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS'));

UPDATE dbo.admin_users
SET support_team='TECHNICIANS',updated_at=SYSUTCDATETIME()
WHERE role='TECHNOLOGY'
  AND JSON_VALUE(CASE WHEN ISJSON(permissions_json)=1 THEN permissions_json ELSE N'{}' END,'$.canAssignTickets')='false';

UPDATE dbo.support_tickets
SET assigned_team='TECHNICIANS',updated_at=SYSUTCDATETIME()
WHERE assigned_department='TECHNOLOGY'
  AND assigned_technician_id IS NOT NULL
  AND assigned_team<>'TECHNICIANS';

PRINT 'Equipo Técnicos habilitado como tercera área de Tecnología.';
