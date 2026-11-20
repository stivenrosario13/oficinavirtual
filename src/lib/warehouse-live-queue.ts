import {deviceTimestamp} from "./display";
import {operationalQueueWindow} from "./operations-center";

export type WarehouseQueueStatus="PENDING"|"AUTHORIZED"|"REJECTED";

// Route by destination, never by the sender's department or the document title.
export function isWarehouseRequest(request:{recipientDepartment?:string|null}){
  return request.recipientDepartment === "Almacén";
}

export type WarehouseQueueRequest={
  id:number;
  status:string;
  createdUtc:string;
  decisionUtc?:string;
};

export type WarehouseQueueItem = WarehouseQueueRequest & {
  title: string; originalFileName: string; department: string;
  recipientDepartment: string; ownerUserName: string; ownerDisplayName?: string; decisionBy?: string;
};

export function warehouseQueueStatus(request:Pick<WarehouseQueueRequest,"status">):WarehouseQueueStatus{
  if(request.status==="approved")return "AUTHORIZED";
  if(request.status==="rejected")return "REJECTED";
  return "PENDING";
}

export function warehouseRequestsInOperationalQueue<T extends WarehouseQueueRequest>(requests:T[],now=new Date()){
  const {start,end}=operationalQueueWindow(undefined,now),current=now.getTime();
  if(current<start.getTime()||current>=end.getTime())return [];
  return requests.filter(request=>{
    const created=deviceTimestamp(request.createdUtc);
    return request.status!=="cancelled"&&created>=start.getTime()&&created<end.getTime();
  });
}

export function warehouseRequestResultForOperationalDate<T extends WarehouseQueueRequest>(request:T,dateValue:string){
  if(request.status==="cancelled")return null;
  const {start,end}=operationalQueueWindow(dateValue),created=deviceTimestamp(request.createdUtc);
  if(created<start.getTime()||created>=end.getTime())return null;
  const decision=request.decisionUtc?deviceTimestamp(request.decisionUtc):Number.NaN;
  const decidedInWindow=Number.isFinite(decision)&&decision>=start.getTime()&&decision<=end.getTime();
  const status=warehouseQueueStatus(request);
  return{
    result:decidedInWindow&&status!=="PENDING"?status:"PENDING" as WarehouseQueueStatus,
    decisionAt:decidedInWindow&&status!=="PENDING"?decision:null,
  };
}

export function warehouseQueueCode(request:Pick<WarehouseQueueRequest,"id">){
  return `ALM-${String(request.id).padStart(4,"0")}`;
}
