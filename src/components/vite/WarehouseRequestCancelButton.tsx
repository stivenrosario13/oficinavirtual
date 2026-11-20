import {useRef,useState} from 'react';
import {XCircle} from 'lucide-react';
import {submitDocumentAction} from '@/lib/document-action';
import type {DocumentItem} from './DocumentSystemModule';
import './warehouseRequestCancel.css';

type Request=Pick<DocumentItem,'id'|'status'|'isOwner'|'warehouseRequest'|'recipientDepartment'> & {warehouseDelivery?:unknown};
export function canCancelWarehouseRequest(item:Request,canManage:boolean){
 return !!item.warehouseRequest&&item.recipientDepartment==='Almacén'&&(!!item.isOwner||canManage)&&!item.warehouseDelivery&&['pending','received','correction_requested','approved'].includes(item.status);
}
export default function WarehouseRequestCancelButton({item,canManage,onCancelled}:{item:Request;canManage:boolean;onCancelled:()=>Promise<void>}){
 const [open,setOpen]=useState(false),[note,setNote]=useState(''),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const submitting=useRef(false);
 if(!canCancelWarehouseRequest(item,canManage))return null;
 async function confirm(){
  if(submitting.current||note.trim().length<5)return;
  submitting.current=true;setBusy(true);setError('');
  try{await submitDocumentAction(item.id,'cancel',{note:note.trim()});setOpen(false);setNote('');await onCancelled();window.dispatchEvent(new Event('support-live-refresh'));}
  catch(reason){setError(reason instanceof Error?reason.message:'No se pudo anular la solicitud.');}
  finally{submitting.current=false;setBusy(false);}
 }
 return <div className="warehouse-card-cancel">
  <button type="button" className="warehouse-card-cancel-trigger" disabled={busy} onClick={()=>{setOpen(!open);setError('');}}><XCircle/>Anular solicitud</button>
  {open&&<section className="warehouse-card-cancel-form" aria-label="Anular solicitud">
   <strong>Anular solicitud de Almacén</strong>
   <label htmlFor={'cancel-reason-'+item.id}>Motivo obligatorio</label>
   <textarea id={'cancel-reason-'+item.id} autoFocus maxLength={2000} disabled={busy} value={note} onChange={event=>setNote(event.target.value)} placeholder="Explica el motivo (mínimo 5 caracteres)"/>
   <p>La solicitud quedará anulada y se liberarán las reservas pendientes. El historial se conservará.</p>
   {error&&<p role="alert">{error}</p>}
   <div><button type="button" disabled={busy||note.trim().length<5} onClick={()=>void confirm()}>{busy?'Anulando…':'Confirmar anulación'}</button><button type="button" disabled={busy} onClick={()=>setOpen(false)}>Volver</button></div>
  </section>}
 </div>;
}
