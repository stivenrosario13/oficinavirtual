import type { PortalSession } from "@/lib/session";

export const canAccessTicketSection = (session: PortalSession) =>
  session.role === "GroupAdministrator" ||
  session.permissions.canCreateTickets ||
  session.permissions.canViewSupportDashboard ||
  session.permissions.canUseSupportChat;
