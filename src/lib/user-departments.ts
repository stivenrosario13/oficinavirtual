export const userDepartments=[
 ['TECHNOLOGY','Tecnología'],['GENERAL_SERVICES','Servicios Generales'],['HUMAN_RESOURCES','Recursos Humanos'],
 ['ADMINISTRATIVE','Administración'],['WAREHOUSE','Almacén'],['WORKSHOP','Taller'],['COMMUNICATION','Comunicación'],
 ['COMMERCIALIZATION','Comercialización'],['TREASURY','Tesorería'],['ACCOUNTING','Contabilidad'],['GENERAL_AUDIT','Auditoría General'],['VAULT','Bóveda'],['CASHIER','Caja'],
 ['CALL_CENTER','Call Center'],['TECHNICAL_FAILURE','Avería Técnica'],['TECHNICIANS','Técnicos'],['SUPERVISION','Supervisión'],
] as const;
export type UserDepartment=typeof userDepartments[number][0];
export function departmentAssignment(code:string){
 if(['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','WAREHOUSE'].includes(code))return {role:code,supportTeam:code==='WAREHOUSE'?'WAREHOUSE':null};
 if(['WORKSHOP','CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS'].includes(code))return {role:'TECHNOLOGY',supportTeam:code};
 return {role:'DEPARTMENT',supportTeam:code};
}
export function assignedUserDepartment(user:{role:string;supportTeam?:string|null}){
 if(user.role==='GROUP_ADMIN')return 'SUPERVISION';
 if(user.role==='DEPARTMENT')return user.supportTeam||'';
 if(user.supportTeam&&['WAREHOUSE','WORKSHOP','CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS'].includes(user.supportTeam))return user.supportTeam;
 return user.role;
}
