import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { ticketBelongsToPersonalTechnician } from "../src/components/vite/TicketCenter";

const read = (path: string) =>
  readFileSync(resolve(process.cwd(), path), "utf8");
const store = read("api/InstitutionalDocumentStore.cs");
const database = read("api/DatabaseSqlServerScoped.cs");
const requests = read("src/components/vite/WarehousePdfRequests.tsx");
const tickets = read("src/components/vite/TicketCenter.tsx");
const app = read("src/App.tsx");

describe("V403 limpieza de Almacén y técnicos de Servicios Generales", () => {
  it("elimina una sola vez únicamente las solicitudes y asignaciones de Almacén", () => {
    expect(store).toContain("V403_CLEAR_WAREHOUSE_REQUESTS");
    expect(store).toContain("DELETE allocation");
    expect(store).toContain(
      "JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL",
    );
    expect(store).toContain(
      "operational_area='WAREHOUSE' AND movement_type='REQUEST'",
    );
  });

  it("registra solicitudes vinculadas a tickets como pendientes de recibir", () => {
    expect(store).toContain('d.Status="pending"');
    expect(requests).toContain("requesterStatus(item) === status");
    expect(requests).toContain('requesterStatus(item) === "pending"');
    expect(requests).toContain("Ticket #${item.warehouseRequest.ticketNumber}");
  });

  it("muestra al técnico solo sus casos asignados y resueltos, sin cola en vivo", () => {
    expect(tickets).toContain("Mis casos asignados");
    expect(tickets).toContain("Mis casos resueltos");
    expect(tickets).toContain('setAssignmentScope("MINE")');
    expect(tickets).toContain("ticketIsAssignedToCurrentUser(ticket)");
    expect(tickets).toContain(
      "ticketBelongsToPersonalTechnician(ticket, session)",
    );
    expect(app).toContain(
      "const canOpenLiveQueue=Boolean(!isPersonalTechnician&&",
    );
    expect(database).toContain(
      "assignment.is_current=1 AND t.assigned_department=@supportDepartment",
    );
    expect(database).toContain(
      "CHARINDEX('@',LTRIM(RTRIM(actor_account.email))+'@')-1",
    );
    expect(database).toContain("assignment.is_current=1");
  });

  it("separa los casos por técnico y departamento", () => {
    const session = {
      id: "adonis-id",
      email: "adonis.batista@grupotejeda.local",
      supportDepartment: "GENERAL_SERVICES",
    } as const;
    expect(
      ticketBelongsToPersonalTechnician(
        {
          assignedDepartment: "GENERAL_SERVICES",
          assignedTechnicianId: "adonis-id",
          isAssignedToCurrentUser: true,
        },
        session,
      ),
    ).toBe(true);
    expect(
      ticketBelongsToPersonalTechnician(
        {
          assignedDepartment: "TECHNOLOGY",
          assignedTechnicianId: "adonis-id",
          isAssignedToCurrentUser: true,
        },
        session,
      ),
    ).toBe(false);
    expect(
      ticketBelongsToPersonalTechnician(
        {
          assignedDepartment: "GENERAL_SERVICES",
          assignedTechnicianId: "otro-id",
          isAssignedToCurrentUser: false,
        },
        session,
      ),
    ).toBe(false);
    expect(
      ticketBelongsToPersonalTechnician(
        {
          assignedDepartment: "GENERAL_SERVICES",
          assignedTechnicianId: "adonis.batista@grupotejeda.local",
          isAssignedToCurrentUser: false,
        },
        session,
      ),
    ).toBe(true);
  });
});
