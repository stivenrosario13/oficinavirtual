import type {WorkshopCase,WorkshopEvent} from './workshopWorkflow';

export type WorkshopDailyMovement={
 id:string;occurredAt:string;time:string;action:string;status:string;documentNumber:string;
 equipment:string;serial:string;department:string;agency:string;actor:string;login:string;detail:string;
};
export type WorkshopReportKind='MOVEMENTS'|'ENTRIES_EXITS'|'UNREPAIRED';

const zone='America/Santo_Domingo';
const actionLabels:Record<string,string>={
 INTAKE:'Registro / envío a Taller',RECEIVE:'Recepción en Taller',REPAIR:'Reparación',REPLACE:'Reemplazo de equipo',
 PART:'Reemplazo de pieza',DISCHARGE:'Salida sin reparación',WORK:'Trabajo registrado',STOCK:'Ingreso a inventario',
 DELIVER:'Envío a Almacén',WAREHOUSE_RECEIVE:'Recepción en Almacén',
};
const departmentLabels:Record<string,string>={TECHNOLOGY:'Tecnología',GENERAL_SERVICES:'Servicios Generales',HUMAN_RESOURCES:'Recursos Humanos'};

function instant(value:string){return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value)?value:value+'Z')}
export function santoDomingoDateKey(value:Date|string){
 const parts=new Intl.DateTimeFormat('en-US',{timeZone:zone,year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(typeof value==='string'?instant(value):value);
 const part=(type:string)=>parts.find(item=>item.type===type)?.value||'';
 return `${part('year')}-${part('month')}-${part('day')}`;
}
export function todayInSantoDomingo(){return santoDomingoDateKey(new Date())}
export function workshopReportDateLabel(date:string){
 const [year,month,day]=date.split('-').map(Number);
 return new Intl.DateTimeFormat('es-DO',{dateStyle:'long',timeZone:zone}).format(new Date(Date.UTC(year,month-1,day,12)));
}
export function workshopReportRangeLabel(from:string,to:string){
 return from===to?workshopReportDateLabel(from):`${workshopReportDateLabel(from)} al ${workshopReportDateLabel(to)}`;
}
function movementTime(value:string){return new Intl.DateTimeFormat('es-DO',{timeZone:zone,hour:'2-digit',minute:'2-digit',hour12:true}).format(instant(value))}
export function santoDomingoTimeKey(value:Date|string){
 const parts=new Intl.DateTimeFormat('en-GB',{timeZone:zone,hour:'2-digit',minute:'2-digit',hourCycle:'h23'}).formatToParts(typeof value==='string'?instant(value):value);
 const part=(type:string)=>parts.find(item=>item.type===type)?.value||'00';
 return `${part('hour')}:${part('minute')}`;
}
function eventRow(item:WorkshopCase,event:WorkshopEvent):WorkshopDailyMovement{
 return {id:event.id,occurredAt:event.occurredAt,time:movementTime(event.occurredAt),action:actionLabels[event.action]||event.action.replaceAll('_',' '),status:event.status,
  documentNumber:item.documentNumber,equipment:item.equipmentType,serial:event.replacementSerial||item.replacementSerial||item.serialNumber||'Sin serial',department:departmentLabels[item.department]||item.department,
  agency:[item.agencyCode,item.agencyName].filter(Boolean).join(' · ')||'Uso interno',actor:event.actorName||'No identificado',login:event.actorLogin||'',detail:event.note||item.failureCause||'Sin detalle'};
}
export function buildWorkshopDailyMovements(items:WorkshopCase[],date:string){
 const rows:WorkshopDailyMovement[]=[];
 for(const item of items){
  if(santoDomingoDateKey(item.sentAt)===date)rows.push({id:`${item.id}-intake`,occurredAt:item.sentAt,time:movementTime(item.sentAt),action:actionLabels.INTAKE,status:'SENT',documentNumber:item.documentNumber,equipment:item.equipmentType,serial:item.serialNumber||'Sin serial',department:departmentLabels[item.department]||item.department,agency:[item.agencyCode,item.agencyName].filter(Boolean).join(' · ')||'Uso interno',actor:item.senderName||'No identificado',login:'',detail:item.failureCause||item.notes||'Sin detalle'});
  for(const event of item.events||[])if(santoDomingoDateKey(event.occurredAt)===date)rows.push(eventRow(item,event));
 }
 return rows.sort((a,b)=>a.occurredAt.localeCompare(b.occurredAt)||a.id.localeCompare(b.id));
}
export function buildWorkshopMovements(items:WorkshopCase[],from:string,to:string,fromTime='00:00',toTime='23:59'){
 const rows:WorkshopDailyMovement[]=[];
 const inside=(value:string)=>{const key=`${santoDomingoDateKey(value)}T${santoDomingoTimeKey(value)}`;return key>=`${from}T${fromTime}`&&key<=`${to}T${toTime}`;};
 for(const item of items){
  if(inside(item.sentAt))rows.push({id:`${item.id}-intake`,occurredAt:item.sentAt,time:movementTime(item.sentAt),action:actionLabels.INTAKE,status:'SENT',documentNumber:item.documentNumber,equipment:item.equipmentType,serial:item.serialNumber||'Sin serial',department:departmentLabels[item.department]||item.department,agency:[item.agencyCode,item.agencyName].filter(Boolean).join(' · ')||'Uso interno',actor:item.senderName||'No identificado',login:'',detail:item.failureCause||item.notes||'Sin detalle'});
  for(const event of item.events||[])if(inside(event.occurredAt))rows.push(eventRow(item,event));
 }
 return rows.sort((a,b)=>a.occurredAt.localeCompare(b.occurredAt)||a.id.localeCompare(b.id));
}
export function buildWorkshopReportRows(items:WorkshopCase[],from:string,to:string,kind:WorkshopReportKind,fromTime='00:00',toTime='23:59'){
 const rows=buildWorkshopMovements(items,from,to,fromTime,toTime);
 if(kind==='ENTRIES_EXITS')return rows.filter(row=>['Registro / envío a Taller','Recepción en Taller','Ingreso a inventario','Recepción en Almacén','Salida sin reparación','Envío a Almacén'].includes(row.action));
 if(kind==='UNREPAIRED')return rows.filter(row=>row.action==='Salida sin reparación');
 return rows;
}
