import { apiFetch as fetch } from "@/lib/api-fetch";
export async function submitDocumentAction<T>(id:number,action:string,body:object):Promise<T|undefined>{
  const response=await fetch(`/api/institutional-documents/${id}/actions/${action}`,{
    method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",
    headers:{"Content-Type":"application/json",Accept:"application/json"},body:JSON.stringify(body),
  });
  const invalid="El servidor devolvió una respuesta no válida. Comprueba que la actualización esté activa antes de volver a confirmar.";
  if(!response.headers.get("content-type")?.toLowerCase().includes("json"))throw new Error(invalid);
  const payload=await response.json().catch(()=>{throw new Error(invalid)}) as {ok?:boolean;document?:T;error?:string;message?:string}|null;
  if(!payload||typeof payload!=="object")throw new Error(invalid);
  if(!response.ok)throw new Error(payload.message||payload.error||"No se pudo completar la acción.");
  if(payload.ok!==true)throw new Error(invalid);
  return payload.document;
}
