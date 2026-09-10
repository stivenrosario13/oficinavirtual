import {renderToStaticMarkup} from "react-dom/server";
import {describe,it,expect} from "vitest";
import {WorkshopTimeline} from "../src/components/vite/WorkshopBoard";
import {workshopCode,workshopTrackingUrl,type WorkshopCase} from "../src/components/vite/workshopWorkflow";
const item:WorkshopCase={id:"source",documentNumber:"ALM-1",qrToken:"REAL-MNT|ALM-1",equipmentType:"Impresora",serialNumber:"ORIGINAL",quantity:1,department:"TECHNOLOGY",senderName:"Registro",returnName:"Remitente original",sentAt:"2026-09-05T12:00:00Z",status:"SENT",failureCause:"No imprime",canOperate:false,events:[]};
describe("seguimiento de Taller",()=>{
 it("recupera el QR original desde el enlace del teléfono",()=>{
   expect(workshopCode(workshopTrackingUrl(item.qrToken,"https://sistema.example"))).toBe(item.qrToken);
   expect(workshopCode("  ALM-1  ")).toBe("ALM-1");
   expect(workshopCode("REAL'MNTÇALM'20260903'195201'970AB")).toBe("REAL-MNT|ALM-20260903-195201-970AB");
   expect(workshopCode("httpsÑ--realagencias.runasp.net-_equipment¡REAL'MNT%7CALM'20260903'144834'B2C92")).toBe("REAL-MNT|ALM-20260903-144834-B2C92");
  });
 it("no anticipa una reparación antes de elegir la intervención",()=>{
   const html=renderToStaticMarkup(<WorkshopTimeline item={item}/>);
   expect(html).toContain("Reparación / reemplazo");
   expect(html.match(/class="complete"/g)).toHaveLength(1);
 });
 it("conserva remitente, serial original, sustituto y etapas terminadas",()=>{
   const events=[{action:"RECEIVE",status:"RECEIVED"},{action:"REPLACE",status:"REPLACEMENT"},{action:"WORK",status:"REPLACEMENT"},{action:"DELIVER",status:"DELIVERED"}].map((e,i)=>({...e,id:String(i),status:e.status as WorkshopCase["status"],actorName:"Técnico",actorLogin:"tecnico",occurredAt:"2026-09-05T13:00:00Z",note:e.action==="WORK"?"Prueba satisfactoria":undefined}));
   const html=renderToStaticMarkup(<WorkshopTimeline item={{...item,status:"DELIVERED",replacementSerial:"NUEVO",events}}/>);
   expect(html.match(/class="complete"/g)).toHaveLength(4);
   for(const value of ["ORIGINAL","NUEVO","Entregado a Remitente original","Prueba satisfactoria","tecnico"])expect(html).toContain(value);
   expect(html).not.toContain("Reparación / reemplazo");
 });
});
