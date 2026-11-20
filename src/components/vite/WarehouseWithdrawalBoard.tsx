import { apiFetch as fetch } from "@/lib/api-fetch";
import {useCallback,useEffect,useState} from 'react';
import type {DocumentDetail} from './DocumentSystemModule';
import InstitutionalDocumentDetail from './InstitutionalDocumentDetail';
import './warehouseFulfillment.css';

type Allocation={id:number;documentId:number;requestNumber:string;department:string;agencyCode:string;agencyName:string;agencyGroup:string;equipment:string;serial:string;quantity:number;status:string;deliveryNumber?:string;deliveredBy?:string;receivedBy?:string;withdrawnUtc?:string};
export default function WarehouseWithdrawalBoard({warehouse=false,department,onOpenDocument}:{warehouse?:boolean;department:string;onOpenDocument?:(document:DocumentDetail)=>void}){
 const [rows,setRows]=useState<Allocation[]>([]),[error,setError]=useState(''),[detail,setDetail]=useState<DocumentDetail|null>(null);
 const load=useCallback(async()=>{try{const response=await fetch('/api/warehouse/fulfillment/allocations',{credentials:'same-origin',cache:'no-store'});if(!response.ok)throw new Error('No se pudieron consultar los equipos pendientes y retirados.');setRows(await response.json());setError('')}catch(reason){setError((reason as Error).message)}},[]);
 useEffect(()=>{void load();const refresh=()=>void load();window.addEventListener('support-live-refresh',refresh);return()=>window.removeEventListener('support-live-refresh',refresh)},[load]);
 async function openDocument(id:number){const response=await fetch(`/api/institutional-documents/${id}`,{credentials:'same-origin',cache:'no-store'});if(!response.ok)throw new Error('No se pudo abrir el comprobante.');const document=await response.json();if(onOpenDocument)onOpenDocument(document);else setDetail(document)}
 const visible=rows.filter(row=>!department||row.department===department);
 if(detail)return <InstitutionalDocumentDetail key={detail.id} item={detail} warehouseAccess={warehouse} onBack={()=>setDetail(null)} onChanged={async()=>{await load();await openDocument(detail.id)}}/>;
 return <section className="warehouse-withdrawal-board"><h3>Equipos autorizados y retirados</h3><p>El retiro por QR se registra desde la tarjeta de cada solicitud.</p>{error&&<p role="alert">{error}</p>}
  {[['PENDING_PICKUP','Equipo pendiente de retiro'],['WITHDRAWN','Retirados']].map(([status,title])=><div key={status}><h4>{title} · {visible.filter(row=>row.status===status).reduce((sum,row)=>sum+row.quantity,0)} unidad(es)</h4><div className="warehouse-table-scroll"><table><thead><tr><th>Equipo / serial</th><th>Cantidad</th><th>Agencia / departamento</th><th>Entregado por</th><th>Recibido por</th><th>Fecha y hora</th><th>Comprobante</th></tr></thead><tbody>{visible.filter(row=>row.status===status).map(row=><tr key={row.id}><td>{row.equipment}<small>{row.serial}</small></td><td>{row.quantity}</td><td>{row.agencyCode} · {row.agencyName}<small>{row.agencyGroup} · {row.department}</small></td><td>{row.deliveredBy||'Pendiente'}</td><td>{row.receivedBy||'Pendiente'}</td><td>{row.withdrawnUtc?new Date(row.withdrawnUtc.endsWith('Z')?row.withdrawnUtc:row.withdrawnUtc+'Z').toLocaleString('es-DO',{timeZone:'America/Santo_Domingo'}):'Pendiente'}</td><td><button onClick={()=>void openDocument(row.documentId).catch(reason=>setError(reason.message))}>{row.deliveryNumber||row.requestNumber}</button></td></tr>)}{!visible.some(row=>row.status===status)&&<tr><td colSpan={7}>No hay equipos en esta tabla.</td></tr>}</tbody></table></div></div>)}
 </section>;
}
