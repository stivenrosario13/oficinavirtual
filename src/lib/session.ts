import {userDepartments} from './user-departments';
import {isAdministrativeProfile} from './administrative-profiles';
export type PortalSession = {
  authenticated: boolean;
  persistentSessionToken?: string | null;
  id: string;
  email: string;
  displayName: string;
  role:
    | "Administrator"
    | "Administrative"
    | "Department"
    | "Viewer"
    | "GroupAdministrator"
    | "Fiscalizador"
    | "Technology"
    | "GeneralServices"
    | "HumanResources"
    | "Warehouse";
  mustChangePassword: boolean;
  avatarUrl: string | null;
  scope: "all" | "assigned-groups" | "support-department";
  supportDepartment: string | null;
  assignedDepartment?: string | null;
  supportTeam:
    | "CALL_CENTER"
    | "TECHNICAL_FAILURE"
    | "TECHNICIANS"
    | "WAREHOUSE"
    | "WORKSHOP"
    | null;
  permissions: Partial<Record<import('./operational-permissions').OperationalPermissionKey,boolean>> & {
    "profile:WAREHOUSE_REQUESTER"?: boolean;
    "profile:SUPPORT"?: boolean;
    "profile:ADMINISTRATIVE"?: boolean;
    "profile:DEPARTMENT_ADMINISTRATIVE"?: boolean;
    usesExplicitPermissions?: boolean;
    canViewFieldAudits?: boolean;
    canManageDepartment?: boolean;
    canView: boolean;
    canManage: boolean;
    canManageUsers: boolean;
    canManageAudits: boolean;
    canManageAgencies: boolean;
    canViewDashboard: boolean;
    canAudit: boolean;
    canCreateTickets: boolean;
    canAssignTickets: boolean;
    canResolveTickets: boolean;
    canConfigureProfile: boolean;
    canExportAudits: boolean;
    canViewSupportDashboard: boolean;
    canViewAllSupportDepartments: boolean;
    canConfigureSupport: boolean;
    canViewSupportFindings: boolean;
    canCancelFindingWithEvidence: boolean;
    canCancelFindingWithoutEvidence: boolean;
    canCancelFindingWithReason: boolean;
    canCancelFindingWithoutReason: boolean;
    canUseSupportChat: boolean;
    canManageSupportChat: boolean;
    canModerateSupportChat: boolean;
    canDeleteSupportConversations: boolean;
    canShareSupportConversations: boolean;
    canDeleteSupportTickets: boolean;
    canViewTicketNotifications: boolean;
    canAuthorizeWarehouseRequests: boolean;
    canLoadWarehouseInventory: boolean;
    canCreateWarehouseArticles?: boolean;
  };
};

export function roleLabel(role: PortalSession["role"]): string {
  if (role === "Administrative") return "Administrativo";
  if (role === "Administrator") return "Administrador principal";
  if (role === "GroupAdministrator") return "Supervisor";
  if (role === "Fiscalizador") return "Fiscalizador";
  if (role === "Technology") return "Técnico de Tecnología";
  if (role === "GeneralServices") return "Técnico de Servicios Generales";
  if (role === "HumanResources") return "Recursos Humanos";
  if (role === "Warehouse") return "Almacén";
  return "Consulta general";
}

export function sessionRoleLabel(session: PortalSession): string {
  if(session.role==='Department')return `${session.permissions.canManageDepartment?'Gerente':isAdministrativeProfile(session)?'Administrativo':'Usuario'} · ${userDepartments.find(([code])=>code===session.assignedDepartment)?.[1]||'Departamento'}`;
  if (session.role === "Administrator") return "Administrador principal";
  if (session.role === "Administrative") return session.permissions.canManageDepartment ? "Gerente · Administración" : "Administrativo";
  if(session.permissions.canManageDepartment){
    const area=session.role==='Warehouse'||session.supportTeam==='WAREHOUSE'?'Almacén':session.supportTeam==='WORKSHOP'?'Taller':session.role==='Technology'?(session.supportTeam==='CALL_CENTER'?'Call Center':session.supportTeam==='TECHNICAL_FAILURE'?'Avería Técnica':'Tecnología'):session.role==='GeneralServices'?'Servicios Generales':session.role==='HumanResources'?'Recursos Humanos':null;
    if(area)return `Gerente · ${area}`;
  }
  if (session.role === "GroupAdministrator") return "Supervisor de agencia";
  if (session.supportDepartment) {
    const department =
      session.supportDepartment === "TECHNOLOGY"
        ? "Tecnología"
        : session.supportDepartment === "GENERAL_SERVICES"
          ? "Servicios Generales"
          : "Recursos Humanos";
    if (session.supportTeam === "WAREHOUSE") return "Operador · Almacén central";
    if (session.supportTeam === "WORKSHOP") return `Operador de Taller · ${department}`;
    if (!session.permissions.canAssignTickets) return `Técnico · ${department}`;
    if (session.supportTeam === "CALL_CENTER") return "Soporte Call Center · Tecnología";
    if (session.supportTeam === "TECHNICAL_FAILURE") return "Soporte Avería · Tecnología";
    if (!session.supportTeam && session.permissions.canAssignTickets) return `Administrador de departamento · ${department}`;
    return `Soporte · ${department}`;
  }
  return roleLabel(session.role);
}
