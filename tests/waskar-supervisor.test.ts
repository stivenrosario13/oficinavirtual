import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const program = readFileSync(resolve("api/Program.cs"), "utf8");
const database = readFileSync(resolve("api/DatabaseSqlServerScoped.cs"), "utf8");
const recovery = readFileSync(resolve("api/LegacyAccountRecovery.cs"), "utf8");
const importUsers = readFileSync(resolve("database-mssql/008_import_group_admins.sql"), "utf8");
const app = readFileSync(resolve("src/App.tsx"), "utf8");

describe("Waskar supervisor access", () => {
  it("keeps the account active as a scoped group supervisor with its assigned group", () => {
    expect(importUsers).toContain("waskar.sepulveda@grupotejeda.local");
    expect(importUsers).toContain("N'146-WASCAR SP'");
    expect(importUsers).toContain("role='GROUP_ADMIN'");
    expect(importUsers).toContain("is_active=1");
    expect(database).toContain('"GROUP_ADMIN" => "GroupAdministrator"');
  });

  it("allows normal login and preserves the personal password after recovery", () => {
    expect(program).toContain("var email=NormalizeLogin(login.Email)");
    expect(program).toContain('$"{login}@grupotejeda.local"');
    expect(database).toContain("WHERE email=@email AND is_active=1");
    expect(recovery).toContain("V428_WASKAR_TEMPORARY_PASSWORD");
    expect(recovery).toContain("later password changes are preserved");
  });

  it("grants lifting and evidence uploads to group supervisors", () => {
    expect(program).toContain('options.AddPolicy("FieldAudit", policy => policy.RequireRole("Administrator", "GroupAdministrator"))');
    expect(program).toContain('"canAudit",role is "Administrator" or "GroupAdministrator"');
    expect(program).toContain('MapPost("/api/submit"');
    expect(program).toContain('MapPost("/api/image"');
    expect(program).toContain('.RequireAuthorization("FieldAudit")');
    expect(database).toContain('role == "GroupAdministrator" ? 1 : 0');
    expect(app).toContain('view === "audit" && session.permissions.canAudit');
    expect(app).toContain("<EmployeeAuditForm");
  });
});
