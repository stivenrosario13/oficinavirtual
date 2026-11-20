import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const db = readFileSync(resolve("api/DatabaseSqlServerScoped.cs"), "utf8");
const api = readFileSync(resolve("api/Program.cs"), "utf8");
const push = readFileSync(resolve("api/PwaPushService.cs"), "utf8");
const app = readFileSync(resolve("src/App.tsx"), "utf8");
const center = readFileSync(resolve("src/components/vite/TicketCenter.tsx"), "utf8");

describe("hallazgos automáticos asignados", () => {
  it("limita la bandeja del técnico al responsable y genera un aviso al cambiar la asignación", () => {
    expect(db).toContain("@technicianOnly=0 OR af.assigned_technician_id=@userId");
    expect(db).toContain("!canAssignTickets?1:0");
    expect(db).toContain("automatic_finding_notifications");
    expect(db).toContain("previous is null?\"ASSIGNED\":\"REASSIGNED\"");
    expect(api).toContain("db.AutomaticFindingNotifications(scope.UserId,scope.Role,ct)");
    expect(api).toContain("broker.Publish(\"finding-assigned\")");
  });

  it("entrega push y abre el hallazgo desde la notificación", () => {
    expect(push).toContain("db.PendingFindingPushDeliveries(ct)");
    expect(push).toContain("notification=finding&findingId=");
    expect(app).toContain('key.startsWith("FINDING_EVENT:")');
    expect(app).toContain('kind:"FINDING",assignedDepartment:department,findingId');
    expect(center).toContain('navigationKind==="FINDING"');
    expect(db).toContain('key.StartsWith("FINDING_EVENT:",StringComparison.Ordinal)');
  });

  it("expone supervisor y filtro en tarjetas, y configura a Deliana como administradora de área", () => {
    expect(db).toContain("$.displayName");
    expect(center).toContain("findingSupervisorFilter");
    expect(center).toContain("row.supervisorLogin");
    expect(center).toContain("Login autor:");
    expect(db).toContain("auditAuthor.display_name");
    expect(db).toContain("deliana.solis@grupotejeda.local");
    expect(db).toContain("role='GENERAL_SERVICES',support_team=NULL");
    expect(api).toContain('values["canCancelFindingWithoutEvidence"]=true');
    expect(api).toContain('values["canCancelFindingWithoutReason"]=false');
  });
});
