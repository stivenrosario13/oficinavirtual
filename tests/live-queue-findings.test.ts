import {describe,expect,it} from "vitest";
import {readFileSync} from "node:fs";
import {findingAgencyKey} from "../src/components/vite/findingAreaFilters";
import {summarizeSantoDomingoFindings} from "../src/lib/automatic-findings";

describe("tarjeta de hallazgos automáticos de Santo Domingo",()=>{
  const regions=new Map([[findingAgencyKey("001","Grupo A"),"Santo Domingo"],[findingAgencyKey("002","Grupo B"),"Norte"]]);
  it("agrupa solamente hallazgos activos de Santo Domingo por categoría",()=>{
    const rows=summarizeSantoDomingoFindings([
      {codigo:"001",grupo:"Grupo A",department:"GENERAL_SERVICES",findingKey:"NOT_PAINTED",title:"Agencia no pintada",status:"OPEN"},
      {codigo:"001",grupo:"Grupo A",department:"GENERAL_SERVICES",findingKey:"DAMAGE_PAINTING",title:"Daño de pintura",status:"IN_PROGRESS"},
      {codigo:"002",grupo:"Grupo B",department:"GENERAL_SERVICES",findingKey:"NO_INVERTER",title:"Sin inversor",status:"OPEN"},
      {codigo:"001",grupo:"Grupo A",department:"TECHNOLOGY",findingKey:"PRINTER_NO_MAINTENANCE",title:"Impresora",status:"RESOLVED"},
    ],regions);
    expect(rows).toEqual([{key:"GENERAL_SERVICES:PAINTING",title:"Pintura",department:"GENERAL_SERVICES",total:2,open:1,inProgress:1}]);
  });
  it("acepta municipios de Santo Domingo cuando la región no está registrada",()=>{
    expect(summarizeSantoDomingoFindings([{codigo:"003",grupo:"Grupo C",department:"TECHNOLOGY",findingKey:"DAMAGE_CONNECTIVITY",title:"Sin internet",status:"OPEN",municipio:"Santo Domingo Este"}],regions)[0]?.title).toBe("Conectividad e internet");
  });
  it("se muestra dentro de la cola y comparte su actualización en vivo",()=>{
    const source=readFileSync("src/components/vite/OperationsCenter.tsx","utf8");
    expect(source).toContain("HALLAZGOS AUTOMÁTICOS · SANTO DOMINGO");
    expect(source).toContain("Hallazgos activos por categoría");
    expect(source).toContain('window.addEventListener("support-live-refresh", refresh)');
  });
});
