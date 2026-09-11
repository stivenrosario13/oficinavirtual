import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const read = (path: string) =>
  readFileSync(resolve(process.cwd(), path), "utf8");

const program = read("api/Program.cs");
const database = read("api/DatabaseSqlServerScoped.cs");
const agencyLocationSeedDatabase = read("api/DatabaseAgencyLocationSeed.cs");
const agencyLocationSeed = JSON.parse(read("api/Data/agency-locations-sonadora-20260907.json")) as Array<{codigo:string;direccion:string|null;latitude:number;longitude:number}>;
const migration = read(
  "database-mssql/028_department_ticket_workflow_history_notifications.sql",
);
const technicianTeamMigration = read("database-mssql/031_technology_technicians_team.sql");
const findingMigration = read("database-mssql/018_separate_automatic_findings.sql");
const maintenanceMigration = read("database-mssql/032_maintenance_inventory_movements.sql");
const maintenanceOperationsMigration = read("database-mssql/041_maintenance_workshop_warehouse_qr_forms.sql");
const documentPdf = read("src/components/vite/maintenanceDocumentPdf.ts");
const documentDatabase = read("api/DatabaseMaintenanceDocuments.cs");
const center = read("src/components/vite/TicketCenter.tsx");
const auditForm = read("src/components/vite/EmployeeAuditForm.tsx");
const app = read("src/App.tsx");
const chat = read("src/components/vite/SupportChat.tsx");
const styles = read("src/notification-links.css");
const admin = read("src/components/vite/AdminPanel.tsx");
const session = read("src/lib/session.ts");
const directoryStyles = read("src/ticket-area-agency-directory-v190.css");
const supervisorStyles = read("src/supervisor-pending-v229.css");

describe("flujo departamental de tickets", () => {
  it("obliga al supervisor a justificar el escalamiento y adjuntar evidencia", () => {
    expect(program).toContain('scope.Role=="GroupAdministrator"');
    expect(program).toContain("Los supervisores deben justificar el escalamiento y adjuntar evidencia fotográfica.");
    expect(program).toContain('form.Files.GetFiles("evidence")');
    expect(database).toContain('"SUPERVISOR_ESCALATED"');
    expect(database).toContain("creationEvidence");
    expect(center).toContain("supervisor-ticket-escalation-evidence");
    expect(center).toContain("Evidencia obligatoria del supervisor");
    expect(center).toContain("supervisorEscalationReason.trim().length < 15");
    expect(center).toContain('form.append("evidence"');
  });

  it("genera un comprobante institucional con QR, logos y sello de recibido", () => {
    expect(documentPdf).toContain('fetch("/loto-real-logo-transparent.png")');
    expect(documentPdf).toContain("CONDUCE / FORMULARIO");
    expect(documentPdf).toContain('pdf.text("Grupo Tejeda"');
    expect(documentPdf).toContain("brandLockup(12,8,68)");
    expect(documentPdf).toContain("brandLockup(x+2,y+1,45)");
    expect(documentPdf).toContain("CADENA DE CUSTODIA");
    expect(documentPdf).toContain("Login receptor");
    expect(documentPdf).toContain("pdf.text(stampLabel");
    expect(center).toContain("PDF institucional + QR");
    expect(database).toContain("received_by_login");
    expect(database).not.toContain("COALESCE(receiver.email,creator.email)");
    expect(documentDatabase).toContain("received_at=SYSUTCDATETIME()");
    expect(documentDatabase).toContain("receiver_login=@login");
    expect(program).toContain("ReceivedByLogin");
  });

  it("expone en el API las operaciones de ruta, uso compartido, resolución y reportes", () => {
    expect(program).toContain('MapPut("/api/tickets/{id:guid}/route"');
    expect(program).toContain('MapPut("/api/tickets/{id:guid}/share"');
    expect(program).toContain('MapPost("/api/tickets/{id:guid}/resolve"');
    expect(program).toContain(
      'MapGet("/api/tickets/evidence/{evidenceId:guid}"',
    );
    expect(program).toContain('MapGet("/api/tickets/statistics"');
    expect(program).toContain('RequireAuthorization("TicketRouting")');
    expect(program).toContain('HasPermission(context.User,"canResolveTickets")');
  });

  it("separa Call Center, Avería Técnica y Técnicos y restringe la asignación", () => {
    expect(database).toContain('supportTeam=="CALL_CENTER"');
    expect(database).toContain('team="CALL_CENTER"');
    expect(database).toContain("ValidTicketAssignee");
    expect(database).toContain("u.support_team='TECHNICIANS'");
    expect(database).toContain("JSON_VALUE(u.permissions_json,'$.canResolveTickets')='true'");
    expect(center).toContain("item.isTechnician");
    expect(center).toContain("Call Center solo puede crear tickets internos.");
    expect(center).toContain("Escalar a Avería Técnica");
    expect(center).toContain('technician.supportTeam===assignedTeam');
    expect(center).toContain('assigneeTeam(event.target.value');
    expect(database).toContain("(@team='CALL_CENTER' AND u.support_team='CALL_CENTER')");
    expect(database).toContain("(@team='TECHNICAL_FAILURE' AND u.support_team='TECHNICAL_FAILURE')");
  });

  it("mantiene completos los tickets internos y permite localizar responsables por nombre", () => {
    expect(center).toContain("normalizeSupportTicket");
    expect(center).toContain('category: ticketText(raw.category, "OTHER")');
    expect(center).toContain('subject: ticketText(raw.subject, "Ticket sin asunto")');
    expect(center).toContain('placeholder="Buscar responsable por nombre..."');
    expect(center).toContain("matchesResponsibleQuery");
    expect(center).toContain('item.supportTeam==="CALL_CENTER"||item.supportTeam==="TECHNICAL_FAILURE"||item.supportTeam==="TECHNICIANS"');
    expect(program).toContain("string? AssignedTeam=null");
    expect(database).toContain("assigned_team=@team");
    expect(database).toContain("var requestedTeam=string.IsNullOrWhiteSpace(body.AssignedTeam)?state.Team:body.AssignedTeam.Trim().ToUpperInvariant();");
  });

  it("obliga a seleccionar el área antes de abrir cada bandeja", () => {
    expect(center).toContain("Selecciona el área de Tecnología");
    expect(center).toContain("Después podrás abrir la bandeja activa");
    expect(center).toContain('setTechnologyTeamFilter("CALL_CENTER")');
    expect(center).toContain('setTechnologyTeamFilter("TECHNICAL_FAILURE")');
    expect(center).toContain('setTechnologyTeamFilter("TECHNICIANS")');
    expect(center).toContain('setTicketWorkspaceView("menu")');
    expect(center).toContain("ticketWorkspaceView === \"menu\" && teamViewChosen");
  });

  it("limita la actualización de agencias al administrador principal y Franklin", () => {
    expect(program).toContain('AddPolicy("AgencyDirectoryManage"');
    expect(program).toContain('MapPut("/api/technology/agencies/{id:guid}"');
    expect(program).toContain('IsFranklinAgencyAdministrator');
    expect(program).toContain('account.Role=="Technology"&&IsFranklinAgencyAdministrator(account.Email)');
    expect(program).toContain('RequireAuthorization("AgencyDirectoryManage")');
    expect(database).toContain("UpdateAgencyDirectory");
    expect(database).toContain("AGENCY_DIRECTORY_UPDATED");
    expect(center).toContain("Actualizar agencias");
    expect(center).toContain("agency-directory-editor");
    expect(session).toContain("canManageAgencies");
    expect(directoryStyles).toContain(".agency-directory-workspace");
    expect(directoryStyles).toContain(".agency-directory-board.has-editor .agency-directory-list");
  });

  it("importa agencias nuevas desde Excel con plantilla, validación, duplicados y auditoría", () => {
    expect(program).toContain('MapPost("/api/technology/agencies/import"');
    expect(program).toContain("ValidateAgencyDirectoryImport");
    expect(program).toContain('RequireAuthorization("AgencyDirectoryManage")');
    expect(database).toContain("ImportAgencyDirectory");
    expect(database).toContain("WITH(UPDLOCK,HOLDLOCK)");
    expect(database).toContain("AGENCY_DIRECTORY_EXCEL_IMPORTED");
    expect(center).toContain("downloadAgencyDirectoryTemplate");
    expect(center).toContain("readAgencyDirectoryExcel");
    expect(center).toContain("Revisión antes de importar");
    expect(center).toContain("Subir agencias");
    expect(center).toContain('header:"Código *"');
    expect(center).toContain('header:"Dirección"');
    expect(center).toContain("municipio:municipio||null");
    expect(database).toContain("EnsureAgencyDirectorySchema");
    expect(database).toContain("directory_direccion");
    expect(database).toContain("COALESCE(p.municipio,a.directory_municipio)");
    expect(app).toContain('window.addEventListener("support-live-refresh", refreshAgencyCatalog)');
    expect(admin).toContain('window.addEventListener("support-live-refresh", refreshAgencyCatalog)');
    expect(directoryStyles).toContain(".agency-import-preview");
  });

  it("sincroniza una sola vez las direcciones y coordenadas del archivo Soñadora", () => {
    expect(agencyLocationSeed).toHaveLength(3403);
    expect(new Set(agencyLocationSeed.map((item) => item.codigo)).size).toBe(3403);
    expect(agencyLocationSeed.every((item) => item.latitude >= -90 && item.latitude <= 90 && item.longitude >= -180 && item.longitude <= 180)).toBe(true);
    expect(agencyLocationSeedDatabase).toContain('AgencyLocationSeedId = "SONADORA-2026-09-07"');
    expect(agencyLocationSeedDatabase).toContain("dbo.agency_location_imports");
    expect(agencyLocationSeedDatabase).toContain("AGENCY_LOCATIONS_SYNCHRONIZED");
    expect(database).toContain("EnsureBundledAgencyLocations(connection,ct)");
  });

  it("clasifica hallazgos y mantenimiento por el departamento propietario", () => {
    expect(database).toContain("DAMAGE_FURNITURE");
    expect(database).toContain("DAMAGE_ELECTRICAL");
    expect(database).toContain("DAMAGE_SHUTTER");
    expect(database).toContain("DAMAGE_GENERATOR_REQUEST");
    expect(database).toContain("damage.Department");
    expect(database).toContain("EnsureMaintenanceSchema");
    expect(database).toContain("TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES");
    expect(center).toContain("automaticFindingCategory");
    expect(auditForm).toContain('value: "GENERAL_PAINTING"');
    expect(auditForm).toContain('value: "GENERAL_ELECTRICAL"');
    expect(auditForm).toContain('value: "GENERAL_SHUTTER"');
    expect(auditForm).toContain('label: "Otro servicio general"');
    expect(center).toContain("Reestructuración física / Sheetrock");
    expect(center).toContain("Otro servicio general");
    expect(center).toContain("Redes y cableado de datos");
    expect(center).toContain("Nómina e incentivos");
    expect(center).toContain("maintenanceDepartmentCatalog");
    expect(center).toContain("Máquina 2Connect (Todo en uno)");
    expect(center).toContain("Impresora POS V8");
    expect(center).toContain("DS / Cajita de Raza o Perros");
    expect(center).toContain("Adaptador DP a HDMI");
    expect(center).toContain("Fuente de escáner Witek");
    expect(center).toContain("Cable de corriente / Power cord");
    expect(center).toContain("humanResourcesEquipment");
    expect(center).toContain("departmentMaintenanceMovements");
    expect(center).toContain('openMaintenanceForm(true,"WORKSHOP")');
    expect(center).toContain("maintenanceScannerBufferRef");
    expect(center).toContain("applyMaintenanceScan");
    expect(center).toContain("maintenance-inventory-table");
    expect(center).toContain("Registro inicial de activo");
    expect(center).toContain("Taller y Almacén");
    expect(center).toContain("Descargo definitivo");
    expect(center).toContain("downloadMaintenanceForm");
    expect(documentPdf).toContain('import("qrcode")');
    expect(center).toContain("Firmas de entrega y recepción");
    expect(center).toContain("if(savedMovement)setDocumentToEdit(savedMovement)");
    expect(findingMigration).toContain("'GENERAL_SERVICES'',''HIGH''");
    expect(maintenanceMigration).toContain("'HUMAN_RESOURCES'");
    expect(maintenanceOperationsMigration).toContain("operational_area");
    expect(maintenanceOperationsMigration).toContain("'DISCHARGE'");
    expect(maintenanceOperationsMigration).toContain("signature_data");
    expect(maintenanceOperationsMigration).toContain("UX_maintenance_qr_token");
    expect(maintenanceOperationsMigration).toContain("DROP CONSTRAINT FK_maintenance_agency");
    expect(maintenanceOperationsMigration).toContain("DROP INDEX IX_maintenance_agency_date");
    expect(maintenanceOperationsMigration).toContain("ALTER COLUMN agency_id char(36) NULL");
    expect(maintenanceOperationsMigration).toContain("FOREIGN KEY(agency_id) REFERENCES dbo.agencies(id)");
    expect(center).toContain("maintenanceRetryAfterRef");
    expect(center).toContain("maintenance-service-warning");
    expect(center).toContain("maintenanceProductSuggestion");
    expect(center).toContain("maintenance-product-catalog");
    expect(center).toContain("El sistema guardará la relación");
    expect(maintenanceOperationsMigration).toContain("dbo.maintenance_products");
    expect(maintenanceOperationsMigration).toContain("UQ_maintenance_products_department_code");
    expect(maintenanceOperationsMigration).toContain("MERGE dbo.maintenance_products");
  });

  it("permite anular y cambiar responsable solo al administrador o al soporte propietario", () => {
    expect(database).toContain("CanAdministerTicket");
    expect(database).toContain("Solo el administrador o el soporte del área propietaria");
    expect(database).toContain('state.TechnicianId is null?"ASSIGNED":"REASSIGNED"');
    expect(center).toContain("canAdministerTicket");
    expect(center).toContain("Estado / resolver");
    expect(center).toContain("Confirmar anulación");
    expect(center).toContain("Cambiar responsable");
    expect(center).toContain("finding-ticket-card");
    expect(center).toContain("Ticket completo del hallazgo");
  });

  it("permite que el supervisor vea y atienda todos los casos asignados por soporte", () => {
    expect(database).toContain("u.role='GROUP_ADMIN'");
    expect(database).toContain("ug.group_name=@agencyGroup");
    expect(database).toContain(
      "@groupSupervisor=1 AND (t.assigned_technician_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)))",
    );
    expect(center).toContain("isAssignedInternalSupervisor");
    expect(admin).not.toContain('fetch("/api/tickets")');
    expect(admin).not.toContain("Tickets de soporte asignados");
    expect(center).not.toContain("Bandeja de tickets asignados");
    expect(center).not.toContain("supervisor-assigned-table-wrap");
    expect(center).toContain('className="ticket-board"');
    expect(center).toContain('className="ticket-list"');
    expect(center).toContain('(supervisorHistoryView || personalAssignmentView || matchesTicketKind)');
    expect(center).toContain("(!supervisorHistoryView || successfulStatuses.has(ticket.status))");
    expect(center).toContain("Casos abiertos, en proceso o pendientes asignados directamente a tu cuenta.");
    expect(center).toContain("Casos resueltos o cerrados en los que participaste.");
    expect(center).toContain("successfulStatuses.has(ticket.status)");
    expect(center).not.toContain('className="supervisor-ticket-tabs"');
    expect(center).not.toContain("Mis tickets solucionados");
    expect(app).toContain('ticketId: item.kind === "TICKET" ? item.id : undefined');
    expect(center).not.toContain("Casos asignados a mi supervisión");
    expect(center).not.toContain("supervisor-ticket-inbox-table");
    expect(center).toContain('optgroup label="Todos los supervisores"');
  });

  it("conserva tickets y construcciones pendientes con motivo y permite reanudarlos", () => {
    expect(program).toContain('["OPEN","IN_PROGRESS","PENDING","RESOLVED","CLOSED","CANCELLED"]');
    expect(program).toContain('MapPut("/api/agency-transitions/{id:guid}/status"');
    expect(database).toContain("status IN('ACTIVE','PENDING','COMPLETED','CANCELLED')");
    expect(database).toContain("El ticket quedó pendiente");
    expect(database).toContain("Proceso puesto en pendiente");
    expect(center).toContain('PENDING: "Pendiente"');
    expect(center).toContain("Guardar como pendiente");
    expect(center).toContain("Reanudar proceso");
    expect(center).toContain("<span><b>1</b> Servicios Generales <small>Remodelación y adecuación de la agencia</small></span>");
    expect(center).toContain("<span><b>2</b> Tecnología <small>Instalación y ejecución del trabajo técnico</small></span>");
    expect(center).not.toContain("Servicios Generales + Taller");
    expect(center).not.toContain("Almacén + Tecnología");
    expect(center).toContain('const entryType=(entry.evidence?.length||0)>0?"ADVANCE":storedEntryType');
    expect(center).toContain('entry.progressStatus==="COMPLETED"&&(entry.evidence?.length||0)>0');
  });

  it("mueve cada asignación a en proceso y conserva la visibilidad del soporte", () => {
    expect(database).toContain(
      "status=CASE WHEN @technician IS NULL THEN 'OPEN' ELSE 'IN_PROGRESS' END",
    );
    expect(database).toContain(
      "assigned_technician_id IS NOT NULL AND status='OPEN'",
    );
    expect(database).toContain("@supportTeam='CALL_CENTER' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER'");
    expect(database).toContain("UPDATE t SET assigned_team='TECHNICAL_FAILURE'");
    expect(database).toContain("creator.support_team='CALL_CENTER'");
    expect(center).toContain("showingMyAssignedWork");
    expect(center).toContain("belongsToSelectedTechnologyTeam");
    expect(center).toContain(
      'ticket.assignedTechnicianId === session.id',
    );
  });

  it("obliga a cerrar con evidencia de imagen válida y conserva trazabilidad", () => {
    expect(program).toContain("form.Files.Count is < 1 or > 6");
    expect(program).toContain("ValidEvidenceImage");
    expect(database).toContain("dbo.support_ticket_evidence");
    expect(database).toContain("dbo.support_ticket_history");
    expect(database).toContain(
      "Para resolver o cerrar el ticket debes adjuntar evidencia",
    );
    expect(center).toContain("Resolver con evidencia");
    expect(center).toContain("Historial y evidencias del caso");
  });

  it("notifica al supervisor al abrir y resolver mediante eventos independientes", () => {
    expect(database).toContain("dbo.support_ticket_notifications");
    expect(database).toContain('id,"CREATED"');
    expect(database).toContain("notifySupervisors");
    expect(database).toContain('body.Status=="CANCELLED"?"anulado":"resuelto"');
    expect(app).toContain('CREATED: "Caso abierto"');
    expect(app).toContain('RESOLVED: "Caso resuelto"');
    expect(app).toContain("normalizeTicketNotification");
    expect(app).toContain("notificationId");
    expect(app).toContain("ticketType");
    expect(app).toContain("unreadTicketNotifications");
    expect(app).toContain("unreadChatNotifications");
    expect(app).not.toContain("ticketAlerts");
    expect(database).toContain("SetNotificationState");
  });

  it("mantiene Avería Técnica dentro de Tickets y separa cada origen", () => {
    const tickets = center.indexOf("<strong>Tickets</strong>");
    const internal = center.indexOf("<strong>Tickets internos</strong>", tickets);
    const transitions = center.indexOf("<strong>Agencias en construcción</strong>", internal);
    const maintenance = center.indexOf("<strong>Mantenimiento y equipos</strong>", transitions);
    const findings = center.indexOf("<strong>Hallazgos automáticos</strong>", maintenance);
    const failureInsideTickets = center.indexOf("<strong>Avería Técnica</strong>", findings);
    expect([tickets, internal, transitions, maintenance, findings, failureInsideTickets].every(index => index >= 0)).toBe(true);
    expect(tickets).toBeLessThan(internal);
    expect(internal).toBeLessThan(transitions);
    expect(transitions).toBeLessThan(maintenance);
    expect(maintenance).toBeLessThan(findings);
    expect(findings).toBeLessThan(failureInsideTickets);
    expect(center).not.toContain("technical-failure-only");
    expect(center).not.toContain("technicians-from-failure");
    expect(center).toContain("ticket.ticketType===ticketTypeFilter");
    expect(center).toContain('showFullSupportHome=!isTechnologyAreaSupport||session.supportTeam==="TECHNICAL_FAILURE"');
  });

  it("incluye historial y gráficas por técnico, categoría y departamento", () => {
    expect(database).toContain("TicketStatistics");
    expect(center).toContain('"technician" | "category" | "department"');
    expect(center).toContain("Gráfica de barras de casos resueltos");
    expect(center).toContain("Historial de resoluciones");
    expect(center).toContain("Bandeja activa");
    expect(center).toContain("Reportes y estadísticas");
  });

  it("mantiene iguales los conteos de tarjetas, bandejas y reportes por tipo y área", () => {
    expect(center).toContain('const supportTicketCount=workspaceTickets.filter(ticket=>ticket.ticketType==="SUPPORT").length');
    expect(center).toContain('const internalTicketCount=workspaceTickets.filter(ticket=>ticket.ticketType==="INTERNAL").length');
    expect(center).toContain('const supervisorAssignedTickets=workspaceTickets.filter(ticket=>ticket.ticketType==="SUPPORT"');
    expect(center).toContain('ticket.ticketType==="SUPPORT"&&ticket.assignedTechnicianId===session.id');
    expect(center).toContain('{countedTickets.length} tickets · {adminControlOpen ? "Ocultar" : "Abrir control"}');
    expect(center).toContain('{countedTickets.map((ticket) => (');
    expect(center).toContain('if (ticket.ticketType !== ticketTypeFilter) return false;');
    expect(center).toContain('const selectedTechnologyTeam = lockedTechnologyTeam || technologyTeamFilter');
    expect(center).toContain('setTicketWorkspaceView("process");');
    expect(center).not.toContain('setTicketWorkspaceView("process");\n              setStatusFilter("IN_PROGRESS");');
    expect(center).not.toContain('if (ticketStatistics && reportPeriod === "ALL"');
    expect(center).not.toContain('if (ticketStatistics?.history.length');
  });

  it("carga todos los hallazgos reales pendientes y conserva categorías no catalogadas", () => {
    expect(center).toContain('(finding) => !resolvedStatuses.has(finding.status)');
    expect(center).toContain('? resolvedStatuses.has(finding.status)');
    expect(center).toContain(': !resolvedStatuses.has(finding.status)');
    expect(center).toContain('key:finding.findingKey||"OTHER"');
    expect(center).not.toContain('(findingAssigneeKind !== "ALL" || !finding.assignedTechnicianId)');
    expect(database).toContain('FROM dbo.automatic_findings af');
  });

  it("persiste equipo y permiso granular desde la administración", () => {
    expect(session).toContain('| "WAREHOUSE"');
    expect(session).toContain('| "WORKSHOP"');
    expect(session).toContain("canAssignTickets: boolean");
    expect(admin).toContain('key: "canAssignTickets"');
    expect(admin).toContain("Equipo de Tecnología");
    expect(admin).toContain('value="CALL_CENTER"');
    expect(admin).toContain('value="TECHNICAL_FAILURE"');
    expect(admin).toContain('value="TECHNICIANS"');
    expect(migration).toContain("CK_admin_users_support_team");
    expect(technicianTeamMigration).toContain("'TECHNICIANS'");
  });

  it("adapta el centro de notificaciones y el chat a pantallas móviles", () => {
    expect(styles).toContain(".notification-center");
    expect(styles).toContain("height: 100dvh");
    expect(styles).toContain("env(safe-area-inset-bottom)");
    expect(styles).toContain(".chat-mobile-back");
    expect(chat).toContain("Volver a la lista de chats");
    expect(chat).toContain("Cerrar chat");
  });
});






