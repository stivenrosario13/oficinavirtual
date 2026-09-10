import { describe, expect, it } from "vitest";
import {
  activeTicketsByDepartment,
  buildTicketAuditEvents,
  sortTicketsByPriority,
  turnCode,
  type OperationsTicket,
} from "../src/lib/operations-center";

const ticket = (overrides: Partial<OperationsTicket> = {}): OperationsTicket => ({
  id: "ticket-1",
  ticketNumber: 42,
  codigo: "6010300",
  terminal: "Villa Mella V",
  grupo: "Grupo Tejeda",
  category: "EQUIPMENT",
  assignedDepartment: "TECHNOLOGY",
  priority: "HIGH",
  status: "OPEN",
  subject: "Falla de impresora",
  description: "La impresora no responde.",
  createdByName: "Steven",
  createdAt: "2026-08-31T12:00:00Z",
  updatedAt: "2026-08-31T12:00:00Z",
  ticketType: "SUPPORT",
  history: [],
  ...overrides,
});

describe("centro de operaciones", () => {
  it("ordena la cola FIFO de cada departamento y excluye los finalizados", () => {
    const queues = activeTicketsByDepartment([
      ticket({ id: "later", ticketNumber: 2, createdAt: "2026-08-31T12:10:00Z" }),
      ticket({ id: "closed", ticketNumber: 3, status: "CLOSED" }),
      ticket({ id: "first", ticketNumber: 1, createdAt: "2026-08-31T12:01:00Z" }),
    ]);
    expect(queues.get("TECHNOLOGY")?.map((item) => item.id)).toEqual(["first", "later"]);
  });

  it("crea el evento inicial cuando un ticket antiguo no tiene historial", () => {
    const events = buildTicketAuditEvents([ticket()]);
    expect(events).toHaveLength(1);
    expect(events[0]).toMatchObject({ action: "CREATED", actorName: "Steven", ticketNumber: 42 });
  });

  it("conserva actor, comentario y ruta de los eventos registrados", () => {
    const events = buildTicketAuditEvents([
      ticket({
        history: [{
          id: "history-1",
          action: "ROUTED",
          actorName: "Soporte",
          comment: "Enviar a taller",
          fromDepartment: "TECHNOLOGY",
          toDepartment: "GENERAL_SERVICES",
          createdAt: "2026-08-31T12:30:00Z",
        }],
      }),
    ]);
    expect(events[0]).toMatchObject({
      action: "ROUTED",
      actorName: "Soporte",
      comment: "Enviar a taller",
      department: "GENERAL_SERVICES",
    });
  });

  it("genera un código de turno legible por departamento", () => {
    expect(turnCode(ticket())).toBe("TEC-0042");
  });

  it("ordena la vista departamental por prioridad y luego por antigüedad", () => {
    const sorted = sortTicketsByPriority([
      ticket({ id: "medium", priority: "MEDIUM", createdAt: "2026-08-31T10:00:00Z" }),
      ticket({ id: "critical-new", priority: "CRITICAL", createdAt: "2026-08-31T11:00:00Z" }),
      ticket({ id: "high", priority: "HIGH", createdAt: "2026-08-31T09:00:00Z" }),
      ticket({ id: "critical-old", priority: "CRITICAL", createdAt: "2026-08-31T08:00:00Z" }),
    ]);
    expect(sorted.map((item) => item.id)).toEqual(["critical-old", "critical-new", "high", "medium"]);
  });
});
