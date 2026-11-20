import {readFileSync} from "node:fs";
import {describe,expect,it} from "vitest";

describe("flujo de equipos dañados entre Almacén y Taller",()=>{
 const center=readFileSync("src/components/vite/TicketCenter.tsx","utf8");
 const tracking=readFileSync("src/components/vite/WorkshopBoard.tsx","utf8");
 const composer=readFileSync("src/components/vite/WarehouseRequestComposer.tsx","utf8");
 const compact=(value:string)=>value.replace(/\s+/g,"");
 const compactCenter=compact(center),compactTracking=compact(tracking),compactComposer=compact(composer);

 it("mantiene el equipo dañado en inventario hasta guardar su transferencia",()=>{
  expect(center).toContain("Inventario en reparación / reemplazo");
  expect(compactCenter).toContain('movement.movementType==="DAMAGED_RETURN"');
  expect(center).toContain("Enviar a reparar");
  expect(center).toContain("Cambio de componente");
  expect(center).toContain("Recibir y preparar envío a Taller");
  expect(center).not.toContain("Equipos dañados recibidos");
 });

 it("restaura el tablero operativo anterior de Taller",()=>{
  expect(center).not.toContain("<WorkshopBoard");
  expect(compactCenter).toContain('["WAREHOUSE","WORKSHOP"].includes(maintenanceArea)');
  expect(compactCenter).toContain('maintenanceArea==="WORKSHOP"?[["ENTRY","RecibirenTaller"]');
 });

 it("no solicita responsable asignado en el formulario de envío a Taller",()=>{
  expect(compactCenter).toContain('maintenanceDraft.movementType!=="TRANSFER_TO_WORKSHOP"&&(<label>');
  expect(compactCenter).toContain('technicianUserId:"",technicianName:""');
 });

  it("permite buscar agencias y muestra todo el catálogo con su disponibilidad",()=>{
  expect(composer).toContain("Nombre, terminal, código o grupo");
  expect(composer).toContain("visibleAgencies");
  expect(compactComposer).toContain('value={item.productId}');
  expect(composer).not.toContain('item.available');
  expect(composer).toContain('Catálogo oficial completo, incluso artículos sin existencia.');
 });

 it("abre la cámara trasera con alta resolución y consulta el QR detectado",()=>{
  expect(compactTracking).toContain('facingMode:{ideal:"environment"}');
  expect(compactTracking).toContain('width:{ideal:3840},height:{ideal:2160}');
  expect(compactTracking).toContain('newDetector({formats:["qr_code"]})');
  expect(tracking).toContain("Escanear con cámara");
 });

 it("recupera envíos confirmados desde movimientos cuando el listado de Taller está vacío",()=>{
  expect(compactTracking).toContain('maintenanceApi<unknown>("/api/maintenance/movements",)');
  expect(compactTracking).toContain('item?.movementType==="TRANSFER_TO_WORKSHOP"');
  expect(compactTracking).toContain('status:"SENT"');
 });
});
