import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const form = readFileSync(resolve(process.cwd(), "src/components/vite/EmployeeAuditForm.tsx"), "utf8");
const panel = readFileSync(resolve(process.cwd(), "src/components/vite/AdminPanel.tsx"), "utf8");
const api = readFileSync(resolve(process.cwd(), "api/Program.cs"), "utf8");
const database = readFileSync(resolve(process.cwd(), "api/DatabaseSqlServerScoped.cs"), "utf8");

describe("mantenimiento de impresora", () => {
  it("aparece como octava pregunta y permite no aplica", () => {
    expect(form).toContain("¿Le dieron mantenimiento a la impresora?");
    expect(form).toContain("printerMaintained");
    expect(form).toContain("answerCount === 8");
    expect(form).toContain("allowNotApplicable: true");
  });

  it("se valida, almacena y presenta en el panel", () => {
    expect(api).toContain("PrinterMaintained");
    expect(api).toContain("Completa las ocho respuestas");
    expect(database).toContain('["printerMaintained"]');
    expect(panel).toContain('["printerMaintained", "¿Le dieron mantenimiento a la impresora?"]');
  });
});
