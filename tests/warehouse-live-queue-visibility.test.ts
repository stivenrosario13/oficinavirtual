import {readFileSync} from "node:fs";
import {describe,expect,it} from "vitest";

describe("visibilidad de la cola de Almacén",()=>{
  const overview=readFileSync("src/components/vite/MaintenanceOverview.tsx","utf8");
  const app=readFileSync("src/App.tsx","utf8");

  it("deja el inicio de Almacén enfocado en los departamentos",()=>{
    expect(overview).not.toContain('warehouse&&<WarehouseLiveQueue');
    expect(overview).not.toContain('useWarehouseQueue(warehouse)');
    expect(overview).toContain('Selecciona el departamento');
  });

  it("mueve el control diario a una página independiente",()=>{
    expect(app).toContain('const WarehouseLiveQueuePage=lazy');
    expect(app).toContain('<WarehouseLiveQueuePage session={session}/>');
  });

  it("muestra el control diario de Almacén en el sidebar",()=>{
    expect(app).toContain('navigateTo("warehouseQueue")');
    expect(app).toContain("navigateTo('warehouseQueue')");
    expect(app).toContain('Control diario de Almacén');
    expect(app).toContain('title="Almacén central"');
  });
});
