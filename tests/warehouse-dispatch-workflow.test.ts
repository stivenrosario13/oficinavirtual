import {readFileSync} from "node:fs";
import {describe,expect,it} from "vitest";

describe("flujo interno Despachar de Almacén",()=>{
  const endpoints=readFileSync("api/WarehouseFulfillmentEndpoints.cs","utf8");
  const database=readFileSync("api/DatabaseWarehouseFulfillment.cs","utf8");
  const store=readFileSync("api/InstitutionalDocumentStore.cs","utf8");
  const requests=readFileSync("src/components/vite/WarehousePdfRequests.tsx","utf8");
  const detail=readFileSync("src/components/vite/InstitutionalDocumentDetail.tsx","utf8");
  const allocation=readFileSync("src/components/vite/WarehouseAllocationForm.tsx","utf8");
  const compact=(value:string)=>value.replace(/\s+/g,"");

  it("permite a cualquier usuario de Almacén preparar y reserva la autorización para el permiso especial",()=>{
    expect(endpoints).toContain('/requests/{id:long}/prepare');
    expect(endpoints).toContain('db.WarehousePrepare');
    expect(endpoints).toContain('/requests/{id:long}/approve');
    expect(endpoints).toContain('Permission(context,"canAuthorizeWarehouseRequests")');
    expect(allocation).toContain('/prepare');
    expect(detail).toContain('warehouseAuthorizeAccess&&effectiveRecipient');
  });

  it("autoriza antes de asignar y después envía los equipos a Despachar",()=>{
    const prepare=database.slice(database.indexOf("WarehousePrepare"),database.indexOf("WarehouseApprovePrepared"));
    const approveStart=database.indexOf("WarehouseApprovePrepared");
    const approve=database.slice(approveStart,database.indexOf("public async Task<(string? Error",approveStart));
    expect(prepare).toContain('document.Status!="approved"');
    expect(prepare).toContain('new("dispatch"');
    expect(prepare).toContain('document.WarehouseAssigned=');
    expect(approve).toContain('document.Status="approved"');
    expect(approve).not.toContain('Primero asigna los equipos y seriales');
  });

  it("muestra Despachar solo como estado de solicitud y oculta la preparación al solicitante",()=>{
    expect(requests).not.toContain('className="warehouse-dispatch-board"');
    expect(compact(requests)).toContain('[["all","Todas"],["pending","Pendientesderecibir"],["authorization","Pendientesdeautorizar"],["approved","Autorizadas"],["dispatch","Despachar"],["withdrawn","Retiradas"],["rejected","Rechazadas"],]');
    expect(compact(requests)).toContain('useState(warehouse?"all":"pending")');
    expect(requests).toContain('["withdraw", "Retirar"]');
    expect(requests).not.toContain('import WarehouseLiveQueue');
    expect(store).toContain('!warehouseActor&&Status!="approved"');
    expect(store).toContain('WarehouseAssigned=[]');
    expect(store).toContain('WarehousePreparedBy=null');
  });
});
