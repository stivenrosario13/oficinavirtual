import { apiFetch as fetch } from "@/lib/api-fetch";
import {useCallback,useEffect,useState} from 'react';
import {LifeBuoy,RefreshCw,Search} from 'lucide-react';
import type {OperationsTicket} from '@/lib/operations-center';
import './departmentOperationalAudit.css';
export default function ManagerDepartmentSupport({name}:{name:string}){
 const [rows,setRows]=useState<OperationsTicket[]>([]),[query,setQuery]=useState(''),[error,setError]=useState(''),[busy,setBusy]=useState(false);
 const load=useCallback(async()=>{setBusy(true);try{const r=await fetch('/api/department-support',{credentials:'same-origin',cache:'no-store'}),body=await r.json();if(!r.ok)throw Error(body.error||'No se pudo cargar el soporte');setRows(body.tickets||[]);setError('')}catch(e){setError((e as Error).message)}finally{setBusy(false)}},[]);
 useEffect(()=>{void load();const timer=setInterval(()=>void load(),30000);return()=>clearInterval(timer)},[load]);
 return <section className="department-operational-audit"><header><div><small><LifeBuoy/> GERENCIA · {name}</small><h1>Soporte de {name}</h1><p>Consulta los casos del departamento y su estado, sin modificar las asignaciones existentes.</p></div><button disabled={busy} onClick={()=>void load()}><RefreshCw/>Actualizar</button></header><label><Search/>Buscar ticket, agencia o asunto<input value={query} onChange={e=>setQuery(e.target.value)}/></label>{error&&<p role="alert">{error}</p>}<div className="manager-audit-table"><table><thead><tr><th>Ticket</th><th>Agencia / asunto</th><th>Estado</th><th>Detalle</th></tr></thead><tbody>{rows.filter(t=>`${t.ticketNumber} ${t.codigo} ${t.terminal} ${t.subject}`.toLowerCase().includes(query.toLowerCase())).map(t=><tr key={t.id}><td>#{t.ticketNumber}</td><td><strong>{t.subject}</strong><small>{t.codigo} · {t.terminal}</small></td><td>{({OPEN:'Pendiente',IN_PROGRESS:'En atención',PENDING:'Pendiente',RESOLVED:'Resuelto',CLOSED:'Cerrado',CANCELLED:'Anulado'} as Record<string,string>)[t.status]||t.status}</td><td>{t.description}</td></tr>)}</tbody></table></div>{!rows.length&&!error&&<p>{busy?'Cargando…':'No hay tickets en este departamento.'}</p>}</section>
}
