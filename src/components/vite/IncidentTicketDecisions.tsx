import {useState} from 'react';
import {CheckCircle2,XCircle} from 'lucide-react';
import {apiFetch} from '@/lib/api-fetch';
import type {DocumentDetail} from './DocumentSystemModule';

type IncidentRow={ticketDate?:string;ticketCode?:string;agentPhone?:string;decisionStatus?:'APPROVED'|'REJECTED';decisionNote?:string;decisionBy?:string;decisionLogin?:string;decisionUtc?:string;approvedAmount?:number|string};
type PendingDecision={index:number;status:'APPROVED'|'REJECTED';ticketCode:string};

const money=(value:number|string)=>new Intl.NumberFormat('es-DO',{style:'currency',currency:'DOP'}).format(Number(value)||0);
const statusLabel=(status?:string)=>status==='APPROVED'?'Aprobado':status==='REJECTED'?'Rechazado':'Pendiente';

export default function IncidentTicketDecisions({item,onChanged,operatorAccess=false}:{item:DocumentDetail;onChanged:(updated?:DocumentDetail)=>Promise<void>;operatorAccess?:boolean}){
 const data=item.administrativeRequest?.formData||{};
 const rows=Array.isArray(data.rows)?data.rows.filter((row):row is IncidentRow=>!!row&&typeof row==='object'):[];
 const[decision,setDecision]=useState<PendingDecision|null>(null),[amount,setAmount]=useState(''),[note,setNote]=useState(''),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const canDecide=!!item.canDecideIncidentTickets||operatorAccess;
 const canUseOptions=canDecide&&!!item.receivedUtc&&['received','approved','rejected'].includes(item.status);
 function open(index:number,status:'APPROVED'|'REJECTED',row:IncidentRow){setDecision({index,status,ticketCode:row.ticketCode||`Ticket ${index+1}`});setAmount(status==='APPROVED'&&row.approvedAmount!=null?String(row.approvedAmount):'');setNote(row.decisionNote||'');setError('')}
 async function save(){
  if(!decision||busy)return;
  const parsed=Number(amount.replace(/,/g,''));
  if(decision.status==='APPROVED'&&(!Number.isFinite(parsed)||parsed<=0)){setError('Indica el monto de compensación del ticket aprobado.');return}
  if(decision.status==='REJECTED'&&note.trim().length<5){setError('Indica el motivo del rechazo (mínimo 5 caracteres).');return}
  setBusy(true);setError('');
  try{
   const response=await apiFetch(`/api/institutional-documents/${item.id}/incident-tickets/decision`,{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify({ticketIndex:decision.index,status:decision.status,note:note.trim(),amount:decision.status==='APPROVED'?parsed:null})});
   const result=await response.json().catch(()=>({})) as {document?:DocumentDetail;message?:string;error?:string};
   if(!response.ok||!result.document)throw new Error(result.message||result.error||'No se confirmó la decisión del ticket.');
   await onChanged(result.document);setDecision(null);
  }catch(reason){setError((reason as Error).message)}finally{setBusy(false)}
 }
 if(!rows.length)return null;
 return <section className="incident-ticket-decisions" aria-label="Decisiones por ticket"><header><div><small>DECISIÓN INDIVIDUAL</small><h3>Tickets incluidos en el informe</h3></div><span>{rows.filter(row=>row.decisionStatus).length}/{rows.length} decididos</span></header>{!item.receivedUtc&&item.canDecideIncidentTickets&&<p className="incident-ticket-help">Primero usa “Recibir y sellar”. Luego aparecerán las opciones para aprobar o rechazar cada ticket.</p>}<div className="incident-ticket-list">{rows.map((row,index)=><article key={`${row.ticketCode||'ticket'}-${index}`} className={row.decisionStatus?.toLowerCase()||'pending'}><div><small>Ticket · {row.ticketDate?new Date(`${row.ticketDate}T12:00:00`).toLocaleDateString('es-DO'):'Fecha no registrada'}</small><strong>{row.ticketCode||`Sin número ${index+1}`}</strong><span>{row.agentPhone||'Sin contacto registrado'}</span></div><div className="incident-ticket-result"><b>{statusLabel(row.decisionStatus)}</b>{row.decisionStatus==='APPROVED'&&row.approvedAmount!=null&&<strong title="Monto de compensación">{money(row.approvedAmount)}</strong>}{row.decisionNote&&<p>{row.decisionNote}</p>}{row.decisionBy&&<small>{row.decisionBy}{row.decisionUtc?` · ${new Date(row.decisionUtc).toLocaleString('es-DO',{timeZone:'America/Santo_Domingo'})}`:''}</small>}</div>{canUseOptions&&<div className="incident-ticket-options">{!row.decisionStatus&&<><button className="primary" onClick={()=>open(index,'APPROVED',row)}><CheckCircle2/> Aprobar</button><button className="danger" onClick={()=>open(index,'REJECTED',row)}><XCircle/> Rechazar</button></>}{row.decisionStatus==='APPROVED'&&<button onClick={()=>open(index,'APPROVED',row)}>Editar compensación</button>}</div>}</article>)}</div>{decision&&<div className="incident-decision-editor"><h4>{decision.status==='APPROVED'?'Aprobar':'Rechazar'} ticket {decision.ticketCode}</h4>{decision.status==='APPROVED'&&<label>Monto de compensación (RD$) *<input autoFocus type="number" min="0.01" max="1000000000" step="0.01" value={amount} onChange={event=>setAmount(event.target.value)}/></label>}<label>{decision.status==='REJECTED'?'Motivo del rechazo *':'Observación de la aprobación'}<textarea autoFocus={decision.status==='REJECTED'} maxLength={2000} value={note} onChange={event=>setNote(event.target.value)}/></label>{error&&<p className="mw-error" role="alert">{error}</p>}<div><button disabled={busy} onClick={()=>setDecision(null)}>Cancelar</button><button className="primary" disabled={busy} onClick={()=>void save()}>{busy?'Guardando…':'Confirmar decisión'}</button></div></div>}</section>;
}
