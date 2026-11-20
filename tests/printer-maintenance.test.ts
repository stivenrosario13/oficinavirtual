import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const form = readFileSync(resolve(process.cwd(), "src/components/vite/EmployeeAuditForm.tsx"), "utf8");
const panel = readFileSync(resolve(process.cwd(), "src/components/vite/AdminPanel.tsx"), "utf8");
const api = readFileSync(resolve(process.cwd(), "api/Program.cs"), "utf8");
const database = readFileSync(resolve(process.cwd(), "api/DatabaseSqlServerScoped.cs"), "utf8");

describe("mantenimiento de impresora", () => {
  it("permanece en el formulario y permite no aplica", () => {
    expect(form).toContain("¿Le dieron mantenimiento a la impresora?");
    expect(form).toContain("printerMaintained");
    expect(form).toContain("answerCount === 9");
    expect(form).toContain("allowNotApplicable: true");
  });

  it("se valida, almacena y presenta en el panel", () => {
    expect(api).toContain("PrinterMaintained");
    expect(api).toContain("Completa las respuestas de la auditoría");
    expect(database).toContain('["printerMaintained"]');
    expect(panel).toContain('["printerMaintained", "¿Le dieron mantenimiento a la impresora?"]');
  });

  it("registra el tipo de caja como la nueva verificación", () => {
    expect(form).toContain("¿Qué tipo de caja tiene la agencia?");
    expect(form).toContain('cashBoxType: "cash_register"');
    expect(form).toContain('cashBoxType: "wooden_box"');
    expect(api).toContain("answers.CashBoxType");
    expect(panel).toContain('["cashBoxType", "¿La agencia tiene caja registradora o caja de madera?"]');
  });
});
