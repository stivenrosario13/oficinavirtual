import type {PortalSession} from './session';

export function isAdministrativeProfile(session:Pick<PortalSession,'role'|'permissions'>):boolean{
 if(session.role==='Administrative')return true;
 if(session.role!=='Department'||session.permissions['profile:WAREHOUSE_REQUESTER'])return false;
 const permissions=session.permissions;
 if(permissions['profile:ADMINISTRATIVE']||permissions['profile:DEPARTMENT_ADMINISTRATIVE'])return true;
 // Earlier quick-profile accounts saved these permissions without a profile marker.
 return !!permissions.usesExplicitPermissions&&permissions.canCreateTickets&&!!permissions.canViewDocuments&&!!permissions.canUploadDocuments
  &&!permissions.canManageDepartment&&!permissions.canManageUsers&&!permissions.canViewSupportDashboard&&!permissions.canAssignTickets&&!permissions.canResolveTickets;
}

export const administrativeForms:Record<string,string[]>={
 'gledys.tejeda':['sim','marbete','device','payment','fleet','modem','contract_request','contract_renewal','rental_contract','rent_receipt','increase_letter','fuel_log','gas_log','general','labor_benefits_request','blank_checks_delivery','signed_benefits_checks_delivery'],
 'emma.mendoza':['sim','marbete','device','payment','fleet'],
 'derihan.alisha':['device','modem'],
 'whilmaly.sulay':['contract_request','contract_renewal','rental_contract','rent_receipt','increase_letter','gas_log','general','labor_benefits_request','blank_checks_delivery','signed_benefits_checks_delivery'],
 'sandra.ramirez':['fuel_log'],
 'emely.figueroa':['contract_request'],
 'diana.ventura':['contract_request'],
 'yessica.mercedes':['incident_report'],
 'yesica.mercedes':['incident_report'],
 'lucianny.gonzalez':['incident_report'],
 'lucianny.gonzales':['incident_report'],
};
export const administrativeLogin=(email:string)=>email.trim().toLowerCase().split('\\').at(-1)!.split('/').at(-1)!.split('@')[0];
const administrativeIdentity=(value:string)=>value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]/g,'');
export const isIncidentOperatorAccount=(email:string,displayName='')=>['yessica.mercedes','yesica.mercedes','lucianny.gonzalez','lucianny.gonzales'].includes(administrativeLogin(email))||['yessicamercedes','yesicamercedes','luciannygonzalez','luciannygonzales'].includes(administrativeIdentity(displayName));
export const assignedAdministrativeForms=(email:string)=>administrativeForms[administrativeLogin(email)];
