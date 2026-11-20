import {readFileSync} from "node:fs";
import {describe,expect,it} from "vitest";

describe("vínculo ticket, solicitud de Almacén y destino final",()=>{
  const composer=readFileSync("src/components/vite/WarehouseRequestComposer.tsx","utf8");
  const center=readFileSync("src/components/vite/TicketCenter.tsx","utf8");
  const documents=readFileSync("api/InstitutionalDocumentStore.cs","utf8");
  const fulfillment=readFileSync("api/DatabaseWarehouseFulfillment.cs","utf8");
  const tickets=readFileSync("api/DatabaseSqlServerScoped.cs","utf8");
  const detail=readFileSync("src/components/vite/InstitutionalDocumentDetail.tsx","utf8");

  it("crea la solicitud desde un ticket y conserva sus identificadores",()=>{
    expect(center).toContain("Solicitar equipo a Almacén");
    expect(center).toContain("setWarehouseRequestTicket(ticket)");
    expect(composer).toContain("ticketId:linkedTicket?.id||null");
    expect(composer).toContain("ticketNumber:linkedTicket?.ticketNumber||null");
    expect(composer).toContain("ticketId:linkedTicket?.id||null");
    expect(documents).toContain("Guid? TicketId=null,long? TicketNumber=null");
  });

  it("valida que ticket y solicitud correspondan a la misma agencia y departamento",()=>{
    expect(fulfillment).toContain("agency_id=@agency AND assigned_department=@department");
    expect(fulfillment).toContain("status NOT IN('RESOLVED','CLOSED','CANCELLED')");
  });

  it("arrastra el ticket hasta la entrega y bloquea la resolución antes del retiro",()=>{
    expect(fulfillment).toContain("VALUES(NEWID(),@agency,@ticket,@department");
    expect(fulfillment).toContain('movement.Parameters.AddWithValue("@ticket",Db(request.TicketId))');
    expect(tickets).toContain("Primero debe autorizarse, retirarse y entregarse el equipo");
  });

  it("agrega resolución y evidencia al historial del formulario",()=>{
    expect(tickets).toContain("LinkWarehouseTicketResolution");
    expect(documents).toContain('new("installed"');
    expect(documents).toContain("WarehouseInstallation=new");
    expect(detail).toContain("TRAZABILIDAD HASTA EL DESTINO FINAL");
    expect(detail).toContain("Evidencias vinculadas desde el ticket");
    expect(detail).toContain("warehouse-evidence");
  });
});
