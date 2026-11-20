import {describe,expect,it} from "vitest";
import {readFileSync} from "node:fs";

const app=readFileSync("src/App.tsx","utf8");
const operations=readFileSync("src/components/vite/OperationsCenter.tsx","utf8");
const support=readFileSync("src/components/vite/TicketCenter.tsx","utf8");
const files=readFileSync("src/components/vite/AdministrativeWorkspace.tsx","utf8");
const database=readFileSync("api/DatabaseSqlServerScoped.cs","utf8");

describe("V399 · cola y accesos por departamento",()=>{
  it("habilita la cola para cada departamento y deja al administrador sin filtro",()=>{
    expect(app).toContain("(isAdministrator||session.supportDepartment)&&session.permissions.canViewSupportDashboard");
    expect(app).toContain('departmentScope={view === "queue" && !isAdministrator ? (session.supportDepartment || selectedSupportDepartment || null) : null}');
    expect(app).toContain("teamScope={null}");
  });

  it("aplica el mismo alcance departamental a tickets, historial y hallazgos",()=>{
    expect(operations).toContain("tickets.filter(ticket => ticket.assignedDepartment === departmentScope)");
    expect(operations).toContain('departmentScope || (!usesTechnologyAreas && department !== "ALL" ? department : null)');
    expect(operations).toContain("findings.filter(finding => finding.department === findingDepartmentScope)");
    expect(operations).toContain("ticketsInOperationalQueue(scopedTickets");
    expect(operations).toContain("summarizeSantoDomingoFindings(scopedFindings");
  });

  it("mantiene tres tarjetas en Soporte y cuatro accesos en Archivos y comunicaciones",()=>{
    const supportHome=support.slice(support.indexOf('className="support-section-tabs support-primary-three"'),support.indexOf('className={`support-section-tabs',support.indexOf('className="support-section-tabs support-primary-three"')));
    expect(supportHome.match(/<strong>/g)).toHaveLength(3);
    expect(supportHome).toContain("<strong>Tickets</strong>");
    expect(supportHome).toContain("<strong>Tickets internos</strong>");
    expect(supportHome).toContain("<strong>Hallazgos automáticos</strong>");
    expect(files).toContain("<strong>Archivos</strong>");
    expect(files).toContain("<strong>Solicitudes a Administrativos</strong>");
    expect(files).toContain("<strong>Solicitudes a Almacén</strong>");
    expect(files).toContain("<strong>Solicitudes a Taller</strong>");
  });

  it("crea o actualiza a Adonis como técnico de Servicios Generales",()=>{
    expect(database).toContain("adonis.batista@grupotejeda.local");
    expect(database).toContain("N'Adonis Batista','GENERAL_SERVICES'");
    expect(database).toContain('"canAssignTickets":false');
    expect(database).toContain('"canResolveTickets":true');
  });
});
