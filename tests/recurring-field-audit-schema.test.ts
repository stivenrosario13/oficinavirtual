import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const schema = readFileSync(resolve(process.cwd(), "database-mssql/001_schema.sql"), "utf8");
const migration = readFileSync(resolve(process.cwd(), "database-mssql/048_recurring_field_audits.sql"), "utf8");

describe("esquema de levantamientos recurrentes", () => {
  it("no limita agency_profiles a una fila por agencia", () => {
    expect(schema).not.toContain("CONSTRAINT UQ_profiles_agency UNIQUE (agency_id)");
    expect(schema).toContain("IX_agency_profiles_agency_submitted");
  });

  it("migra instalaciones existentes de forma transaccional e idempotente", () => {
    expect(migration).toContain("SET XACT_ABORT ON");
    expect(migration).toContain("DROP CONSTRAINT UQ_profiles_agency");
    expect(migration).toContain("IF NOT EXISTS");
    expect(migration).toContain("CREATE INDEX IX_agency_profiles_agency_submitted");
    expect(migration).toContain("IF @@TRANCOUNT>0 ROLLBACK TRANSACTION");
  });
});
