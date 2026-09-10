import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const databaseSource = readFileSync(
  resolve(process.cwd(), "api/DatabaseSqlServerScoped.cs"),
  "utf8",
);
const adminPanelSource = readFileSync(
  resolve(process.cwd(), "src/components/vite/AdminPanel.tsx"),
  "utf8",
);

describe("aislamiento de administradores de grupo", () => {
  it("aplica alcance por admin_user_groups en todas las consultas sensibles", () => {
    const scopePredicates = databaseSource.match(
      /ug\.user_id=@userId AND ug\.group_name=a\.grupo/g,
    );

    // Agencia, perfil, grupos, agencias, dashboard, auditorías y detalle.
    expect(scopePredicates?.length).toBeGreaterThanOrEqual(6);
    expect(databaseSource).toContain(
      'var scopeSql = role == "GroupAdministrator"',
    );
    expect(databaseSource).toContain("{scopeSql}");
  });

  it("solo activa el filtro estricto para GroupAdministrator", () => {
    expect(databaseSource).toContain(
      'role == "GroupAdministrator" ? 1 : 0',
    );
  });

  it("muestra agencias asignadas y evidencias al administrador de grupo", () => {
    expect(adminPanelSource).toContain("Avance por agencias asignadas");
    expect(adminPanelSource).toContain(
      'isGroupAdministrator ? "Evidencias" : "Auditorías completadas"',
    );
    expect(adminPanelSource).toContain('setSectionTab("agencies")');
  });

  it("recupera las agencias asignadas si falla el resumen del panel", () => {
    expect(adminPanelSource).toContain("loadDashboardFallback");
    expect(adminPanelSource).toContain('fetch("/api/groups")');
    expect(adminPanelSource).toContain(
      "`/api/agencies?grupo=${encodeURIComponent(group.grupo)}`",
    );
    expect(adminPanelSource).toContain(
      "Se cargaron tus ${agencies.length} agencias asignadas",
    );
  });

  it("hidrata siempre el avance de agencias del supervisor", () => {
    expect(adminPanelSource).toContain('fetch("/api/panel/groups")');
    expect(adminPanelSource).toContain("`/api/panel/agencies?grupo=");
    expect(adminPanelSource).toContain(
      'if (session.role === "GroupAdministrator")',
    );
  });
});
