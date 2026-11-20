export const unassignedFindingRegion="Sin región registrada";

const cleanFindingValue=(value:unknown)=>String(value??"").trim();

export const findingAgencyKey=(code:unknown,group:unknown)=>`${cleanFindingValue(code)}\u0000${cleanFindingValue(group)}`;

export function regionForFinding(finding:{codigo?:unknown;grupo?:unknown},agencyRegions:ReadonlyMap<string,string>){
  return agencyRegions.get(findingAgencyKey(finding.codigo,finding.grupo))||unassignedFindingRegion;
}

export function matchesFindingArea(finding:{codigo?:unknown;grupo?:unknown},agencyRegions:ReadonlyMap<string,string>,regions:readonly string[],groups:readonly string[]){
  const group=cleanFindingValue(finding.grupo);
  return (!groups.length||groups.includes(group))&&(!regions.length||regions.includes(regionForFinding(finding,agencyRegions)));
}
