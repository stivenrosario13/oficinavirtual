import { apiFetch as fetch } from './api-fetch';
// Never retry a resolution automatically: a timed-out write may have committed.
export async function submitTicketResolution(id:string,form:FormData,fetcher:typeof fetch=fetch,timeoutMs=90000){
 const controller=new AbortController();
 const timer=setTimeout(()=>controller.abort(),timeoutMs);
 try{
  const response=await fetcher(`/api/tickets/${encodeURIComponent(id)}/resolve`,{method:'POST',body:form,credentials:'same-origin',signal:controller.signal});
  const data=await response.json().catch(()=>null) as {ok?:boolean;error?:string;message?:string}|null;
  if(!response.ok||data?.ok!==true){
   const message=data?.error||data?.message||(response.status===401?'Tu sesión venció. Inicia sesión nuevamente.':response.status===413?'Las fotografías superan el tamaño que admite el servidor. Reduce su tamaño.':`No se confirmó la resolución (HTTP ${response.status}). Actualiza los tickets antes de volver a guardar.`);
   throw Object.assign(new Error(message),{status:response.status});
  }
 }catch(error){
  if(controller.signal.aborted)throw new Error('El servidor tardó demasiado en confirmar. Actualiza los tickets para comprobar si quedó resuelto antes de volver a guardar. Tus datos siguen en el formulario.');
  if(error instanceof TypeError)throw new Error('No se pudo confirmar la respuesta del servidor. Revisa la conexión y actualiza los tickets antes de volver a guardar.');
  throw error;
 }finally{clearTimeout(timer)}
}
