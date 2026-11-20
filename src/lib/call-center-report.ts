import {deviceDate} from './display';
export type AgentReportCase={id:string;ticketNumber:number;codigo:string;terminal:string;grupo:string;assignedDepartment:string;assignedTeam?:string|null;status:string;priority:string;assignedTechnicianName?:string|null;resolvedByName?:string|null;createdAt:string;updatedAt:string;closedAt?:string|null;resolutionMinutes?:number|null;evidence?:unknown[]|null;category:string;resolution?:string|null};
export const reportDateKey=(value:string|Date)=>new Intl.DateTimeFormat('en-CA',{timeZone:'America/Santo_Domingo',year:'numeric',month:'2-digit',day:'2-digit'}).format(deviceDate(value));
const validReportDate=(value:string)=>{if(!/^\d{4}-\d{2}-\d{2}$/.test(value))return false;const day=new Date(value+'T12:00:00Z');return Number.isFinite(day.getTime())&&day.toISOString().slice(0,10)===value;};
export function agentReportRange(fromOrEnd:string,toOrDays:string|number){
 if(typeof toOrDays==='number'){
  if(!validReportDate(fromOrEnd)||![1,7,15,30].includes(toOrDays))throw new Error('Selecciona una fecha y un periodo válido.');
  const day=new Date(fromOrEnd+'T12:00:00Z');day.setUTCDate(day.getUTCDate()-toOrDays+1);
  return {from:day.toISOString().slice(0,10),to:fromOrEnd};
 }
 if(!validReportDate(fromOrEnd)||!validReportDate(toOrDays))throw new Error('Selecciona fechas válidas en Desde y Hasta.');
 if(fromOrEnd>toOrDays)throw new Error('La fecha Desde no puede ser posterior a la fecha Hasta.');
 return {from:fromOrEnd,to:toOrDays};
}
export const agentReportDayCount=(from:string,to:string)=>Math.round((Date.parse(to+'T12:00:00Z')-Date.parse(from+'T12:00:00Z'))/86400000)+1;
const resolved=(row:AgentReportCase)=>['RESOLVED','CLOSED'].includes(row.status);
export const agentReportOwner=(row:AgentReportCase)=>row.resolvedByName||row.assignedTechnicianName||'Sin asignar';
export function buildAgentReport(tickets:AgentReportCase[],from:string,toOrDays:string|number){
 const range=agentReportRange(from,toOrDays);
 const rows=[...new Map(tickets.filter(row=>row.assignedDepartment==='TECHNOLOGY'&&row.assignedTeam==='CALL_CENTER').map(row=>[row.id,row])).values()].filter(row=>{
  const value=resolved(row)||row.status==='CANCELLED'?row.closedAt||row.updatedAt:row.createdAt;
  if(!Number.isFinite(deviceDate(value).getTime()))return false;
  const date=reportDateKey(value);return date>=range.from&&date<=range.to;
 }).sort((a,b)=>agentReportOwner(a).localeCompare(agentReportOwner(b),'es')||a.status.localeCompare(b.status)||a.ticketNumber-b.ticketNumber);
 const agents=new Map<string,{name:string;total:number;resolved:number;inProgress:number;cancelled:number;evidence:number}>();
 for(const row of rows){const name=agentReportOwner(row),agent=agents.get(name)||{name,total:0,resolved:0,inProgress:0,cancelled:0,evidence:0};agent.total++;if(resolved(row))agent.resolved++;else if(row.status==='CANCELLED')agent.cancelled++;else agent.inProgress++;agent.evidence+=row.evidence?.length||0;agents.set(name,agent);}
 return {...range,rows,agents:[...agents.values()],total:rows.length,resolved:rows.filter(resolved).length,inProgress:rows.filter(row=>!resolved(row)&&row.status!=='CANCELLED').length,cancelled:rows.filter(row=>row.status==='CANCELLED').length};
}
export const agentDetailHeaders=['Ticket','Agencia','Grupo','Departamento','Estado','Prioridad','Responsable','Creado','Actualizado','Duración (min)','Evidencias','Categoría','Solución'];
export function agentDetailCells(row:AgentReportCase,categoryLabel:(key:string)=>string){
 const states:Record<string,string>={OPEN:'Abierto',PENDING:'Pendiente',IN_PROGRESS:'En proceso',RESOLVED:'Resuelto',CLOSED:'Cerrado',CANCELLED:'Anulado'};
 const priorities:Record<string,string>={CRITICAL:'Crítica',HIGH:'Alta',MEDIUM:'Media',LOW:'Baja'};
 const date=(value:string)=>Number.isFinite(deviceDate(value).getTime())?deviceDate(value).toLocaleString('es-DO',{timeZone:'America/Santo_Domingo'}):'';
 const minutes=resolved(row)?Math.max(0,row.resolutionMinutes??Math.round((deviceDate(row.closedAt||row.updatedAt).getTime()-deviceDate(row.createdAt).getTime())/60000)):0;
 return [row.ticketNumber,`${row.codigo} · ${row.terminal}`,row.grupo,'Mesa de Servicios',states[row.status]||row.status,priorities[row.priority]||row.priority,agentReportOwner(row),date(row.createdAt),date(row.updatedAt),Number.isFinite(minutes)?minutes:0,row.evidence?.length||0,categoryLabel(row.category),row.resolution||''];
}
