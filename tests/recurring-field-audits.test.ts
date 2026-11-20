import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const app = readFileSync(resolve(process.cwd(), "src/App.tsx"), "utf8");
const panel = readFileSync(resolve(process.cwd(), "src/components/vite/AdminPanel.tsx"), "utf8");
const database = readFileSync(resolve(process.cwd(), "api/DatabaseSqlServerScoped.cs"), "utf8");

describe("auditorías de campo recurrentes", () => {
  it("mantiene las agencias auditadas disponibles y expone su historial", () => {
    expect(database).toContain("COUNT(p.id) AS audit_count");
    expect(database).toContain("MAX(p.submitted_at) AS last_audited_at");
    expect(database).toContain("a.status IN ('PENDING','COMPLETED','REVIEW_REQUIRED')");
    expect(database).not.toContain('if (status != "PENDING") throw new AgencyConflict();');
    expect(app).toContain("Las ya auditadas permanecen aquí para próximas visitas.");
    expect(app).toContain("auditorías realizadas");
  });

  it("marca cada auditoría como autorizada en Mi panel", () => {
    expect(panel).toContain("auditorías autorizadas");
    expect(panel).toContain("Autorizada</small>");
    expect(database).toContain("PROFILE_DELETED_AND_AGENCY_STATUS_RECALCULATED");
  });
});
