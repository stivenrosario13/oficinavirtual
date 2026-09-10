import {renderToStaticMarkup} from "react-dom/server";
import {describe,it,expect} from "vitest";
import MaintenanceOverview from "../src/components/vite/MaintenanceOverview";
import type {MaintenanceDocumentMovement} from "../src/components/vite/maintenanceDocumentPdf";
const base={id:"one",serialNumber:"S1",operationalArea:"WAREHOUSE",department:"TECHNOLOGY",movementType:"TRANSFER_TO_WORKSHOP",equipmentType:"Impresora",createdAt:"2026-09-04T12:00:00Z",documentNumber:"DOC-1"} as MaintenanceDocumentMovement;
const props={loading:false,error:"",onRefresh:()=>{},onDepartment:()=>{},onPanel:()=>{},onDocument:()=>{},pendingOrders:0};
describe("portada de Almacén y Taller",()=>{
 it("muestra ambos departamentos operativos con sus accesos propios",()=>{
   const warehouse=renderToStaticMarkup(<MaintenanceOverview {...props} area="WAREHOUSE" movements={[]}/>);
   const workshop=renderToStaticMarkup(<MaintenanceOverview {...props} area="WORKSHOP" movements={[]}/>);
 expect(warehouse).toContain("Comunicaciones de departamentos");expect(workshop).not.toContain("Comunicaciones de departamentos");
   for(const html of [warehouse,workshop]){expect(html).toContain("Abrir Tecnología");expect(html).toContain("Abrir Servicios Generales");expect(html).toContain("Mis plantillas");}
   expect(warehouse).not.toContain("Abrir Recursos Humanos");expect(workshop).not.toContain("Abrir Recursos Humanos");expect(workshop).toContain("Exclusivo para inversores");
 });
 it("incluye transferencias en Taller y conserva solo el último estado de cada serial",()=>{
   const html=renderToStaticMarkup(<MaintenanceOverview {...props} area="WORKSHOP" movements={[base,{...base,id:"two",operationalArea:"WORKSHOP",movementType:"REPAIR",createdAt:"2026-09-04T15:00:00Z"}]}/>);
   expect(html).toContain("Impresora");expect(html).toMatch(/Por intervenir[\s\S]*?<strong>0<\/strong>/);expect(html).toMatch(/Equipos identificados[\s\S]*?<strong>1<\/strong>/);
 });
 it("distingue un fallo de carga de un inventario vacío",()=>{
   const html=renderToStaticMarkup(<MaintenanceOverview {...props} area="WAREHOUSE" movements={[]} error="Servicio no disponible"/>);
   expect(html).toContain('role="alert"');expect(html).toContain("Reintentar");expect(html).toContain("Información pendiente");expect(html).not.toContain("0 movimientos registrados");
 });
});
