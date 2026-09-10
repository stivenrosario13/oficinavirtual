import {renderToStaticMarkup} from "react-dom/server";
import {describe,it,expect} from "vitest";
import TicketCenter from "../src/components/vite/TicketCenter";
import type {PortalSession} from "../src/lib/session";

const session={id:"test",displayName:"Administrador",role:"Administrator",permissions:{},supportDepartment:null,supportTeam:null} as PortalSession;
describe("entrada directa a Taller",()=>{
  it("muestra al administrador los dos talleres independientes sin Recursos Humanos",()=>{
    const html=renderToStaticMarkup(<TicketCenter session={session} maintenanceEntry="WORKSHOP" onSessionExpired={()=>{}}/>);
    expect(html).toContain("TALLERES INDEPENDIENTES · CONTROL POR DEPARTAMENTO");
    for(const label of ["Tecnología","Servicios Generales"])expect(html).toContain(label);
    expect(html).not.toContain("Abrir Recursos Humanos");expect(html).toContain("Exclusivo para inversores");
  });
  it("limita al operador de Taller a su propio departamento",()=>{
    const account={...session,role:"Technology",supportDepartment:"TECHNOLOGY",supportTeam:"WORKSHOP"} as PortalSession;
    const html=renderToStaticMarkup(<TicketCenter session={account} maintenanceEntry="WORKSHOP" onSessionExpired={()=>{}}/>);
    expect(html).toContain("Abrir Tecnología");expect(html).not.toContain("Abrir Servicios Generales");expect(html).not.toContain("Abrir Recursos Humanos");
    expect(html).toContain("Plantillas y archivos institucionales");
  });
});
