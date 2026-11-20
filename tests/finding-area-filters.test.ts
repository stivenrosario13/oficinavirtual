import {describe,expect,it} from "vitest";
import {readFileSync} from "node:fs";
import {findingAgencyKey,matchesFindingArea,regionForFinding,unassignedFindingRegion} from "../src/components/vite/findingAreaFilters";

const regions=new Map([
  [findingAgencyKey("601001","Grupo Norte"),"Norte"],
  [findingAgencyKey("601002","Grupo Norte"),"Cibao"],
  [findingAgencyKey("701001","Grupo Sur"),"Sur"],
]);
const north={codigo:"601001",grupo:"Grupo Norte"};
const cibao={codigo:"601002",grupo:"Grupo Norte"};
const south={codigo:"701001",grupo:"Grupo Sur"};

describe("filtros de hallazgos por región y grupo",()=>{
  it("incluye la región real en el directorio de agencias usado por hallazgos",()=>{
    const api=readFileSync("api/DatabaseSqlServerScoped.cs","utf8");
    const center=readFileSync("src/components/vite/TicketCenter.tsx","utf8");
    expect(api).toContain("COALESCE(p.latitude,a.expected_latitude),COALESCE(p.longitude,a.expected_longitude),a.region");
    expect(center).toContain("agency.region?.trim()");
    expect(center).toContain('title="regiones"');
    expect(center).toContain('title="grupos"');
  });
  it("identifica la región exacta de la agencia y conserva los casos sin región",()=>{
    expect(regionForFinding(north,regions)).toBe("Norte");
    expect(regionForFinding({codigo:"999999",grupo:"Grupo Norte"},regions)).toBe(unassignedFindingRegion);
  });
  it("combina listas de regiones y grupos sin limitar la selección a una opción",()=>{
    expect(matchesFindingArea(north,regions,[],[])).toBe(true);
    expect(matchesFindingArea(north,regions,["Norte","Sur"],["Grupo Norte"])).toBe(true);
    expect(matchesFindingArea(cibao,regions,["Norte","Sur"],["Grupo Norte"])).toBe(false);
    expect(matchesFindingArea(south,regions,["Norte","Sur"],["Grupo Norte"])).toBe(false);
    expect(matchesFindingArea(south,regions,["Norte","Sur"],["Grupo Norte","Grupo Sur"])).toBe(true);
  });
});
