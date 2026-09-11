export type PortalSession = {
  authenticated: boolean;
  persistentSessionToken?: string | null;
  id: string;
  email: string;
  displayName: string;
  role:
    | "Administrator"
    | "Viewer"
    | "GroupAdministrator"
    | "Fiscalizador"
    | "Technology"
    | "GeneralServices"
    | "HumanResources";
  mustChangePassword: boolean;
  avatarUrl: string | null;
  scope: "all" | "assigned-groups" | "support-department";
  supportDepartment:
    "TECHNOLOGY" | "GENERAL_SERVICES" | "HUMAN_RESOURCES" | null;
  supportTeam:
    | "CALL_CENTER"
    | "TECHNICAL_FAILURE"
    | "TECHNICIANS"
    | "WAREHOUSE"
    | "WORKSHOP"
    | null;
  permissions: {
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
    canUseSupportChat: boolean;
    canManageSupportChat: boolean;
    canModerateSupportChat: boolean;
    canDeleteSupportConversations: boolean;
    canShareSupportConversations: boolean;
    canDeleteSupportTickets: boolean;
    canViewTicketNotifications: boolean;
  };
};

export function roleLabel(role: PortalSession["role"]): string {
  if (role === "Administrator") return "Administrador principal";
  if (role === "GroupAdministrator") return "Supervisor";
  if (role === "Fiscalizador") return "Fiscalizador";
  if (role === "Technology") return "Técnico de Tecnología";
  if (role === "GeneralServices") return "Técnico de Servicios Generales";
  if (role === "HumanResources") return "Recursos Humanos";
  return "Consulta general";
}

export function sessionRoleLabel(session: PortalSession): string {
  if (session.role === "Administrator") return "Administrador principal";
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
