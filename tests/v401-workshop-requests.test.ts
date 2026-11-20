import {describe,expect,it} from "vitest";
import {readFileSync} from "node:fs";

const composer=readFileSync("src/components/vite/WarehouseRequestComposer.tsx","utf8");
const workspace=readFileSync("src/components/vite/AdministrativeWorkspace.tsx","utf8");
const workshopRequests=readFileSync("src/components/vite/WorkshopRequests.tsx","utf8");
const workshopWorkflow=readFileSync("api/WorkshopWorkflow.cs","utf8");
const maintenance=readFileSync("api/DatabaseSqlServerScoped.cs","utf8");
const program=readFileSync("api/Program.cs","utf8");

describe("V401 · selector de artículos y solicitudes a Taller",()=>{
  it("separa el buscador del artículo elegido y muestra opciones solo por nombre",()=>{
    expect(composer).toContain("Artículo seleccionado<select required value={line.productId}");
    expect(composer).toContain('setLine(line.id,{productQuery:event.target.value})');
    expect(composer).toContain("<option key={item.productId} value={item.productId}>{item.name}</option>");
    expect(composer).not.toContain("{item.available} disponibles");
  });

  it("incluye solicitudes a Taller junto con los demás accesos",()=>{
    expect(workspace).toContain("<strong>Solicitudes a Administrativos</strong>");
    expect(workspace).toContain("<strong>Solicitudes a Almacén</strong>");
    expect(workspace).toContain("<strong>Solicitudes a Taller</strong>");
    expect(workspace).toContain("<WorkshopRequests session={session} department={departmentCode}/>");
    expect(workshopRequests).toContain('movementType:"TRANSFER_TO_WORKSHOP"');
    expect(workshopRequests).toContain('operationalArea:"WORKSHOP"');
  });

  it("permite Taller para los tres departamentos y envía reparados a Almacén",()=>{
    expect(program).toContain('"TRANSFER_TO_WORKSHOP" or "REPAIR"');
    expect(maintenance).not.toContain("Recursos Humanos no forma parte de Taller");
    expect(maintenance).not.toContain("recibe únicamente inversores");
    expect(workshopWorkflow).toContain("OR @team='WORKSHOP'");
    expect(workshopWorkflow).toContain('"DELIVER"=>"ENTRY"');
    expect(workshopWorkflow).toContain("CASE WHEN @action='DELIVER' THEN 'WAREHOUSE'");
    expect(workshopWorkflow).toContain("Equipo reparado enviado de Taller a Almacén");
  });
});
