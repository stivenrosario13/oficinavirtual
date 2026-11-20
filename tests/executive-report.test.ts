import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";

describe("reporte ejecutivo exclusivo", () => {
  const app = readFileSync("src/App.tsx", "utf8");
  const api = readFileSync("api/Program.cs", "utf8");
  const report = readFileSync("src/components/vite/executiveReportPdf.ts", "utf8");
  const component = readFileSync("src/components/vite/ExecutiveReports.tsx", "utf8");
  const sectionPdf = readFileSync("src/components/vite/executiveSectionPdf.ts", "utf8");
  const data = readFileSync("api/ExecutiveReport.cs", "utf8");

  it("solo se muestra y sirve al administrador principal", () => {
    expect(app).toContain("isAdministrator&&<button type=\"button\" className={view==='executiveReports'");
    expect(app).toContain('view === "executiveReports" && isAdministrator');
    expect(api).toContain('context.User.IsInRole("Administrator")');
    expect(api).toContain("db.ExecutiveReport(from, to, ct)");
  });

  it("permite consultar un calendario desde y hasta", () => {
    expect(component).toContain('input type="date" value={from}');
    expect(component).toContain('input type="date" value={to}');
    expect(component).toContain('params.set("from", selectedFrom)');
    expect(component).toContain('params.set("to", selectedTo)');
    expect(api).toContain('Request.Query["from"]');
    expect(api).toContain('Request.Query["to"]');
  });

  it("aplica el rango en cada origen de datos", () => {
    expect(data).toContain("requestedFrom");
    expect(data).toContain("created_at>=@from AND created_at<=@to");
    expect(data).toContain("BETWEEN @from AND @to");
    expect(data).toContain("p.submitted_at BETWEEN @from AND @to");
    expect(data).toContain("maintenance_workshop_events");
    expect(data).toContain("'SUPERVISOR' role_name");
    expect(data).toContain("THEN 'TECNICO' ELSE 'SOPORTE'");
    expect(data).toContain("IsOptionalReportQueryFailure(exception)");
    expect(data).toContain("exception.Number != 18456");
    expect(data).toContain("exception.Number != 11001");
    expect(data).toContain("ReadOptional");
    expect(data).toContain("CASE WHEN ISJSON(metadata)=1 THEN metadata ELSE N'{}' END");
    expect(data).toContain("CASE WHEN ISJSON(f.metadata)=1 THEN f.metadata ELSE N'{}' END");
    expect(data).toContain("CASE WHEN ISJSON(p.submitted_by_context)=1 THEN p.submitted_by_context ELSE N'{}' END");
    expect(data).not.toMatch(/JSON_VALUE\(f\.metadata/);
  });

  it("descarga el global y cada sección individual en PDF", () => {
    expect(component).toContain("buildExecutiveSectionPdf");
    expect(component).toContain("downloadGlobal");
    expect(component).toContain("downloadSection");
    expect(component).toContain("metricPdf(metric)");
    expect(component).toContain("departmentPdf(department)");
    expect(component).toContain("auditGroupPdf(row)");
    expect(sectionPdf).toContain("export async function buildExecutiveSectionPdf");
  });

  it("mantiene todos los desgloses del PDF global", () => {
    for (const title of ["Resumen ejecutivo global", "Departamento · ${departmentName}", "Tickets de soporte", "Hallazgos de auditoría", "Detalle de hallazgos y soluciones", "Responsables de hallazgos", "Solicitudes a Almacén", "Actividad de Taller", "Hallazgos automáticos · detalle completo", "Tickets de incidentes por grupo", "Auditorías por grupo", "Detalle de auditorías por agencia"]) expect(report).toContain(title);
    expect(report).toContain("periodLabel");
  });

  it("detalla todo el ciclo de los hallazgos automáticos", () => {
    for (const field of ["findingDetails", "diagnosis", "solution", "verification", "recommendations", "assignedTechnician", "resolvedBy", "resolutionMinutes", "evidenceCount", "supervisor", "address"]) expect(component).toContain(field);
    expect(component).toContain("Diagnósticos, soluciones y trazabilidad");
    expect(component).toContain("Trabajo y solución aplicada");
    expect(data).toContain("ExecutiveFindingDetailRow");
    expect(data).toContain("finding_resolution_evidence");
    expect(data).toContain("af.resolution_minutes");
    expect(report).toContain("row.recommendations");
    expect(report).toContain("row.evidenceCount");
  });

  it("usa el código, nombre y grupo reales de las agencias", () => {
    expect(data).toContain("ISNULL(a.codigo,N'—'),ISNULL(a.terminal,N'—'),ISNULL(a.grupo,N'Sin grupo')");
    expect(data).not.toContain("Agencia de demostración");
  });
});
