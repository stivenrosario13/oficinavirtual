import {describe,expect,it} from "vitest";
import {readFileSync} from "node:fs";
import {automaticFindingCategory,summarizeSantoDomingoFindings} from "../src/lib/automatic-findings";
import {findingAgencyKey,matchesFindingArea,regionForFinding,unassignedFindingRegion} from "../src/components/vite/findingAreaFilters";

describe("estabilidad para usuarios técnicos y supervisores",()=>{
  it("tolera agencias y hallazgos heredados con campos vacíos",()=>{
    const regions=new Map([[findingAgencyKey(null," Grupo A "),"Santo Domingo"]]);
    expect(findingAgencyKey(null," Grupo A ")).toBe("\u0000Grupo A");
    expect(regionForFinding({codigo:null,grupo:" Grupo A "},regions)).toBe("Santo Domingo");
    expect(regionForFinding({codigo:undefined,grupo:undefined},new Map())).toBe(unassignedFindingRegion);
    expect(matchesFindingArea({codigo:null,grupo:null},new Map(),[],[])).toBe(true);
  });

  it("crea una categoría segura aunque el registro automático esté incompleto",()=>{
    expect(automaticFindingCategory({department:null,findingKey:null,title:null})).toEqual({key:"OTHER",title:"Otros hallazgos"});
    expect(summarizeSantoDomingoFindings([
      {codigo:null,grupo:null,department:null,findingKey:null,title:null,status:" open ",municipio:"Santo Domingo Este"},
    ],new Map())).toEqual([{key:"OTHER:OTHER",title:"Otros hallazgos",department:"OTHER",total:1,open:1,inProgress:0}]);
  });

  it("recupera el módulo usando la versión instalada y aísla las consultas opcionales",()=>{
    const boundary=readFileSync("src/components/vite/AppErrorBoundary.tsx","utf8");
    const queue=readFileSync("src/components/vite/OperationsCenter.tsx","utf8");
    expect(boundary).toContain('meta[name="app-release"]');
    expect(boundary).toContain('url.searchParams.set("_release", applicationRelease())');
    expect(boundary).not.toContain('"V372"');
    expect(queue).toContain('fetch("/api/tickets/findings", {cache: "no-store"})');
    expect(queue).toContain('fetch("/api/tickets/agencies", {cache: "no-store"})');
    expect(queue).toContain('Los tickets están disponibles.');
  });
});
