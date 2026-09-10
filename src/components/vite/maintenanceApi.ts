import type {DocumentMarks,SignatureRole} from "./maintenanceDocumentPdf";

export async function registerDocumentSignature(endpoint:string,role:SignatureRole,data:string):Promise<DocumentMarks>{
  const applied=await maintenanceApi<DocumentMarks>(endpoint,"POST",{action:"SIGN",signatureRole:role,signatureData:data});
  const document=(role==="DELIVERY"?applied.deliverySignature:applied.receiptSignature)?.data?applied:await maintenanceApi<DocumentMarks>(endpoint);
  const registered=role==="DELIVERY"?document.deliverySignature:document.receiptSignature;
  if(registered?.data!==data)throw new Error("El servidor no devolvió la firma aplicada. Actualiza el formulario antes de volver a firmar.");
  return document;
}

export async function maintenanceApi<T>(path:string,method="GET",body?:unknown):Promise<T>{
  const response=await fetch(path,{method,credentials:"same-origin",headers:body?{"Content-Type":"application/json"}:undefined,body:body?JSON.stringify(body):undefined,cache:"no-store"});
  const data=await response.json().catch(()=>({}));
  if(!response.ok)throw new Error(data.error||data.message||"No fue posible completar la operación. Actualiza e inténtalo de nuevo.");
  return data as T;
}
