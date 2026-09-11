import { describe, expect, it } from "vitest";
import {
  activeTicketsByTechnologyArea,
  operationalQueueStart,
  ticketsInOperationalQueue,
  turnCode,
  type OperationsTicket,
} from "../src/lib/operations-center";

const ticket = (
  id: string,
  assignedTeam: OperationsTicket["assignedTeam"],
  priority: string,
  createdAt: string,
  overrides: Partial<OperationsTicket> = {},
): OperationsTicket => ({
  id,
  ticketNumber: Number(id),
  codigo: `A-${id}`,
  terminal: `Agencia ${id}`,
  grupo: "Grupo Norte",
  category: "INTERNET",
  assignedDepartment: "TECHNOLOGY",
  assignedTeam,
  priority,
  status: "OPEN",
  subject: `Incidencia ${id}`,
  description: "Prueba",
  createdByName: "Supervisor",
  createdAt,
  updatedAt: createdAt,
  ticketType: "SUPPORT",
  ...overrides,
});

describe("cola tecnológica por áreas", () => {
  it("separa Call Center, Avería Técnica y Técnicos", () => {
    const queues = activeTicketsByTechnologyArea([
      ticket("1", "CALL_CENTER", "MEDIUM", "2026-09-08T10:00:00Z"),
      ticket("2", "TECHNICAL_FAILURE", "HIGH", "2026-09-08T10:01:00Z"),
      ticket("3", "TECHNICIANS", "LOW", "2026-09-08T10:02:00Z"),
    ]);
    expect(queues.get("CALL_CENTER")?.map((item) => item.id)).toEqual(["1"]);
    expect(queues.get("TECHNICAL_FAILURE")?.map((item) => item.id)).toEqual(["2"]);
    expect(queues.get("TECHNICIANS")?.map((item) => item.id)).toEqual(["3"]);
  });

  it("ordena por prioridad y luego por antigüedad", () => {
    const queues = activeTicketsByTechnologyArea([
      ticket("4", "TECHNICAL_FAILURE", "LOW", "2026-09-08T09:00:00Z"),
      ticket("5", "TECHNICAL_FAILURE", "CRITICAL", "2026-09-08T10:00:00Z"),
      ticket("6", "TECHNICAL_FAILURE", "CRITICAL", "2026-09-08T08:00:00Z"),
    ]);
    expect(queues.get("TECHNICAL_FAILURE")?.map((item) => item.id)).toEqual(["6", "5", "4"]);
  });

  it("usa un prefijo distinto para cada área", () => {
    expect(turnCode(ticket("7", "CALL_CENTER", "MEDIUM", "2026-09-08T10:00:00Z"))).toBe("CC-0007");
    expect(turnCode(ticket("8", "TECHNICAL_FAILURE", "MEDIUM", "2026-09-08T10:00:00Z"))).toBe("AT-0008");
    expect(turnCode(ticket("9", "TECHNICIANS", "MEDIUM", "2026-09-08T10:00:00Z"))).toBe("TEC-0009");
  });

  it("reinicia la cola local a las 07:30 sin borrar el historial", () => {
    const afterCutoff = new Date(2026, 8, 11, 8, 0, 0);
    const beforeCutoff = new Date(2026, 8, 11, 7, 29, 0);
    expect(operationalQueueStart(afterCutoff)).toEqual(new Date(2026, 8, 11, 7, 30, 0));
    expect(operationalQueueStart(beforeCutoff)).toEqual(new Date(2026, 8, 10, 7, 30, 0));
    const visible = ticketsInOperationalQueue([
      ticket("10", "CALL_CENTER", "HIGH", "2026-09-11T14:00:00Z"),
      ticket("11", "CALL_CENTER", "HIGH", "2026-09-10T14:00:00Z"),
    ], afterCutoff);
    expect(visible.map((item) => item.id)).toEqual(["10"]);
  });
});
