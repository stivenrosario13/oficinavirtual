import type { PortalSession } from "./session";

type InternetTicketCandidate = {
  ticketType: string;
  assignedDepartment: string;
  status: string;
};

const closedStatuses = new Set(["RESOLVED", "CLOSED", "CANCELLED"]);

export function canRequestModemChange(
  session: Pick<PortalSession, "role" | "supportTeam">,
  ticket: InternetTicketCandidate,
): boolean {
  const permittedActor =
    session.role === "Administrator" ||
    (session.role === "Technology" && session.supportTeam === "TECHNICAL_FAILURE");

  return (
    permittedActor &&
    ticket.ticketType === "INTERNAL" &&
    ticket.assignedDepartment === "TECHNOLOGY" &&
    !closedStatuses.has(ticket.status)
  );
}
