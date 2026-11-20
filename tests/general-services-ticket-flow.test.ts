import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const read=(path:string)=>readFileSync(resolve(process.cwd(),path),"utf8");
const program=read("api/Program.cs");
const database=read("api/DatabaseSqlServerScoped.cs");
const center=read("src/components/vite/TicketCenter.tsx");
const app=read("src/App.tsx");

describe("creación y resolución en Servicios Generales",()=>{
  it("solo soporte puede crear; soporte y técnicos pueden resolver",()=>{
    expect(program).toContain('if(account.Role=="GeneralServices"){values["canCreateTickets"]=values["canAssignTickets"]&&account.SupportTeam!="WORKSHOP";values["canViewSupportDashboard"]=true;}');
    expect(program).toContain('if(role=="GeneralServices"){values["canCreateTickets"]=values["canAssignTickets"]&&supportTeam!="WORKSHOP";values["canViewSupportDashboard"]=true;}');
    expect(program).toContain('role=="GeneralServices"&&supportTeam=="WORKSHOP"');
    expect(program).toContain('HasPermission(context.User,"canResolveTickets")');
    expect(program).toContain('RequireAuthorization("TicketCreate")');
  });
  it("mantiene Taller y el panel limpio de Servicios Generales",()=>{
    expect(app).toContain('session.role === "GeneralServices" && isWorkshopOperator');
    expect(center).toMatch(/isWorkshopOperator\s*&&\s*!!maintenanceEntry/);
    expect(center).not.toContain('className="general-services-create-ticket"');
    expect(center).toContain('<strong>Tickets internos</strong>');
  });
  it("restaura el reporte de incidencia para administradores en la agencia seleccionada",()=>{
    expect(app).toContain('(session.role === "GroupAdministrator" || session.role === "Administrator")');
    expect(app).toContain("Reportar incidencia en mi agencia");
    expect(app).toContain('reportAgency={{ ...agency, grupo: group }}');
  });
  it("impide creación al técnico y conserva la resolución asignada con GPS",()=>{
    expect(database).toContain('if(role=="GeneralServices"&&(!canAssignTickets||supportTeam=="WORKSHOP"))return null;');
    expect(database).not.toContain('role=="GeneralServices"&&!canAssignTickets?userId');
    expect(database).toContain('assignment.is_current=1');
    expect(database).toContain('(role is "GeneralServices" or "HumanResources")&&!canAssignTickets');
    expect(center).not.toContain('isSelfAssigningGeneralServices');
    expect(center).toContain('requiresTechnicianGpsEvidence(ticket)');
  });
});
