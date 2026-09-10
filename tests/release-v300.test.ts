import {readFileSync} from "node:fs";
import {resolve} from "node:path";
import {describe,expect,it} from "vitest";

const read=(path:string)=>readFileSync(resolve(process.cwd(),path),"utf8");

describe("release V300 operational contracts",()=>{
  it("forces chat and notifications on for Technology technicians",()=>{
    const program=read("api/Program.cs");
    expect(program).toContain('account.Role=="Technology"&&account.SupportTeam=="TECHNICIANS"');
    expect(program).toContain('values["canUseSupportChat"]=true');
    expect(read("src/components/vite/AdminPanel.tsx")).toContain('team==="TECHNICIANS")Object.assign(permissions');
  });

  it("publishes status percentages for every Technology area",()=>{
    const operations=read("src/components/vite/OperationsCenter.tsx");
    for(const area of ["CALL_CENTER","TECHNICAL_FAILURE","TECHNICIANS"])expect(operations).toContain(area);
    for(const status of ["pendingPercent","inProgressPercent","completedPercent"])expect(operations).toContain(status);
  });

  it("keeps workshops independent and excludes HR",()=>{
    const database=read("api/DatabaseSqlServerScoped.cs");
    expect(database).toContain('entersWorkshop&&department=="HUMAN_RESOURCES"');
    expect(database).toContain('El Taller de Servicios Generales recibe únicamente inversores');
    const workflow=read("api/WorkshopWorkflow.cs");
    expect(workflow).toContain("m.department<>'HUMAN_RESOURCES'");
    expect(workflow).toContain("LIKE '%INVERSOR%'");
  });

  it("sends and archives each recipient receipt",()=>{
    const workflow=read("api/WorkshopWorkflow.cs");
    expect(workflow).toContain("NotifyMaintenanceReceiptCreated");
    expect(workflow).toContain('maintenanceMovementId=movementId.ToString()');
    expect(workflow).toContain("support_messages");
  });

  it("provides automatic stale-module recovery and mobile hardening",()=>{
    expect(read("src/components/vite/AppErrorBoundary.tsx")).toContain("repairApplication");
    const css=read("src/release-v300-responsive.css");
    expect(css).toContain("@media(max-width:700px)");
    expect(css).toContain("queue-area-metrics");
  });
});
