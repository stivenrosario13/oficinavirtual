import {administrativeDepartmentName} from './administrative-requests';
type RequestRow={department:string;recipientDepartment?:string;isOwner:boolean;status:string};
export function requestHubCounts(rows:RequestRow[],workshop:{department:string;status:string}[],department:string){
 const own=rows.filter(row=>row.isOwner&&row.department===administrativeDepartmentName(department));
 const pending=new Set(['pending','received','correction_requested']);
 return {
  administrative:own.filter(row=>row.recipientDepartment==='Administración'&&pending.has(row.status)).length,
  warehouse:own.filter(row=>row.recipientDepartment==='Almacén'&&pending.has(row.status)).length,
  workshop:workshop.filter(row=>row.department===department&&['SENT','RECEIVED','REPAIR','REPLACEMENT','PART_REPLACEMENT'].includes(row.status)).length,
 };
}
