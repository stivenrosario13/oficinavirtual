import {departmentAssignment,type UserDepartment} from './user-departments';
import {operationalDefaults,operationalPermissionKeys} from './operational-permissions';
import callCenterSupport from '../../shared/call-center-support.json';
export const accessProfiles=[['ADMINISTRATOR','Superadministrador'],['DEPARTMENT_MANAGER','Gerente'],['SUPPORT','Soporte'],['WAREHOUSE_REQUESTER','Solicitudes a Almacén'],['TECHNICIAN','Técnico'],['SUPERVISOR','Supervisor'],['ADMINISTRATIVE','Administrativo'],['FISCALIZADOR','Fiscalizador'],['VIEWER','Consulta']] as const;
export type AccessProfile=typeof accessProfiles[number][0];
export const editablePermissionKeys=[...operationalPermissionKeys,'canViewFieldAudits','canManageDepartment','canManageUsers','canManageAudits','canManageAgencies','canViewDashboard','canAudit','canCreateTickets','canAssignTickets','canResolveTickets','canConfigureProfile','canExportAudits','canViewSupportDashboard','canViewAllSupportDepartments','canConfigureSupport','canViewSupportFindings','canCancelFindingWithEvidence','canCancelFindingWithoutEvidence','canCancelFindingWithReason','canCancelFindingWithoutReason','canUseSupportChat','canManageSupportChat','canModerateSupportChat','canDeleteSupportConversations','canShareSupportConversations','canDeleteSupportTickets','canViewTicketNotifications','canAuthorizeWarehouseRequests','canLoadWarehouseInventory'] as const;
export function userAccessProfile(profile:AccessProfile,department:UserDepartment){
 const assignment=departmentAssignment(department);let role=assignment.role,supportTeam=assignment.supportTeam;
 const permissions:Record<string,boolean>=Object.fromEntries(editablePermissionKeys.map(key=>[key,false]));
 Object.assign(permissions,{canView:true,canManage:false,canConfigureProfile:true,usesExplicitPermissions:true});
 if(profile==='ADMINISTRATOR'){
  role='ADMINISTRATOR';supportTeam=null;for(const key of editablePermissionKeys)permissions[key]=true;permissions.canManage=true;
 }else if(profile==='SUPERVISOR'){
  role='GROUP_ADMIN';supportTeam=null;Object.assign(permissions,{canViewDashboard:true,canAudit:true,canCreateTickets:true,canViewSupportFindings:true,canUseSupportChat:true,canShareSupportConversations:true,canViewTicketNotifications:true});
 }else if(profile==='ADMINISTRATIVE'){
  permissions['profile:ADMINISTRATIVE']=true;
  if(department==='ADMINISTRATIVE'){role='ADMINISTRATIVE';supportTeam=null;}
  else{role='DEPARTMENT';supportTeam=department;}
  permissions.canCreateTickets=true;
 }else if(profile==='FISCALIZADOR'||profile==='VIEWER'){
  role=profile;supportTeam=null;Object.assign(permissions,{canViewDashboard:true,canViewFieldAudits:profile==='FISCALIZADOR',canExportAudits:profile==='FISCALIZADOR'});
 }else{
  const technician=profile==='TECHNICIAN';const operational=assignment.role==='DEPARTMENT'||['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','CALL_CENTER','TECHNICAL_FAILURE'].includes(department);
  if(technician&&department==='TECHNOLOGY')supportTeam='TECHNICIANS';
  Object.assign(permissions,{canViewSupportDashboard:true,canCreateTickets:!technician&&operational,canAssignTickets:!technician&&operational,canResolveTickets:operational,canViewSupportFindings:operational,canCancelFindingWithEvidence:operational,canCancelFindingWithReason:operational,canUseSupportChat:operational||department==='WAREHOUSE'||department==='WORKSHOP',canManageSupportChat:operational&&!technician,canShareSupportConversations:operational,canViewTicketNotifications:operational||department==='WAREHOUSE'||department==='WORKSHOP'});
  if(department==='WAREHOUSE')permissions.canCreateTickets=true;
  if(profile==='DEPARTMENT_MANAGER')Object.assign(permissions,{canManageDepartment:true,canViewFieldAudits:true,canViewDashboard:true});
 }
 Object.assign(permissions,operationalDefaults(role,supportTeam));
 if(profile==='ADMINISTRATIVE')Object.assign(permissions,operationalDefaults('ADMINISTRATIVE',null),{canCreateTickets:true});
 if(profile==='TECHNICIAN')Object.assign(permissions,{canManageProcurement:false,canManageAgencyTransitions:false,canImportWorkshopInventory:false,canDecideDocuments:false});
 if(department==='WAREHOUSE'&&['SUPPORT','DEPARTMENT_MANAGER'].includes(profile))Object.assign(permissions,{canLoadWarehouseInventory:true,canAuthorizeWarehouseRequests:profile==='DEPARTMENT_MANAGER',canCreateWarehouseArticles:profile==='DEPARTMENT_MANAGER'});
 if(profile==='WAREHOUSE_REQUESTER'){
  for(const key of Object.keys(permissions))permissions[key]=false;
  Object.assign(permissions,{canView:true,canConfigureProfile:true,canRequestWarehouse:true,canViewDocuments:true,usesExplicitPermissions:true,'profile:WAREHOUSE_REQUESTER':true});
 }
 if(profile==='SUPPORT'&&department==='CALL_CENTER')Object.assign(permissions,callCenterSupport);
 return {role,supportTeam,permissions};
}
