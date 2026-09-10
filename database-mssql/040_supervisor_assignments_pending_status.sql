SET NOCOUNT ON;

IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_status' AND definition NOT LIKE '%PENDING%')
  ALTER TABLE dbo.support_tickets DROP CONSTRAINT CK_support_tickets_status;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_status')
  ALTER TABLE dbo.support_tickets WITH CHECK ADD CONSTRAINT CK_support_tickets_status
    CHECK(status IN('OPEN','IN_PROGRESS','PENDING','RESOLVED','CLOSED','CANCELLED'));

IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.agency_transition_projects') AND name=N'CK_agency_transition_status' AND definition NOT LIKE '%PENDING%')
  ALTER TABLE dbo.agency_transition_projects DROP CONSTRAINT CK_agency_transition_status;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.agency_transition_projects') AND name=N'CK_agency_transition_status')
  ALTER TABLE dbo.agency_transition_projects WITH CHECK ADD CONSTRAINT CK_agency_transition_status
    CHECK(status IN('ACTIVE','PENDING','COMPLETED','CANCELLED'));

PRINT 'Casos de supervisores y estados pendientes habilitados.';
