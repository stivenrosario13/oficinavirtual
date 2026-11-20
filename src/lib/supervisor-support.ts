export const supervisorSupportDepartments = ['TECHNOLOGY', 'GENERAL_SERVICES', 'HUMAN_RESOURCES'] as const;

export function supervisorCanRequestSupport(department: string) {
  return supervisorSupportDepartments.some(code => code === department);
}

// Never compare display names: different people can have the same name.
export function supervisorCreatedTicket(ticket: { isCreatedByCurrentUser?: boolean; createdByUsername?: string | null }, session: { email?: string | null }) {
  if (typeof ticket.isCreatedByCurrentUser === 'boolean') return ticket.isCreatedByCurrentUser;
  const login = session.email?.trim().toLowerCase();
  return !!login && ticket.createdByUsername?.trim().toLowerCase() === login;
}

export function ticketAssignedToSession(ticket: {isAssignedToCurrentUser?: boolean; assignedTechnicianId?: string | null}, session: {id?: string | null; email?: string | null}) {
  if (typeof ticket.isAssignedToCurrentUser === 'boolean') return ticket.isAssignedToCurrentUser;
  const assignee = ticket.assignedTechnicianId?.trim().toLowerCase();
  return !!assignee && [session.id, session.email].some(value => !!value?.trim() && value.trim().toLowerCase() === assignee);
}

export type SupervisorTrackingView = 'process' | 'resolved';
export function supervisorTracksTicket(ticket: {status: string; isCreatedByCurrentUser?: boolean; createdByUsername?: string | null; isAssignedToCurrentUser?: boolean; assignedTechnicianId?: string | null}, session: {id?: string | null; email?: string | null}, view: SupervisorTrackingView) {
  if (!supervisorCreatedTicket(ticket, session)) return false;
  const terminal = ['RESOLVED', 'CLOSED', 'CANCELLED'].includes(ticket.status);
  return view === 'process' ? !terminal : ['RESOLVED', 'CLOSED'].includes(ticket.status) && !ticketAssignedToSession(ticket, session);
}
