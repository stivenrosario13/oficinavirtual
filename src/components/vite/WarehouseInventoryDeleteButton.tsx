import { apiFetch as fetch } from "@/lib/api-fetch";
import {useState} from 'react';
import {Trash2} from 'lucide-react';
import type {PortalSession} from '@/lib/session';
import {canDeleteWarehouseInventory} from '@/lib/warehouse-inventory-delete';

export default function WarehouseInventoryDeleteButton({session,productId,name,quantity,department,onDeleted}:{session:PortalSession;productId?:string;name:string;quantity:number;department:string;onDeleted:()=>Promise<unknown>}){
 const [busy,setBusy]=useState(false),[error,setError]=useState('');
 if(!canDeleteWarehouseInventory(session)||(!productId&&quantity<1))return null;
 async function remove(){
  const reason=window.prompt(`Eliminar del inventario activo: ${name}\nCódigo: ${productId||'Registro manual'} · Existencias: ${quantity}\nSe conservará el historial. Escribe el motivo (5 a 500 caracteres):`);
  if(reason===null)return;
  if(reason.trim().length<5||reason.length>500){setError('Indica un motivo de 5 a 500 caracteres.');return;}
  if(!window.confirm(`¿Confirmas eliminar únicamente «${name}» (${productId||department}) del inventario activo?`))return;
  setBusy(true);setError('');
  let removed=false;
  try{
   const response=await fetch(`/api/warehouse/fulfillment/inventory/${productId?'products/remove':'remove'}`,{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify(productId?{productId,expectedQuantity:quantity,reason:reason.trim()}:{department,equipment:name,expectedQuantity:quantity,reason:reason.trim()})});
   const result=await response.json().catch(()=>null);
   if(!response.ok||result?.ok!==true)throw new Error(result?.message||'No se pudo eliminar el artículo. Revisa el inventario antes de reintentar.');
   removed=true;window.dispatchEvent(new Event('support-live-refresh'));await onDeleted();
  }catch(cause){setError(removed?'Artículo eliminado. Actualiza el inventario para ver el resultado.':(cause as Error).message)}finally{setBusy(false)}
 }
 return <div><button type="button" className="maintenance-pdf-button" style={{color:'#ffb4c1',borderColor:'#9b4a5d',background:'#432333',marginTop:8}} disabled={busy} aria-label={`Eliminar ${name} del inventario`} onClick={()=>void remove()}><Trash2/>{busy?'Eliminando…':'Eliminar artículo'}</button>{error&&<p role="alert" style={{color:'#ffc4cf',maxWidth:280}}>{error}</p>}</div>;
}
