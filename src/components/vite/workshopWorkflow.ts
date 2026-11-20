export type WorkshopStatus="SENT"|"RECEIVED"|"REPAIR"|"REPLACEMENT"|"INVENTORY"|"PART_REPLACEMENT"|"DISCHARGED"|"DELIVERED";
export type WorkshopEvent={id:string;action:string;status:WorkshopStatus;note?:string;actorName:string;actorLogin:string;occurredAt:string;replacementSerial?:string;movementId?:string};
export type WorkshopCase={pendingWarehouse?:boolean;canReceiveWarehouse?:boolean;isManualEntry?:boolean;canDelete?:boolean;notes?:string;componentType?:string;agencyId?:string;agencyCode?:string;agencyName?:string;ticketId?:string;id:string;documentNumber:string;qrToken:string;equipmentType:string;serialNumber:string;quantity:number;department:string;deliveredByName?:string;senderName:string;returnName:string;sentAt:string;status:WorkshopStatus;failureCause:string;receivedByName?:string;receivedAt?:string;replacementSerial?:string;canOperate:boolean;events:WorkshopEvent[]};
export const workshopLabels:Record<WorkshopStatus,string>={SENT:"Pendiente de recibir",RECEIVED:"Entradas / órdenes recibidas",REPAIR:"En reparación",REPLACEMENT:"Reemplazo de equipo",PART_REPLACEMENT:"Reemplazo de pieza",DISCHARGED:"Sin reparación",INVENTORY:"Inventario de Taller",DELIVERED:"Enviado a Almacén"};
export function workshopCode(raw:string){
 let text=raw.trim();
 for(let attempt=0;attempt<2;attempt+=1){try{const decoded=decodeURIComponent(text);if(decoded===text)break;text=decoded;}catch{break;}}
 let code=text;
 try{const url=new URL(text,typeof window!=="undefined"?window.location.origin:"https://realagencias.runasp.net");code=url.searchParams.get("equipment")||url.searchParams.get("code")||url.searchParams.get("qr")||url.searchParams.get("token")||text;}catch{/* Los lectores USB pueden producir un texto parecido a URL sin ser una URL válida. */}
 try{code=decodeURIComponent(code);}catch{/* Conserva el valor sin decodificar si contiene un porcentaje incompleto. */}
 const upper=code.trim().replace(/[\r\n\t]+/g,"").replace(/^['"]|['"]$/g,"").toUpperCase();
 const compact=upper.replace(/[’‘´`]/g,"'").replace(/[Ç¦]/g,"|");
 const warehouseRequest=compact.match(/REAL\W*MNT\W*SOL\W*ALM\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{4,})$/);
 if(warehouseRequest)return `REAL-MNT|SOL-ALM-${warehouseRequest[1]}-${warehouseRequest[2]}-${warehouseRequest[3]}`;
 const warehouseDocument=compact.match(/(?:^|\W)SOL\W*ALM\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{4,})$/);
 if(warehouseDocument)return `SOL-ALM-${warehouseDocument[1]}-${warehouseDocument[2]}-${warehouseDocument[3]}`;
 const prefixed=compact.match(/REAL\W*MNT\W*(ALM|TAL)\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{5,})$/);
 if(prefixed)return `REAL-MNT|${prefixed[1]}-${prefixed[2]}-${prefixed[3]}-${prefixed[4]}`;
 const document=compact.match(/(?:^|\W)(ALM|TAL)\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{5,})$/);
 if(document)return `${document[1]}-${document[2]}-${document[3]}-${document[4]}`;
 return compact;
}
export function workshopTrackingUrl(code:string,origin=window.location.origin){return origin+"/?equipment="+encodeURIComponent(code);}
