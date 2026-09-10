import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const program = readFileSync(resolve(process.cwd(), "api/Program.cs"), "utf8");
const database = readFileSync(resolve(process.cwd(), "api/DatabaseSqlServerScoped.cs"), "utf8");
const panel = readFileSync(resolve(process.cwd(), "src/components/vite/AdminPanel.tsx"), "utf8");
const session = readFileSync(resolve(process.cwd(), "src/lib/session.ts"), "utf8");
const migration = readFileSync(resolve(process.cwd(), "database-mssql/014_fiscalizador_permission.sql"), "utf8");

describe("permiso Fiscalizador", () => {
  it("ve todas las auditorías y exporta, sin permisos de administración", () => {
    expect(program).toContain('"Fiscalizador"');
    expect(program).toContain('canExportAudits=account.Role is "Administrator" or "Fiscalizador"');
    expect(program).toContain('canManage=account.Role=="Administrator"');
    expect(database).toContain('"FISCALIZADOR" => "Fiscalizador"');
    expect(database).toContain('role is "Administrator" or "Viewer" or "Fiscalizador"');
    expect(session).toContain('| "Fiscalizador"');
    expect(panel).toContain('"FISCALIZADOR"');
  });

  it("incluye la migración compatible con SQL Server", () => {
    expect(migration).toContain("'FISCALIZADOR'");
    expect(migration).toContain("CK_admin_users_role");
  });
});
