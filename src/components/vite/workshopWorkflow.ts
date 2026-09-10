export type WorkshopStatus="SENT"|"RECEIVED"|"REPAIR"|"REPLACEMENT"|"DELIVERED";
export type WorkshopEvent={id:string;action:string;status:WorkshopStatus;note?:string;actorName:string;actorLogin:string;occurredAt:string;replacementSerial?:string;movementId?:string};
export type WorkshopCase={id:string;documentNumber:string;qrToken:string;equipmentType:string;serialNumber:string;quantity:number;department:string;deliveredByName?:string;senderName:string;returnName:string;sentAt:string;status:WorkshopStatus;failureCause:string;receivedByName?:string;receivedAt?:string;replacementSerial?:string;canOperate:boolean;events:WorkshopEvent[]};
export const workshopLabels:Record<WorkshopStatus,string>={SENT:"Pendiente de recibir",RECEIVED:"Entradas / órdenes recibidas",REPAIR:"En reparación",REPLACEMENT:"En reemplazo",DELIVERED:"Entregado"};
export function workshopCode(raw:string){
 const text=raw.trim();
 let code=text;
 try{const url=new URL(text);code=url.searchParams.get("equipment")||text;}catch{/* Los lectores USB pueden producir un texto parecido a URL sin ser una URL válida. */}
 try{code=decodeURIComponent(code);}catch{/* Conserva el valor sin decodificar si contiene un porcentaje incompleto. */}
 const upper=code.trim().toUpperCase();
 const compact=upper.replace(/[’‘´`]/g,"'").replace(/[Ç¦]/g,"|");
 const prefixed=compact.match(/REAL\W*MNT\W*(ALM|TAL)\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{5,})$/);
 if(prefixed)return `REAL-MNT|${prefixed[1]}-${prefixed[2]}-${prefixed[3]}-${prefixed[4]}`;
 const document=compact.match(/(?:^|\W)(ALM|TAL)\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{5,})$/);
 if(document)return `${document[1]}-${document[2]}-${document[3]}-${document[4]}`;
 return compact;
}
export function workshopTrackingUrl(code:string,origin=window.location.origin){return origin+"/?equipment="+encodeURIComponent(code);}
