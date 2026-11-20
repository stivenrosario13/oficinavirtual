import type {DocumentDetail} from '../components/vite/DocumentSystemModule';
type FuelDocument=Pick<DocumentDetail,'createdUtc'|'administrativeRequest'> & {administrativeRegistrationOnly?:boolean};
export function isGasolineRecord(item:FuelDocument){return item.administrativeRegistrationOnly===true||item.administrativeRequest?.formType==='FUEL_LOG'}
export function administrativeDateMatches(item:FuelDocument,from:string,to:string){
  const data=item.administrativeRequest?.formData;
  const fuel=['FUEL_LOG','GAS_LOG'].includes(item.administrativeRequest?.formType||'');
  const dates:string[]=fuel&&Array.isArray(data?.rows)?data.rows.map(row=>typeof row?.date==='string'?row.date:'').filter(Boolean):[];
  if(fuel&&typeof data?.fuelDate==='string')dates.push(data.fuelDate);
  if(!dates.length){const date=new Date(item.createdUtc);if(!Number.isNaN(date.getTime()))dates.push(date.toLocaleDateString('sv-SE',{timeZone:'America/Santo_Domingo'}));}
  return dates.some(date=>(!from||date>=from)&&(!to||date<=to));
}
