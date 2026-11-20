import { apiFetch as fetch } from "@/lib/api-fetch";
import {useCallback,useEffect,useState} from 'react';
import type {PortalSession} from '@/lib/session';
import type {DocumentDetail} from './DocumentSystemModule';
import InstitutionalDocumentDetail from './InstitutionalDocumentDetail';
import './warehouseRequestComposer.css';

export default function WarehouseNotificationDocument({id,session,onClose}:{id:number;session:PortalSession;onClose:()=>void}){
 const [item,setItem]=useState<DocumentDetail|null>(null),[error,setError]=useState('');
 const load=useCallback(async()=>{const response=await fetch(`/api/institutional-documents/${id}`,{credentials:'same-origin',cache:'no-store'});if(!response.ok)throw new Error('No se pudo abrir la solicitud. Puede haber sido retirada de la bandeja.');setItem(await response.json());},[id]);
 useEffect(()=>{setItem(null);setError('');void load().catch(reason=>setError(reason.message));},[load]);
 useEffect(()=>{const close=(event:KeyboardEvent)=>{if(event.key==='Escape')onClose();};window.addEventListener('keydown',close);return()=>window.removeEventListener('keydown',close)},[onClose]);
 return <div className="warehouse-request-backdrop"><section className="warehouse-notification-document" role="dialog" aria-modal="true" aria-label="Solicitud recibida en Almacén"><button type="button" onClick={onClose}>Cerrar solicitud</button>{error?<p role="alert">{error}</p>:item?<InstitutionalDocumentDetail item={item} warehouseAccess warehouseAuthorizeAccess={session.permissions.canAuthorizeWarehouseRequests} onBack={onClose} onChanged={async(updated)=>{if(updated)setItem(updated);else await load();window.dispatchEvent(new Event('support-live-refresh'));}}/>:<p>Cargando solicitud…</p>}</section></div>;
}
