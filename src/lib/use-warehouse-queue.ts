import {useCallback,useEffect,useRef,useState} from 'react';
import {apiFetch} from './api-fetch';
import type {WarehouseQueueItem} from './warehouse-live-queue';

export function useWarehouseQueue(enabled=true) {
  const [items,setItems]=useState<WarehouseQueueItem[]>([]),[loading,setLoading]=useState(enabled),[error,setError]=useState('');
  const pending=useRef<AbortController|null>(null),active=useRef(false);
  const load=useCallback(async()=>{
    if(!enabled||pending.current)return;
    const controller=new AbortController();pending.current=controller;setLoading(true);
    try {
      const response=await apiFetch('/api/institutional-documents/warehouse-queue',{cache:'no-store',signal:controller.signal});
      if(!response.ok)throw new Error(response.status===401?'La sesión expiró. Inicia sesión nuevamente.':response.status===403?'Tu cuenta no tiene acceso a la cola de Almacén.':'No se pudo cargar la cola de Almacén. Pulsa Reintentar.');
      const data:unknown=await response.json();
      if(!Array.isArray(data))throw new Error('La respuesta de Almacén no es válida. Pulsa Reintentar.');
      if(active.current&&!controller.signal.aborted){setItems(data);setError('');}
    } catch(reason) {
      if(active.current&&!controller.signal.aborted)setError((reason as Error).message);
    } finally {
      if(pending.current===controller){pending.current=null;if(active.current)setLoading(false);}
    }
  },[enabled]);
  useEffect(()=>{
    active.current=true;void load();
    const refresh=()=>{if(!document.hidden&&navigator.onLine)void load();};
    const timer=enabled?window.setInterval(refresh,15000):null;
    window.addEventListener('support-live-refresh',refresh);
    return()=>{active.current=false;pending.current?.abort();pending.current=null;if(timer!==null)window.clearInterval(timer);window.removeEventListener('support-live-refresh',refresh);};
  },[enabled,load]);
  return {items,loading,error,refresh:load};
}
