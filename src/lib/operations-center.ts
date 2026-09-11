import {deviceTimestamp} from "./display";

export type OperationsTicketHistory = {
  id: string;
  action: string;
  actorName: string;
  comment?: string | null;
  fromDepartment?: string | null;
  toDepartment?: string | null;
  createdAt: string;
};

export type OperationsTicket = {
  id: string;
  ticketNumber: number;
  codigo: string;
  terminal: string;
  grupo: string;
  category: string;
  assignedDepartment: string;
  priority: string;
  status: string;
  subject: string;
  description: string;
  createdByName: string;
  createdAt: string;
  updatedAt: string;
  ticketType: "SUPPORT" | "INTERNAL";
  assignedTeam?: "CALL_CENTER" | "TECHNICAL_FAILURE" | "TECHNICIANS" | null;
  assignedTechnicianName?: string | null;
  history?: OperationsTicketHistory[] | null;
};

export type OperationsAuditEvent = {
  id: string;
  ticketId: string;
  ticketNumber: number;
  action: string;
  actorName: string;
  comment: string | null;
  fromDepartment: string | null;
  toDepartment: string | null;
  department: string;
  subject: string;
  agency: string;
  group: string;
  priority: string;
  status: string;
  ticketType: "SUPPORT" | "INTERNAL";
  createdAt: string;
};

export const departmentLabels: Record<string, string> = {
  TECHNOLOGY: "Tecnología",
  GENERAL_SERVICES: "Servicios Generales",
  HUMAN_RESOURCES: "Recursos Humanos",
};

export const departmentPrefixes: Record<string, string> = {
  TECHNOLOGY: "TEC",
  GENERAL_SERVICES: "SG",
  HUMAN_RESOURCES: "RRHH",
};

export const technologyAreaLabels: Record<string, string> = {
  CALL_CENTER: "Call Center",
  TECHNICAL_FAILURE: "Avería Técnica",
  TECHNICIANS: "Técnicos",
};

export const technologyAreaPrefixes: Record<string, string> = {
  CALL_CENTER: "CC",
  TECHNICAL_FAILURE: "AT",
  TECHNICIANS: "TEC",
};

export const activeTicketStatuses = new Set(["OPEN", "IN_PROGRESS", "PENDING"]);
export const priorityOrder = ["CRITICAL", "HIGH", "MEDIUM", "LOW"] as const;

export const turnCode = (ticket: Pick<OperationsTicket, "assignedDepartment" | "assignedTeam" | "ticketNumber">) =>
  `${technologyAreaPrefixes[ticket.assignedTeam || ""] || departmentPrefixes[ticket.assignedDepartment] || "TKT"}-${String(ticket.ticketNumber).padStart(4, "0")}`;

export function activeTicketsByTechnologyArea(tickets: OperationsTicket[]) {
  const grouped = new Map<string, OperationsTicket[]>();
  tickets
    .filter((ticket) => ticket.assignedDepartment === "TECHNOLOGY" && activeTicketStatuses.has(ticket.status))
    .forEach((ticket) => {
      const area = ticket.assignedTeam || (ticket.assignedTechnicianName ? "TECHNICIANS" : "TECHNICAL_FAILURE");
      grouped.set(area, [...(grouped.get(area) || []), ticket]);
    });
  grouped.forEach((items, area) => grouped.set(area, sortTicketsByPriority(items)));
  return grouped;
}

export function activeTicketsByDepartment(tickets: OperationsTicket[]) {
  const grouped = new Map<string, OperationsTicket[]>();
  tickets
    .filter((ticket) => activeTicketStatuses.has(ticket.status))
    .sort((left, right) => {
      const byDate = deviceTimestamp(left.createdAt) - deviceTimestamp(right.createdAt);
      return byDate || left.ticketNumber - right.ticketNumber;
    })
    .forEach((ticket) => {
      const department = ticket.assignedDepartment || "UNASSIGNED";
      grouped.set(department, [...(grouped.get(department) || []), ticket]);
    });
  return grouped;
}

export function sortTicketsByPriority(tickets: OperationsTicket[]) {
  return [...tickets].sort((left, right) => {
    const leftPriority = priorityOrder.indexOf(left.priority as (typeof priorityOrder)[number]);
    const rightPriority = priorityOrder.indexOf(right.priority as (typeof priorityOrder)[number]);
    const priorityDifference = (leftPriority < 0 ? priorityOrder.length : leftPriority) -
      (rightPriority < 0 ? priorityOrder.length : rightPriority);
    return priorityDifference || deviceTimestamp(left.createdAt) - deviceTimestamp(right.createdAt);
  });
}

export function buildTicketAuditEvents(tickets: OperationsTicket[]): OperationsAuditEvent[] {
  const events: OperationsAuditEvent[] = [];
  tickets.forEach((ticket) => {
    const history = Array.isArray(ticket.history) ? ticket.history : [];
    const hasCreatedEvent = history.some((event) => event.action.toUpperCase() === "CREATED");
    if (!hasCreatedEvent) {
      events.push({
        id: `${ticket.id}:created`,
        ticketId: ticket.id,
        ticketNumber: ticket.ticketNumber,
        action: "CREATED",
        actorName: ticket.createdByName || "Sistema",
        comment: ticket.description || null,
        fromDepartment: null,
        toDepartment: ticket.assignedDepartment || null,
        department: ticket.assignedDepartment,
        subject: ticket.subject,
        agency: `${ticket.codigo} · ${ticket.terminal}`,
        group: ticket.grupo,
        priority: ticket.priority,
        status: ticket.status,
        ticketType: ticket.ticketType,
        createdAt: ticket.createdAt,
      });
    }
    history.forEach((event) => {
      events.push({
        id: `${ticket.id}:${event.id}`,
        ticketId: ticket.id,
        ticketNumber: ticket.ticketNumber,
        action: event.action.toUpperCase(),
        actorName: event.actorName || "Sistema",
        comment: event.comment || null,
        fromDepartment: event.fromDepartment || null,
        toDepartment: event.toDepartment || null,
        department: event.toDepartment || ticket.assignedDepartment,
        subject: ticket.subject,
        agency: `${ticket.codigo} · ${ticket.terminal}`,
        group: ticket.grupo,
        priority: ticket.priority,
        status: ticket.status,
        ticketType: ticket.ticketType,
        createdAt: event.createdAt,
      });
    });
  });
  return events.sort(
    (left, right) =>
      deviceTimestamp(right.createdAt) - deviceTimestamp(left.createdAt) ||
      right.ticketNumber - left.ticketNumber,
  );
}

export function averageClosedMinutes(tickets: OperationsTicket[]) {
  const durations = tickets
    .filter((ticket) => ["RESOLVED", "CLOSED"].includes(ticket.status))
    .map((ticket) => deviceTimestamp(ticket.updatedAt) - deviceTimestamp(ticket.createdAt))
    .filter((duration) => Number.isFinite(duration) && duration >= 0);
  if (!durations.length) return 0;
  return Math.round(durations.reduce((total, duration) => total + duration, 0) / durations.length / 60000);
}
