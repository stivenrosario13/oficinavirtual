import { expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { isIncidentOperatorAccount } from "../src/lib/administrative-profiles";

const app = readFileSync("src/App.tsx", "utf8");
const database = readFileSync(".codex/decompiled-v495/Database.cs", "utf8");
const profiles = readFileSync(".codex/decompiled-v495/AdministrativeProfiles.cs", "utf8");

it("reconoce las variantes reales de la cuenta de Yessica", () => {
  expect(isIncidentOperatorAccount("yessica.mercedes@grupotejeda.local", "Yessica Mercedes")).toBe(true);
  expect(isIncidentOperatorAccount("yesica.mercedes", "Yesica Mercedes")).toBe(true);
});

it("abre la bandeja de incidentes y muestra su botón en escritorio y móvil", () => {
  expect(app).toContain('if (isIncidentOperatorAccount(authenticated.email, authenticated.displayName)) return "incidentReports"');
  expect(app.match(/!isAdministrator&&!isSupervisor&&incidentOperator&&<button/g)?.length).toBe(2);
  expect(app).toContain("Todos los informes de incidentes creados por supervisores");
});

it("persiste en Yessica los permisos completos del informe de incidentes", () => {
  for (const permission of ["canViewDocuments", "canReceiveDocuments", "canSignDocuments", "canDecideDocuments"])
    expect(database).toContain(`'$.${permission}',CAST(1 AS bit)`);
  expect(database).toContain("N'yesica.mercedes@grupotejeda.local'");
  expect(profiles).toContain('"yessica.mercedes"');
  expect(profiles).toContain("CanReadIncidentReports");
  expect(profiles).toContain("CanDecideIncidentTickets");
});
