import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const program = readFileSync(resolve(process.cwd(), "api/Program.cs"), "utf8");
const apiProject = readFileSync(
  resolve(process.cwd(), "api/RegistroAgencias.Api.csproj"),
  "utf8",
);
const webConfig = readFileSync(
  resolve(process.cwd(), "api/web.config"),
  "utf8",
);
const releaseGuard = readFileSync(
  resolve(process.cwd(), "public/release-guard.js"),
  "utf8",
);
const manifest = readFileSync(
  resolve(process.cwd(), "public/manifest.webmanifest"),
  "utf8",
);
const serviceWorker = readFileSync(
  resolve(process.cwd(), "public/sw.js"),
  "utf8",
);
const pushService = readFileSync(
  resolve(process.cwd(), "api/PwaPushService.cs"),
  "utf8",
);
const database = readFileSync(
  resolve(process.cwd(), "api/DatabaseSqlServerScoped.cs"),
  "utf8",
);
const migration = readFileSync(
  resolve(
    process.cwd(),
    "database-mssql/016_ticket_technical_roles_and_alerts.sql",
  ),
  "utf8",
);
const supportCatalogMigration = readFileSync(
  resolve(process.cwd(), "database-mssql/022_configurable_support_catalog.sql"),
  "utf8",
);
const workshopWorkflow = readFileSync(
  resolve(process.cwd(), "api/WorkshopWorkflow.cs"),
  "utf8",
);
const app = readFileSync(resolve(process.cwd(), "src/App.tsx"), "utf8");
const auditForm = readFileSync(
  resolve(process.cwd(), "src/components/vite/EmployeeAuditForm.tsx"),
  "utf8",
);
const applicationIndex = readFileSync(resolve(process.cwd(), "index.html"), "utf8");
const center = readFileSync(
  resolve(process.cwd(), "src/components/vite/TicketCenter.tsx"),
  "utf8",
);
const operationsCenter = readFileSync(
  resolve(process.cwd(), "src/components/vite/OperationsCenter.tsx"),
  "utf8",
);
const operationsStyles = readFileSync(
  resolve(process.cwd(), "src/operations-center-v250.css"),
  "utf8",
);
const professionalSidebarV254 = readFileSync(
  resolve(process.cwd(), "src/professional-sidebar-v254.css"),
  "utf8",
);
const designSystemV256 = readFileSync(
  resolve(process.cwd(), "src/design-system-v256.css"),
  "utf8",
);
const dashboardModernV258 = readFileSync(
  resolve(process.cwd(), "src/dashboard-modern-v258.css"),
  "utf8",
);
const unifiedWorkspaceV260 = readFileSync(
  resolve(process.cwd(), "src/unified-workspace-v260.css"),
  "utf8",
);
const workflowProfessionalV261 = readFileSync(
  resolve(process.cwd(), "src/workflow-professional-v261.css"),
  "utf8",
);
const mobileDesktopColorParityV264 = readFileSync(
  resolve(process.cwd(), "src/mobile-desktop-color-parity-v264.css"),
  "utf8",
);
const maintenanceProcurementV267 = readFileSync(
  resolve(process.cwd(), "src/maintenance-procurement-v267.css"),
  "utf8",
);
const chat = readFileSync(
  resolve(process.cwd(), "src/components/vite/SupportChat.tsx"),
  "utf8",
);
const supportStyles = readFileSync(
  resolve(process.cwd(), "src/notification-links.css"),
  "utf8",
);
const v228Styles = readFileSync(
  resolve(process.cwd(), "src/desktop-notifications-reports-v228.css"),
  "utf8",
);
const supervisorDashboardV236 = readFileSync(
  resolve(process.cwd(), "src/supervisor-ticket-dashboard-v236.css"),
  "utf8",
);
const supervisorInboxV237 = readFileSync(
  resolve(process.cwd(), "src/supervisor-ticket-inbox-v237.css"),
  "utf8",
);
const supervisorSeparationV238 = readFileSync(
  resolve(process.cwd(), "src/supervisor-ticket-separation-v238.css"),
  "utf8",
);
const maintenanceScannerV246 = readFileSync(
  resolve(process.cwd(), "src/maintenance-scanner-v246.css"),
  "utf8",
);
const responsiveModern = readFileSync(
  resolve(process.cwd(), "src/responsive-modern-v183.css"),
  "utf8",
);
const professionalUi = readFileSync(
  resolve(process.cwd(), "src/professional-ui-v185.css"),
  "utf8",
);
const chatTicketLayout = readFileSync(
  resolve(process.cwd(), "src/chat-ticket-layout-v186.css"),
  "utf8",
);
const institutionalCards = readFileSync(
  resolve(process.cwd(), "src/institutional-cards-mobile-v187.css"),
  "utf8",
);
const mobilePerfect = readFileSync(
  resolve(process.cwd(), "src/mobile-perfect-v188.css"),
  "utf8",
);
const mobileSystemAudit = readFileSync(
  resolve(process.cwd(), "src/mobile-system-audit-v207.css"),
  "utf8",
);
const mobileChatV193 = readFileSync(
  resolve(process.cwd(), "src/chat-mobile-v193-restored-v208.css"),
  "utf8",
);
const transitionMobileV215 = readFileSync(
  resolve(process.cwd(), "src/transition-departments-evidence-v215.css"),
  "utf8",
);
const ticketStatusV216 = readFileSync(
  resolve(process.cwd(), "src/ticket-status-full-width-v216.css"),
  "utf8",
);
const ticketLayoutHeaderV217 = readFileSync(
  resolve(process.cwd(), "src/ticket-layout-header-v217.css"),
  "utf8",
);
const mobileCameraAdminHeaderV218 = readFileSync(
  resolve(process.cwd(), "src/mobile-camera-admin-header-v218.css"),
  "utf8",
);
const mobileSingleScrollV220 = readFileSync(
  resolve(process.cwd(), "src/mobile-single-scroll-v220.css"),
  "utf8",
);
const adminTabsNoScrollV221 = readFileSync(
  resolve(process.cwd(), "src/admin-tabs-no-scroll-v221.css"),
  "utf8",
);
const transitionEvidenceViewerV222 = readFileSync(
  resolve(process.cwd(), "src/transition-evidence-viewer-v222.css"),
  "utf8",
);
const transitionHistoryAccordionV223 = readFileSync(
  resolve(process.cwd(), "src/transition-history-accordion-v223.css"),
  "utf8",
);
const adminPanel = readFileSync(
  resolve(process.cwd(), "src/components/vite/AdminPanel.tsx"),
  "utf8",
);
const automaticMigration = readFileSync(
  resolve(
    process.cwd(),
    "database-mssql/017_automatic_tickets_from_audits.sql",
  ),
  "utf8",
);
const separatedFindingsMigration = readFileSync(
  resolve(process.cwd(), "database-mssql/018_separate_automatic_findings.sql"),
  "utf8",
);
const evidenceMigration = readFileSync(
  resolve(process.cwd(), "database-mssql/022_finding_resolution_evidence.sql"),
  "utf8",
);
const conversationControlsMigration = readFileSync(
  resolve(
    process.cwd(),
    "database-mssql/025_support_conversation_controls.sql",
  ),
  "utf8",
);
const transitionProgressMigration = readFileSync(
  resolve(
    process.cwd(),
    "database-mssql/036_chat_message_deletion_transition_progress.sql",
  ),
  "utf8",
);
const transitionEvidenceMigration = readFileSync(
  resolve(
    process.cwd(),
    "database-mssql/037_agency_transition_record_evidence.sql",
  ),
  "utf8",
);
const transitionEvidenceStyles = readFileSync(
  resolve(process.cwd(), "src/transition-evidence-filter-v193.css"),
  "utf8",
);

describe("centro de soporte por agencia", () => {
  it("registra y muestra push para todos los usuarios en móvil y escritorio", () => {
    expect(program).toContain("AddSingleton<PwaPushService>()");
    expect(program).toContain('MapMethods("/api/push/test",new[]{"GET","POST"}');
    expect(program).toContain('path.StartsWith("/api/push/"');
    expect(program).toContain('values["canViewTicketNotifications"]=true');
    expect(program).toContain('keySource=push.UsesPersistedFallbackKeys?"APP_DATA":"MONSTER_ENVIRONMENT"');
    expect(database).toContain("n.recipient_user_id=@userId");
    expect(app).toContain("ensurePushSubscription");
    expect(app).toContain("updateViaCache:'none'");
    expect(app).toContain("registration.showNotification('Notificaciones activadas'");
    expect(app).toContain("session.mustChangePassword ? (");
    expect(pushService).toContain("VapidHelper.GenerateVapidKeys()");
    expect(pushService).toContain('Path.Combine(environment.ContentRootPath,"App_Data","Push","vapid-keys.json")');
    expect(program).toContain('code="PUSH_DELIVERY_FAILED"');
    expect(pushService).toContain("requireInteraction=true");
    expect(pushService).toContain('notification=ticket&ticketId=');
    expect(pushService).toContain('notification=chat&conversationId=');
    expect(database).toContain("PendingChatPushDeliveries");
    expect(database).toContain("u.role='ADMINISTRATOR'");
    expect(database).not.toContain("u.role='ADMIN' OR c.supervisor_user_id=s.user_id");
    expect(pushService).toContain("DateTime.SpecifyKind(item.CreatedAt,DateTimeKind.Utc)");
    expect(chat).toContain("const chatDate = (value: string)");
    expect(chat).toContain("chatDate(message.createdAt).toLocaleString");
    expect(app).toContain("deviceDate(item.updatedAt).toLocaleString");
    expect(app).toContain("supportNavigationFromUrl");
    expect(app).toContain('url.searchParams.has("ticket")?"TICKET"');
    expect(app).toContain('setSupportNavigation({...pendingNotificationTarget,requestKey:Date.now()})');
    expect(center).not.toContain('strictTechnologyTechnician&&navigationKind!=="TICKET"');
    expect(serviceWorker).toContain("self.skipWaiting()");
    expect(serviceWorker).toContain("self.clients.claim()");
    expect(serviceWorker).toContain("self.registration.showNotification");
    expect(serviceWorker).toContain("renotify:data.renotify===true");
    expect(pushService).toContain("notificationKey=");
    expect(pushService).toContain("notificationAt=");
    expect(pushService).toContain("renotify=false");
    expect(database).toContain("n.created_at>=s.created_at");
    expect(database).toContain("m.created_at>=s.created_at");
    expect(database).toContain("receipt.read_at IS NOT NULL OR receipt.dismissed_at IS NOT NULL");
    expect(app).toContain("notificationReceiptFromUrl");
    expect(app).toContain("deviceTimestamp(candidate.updatedAt)<=openedAt");
    expect(manifest).toContain('"src":"/loto-real-logo.svg"');
    expect(manifest).toContain('"sizes":"any"');
  });
  it("mantiene la sesión, agrega Falla CPU y limita la administración departamental a Tecnología", () => {
    expect(program).toContain("options.Cookie.MaxAge = TimeSpan.FromDays(3650)");
    expect(program).toContain("options.SlidingExpiration = true");
    expect(program).toContain("IsPersistent=true");
    expect(program).toContain('PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))');
    expect(program).toContain('MapPost("/api/admin/session/restore"');
    expect(program).toContain("CreatePersistentLoginToken(account.Id,ct)");
    expect(app).toContain('fetch("/api/admin/session", { cache: "no-store", credentials: "same-origin" })');
    expect(app).toContain('persistentSessionStorageKey="oficina-virtual-device-session"');
    expect(app).toContain('fetch("/api/admin/session/restore"');
    expect(app).toContain("rememberPersistentSession(authenticated)");
    expect(app).not.toContain('includes("presence-touch-v2")');
    expect(database).toContain("N'joel.vizcaino'");
    expect(database).toContain("N'eddi.bono'");
    expect(database).toContain("N'yesica.mercedes'");
    expect(database).toContain("N'franklin.calderon'");
    expect(database).toContain("N'jordaniel.ortega'");
    expect(database).toContain("role='TECHNOLOGY',support_team=NULL");
    expect(database).toContain("'CPU_FAILURE' code");
    expect(database).toContain("N'Falla CPU' name,N'CPU sin encender, lenta o con avería de hardware.' description,'CRITICAL' default_priority");
    expect(supportCatalogMigration).toContain("'TECHNOLOGY','CPU_FAILURE',N'Falla CPU'");
    expect(app).toContain("isWarehouseOperator || isDepartmentManager");
    expect(app).toContain("isWorkshopOperator || isDepartmentManager");
    expect(center).toContain("isTechnologyDepartmentManager ||");
    expect(center).toContain('session.role==="Administrator"&&workspaceDepartment==="TECHNOLOGY"');
    expect(center).toContain('setMaintenanceArea(isTechnologyWarehouseRequester?"WAREHOUSE"');
    expect(center).toContain('setMaintenanceView(isTechnologyWarehouseRequester?"history"');
    expect(center).toContain('!isDedicatedWarehousePortal&&!isWarehouseOperator');
    expect(center).toContain('openMaintenanceForm(false,maintenanceArea,isTechnologyWarehouseRequester?"REQUEST":undefined)');
    expect(center).not.toContain('!isTechnologyWarehouseRequester&&<div className="maintenance-quick-actions"');
    expect(center).toContain('"Agencia que requiere el equipo (obligatoria)"');
    expect(center).toContain('"Técnico que instalará el equipo (obligatorio)"');
    expect(center).toContain('maintenanceDraft.movementType!=="REQUEST"');
    expect(program).toContain('missingRequestRoute=movement=="REQUEST"&&body.AgencyId is null');
    expect(database).toContain('var serialNumber=string.IsNullOrWhiteSpace(body.SerialNumber)?null');
    expect(database).toContain('movementType is ("REQUEST" or "EXIT" or "NEW_DELIVERY")');
    expect(workshopWorkflow).toContain('Role=="Technology"&&Team is null');
    expect(database).toContain('technologyManager&&department!="TECHNOLOGY"');
  });
  it("acepta en el servidor todas las categorías de avería mostradas en el levantamiento", () => {
    const damageTypes = [
      "WIRING",
      "SCREENS",
      "CONNECTIVITY",
      "GENERAL_PAINTING",
      "GENERAL_INVERTER",
      "GENERAL_BATTERIES",
      "GENERAL_RESTRUCTURING",
      "GENERAL_LIGHTING",
      "GENERAL_METALWORK",
      "GENERAL_GENERATOR_REQUEST",
      "GENERAL_ELECTRICAL",
      "GENERAL_SHUTTER",
      "GENERAL_OTHER",
    ];

    for (const damageType of damageTypes) {
      expect(auditForm).toContain(`value: "${damageType}"`);
      expect(program).toContain(`"${damageType}"`);
      expect(database).toContain(`["${damageType}"]`);
    }
    expect(program).toContain('"FURNITURE"');
  });

  it("expone operaciones protegidas y respeta el alcance de grupos", () => {
    expect(program).toContain('MapGet("/api/tickets"');
    expect(program).toContain('MapPost("/api/tickets"');
    expect(program).toContain('.RequireAuthorization("TicketCreate")');
    expect(program).toContain('MapPut("/api/tickets/{id:guid}"');
    expect(program).toContain('MapPut("/api/tickets/{id:guid}"');
    expect(program).toContain('.RequireAuthorization("PortalUser")');
    expect(database).toContain("CanWorkTicket");
    expect(database).toContain("dbo.support_tickets");
    expect(database).toContain("AddScope(command, userId, role)");
    expect(database).toContain("admin_user_groups");
    expect(database).toContain('"HumanResources"=>"HUMAN_RESOURCES"');
    expect(center).toContain('strictTechnologyTechnician&&assignedToCurrentUser');
    expect(program).toContain('MapDelete("/api/admin/users/{id:guid}"');
    expect(database).toContain('USER_DEACTIVATED');
  });

  it("incluye navegación, creación, filtros y seguimiento", () => {
    expect(app).toContain('authenticated.supportTeam === "WAREHOUSE"');
    expect(app).toContain('title="Departamento de Almacén"');
    expect(app).toContain('title="Departamento Taller"');
    expect(app).toContain("<TicketCenter");
    expect(center).toContain("Crear ticket");
    expect(center).not.toContain("Prioridad crítica");
    expect(center).toContain("Todo Tecnología");
    expect(center).toContain("activeTeamCounts.callCenter");
    expect(center).toContain("activeTeamCounts.technicalFailure");
    expect(center).toContain("Guardar seguimiento");
    expect(center).toContain("department-card-grid");
    expect(center).toContain("Configurar soporte");
    expect(center).toContain("Buscar ticket, agencia, grupo o responsable");
    expect(center).toContain("Agencias en construcci");
    expect(center).toContain("agency-transitions");
    expect(center).toContain("Mis tickets asignados");
    expect(center).toContain("personalAssignmentView");
    expect(center).toContain('TECHNICIANS: "Técnicos"');
    expect(database).toContain("(@team='CALL_CENTER' AND u.support_team='CALL_CENTER')");
    expect(database).toContain("(@team='TECHNICAL_FAILURE' AND u.support_team='TECHNICAL_FAILURE')");
    expect(center).toContain('label="Responsables del área"');
    expect(program).toContain('/api/agency-transitions');
    expect(database).toContain("UpdateAgencyTransitionStage");
    expect(center).toContain("Mantenimiento y equipos");
    expect(center).toContain("Tickets Técnicos");
    expect(center).toContain("Tickets Supervisores");
    expect(center).toContain("personnelSearch");
    expect(program).toContain('/api/maintenance/movements');
    expect(program).toContain('area=="WAREHOUSE"');
    expect(program).toContain('"TRANSFER_TO_WORKSHOP"');
    expect(database).toContain("CK_maintenance_operational_area");
    expect(database).toContain("signature_data");
    expect(database).toContain("REAL-MNT|");
    expect(database).toContain("columnsCommand");
    expect(database).toContain("dependenciesCommand");
    expect(database).toContain("seedCatalogSql");
    expect(database).toContain("MaintenanceProducts");
    expect(program).toContain("products=await db.MaintenanceProducts");
    expect(database).toContain("CreateMaintenanceMovement");
  });

  it("envÃ­a el chat con Enter y respuestas rÃ¡pidas inmediatamente", () => {
    expect(chat).toContain('event.key === "Enter" && !event.shiftKey');
    expect(chat).toContain("event.currentTarget.form?.requestSubmit()");
    expect(chat).toContain("sendText(reply, false)");
  });

  it("ofrece CRM con búsqueda, grupos, perfiles, presencia y typing", () => {
    expect(chat).toContain("conversationQuery");
    expect(chat).toContain("groupFilter");
    expect(chat).toContain("Buscar conversación...");
    expect(chat).toContain("chat-contact-modal");
    expect(chat).toContain("Buscar por nombre, usuario, grupo o teléfono");
    expect(program).toContain('MapGet("/api/support/chat/contacts"');
    expect(program).toContain('MapPost("/api/support/chat/direct"');
    expect(database).toContain("SUBSTRING(c.category,8,80)=@userId");
    expect(database).toContain("SUBSTRING(category,8,80)=@userId");
    expect(database).not.toContain("inicié esta conversación directa contigo");
    expect(chat).toContain("chat-profile-panel");
    expect(chat).toContain("está escribiendo");
    expect(chat).toContain("Soporte en línea");
    expect(chat).toContain("message.avatarUrl");
    expect(program).toContain('MapPost("/api/support/chat/presence"');
    expect(database).toContain("SupportPresenceContext");
    expect(database).toContain("ReadBooleanValue(reader,5)");
    expect(chat).toContain("conversationGroups");
    expect(chat).toContain("Array.isArray(conversation.groups)");
    expect(chat).toContain("normalizePresence");
    expect(chat).toContain('includes("presence-touch-v2")');
    expect(supportStyles).toContain("font-size: 14px");
  });

  it("mantiene controles auditados sin el menú de tres puntos y elimina mensajes", () => {
    expect(program).toContain(
      'MapPost("/api/support/chat/conversations/{id:guid}/control"',
    );
    expect(program).toContain("canModerateSupportChat");
    expect(program).toContain("canDeleteSupportConversations");
    expect(chat).toContain("Silenciar chat");
    expect(chat).toContain("Eliminar conversación");
    expect(chat).not.toContain("MoreVertical");
    expect(chat).toContain("deleteMessage");
    expect(program).toContain('MapDelete("/api/support/chat/messages/{id:guid}"');
    expect(database).toContain("DeleteSupportMessage");
    expect(chat).not.toContain("loadConversations(true)");
    expect(chat).not.toContain("selectFirst && !isSupervisor");
    expect(chat).not.toContain("manualListModeRef");
    expect(transitionProgressMigration).toContain("deleted_at");
    expect(database).toContain("UpdateSupportConversationControl");
    expect(conversationControlsMigration).toContain(
      "support_conversation_controls",
    );
    expect(supportStyles).toContain("granular-permission-grid label:has");
    expect(supportStyles).toContain(".chat-controls-menu > header");
  });

  it("conserva cada avance y permite seleccionar, editar y completar etapas", () => {
    expect(transitionProgressMigration).toContain("agency_transition_progress");
    expect(database).toContain("INSERT INTO dbo.agency_transition_progress");
    expect(database).toContain("'EDITED'");
    expect(transitionProgressMigration).toContain("Registro migrado V190");
    expect(database).toContain("EditAgencyTransitionStage");
    expect(database).toContain("UpdateAgencyTransitionProgress");
    expect(database).toContain("DeleteAgencyTransitionProgress");
    expect(database).toContain("DeleteAgencyTransitionStageContent");
    expect(transitionProgressMigration).toContain("entry_type");
    expect(program).toContain(
      'MapPut("/api/agency-transitions/{id:guid}/stages/{stageId:guid}"',
    );
    expect(program).toContain(
      'MapPut("/api/agency-transitions/{id:guid}/stages/{stageId:guid}/progress/{progressId:guid}"',
    );
    expect(program).toContain(
      'MapDelete("/api/agency-transitions/{id:guid}/stages/{stageId:guid}/progress/{progressId:guid}"',
    );
    expect(program).toContain(
      'MapDelete("/api/agency-transitions/{id:guid}/stages/{stageId:guid}"',
    );
    expect(program).toContain('version = "2026.09.11.323"');
    expect(program).toContain('X-Application-Release"]="V323"');
    expect(program).toContain("AddResponseCompression");
    expect(program).toContain('CacheControl = "public,max-age=31536000,immutable"');
    expect(program).toContain('form["locationCapturedAt"]');
    expect(database).toContain("accuracy_meters decimal(8,2)");
    expect(apiProject).toContain(
      "<AssemblyName>RegistroAgencias.SqlServer.V323</AssemblyName>",
    );
    expect(webConfig).toContain(
      'processPath=".\\RegistroAgencias.SqlServer.V323.exe"',
    );
    expect(releaseGuard).toContain('var release = "V323"');
    expect(v228Styles).toContain("z-index: 4600 !important");
    expect(v228Styles).toContain("overflow-y: auto !important");
    expect(v228Styles).toContain(".notification-tables");
    expect(v228Styles).toContain(".report-kpi-grid strong");
    expect(v228Styles).toContain("grid-template-columns: 50px minmax(0, 1fr) max-content");
    expect(v228Styles).toContain("overflow-wrap: anywhere");
    expect(v228Styles).toContain("text-overflow: clip");
    expect(v228Styles).toContain("white-space: normal");
    expect(releaseGuard).toContain('window.addEventListener("vite:preloadError"');
    expect(program).toContain('MapDelete("/api/agency-transitions/{id:guid}"');
    expect(center).toContain("transition-department-card");
    expect(center).toContain("Historial de avances");
    expect(center).toContain("transition-current-note");
    expect(center).toContain("Editó la descripción de la etapa");
    expect(center).toContain("Crear registro");
    expect(center).toContain("Completar etapa");
    expect(center).toContain("Completar registro");
    expect(center).toContain("Editar registro");
    expect(center).toContain("Crear otro registro");
    expect(center).toContain("Solo lectura");
    expect(center).toContain("canManageSelectedTransitionStage");
    expect(center).not.toContain("transition-current-record-actions");
    expect(center).not.toContain("selectedTransitionCurrentRecord");
    expect(center).toContain("transition-detail-stage-tabs");
    expect(database).toContain("EnsureAgencyTransitionStages");
    expect(database).toContain("AgencyTransitionProgressCapabilities");
    expect(database).not.toContain("EnsureAgencyTransitionProgressSchema");
    expect(center).toContain("Asignar responsable");
    expect(center).toContain("Técnico del departamento");
    expect(center).toContain("Contratista");
    expect(center).toContain("selectedTransitionTechnicians");
    expect(database).toContain("AssignAgencyTransitionProgress");
    expect(database).toContain("'ASSIGNMENT'");
    expect(program).toContain('stages/{stageId:guid}/progress/{progressId:guid}/assignment');
    expect(database).toContain("__ASSIGNMENT__");
    expect(database).toContain("targetProgressId=progressId");
    expect(center).toContain("transitionRecordAssignment");
    expect(center).toContain("RESPONSABLE DE ESTE REGISTRO");
    expect(center).not.toContain("RESPONSABLE DE LA ETAPA");
    expect(database).toContain("'IN_PROGRESS'{typeValue}");
    expect(program).toContain('progress/{progressId:guid}/evidence"');
    expect(program).toContain("form.Files.Count is <1 or >4");
    expect(database).toContain("EnsureAgencyTransitionEvidenceSchema");
    expect(database).toContain("AddAgencyTransitionEvidence");
    expect(database).toContain("Sube entre 1 y 4 fotografías de evidencia");
    expect(transitionEvidenceMigration).toContain("agency_transition_progress_evidence");
    expect(center).toContain("Fotografías con cámara y GPS");
    expect(center).toContain('capture="environment"');
    expect(center).toContain("prepareAgencyEvidence");
    expect(center).toContain("Promise.all([gpsPromise,filesPromise])");
    expect(center).toContain("maximumAge:60000");
    expect(center).toContain('accept="image/*,.heic,.heif"');
    expect(center).toContain("transition-evidence-feedback");
    expect(center).toContain("Ver evidencias");
    expect(center).toContain("Subida por");
    expect(center).toContain("transition-evidence-viewer-backdrop");
    expect(center).toContain("expandedTransitionRecords");
    expect(center).toContain("Expandir todos");
    expect(center).toContain('aria-expanded={isExpanded}');
    expect(database).toContain("e.uploaded_by_name");
    expect(center).toContain("Agencias en proceso de construcción");
    expect(center).toContain("filteredAgencyTransitions.filter(project=>!selectedTransitionId||project.id===selectedTransitionId)");
    expect(transitionEvidenceStyles).toContain(".transition-evidence-gallery");
    expect(transitionEvidenceStyles).toContain(".transition-status-segments");
    expect(transitionMobileV215).toContain("overflow: visible !important");
    expect(transitionMobileV215).toContain("grid-template-columns: repeat(3, minmax(0, 1fr)) !important");
    expect(transitionMobileV215).toContain("grid-template-columns: minmax(0, 1fr) !important");
    expect(ticketStatusV216).toContain("button:only-of-type");
    expect(ticketStatusV216).toContain("grid-column: 1 / -1 !important");
    expect(ticketStatusV216).toContain("text-align: center !important");
    expect(ticketLayoutHeaderV217).toContain("status-layout-active");
    expect(ticketLayoutHeaderV217).toContain("repeat(2, minmax(0, 1fr))");
    expect(ticketLayoutHeaderV217).toContain("grid-template-columns: repeat(4, 40px) !important");
    expect(mobileCameraAdminHeaderV218).toContain(".site-header .header-account");
    expect(mobileCameraAdminHeaderV218).toContain(".atlas-admin .atlas-admin-hero");
    expect(mobileCameraAdminHeaderV218).toContain(".transition-camera-button");
    expect(mobileSingleScrollV220).toContain(".audit-detail-modal > :not(header):not(.evidence-lightbox)");
    expect(mobileSingleScrollV220).toContain(".user-manager-modal > :not(header):not(.new-user-modal-backdrop)");
    expect(mobileSingleScrollV220).toContain("scrollbar-width: none !important");
    expect(adminTabsNoScrollV221).toContain("grid-template-columns: repeat(2, minmax(0, 1fr))");
    expect(adminTabsNoScrollV221).toContain("overflow: visible !important");
    expect(transitionEvidenceViewerV222).toContain(".transition-evidence-ready");
    expect(transitionEvidenceViewerV222).toContain(".transition-evidence-viewer-grid");
    expect(transitionEvidenceViewerV222).toContain("height: 100dvh");
    expect(transitionHistoryAccordionV223).toContain(".transition-history-record-toggle");
    expect(transitionHistoryAccordionV223).toContain(".transition-history-record-details");
    expect(transitionHistoryAccordionV223).toContain("grid-template-columns: auto minmax(0, 1fr) auto");
    expect(responsiveModern).toContain("--ui-radius-card: 18px");
    expect(responsiveModern).toContain("@media (max-width: 900px)");
    expect(responsiveModern).toContain("grid-template-columns: minmax(0,1fr)");
    expect(mobileSystemAudit).toContain("--mobile-touch: 44px");
    expect(mobileSystemAudit).toContain("font-size: 16px !important");
    expect(mobileSystemAudit).toContain("height: 100dvh !important");
    expect(mobileSystemAudit).toContain("overscroll-behavior-x: contain");
    expect(mobileSystemAudit).toContain(".support-chat-crm");
    expect(mobileChatV193).toContain("Restauración exacta del chat móvil");
    expect(mobileChatV193).toContain(".support-chat-layout:not(.has-active-chat)>aside");
    expect(mobileChatV193).toContain(".support-chat-crm{width:100vw!important");
    expect(professionalUi).toContain("Consolidación visual profesional");
    expect(professionalUi).toContain(".department-primary-grid > button");
    expect(professionalUi).toContain(".ticket-report-filters");
    expect(professionalUi).toContain("grid-template-columns: repeat(3, minmax(0, 1fr))");
    expect(professionalUi).toContain(".transition-progress-actions");
    expect(chatTicketLayout).toContain("min-height: 78px");
    expect(chatTicketLayout).toContain("flex: 0 0 auto");
    expect(chatTicketLayout).toContain("grid-auto-columns: minmax(82px, 1fr)");
    expect(chatTicketLayout).toContain("min-width: 118px");
    expect(institutionalCards).toContain("--v187-card-surface");
    expect(institutionalCards).toContain("content-visibility: auto");
    expect(institutionalCards).toContain(".transition-detail-actions");
    expect(institutionalCards).toContain("repeat(3, minmax(0, 1fr))");
    expect(institutionalCards).toContain(".atlas-admin-hero > div:first-child");
    expect(center).toContain("loadAgencyTransitions");
    expect(center).toContain("loadMaintenanceMovements");
    expect(mobilePerfect).toContain(".support-category-grid > button");
    expect(mobilePerfect).toContain("grid-template-columns: 22px minmax(0,1fr) auto");
    expect(mobilePerfect).toContain(".user-department-cards.department-open > button:not(.active)");
    expect(mobilePerfect).toContain(".user-manager-actions");
    expect(mobilePerfect).toContain("overflow-x: clip");
  });

  it("notifica mensajes, navega al destino y cierra usuarios al guardar", () => {
    expect(program).toContain('MapGet("/api/support/chat/notifications"');
    expect(database).toContain("SupportChatNotifications");
    expect(app).toContain("openNotificationDestination");
    expect(center).toContain("navigationTarget?.requestKey");
    expect(adminPanel).toContain("setUserManagerOpen(false)");
  });

  it("muestra avisos emergentes y separa tickets y chat con limpieza", () => {
    expect(app).toContain("notification-toast-stack");
    expect(app).toContain("ticketNotificationItems");
    expect(app).toContain("chatNotificationItems");
    expect(app).toContain("Limpiar bandeja");
    expect(app).toContain("Marcar todas leídas");
    expect(program).toContain('MapPost("/api/notifications/state"');
    expect(database).toContain("support_notification_receipts");
  });

  it("inicia incidencias desde la agencia sin selector ni modal visual", () => {
    expect(app).toContain('const isAdministrator = session.role === "Administrator"');
    expect(app).toContain('!isSupervisor && !isMaintenanceOperator && session.permissions.canViewSupportDashboard');
    expect(app).toContain('authenticated.role === "GroupAdministrator"');
    expect(app).toContain("setView(initialViewForSession(authenticated))");
    expect(app).toContain("Reportar incidencia");
    expect(app.match(/className="report-incidence-button"/g)).toHaveLength(1);
    expect(app).toContain('step === "details"');
    expect(app).toContain('onClick={() => setReportAgencyOpen(true)}');
    expect(app).not.toContain('step !== "details"');
    expect(center).toContain("reportAgency");
    expect(center).toContain("AGENCIA SELECCIONADA");
    expect(center).toContain("incident-report-view");
    expect(supportStyles).not.toContain(":not(.supervisor-assigned-cases)");
    expect(app).toContain("Web V323");
  });

  it("abre el tablero departamental del supervisor y protege tickets duplicados", () => {
    expect(center).toContain("setWorkspaceDepartment(department.code)");
    expect(center).toContain("Mis tickets asignados");
    expect(center).toContain("Mis tickets resueltos");
    expect(center).toContain("Reportes y estadísticas");
    expect(center).toContain("Historial de mi gestión");
    expect(center).toContain("allowDuplicate: duplicateCreationConfirmed === duplicateSignature");
    expect(program).toContain('code="DUPLICATE_TICKET"');
    expect(database).toContain("body.AllowDuplicate!=true");
    expect(database).toContain("WITH(UPDLOCK,HOLDLOCK)");
    expect(database).toContain("TicketDuplicateException");
    expect(supervisorDashboardV236).toContain(".supervisor-department-home");
    expect(supervisorDashboardV236).toContain("grid-template-columns:repeat(2,minmax(0,1fr))");
  });

  it("abre tickets directamente sin completar el buscador y reutiliza la lista del historial", () => {
    expect(center).not.toContain("Bandeja de tickets asignados");
    expect(center).not.toContain("showSupervisorAssignedTable");
    expect(center).not.toContain("supervisor-ticket-inbox-table");
    expect(center).toContain('className="ticket-board"');
    expect(center).toContain('className="ticket-list"');
    expect(center).toContain("directTicketFocusRef.current = true");
    expect(center).toContain("setDirectTicketFocus({ id: ticket.id, ticketNumber: ticket.ticketNumber })");
    expect(center).toContain('setQuery("");');
    expect(center).not.toContain("setQuery(String(navigationTicketNumber");
    expect(center).not.toContain("setQuery(String(ticket.ticketNumber");
    expect(center).toContain('session.role === "Administrator" || session.role === "GroupAdministrator"');
    expect(center).toContain("ticket.assignedTechnicianName");
    expect(app).toContain('ticketId: item.kind === "TICKET" ? item.id : undefined');
    expect(adminPanel).not.toContain("Tickets de soporte asignados");
    expect(supervisorInboxV237).toContain(".supervisor-ticket-inbox-table");
  });

  it("restaura la creación tras revisar el duplicado y separa activas de resueltas", () => {
    expect(center).toContain("restoreTicketCreationAfterDuplicate");
    expect(center).toContain("setReturnToTicketCreation(true)");
    expect(center).toContain("if(returnToTicketCreation){restoreTicketCreationAfterDuplicate();return;}");
    expect(program).toContain("public bool IsAssignedToCurrentUser { get; init; }");
    expect(database).toContain("IsAssignedToCurrentUser=string.Equals(row.AssignedTechnicianId?.Trim(),userId.Trim(),StringComparison.OrdinalIgnoreCase)");
    expect(center).toContain("ticket.isAssignedToCurrentUser === true");
    expect(center).toContain('hidden={personalAssignmentView&&ticketWorkspaceView!=="active"}');
    expect(center).toContain('["RESOLVED", "CLOSED"].includes(ticket.status)');
    expect(center).toContain("void load(true)");
    expect(supervisorSeparationV238).toContain(".ticket-status-filter[hidden]");
    expect(maintenanceScannerV246).toContain(".maintenance-scanner-console");
    expect(maintenanceScannerV246).toContain(".maintenance-kpis");
  });

  it("sincroniza el contador del historial con la lista y limpia filtros ocultos", () => {
    expect(center).toContain("countedTickets.filter((ticket) => resolvedStatuses.has(ticket.status)).length");
    expect(center).toContain('setTechnicianFilter("ALL");setQuery("");setSupportTab("tickets")');
    expect(center).toContain('setAssignmentScope("ALL");');
  });

  it("prioriza la asignación directa al supervisor aunque la agencia no esté en sus grupos", () => {
    expect(database).toContain("t.assigned_technician_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))");
    expect(database).toContain("@groupSupervisor=1 AND (t.assigned_technician_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)))");
    expect(database).toContain("@supportTeam='TECHNICIANS' AND (t.assigned_technician_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)))");
    expect(center).toContain("!resolvedStatuses.has(ticket.status) &&\n                  assignedToCurrentUser");
  });

  it("mantiene las notificaciones del supervisor dentro de Mis tickets", () => {
    expect(center).toContain('session.role === "GroupAdministrator" && navigationKind === "TICKET"');
    expect(center).toContain('setAssignmentScope(strictTechnologyTechnician||openingSupervisorTicket?"MINE":"ALL")');
    expect(center).toContain("setPersonalAssignmentView(strictTechnologyTechnician||openingSupervisorTicket)");
    expect(center).toContain("supervisorHistoryView || personalAssignmentView || matchesTicketKind");
    expect(center).toContain('const supervisorAssignedTickets=workspaceTickets.filter(ticket=>ticket.ticketType==="SUPPORT"&&isCurrentSupervisorTicket(ticket))');
  });

  it("restaura los estados personales y cierra el modal después de guardar", () => {
    expect(center).toContain('[["IN_PROGRESS", "Tickets en proceso"], ["PENDING", "Tickets pendientes"]]');
    expect(center).toContain('personalAssignmentView&&ticketWorkspaceView==="active"&&statusFilter==="ACTIVE"');
    expect(center).toContain('setStatusFilter(personalAssignmentView&&ticketWorkspaceView==="active"&&statusFilter===value?"ACTIVE":value)');
    expect(center).toContain('if (ticketActionMode === "resolve")');
    expect(center).toMatch(/resuelto con evidencia[\s\S]{0,220}setExpandedTicketId\(null\)[\s\S]{0,160}setTicketActionMode\(null\)/);
  });

  it("muestra el avatar propio solo en el perfil y circular ante los contactos", () => {
    expect(chat).toMatch(/chat-header-actions[\s\S]{0,180}<UserRound \/>[\s\S]{0,40}Mi perfil/);
    expect(chat).toMatch(/className="chat-mobile-profile"[\s\S]{0,180}<Settings \/>/);
    expect(chat).toContain('item.supervisorAvatarUrl && !isSupervisor ? " has-avatar"');
    expect(chat).toContain('<div className="chat-profile-avatar">');
    expect(v228Styles).toContain(".chat-profile-avatar > img");
    expect(v228Styles).toContain(".chat-list-icon.has-avatar");
    expect(v228Styles).toContain("border-radius: 50% !important");
  });

  it("organiza soporte y hallazgos con tarjetas sin duplicar pendientes", () => {
    expect(app).toContain("incident-report-workspace");
    expect(center).toContain("support-section-copy");
    expect(center).toContain('supportStage === "home"');
    expect(center).toContain('setSupportStage("findings")');
    expect(center).toContain('findingView === "menu"');
    expect(center).toContain("Volver a Hallazgos automáticos");
    expect(center).toContain("Hallazgos automáticos");
    expect(center).toContain('findingView !== "tracking"');
    expect(center).toContain('findingView === "tracking"');
    expect(center).toContain("Resolver con evidencia");
    expect(center).toContain("Asignados a técnicos");
    expect(center).toContain("Asignados a supervisores");
    expect(center).toContain("pendingFindingCount");
    expect(center).toContain("resolvedFindingCount");
    expect(center).toContain("workspaceFindings.filter");
  });

  it("documenta soluciones con varias evidencias y auditoría PDF", () => {
    expect(program).toContain("Adjunta entre 1 y 8 fotografías de evidencia.");
    expect(program).toContain('form["replacementSerial"]');
    expect(program).toContain('form["removedSerial"]');
    expect(center).toContain("scanTicketSerialEvidence");
    expect(center).toContain("Foto del serial reemplazado");
    expect(center).toContain("Foto del serial retirado");
    expect(program).toContain("form.Files.Count is < 1 or > 6");
    expect(program).toContain("ValidEvidenceImage");
    expect(program).toContain("!await db.CanAccessProfile(id,photoScope.UserId,photoScope.Role,ct)");
    expect(database).toContain("finding_resolution_evidence");
    expect(database).toContain("FindingEvidencePhoto");
    expect(center).toContain('type="file"');
    expect(center).toContain("multiple");
    expect(center).toContain('import("jspdf")');
    expect(center).toContain("Auditoría técnica de solución");
    expect(center).toContain("Duración total del caso");
    expect(center).toContain("department-live-metrics");
    expect(evidenceMigration).toContain("finding_resolution_evidence");
  });

  it("abre Call Center en sus tarjetas sin entrar automáticamente a una bandeja", () => {
    expect(center).toContain('session.supportTeam==="CALL_CENTER"&&<button className="technology-area-entry call-center-only"');
    expect(center).toContain('setSupportStage("tickets");setTicketWorkspaceView("menu");setTeamViewChosen(true);');
    expect(center).not.toContain('setSupportStage("tickets");setTicketWorkspaceView("active");setTeamViewChosen(true);}}><span className="support-section-icon"><LifeBuoy/></span><span className="support-section-copy"><em>RECEPCIÓN Y CIERRE');
  });

  it("incluye una migración SQL Server idempotente", () => {
    expect(migration).toContain(
      "IF OBJECT_ID(N'dbo.support_tickets', N'U') IS NULL",
    );
    expect(migration).toContain("IX_support_tickets_department_status");
    expect(migration).toContain("FK_support_tickets_agency");
  });

  it("separa técnicos de Tecnología y Servicios Generales", () => {
    expect(program).toContain('"Technology", "GeneralServices"');
    expect(program).toContain("canResolveTickets");
    expect(program).toContain('MapGet("/api/tickets/alerts"');
    expect(center).toContain("session.permissions.canCreateTickets");
    expect(center).toContain("session.permissions.canResolveTickets");
  });

  it("actualiza en vivo, filtra prioridades y genera hallazgos automáticos", () => {
    expect(program).toContain('MapGet("/api/live/stream"');
    expect(program).toContain('broker.Publish("chat-message")');
    expect(app).not.toContain('new EventSource("/api/live/stream")');
    expect(app).toContain("scheduleRefresh");
    expect(app).toContain("now - lastPublishedAt < 2_500");
    expect(app).toContain("refreshInFlight");
    expect(center).toContain("loadInFlightRef");
    expect(operationsCenter).toContain("loadInFlightRef");
    expect(operationsCenter).not.toContain("window.setInterval(() => {");
    expect(program).toContain("heartbeat");
    expect(program).toContain('Headers["X-Accel-Buffering"]="no"');
    expect(app).toContain('"support-live-refresh"');
    expect(chat).toContain('"support-live-refresh"');
    expect(center).toContain('"support-live-refresh"');
    expect(app).toContain("!reportAgencyOpen");
    expect(center).toContain("priorityFilter");
    expect(center).toContain("Crítica");
    expect(database).toContain("dbo.automatic_findings");
    expect(database).toContain("PRINTER_NO_MAINTENANCE");
    expect(database).toContain('"CRITICAL"');
    expect(separatedFindingsMigration).toContain("JSON_VALUE");
    expect(separatedFindingsMigration).toContain(
      "DELETE FROM dbo.support_tickets WHERE is_automatic=1",
    );
  });

  it("reserva soporte global y auditoría operativa, pero conserva Mis tickets del supervisor", () => {
    expect(app).toContain('const isAdministrator = session.role === "Administrator"');
    expect(app).toContain('const canOpenTicketWorkspace =');
    expect(app).toContain('session.permissions.canViewSupportDashboard || isSupervisor');
    expect(app).toContain("isAdministrator && session.permissions.canManageAudits");
    expect(app).toContain('const isSupervisor = session.role === "GroupAdministrator"');
    expect(app).toContain('<LifeBuoy /><span>Mis tickets</span>');
    expect(app).toContain('view === "tickets" && canOpenTicketWorkspace');
    expect(app).toContain('view === "queue" && canOpenLiveQueue');
    expect(app).toContain('teamScope={view==="queue"?technologyQueueTeam:null}');
  });

  it("mantiene la pantalla visible al actualizar y organiza la navegación en un sidebar adaptable", () => {
    expect(app).toContain("portal-sidebar");
    expect(app).toContain("sidebarCollapsed");
    expect(app).toContain("sidebar-mobile-trigger");
    expect(app).toContain('localStorage.setItem("portal-sidebar-collapsed"');
    expect(app).toContain('className="main-nav mobile-main-nav"');
    expect(center).toContain("loading && !tickets.length");
    expect(operationsCenter).toContain("if (loading && !tickets.length)");
    expect(professionalSidebarV254).toContain(".portal-sidebar");
    expect(professionalSidebarV254).toContain(".portal-main-column");
    expect(professionalSidebarV254).toContain("@media (max-width: 900px)");
    expect(professionalSidebarV254).toContain("transform: translateX(-105%)");
    expect(applicationIndex).toContain('name="app-release" content="V323"');
    expect(applicationIndex).toContain('/release-guard.js?v=V323');
  });

  it("abre Avería en el menú operativo y adapta la cola con zoom y barra azul", () => {
    const failureCard = center.indexOf('(lockedTechnologyTeam||technologyTeamFilter) === "TECHNICAL_FAILURE"');
    const failureMenu = center.indexOf('setTicketWorkspaceView("menu")', failureCard);
    expect(failureCard).toBeGreaterThanOrEqual(0);
    expect(failureMenu).toBeGreaterThan(failureCard);
    expect(operationsCenter).toContain("automaticQueueZoom");
    expect(operationsCenter).toContain('className="queue-zoom-controls"');
    expect(operationsCenter).toContain('className="queue-zoom-stage"');
    expect(operationsCenter).toContain('const adjustQueueZoom = (delta: number)');
    expect(operationsCenter).toContain('adjustQueueZoom(-0.1)');
    expect(operationsCenter).toContain('adjustQueueZoom(0.1)');
    expect(operationsCenter).toContain('Alejar, reducir tamaño');
    expect(operationsCenter).toContain('Acercar, aumentar tamaño');
    expect(operationsStyles).toContain("scrollbar-color:#45bfe9 #061a2d");
    expect(operationsStyles).toContain("body:has(.operations-queue)::-webkit-scrollbar-thumb");
    expect(operationsStyles).toContain("overflow:auto;scrollbar-color:#45bfe9 #061a2d");
    expect(operationsStyles).toContain("En móvil se conserva la composición compacta original de la cola");
    expect(operationsStyles).toContain(".operations-queue .queue-zoom-controls{display:none}");
    expect(operationsStyles).toContain(".operations-queue .queue-zoom-stage{width:100%!important;zoom:1!important}");
    expect(operationsStyles).toContain(".operations-queue .queue-area-stat.progress{background:#0a2942!important}");
    expect(operationsStyles).toContain("acabado moderno y controles de escala inequívocos");
  });

  it("unifica el color móvil, conserva las utilidades del header y fija las secciones abajo", () => {
    expect(designSystemV256).toContain("@media (min-width: 901px)");
    expect(designSystemV256).not.toContain("@media (max-width: 900px)");
    expect(workflowProfessionalV261).toContain("@media (max-width: 900px)");
    expect(mobileDesktopColorParityV264).toContain("escritorio-móvil");
    expect(mobileDesktopColorParityV264).toContain(".maintenance-inventory-table");
    expect(mobileDesktopColorParityV264).toContain(".maintenance-area-cards > article.warehouse");
    expect(mobileDesktopColorParityV264).toContain("bottom: calc(8px + env(safe-area-inset-bottom)) !important");
    expect(workflowProfessionalV261).toContain(".site-header .mobile-main-nav");
    expect(workflowProfessionalV261).toContain("bottom: calc(8px + env(safe-area-inset-bottom)) !important");
    expect(app).toContain('className="sidebar-desktop-collapse"');
    expect(app).toContain('className="header-account"');
    expect(app).toContain('className="notification-bell"');
    expect(app).toContain('className="header-profile"');
    expect(app).toContain('className="header-logout"');
    expect(app).not.toContain('className="sidebar-inbox"');
    expect(app).not.toContain('className="sidebar-footer"');
  });

  it("abre Almacén y Taller directamente, sin tareas pendientes, y conserva avisos", () => {
    expect(center).not.toContain("pending-task-checklist");
    expect(center).not.toContain("Tareas pendientes por resolver");
    expect(center).toContain("<WorkshopBoard");
    expect(center).not.toContain('className="warehouse-department-selector"');
    expect(center).toContain('useState<string|null>(null)');
    expect(center).toContain('<MaintenanceOverview area={maintenanceArea}');
    expect(center).toContain('setMaintenanceSelectedDepartment(department)');
    expect(center).toContain('Departamentos de ${maintenanceArea==="WORKSHOP"?"Taller":"Almacén"}');
    expect(center).toContain("const isDedicatedMaintenancePortal=isDedicatedWarehousePortal||isDedicatedWorkshopPortal");
    expect(center).toContain("!isDedicatedMaintenancePortal && !isTechnologyTechnician");
    expect(center).toContain('{!isDedicatedMaintenancePortal&&<header className="ticket-hero">');
    expect(center).toContain('supportStage !== "home" && !isDedicatedMaintenancePortal');
    expect(center).toContain('["TECHNOLOGY","GENERAL_SERVICES","HUMAN_RESOURCES"]');
    expect(center).toContain('REQUEST:"Solicitud de equipo"');
    expect(center).toContain('TRANSFER_TO_WORKSHOP:"Envío a Taller"');
    expect(database).toContain('supportTeam=="WAREHOUSE"');
    expect(database).toContain('supportTeam=="WORKSHOP"');
    expect(program).toContain('account.SupportTeam is "WAREHOUSE" or "WORKSHOP"');
    expect(app).toContain('aria-live="assertive"');
    expect(app).toContain("openNotificationDestination(toast.item)");
    expect(workflowProfessionalV261).toContain(".warehouse-department-hub");
    expect(app).toContain('maintenanceEntry={view === "warehouse" ? "WAREHOUSE" : view === "workshop" ? "WORKSHOP" : null}');
    expect(center).toContain('maintenanceEntry?: MaintenanceArea | null');
    expect(center).toContain("REQUERIMIENTOS A TALLER Y ALMACÉN");
    expect(center).toContain("Solicitudes, entregas a técnicos o supervisores, recepción de dañados, reparación, devolución y cadena de custodia.");
  });

  it("autocompleta el equipo escaneado y mantiene nombre, serial y hora editables", () => {
    expect(center).toContain("const suggestedProduct=");
    expect(center).toContain("equipmentType:suggestedProduct||current.equipmentType");
    expect(center).toContain("occurredAt:maintenanceLocalNow()");
    expect(center).toContain('className="maintenance-autofill-note"');
    expect(center).toContain("Producto, serial, fecha y hora se completan automáticamente");
    expect(center).toContain("El escáner lo completa; también puedes corregirlo");
    expect(center).not.toContain("!maintenanceScannerActive&&<label>Serial o código");
  });

  it("guarda mantenimiento con validación visible y controla el abastecimiento completo", () => {
    expect(center).toContain('className="maintenance-form-error" role="alert"');
    expect(center).toContain('type="button" className="primary" disabled={saving} onClick={()=>void createMaintenanceMovement()}');
    expect(center).toContain("No se pudo guardar todavía");
    expect(center).toContain('maintenanceView==="dashboard"');
    expect(center).toContain("Artículos de mayor consumo");
    expect(center).toContain("Stock crítico");
    expect(center).toContain("Requisiciones pendientes");
    expect(center).toContain("Recepción física contra orden");
    expect(center).toContain("Proveedores");
    expect(center).toContain("Devoluciones");
    expect(program).toContain('MapGet("/api/maintenance/procurement"');
    expect(program).toContain('MapPost("/api/maintenance/suppliers"');
    expect(program).toContain('MapPost("/api/maintenance/requisitions"');
    expect(program).toContain('MapPost("/api/maintenance/purchase-orders"');
    expect(program).toContain('/purchase-orders/{id:guid}/receive');
    expect(program).toContain('MapPost("/api/maintenance/returns"');
    expect(database).toContain("CREATE TABLE dbo.maintenance_suppliers");
    expect(database).toContain("CREATE TABLE dbo.maintenance_requisitions");
    expect(database).toContain("CREATE TABLE dbo.maintenance_purchase_orders");
    expect(database).toContain("CREATE TABLE dbo.maintenance_returns");
    expect(database).toContain("Recepción física contra orden de compra");
    expect(maintenanceProcurementV267).toContain(".maintenance-operational-dashboard");
    expect(maintenanceProcurementV267).toContain(".maintenance-procurement-board");
    expect(maintenanceProcurementV267).toContain("@media(max-width:900px)");
  });

  it("moderniza Mi panel, registra el login auditor y restaura la barra inferior móvil", () => {
    expect(dashboardModernV258).toContain("@media (min-width: 901px)");
    expect(dashboardModernV258).toContain(".atlas-admin .atlas-admin-hero");
    expect(dashboardModernV258).toContain(".atlas-admin .audit-login-cell");
    expect(dashboardModernV258).toContain("@media (max-width: 760px)");
    expect(dashboardModernV258).toContain(".site-header .mobile-main-nav");
    expect(app).toContain('aria-label="Secciones principales en móvil"');
    expect(adminPanel).toContain("Login auditor");
    expect(adminPanel).toContain("Fecha y hora");
    expect(adminPanel).toContain("submittedByLogin");
    expect(database).toContain("JSON_VALUE(p.submitted_by_context,'$.login')");
    expect(database).toContain("JSON_VALUE(p.submitted_by_context,'$.userId')");
    expect(database).toContain("canonicalLogin = submitterReader.GetString(0)");
    expect(database).toContain("ug.group_name=a.grupo AND u.role='GROUP_ADMIN'");
    expect(database).toContain(
      "DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc)",
    );
    expect(database).toContain("login = canonicalLogin");
    expect(program).toContain('http.User.Identity?.Name');
    expect(unifiedWorkspaceV260).toContain("@media (min-width: 901px)");
    expect(unifiedWorkspaceV260).toContain(".operations-hero");
    expect(unifiedWorkspaceV260).toContain(".maintenance-board");
    expect(unifiedWorkspaceV260).toContain(".audit-ledger");
  });

  it("obliga al supervisor a resolver presencialmente desde la agencia con GPS", () => {
    expect(center).toContain("TICKET_AGENCY_RADIUS_METERS=75");
    expect(center).toContain("TICKET_GPS_MAX_ACCURACY_METERS=50");
    expect(center).toContain("captureTicketGps");
    expect(center).toContain("Resolución presencial desde la agencia");
    expect(center).toContain("Completa primero la Auditoría de campo de la agencia");
    expect(database).toContain('if(role=="GroupAdministrator")');
    expect(database).toContain("item.AccuracyMeters>50");
    expect(database).toContain("if(distance>75)");
    expect(database).toContain("La evidencia se capturó a");
  });

  it("separa los hallazgos en tablas por pregunta y permite filtrar grupos", () => {
    expect(program).toContain('MapGet("/api/tickets/findings"');
    expect(database).toContain("AutomaticFindings");
    expect(database).toContain("DAMAGE_WIRING");
    expect(database).toContain("DAMAGE_CONNECTIVITY");
    expect(database).toContain("af.assigned_department=@department");
    expect(database).toContain("DepartmentForRole(role)");
    expect(center).toContain("Hallazgos automáticos");
    expect(center).toContain("findingsByType");
    expect(center).toContain("Filtrar grupo");
    expect(center).toContain("finding-table-card");
  });

  it("detiene la actualización al vencer la sesión y valida el cierre", () => {
    expect(center).toContain("error.status = response.status");
    expect(center).toContain(
      "Escribe la solución aplicada antes de resolver o cerrar el ticket.",
    );
    expect(center).toContain("onSessionExpired()");
  });
});
