import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const read = (path: string) => readFileSync(resolve(process.cwd(), path), "utf8");

describe("permisos de resolución de tickets", () => {
  it("mantiene habilitada la resolución para soporte y administradores departamentales", () => {
    const program = read("api/Program.cs");
    expect(program).toContain("IsDepartmentTicketResolver");
    expect(program).toContain('values["canResolveTickets"]=true');
    expect(program).toContain('role is "Administrator" or "Technology" or "GeneralServices" or "HumanResources"');
  });

  it("reconoce asignaciones actuales e históricas al mostrar la acción de resolver", () => {
    const center = read("src/components/vite/TicketCenter.tsx");
    const database = read("api/DatabaseSqlServerScoped.cs");
    const program = read("api/Program.cs");
    expect(center).toContain("ticketIsAssignedToCurrentUser");
    expect(center).toContain("ticket.isAssignedToCurrentUser === true");
    expect(center).toContain("[session.id, session.email]");
    expect(center).toContain("? ticketIsAssignedToCurrentUser(ticket)");
    expect(database).toContain("IsAssignedToCurrentUser=ReadBooleanValue(reader,29)");
    expect(database).toContain("assigned_account.email");
    expect(program).toContain("public bool IsAssignedToCurrentUser { get; init; }");
  });

  it("muestra expediente completo a técnicos asignados de Tecnología y Servicios Generales", () => {
    const center = read("src/components/vite/TicketCenter.tsx");
    const database = read("api/DatabaseSqlServerScoped.cs");
    expect(center).toContain('const canViewTicketEvidence = (ticket: Ticket) =>');
    expect(center).toContain('(session.role === "Technology" || session.role === "GeneralServices" || session.role === "GroupAdministrator") &&');
    expect(center).toContain('ticketIsAssignedToCurrentUser(ticket))');
    expect(center).toMatch(/canViewTicketEvidence\(ticket\)\s*&&\s*\(/);
    expect(center).toContain("ticketBelongsToPersonalTechnician(ticket, session)");
    expect(center).toMatch(/ticketIsAssignedToCurrentUser\(ticket\)\s*&&\s*!resolvedStatuses\.has\(ticket\.status\)/);
    expect(database).toContain('SELECT h.ticket_id,h.id,h.action');
    expect(database).toContain('SELECT e.ticket_id,e.id,e.file_name');
    expect(database).toContain('public async Task<(byte[] Data,string Mime,string FileName)?> TicketEvidencePhoto');
  });

  it("exige GPS al técnico que resuelve, pero no al soporte de Call Center", () => {
    const database = read("api/DatabaseSqlServerScoped.cs");
    expect(database).toContain('role=="Technology"&&supportTeam=="TECHNICIANS"');
    expect(database).toContain('(role is "GeneralServices" or "HumanResources")&&!canAssignTickets');
    expect(database).not.toContain("assignedTechnologyTechnician");
    expect(database).not.toContain('state.TechnicianId==userId&&evidence.Any');
  });

  it("permite quitar cualquier imagen seleccionada antes de guardar la resolución", () => {
    const center = read("src/components/vite/TicketCenter.tsx");
    const styles = read("src/notification-links.css");
    expect(center).toContain("removeTicketEvidenceFile");
    expect(center).toContain('className="ticket-remove-evidence"');
    expect(center).toContain("evidenceIndex !== index");
    expect(styles).toContain(".ticket-remove-evidence");
  });
});
