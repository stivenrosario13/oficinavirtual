import { apiFetch as fetch } from "@/lib/api-fetch";
import {useRef,useState} from 'react';
import {createPortal} from 'react-dom';
import {ScanBarcode,X} from 'lucide-react';
import type {DocumentDetail,DocumentItem} from './DocumentSystemModule';
import './warehouseFulfillment.css';

export default function WarehouseRequestPickupButton({item,onCompleted}:{item:DocumentItem;onCompleted:(document:DocumentDetail)=>Promise<void>}){
 const [open,setOpen]=useState(false),[qr,setQr]=useState(''),[name,setName]=useState(''),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const scanner=useRef<HTMLInputElement>(null),inFlight=useRef(false);
 const retired=Boolean((item as DocumentDetail).warehouseDelivery);
 async function begin(){
  setError('');
  try{const response=await fetch(`/api/institutional-documents/${item.id}`,{credentials:'same-origin',cache:'no-store'});if(!response.ok)throw new Error('No se pudo cargar la solicitud.');const detail=await response.json() as DocumentDetail;if(!detail.warehouseAssigned?.length&&detail.warehouseRequest?.lines.some(line=>line.kind!=='RETURN')){await onCompleted(detail);return;}setOpen(true)}catch(reason){setOpen(true);setError((reason as Error).message)}
 }
 async function retire(){
  if(inFlight.current)return;
  inFlight.current=true;setBusy(true);setError('');
  try{
   const response=await fetch('/api/warehouse/fulfillment/withdraw',{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify({documentId:item.id,sealQr:qr,receivedBy:name.trim()})});
   const body=await response.json().catch(()=>({}));
   if(!response.ok||body.ok!==true)throw new Error(body.message||body.error||`El servidor no confirmó el retiro (HTTP ${response.status}). Actualiza la solicitud antes de intentarlo nuevamente.`);
   const detailResponse=await fetch(`/api/institutional-documents/${item.id}`,{credentials:'same-origin',cache:'no-store'});
   if(!detailResponse.ok)throw new Error('El retiro fue registrado. Abre el formulario para ver sus sellos actualizados.');
   await onCompleted(await detailResponse.json());
   setOpen(false);setQr('');setName('');window.dispatchEvent(new Event('support-live-refresh'));
  }catch(reason){setError((reason as Error).message)}finally{inFlight.current=false;setBusy(false)}
 }
 return <>
  <button type="button" className="primary warehouse-request-pickup-button" disabled={item.status!=='approved'||retired} title={retired?'El retiro ya se registró; abre el formulario para ver los sellos':item.status==='approved'?'Registrar retiro de esta solicitud con su QR autorizado':'Primero recibe y autoriza esta solicitud'} onClick={()=>void begin()}><ScanBarcode/>{retired?'Equipo retirado':'Retiro de equipo por QR'}</button>
  {open&&createPortal(<div className="warehouse-pickup-backdrop"><section className="warehouse-pickup-dialog" role="dialog" aria-modal="true" aria-labelledby={`pickup-title-${item.id}`} onKeyDown={event=>{if(event.key==='Escape'&&!busy)setOpen(false)}}>
   <header><div><small>SOLICITUD AUTORIZADA · ALMACÉN</small><h3 id={`pickup-title-${item.id}`}>Retiro de equipo por QR</h3><p>{item.title}</p></div><button type="button" disabled={busy} aria-label="Cerrar retiro" onClick={()=>setOpen(false)}><X/></button></header>
   <form onSubmit={event=>{event.preventDefault();void retire()}}>
    <label>Nombre de quien recibe<input autoFocus required minLength={3} maxLength={160} value={name} onChange={event=>setName(event.target.value)} onKeyDown={event=>{if(event.key==='Enter'){event.preventDefault();scanner.current?.focus()}}} placeholder="Persona que retira el equipo"/></label>
    <label>Escanear QR del formulario o del sello AUTORIZADO<input ref={scanner} required data-warehouse-scanner="true" autoComplete="off" value={qr} onFocus={event=>event.currentTarget.select()} onChange={event=>{setQr(event.target.value);setError('')}} placeholder="Escanea el formulario de esta solicitud y confirma con Enter"/></label>
    <p>Puedes escanear el QR principal del formulario o el QR del sello autorizado. El QR debe corresponder a esta tarjeta. Al confirmar, se registra el retiro y se actualiza el comprobante con los sellos de entregado y recibido.</p>
    {error&&<p className="warehouse-pickup-error" role="alert">{error}</p>}
    <footer><button type="button" disabled={busy} onClick={()=>setOpen(false)}>Cancelar</button><button className="primary" disabled={busy||name.trim().length<3||!qr.trim()}>{busy?'Registrando retiro…':'Confirmar retiro y actualizar formulario'}</button></footer>
   </form>
  </section></div>,document.body)}
 </>;
}
