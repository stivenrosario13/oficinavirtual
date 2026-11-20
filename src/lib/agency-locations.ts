export type AgencyLocation = {id:string;codigo:string;terminal:string;grupo:string;direccion?:string|null;sector?:string|null;municipio?:string|null;provincia?:string|null;latitude:number|null;longitude:number|null};
export function locationUrl(agency:AgencyLocation):string|null {
  const {latitude:lat,longitude:lng}=agency;
  if(typeof lat!=='number'||typeof lng!=='number'||!Number.isFinite(lat)||!Number.isFinite(lng)||Math.abs(lat)>90||Math.abs(lng)>180||(lat===0&&lng===0))return null;
  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(`${lat},${lng}`)}`;
}
export function locationAddress(agency:AgencyLocation){return [agency.direccion,agency.sector,agency.municipio,agency.provincia].filter(Boolean).join(', ');}
export function locationText(agency:AgencyLocation){return [`${agency.codigo} · ${agency.terminal}`,`Grupo: ${agency.grupo||'Sin grupo'}`,locationAddress(agency),locationUrl(agency)||'Sin coordenadas registradas'].filter(Boolean).join('\n');}
export function searchLocation(agency:AgencyLocation,query:string){const normalize=(value:string)=>value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLocaleLowerCase('es');return normalize([agency.codigo,agency.terminal,agency.grupo,locationAddress(agency)].join(' ')).includes(normalize(query.trim()));}
