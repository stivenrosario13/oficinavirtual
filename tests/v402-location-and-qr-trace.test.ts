import {describe,expect,it} from "vitest";
import {readFileSync} from "node:fs";
import {workshopCode} from "../src/components/vite/workshopWorkflow";

const tickets=readFileSync("src/components/vite/TicketCenter.tsx","utf8");
const warehouse=readFileSync("src/components/vite/WarehousePdfRequests.tsx","utf8");
const workshop=readFileSync("src/components/vite/WorkshopRequests.tsx","utf8");
const tracking=readFileSync("src/components/vite/WorkshopBoard.tsx","utf8");
const backend=readFileSync("api/DatabaseMaintenanceDocuments.cs","utf8");

describe("V402 · Google Maps y trazabilidad QR",()=>{
  it("abre y comparte la dirección desde la tarjeta usando Google Maps",()=>{
    expect(tickets).toContain("https://www.google.com/maps/search/?api=1&query=");
    expect(tickets).toContain("Compartir dirección");
    expect(tickets).toContain("navigator.share");
    expect(tickets).not.toContain("https://www.openstreetmap.org/?mlat=");
  });

  it("normaliza el enlace completo contenido en el QR grande",()=>{
    expect(workshopCode("https://realagencias.runasp.net/?equipment=REAL-MNT%7CSOL-ALM-20260923-091134-48E0")).toBe("REAL-MNT|SOL-ALM-20260923-091134-48E0");
    expect(workshopCode("REAL-MNTÇSOL-ALM-20260923-091134-48E0")).toBe("REAL-MNT|SOL-ALM-20260923-091134-48E0");
    expect(backend).toContain('new[]{"equipment","code","qr","token"}');
  });

  it("ofrece consulta por QR en departamentos, Almacén y Taller",()=>{
    expect(warehouse).toContain("Escanear QR y consultar etapa");
    expect(warehouse.replace(/\s+/g,"")).toContain("<QrCode/>Consultaretapa");
    expect(workshop).toContain("Escanear QR y consultar etapa");
    expect(tickets).toContain("Escanear QR grande y consultar etapa");
    expect(tracking).toContain("(await import('jsqr')).default");
    expect(tracking).toContain('else if(context&&jsQR)');
    expect(tracking).toContain('raw=jsQR(context.getImageData');
  });

  it("muestra las etapas de despacho y autorización y corrige los rechazados",()=>{
    expect(backend).toContain("'AUTHORIZATION'");
    expect(backend).toContain("'DISPATCH'");
    expect(backend).toContain("'REJECTED'");
    expect(backend).toContain("Pendiente de autorización final");
    expect(tracking).toContain('"DISPATCHED", "Equipo y serial asignados"');
  });
});
