import {readFileSync} from "node:fs";
import {describe,expect,it} from "vitest";

describe("interfaz profesional de solicitudes de Almacén",()=>{
  const communications=readFileSync("src/components/vite/maintenanceCommunications.css","utf8");
  const fulfillment=readFileSync("src/components/vite/warehouseFulfillment.css","utf8");

  it("mantiene el estado visible y desplaza únicamente las solicitudes",()=>{
    expect(communications).toContain(".warehouse-pdf-management .mc-catalog-layout>aside{position:sticky");
    expect(communications).toContain(".warehouse-pdf-management .warehouse-pdf-list{max-height:");
    expect(communications).toContain("overflow-y:auto");
    expect(communications).toContain("scrollbar-color:");
  });

  it("muestra el editor con fondo oscuro y letras blancas",()=>{
    expect(fulfillment).toContain(".id-decision.warehouse-request-editor{background:linear-gradient");
    expect(fulfillment).toContain("color:#eff9ff!important");
    expect(fulfillment).toContain(".warehouse-request-editor input,.warehouse-request-editor select,.warehouse-request-editor textarea{background:#071a2b!important");
    expect(fulfillment).toContain("color:#fff!important");
  });
});
