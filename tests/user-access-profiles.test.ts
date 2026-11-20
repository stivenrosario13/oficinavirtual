import {describe,it,expect} from 'vitest';
import {readFileSync} from 'node:fs';
import {accessProfiles,editablePermissionKeys,userAccessProfile} from '../src/lib/user-access-profiles';
import {userDepartments} from '../src/lib/user-departments';
import {allPermissionCatalog} from '../src/components/vite/UsersPermissionsWorkspace';
import callCenterSupport from '../shared/call-center-support.json';
describe('perfiles rápidos de usuarios',()=>{
 it('Call Center usa un perfil principal completo y separado de Tecnología',()=>{
  const profile=userAccessProfile('SUPPORT','CALL_CENTER');
  expect(profile.role).toBe('TECHNOLOGY');
  expect(profile.supportTeam).toBe('CALL_CENTER');
  expect(profile.permissions).toEqual(callCenterSupport);
  for(const key of editablePermissionKeys)expect(callCenterSupport).toHaveProperty(key);
  for(const key of ['canViewWorkshop','canViewMaintenance','canManageProcurement','canViewAgencyTransitions','canManageDepartment','canViewAllSupportDepartments','canViewSupportFindings'])expect(profile.permissions[key]).toBe(false);
  for(const key of ['canAssignTickets','canResolveTickets','canCreateTickets','canUseSupportChat','canViewDocuments','canRequestWarehouse'])expect(profile.permissions[key]).toBe(true);
 });
 for(const [department] of userDepartments)it(`solicitante de almacén sin acceso operativo: ${department}`,()=>{
  const preset=userAccessProfile('WAREHOUSE_REQUESTER',department);
  expect(preset.permissions['profile:WAREHOUSE_REQUESTER']).toBe(true);
  expect(preset.permissions.canRequestWarehouse).toBe(true);
  for(const key of editablePermissionKeys)expect(preset.permissions[key]).toBe(['canViewDocuments','canRequestWarehouse','canConfigureProfile'].includes(key));
 });
 for(const [department] of userDepartments)it(`soporte inicial restringido: ${department}`,()=>{
  const profile=userAccessProfile('SUPPORT',department);
  for(const permission of ['canManageUsers','canManageDepartment','canViewAllSupportDepartments','canConfigureSupport','canManageAudits','canManageAgencies','canDeleteSupportTickets'])expect(profile.permissions[permission]).toBe(false);
  expect(profile.role).not.toBe('ADMINISTRATOR');expect(profile.role).not.toBe('GROUP_ADMIN');expect(profile.permissions.canViewSupportDashboard).toBe(true);
 });
 it('restablece equipo y rol al cambiar de departamento',()=>{
  expect(userAccessProfile('SUPPORT','GENERAL_SERVICES').supportTeam).toBeNull();
  expect(userAccessProfile('SUPPORT','TECHNOLOGY').supportTeam).toBeNull();
  expect(userAccessProfile('SUPPORT','CALL_CENTER').supportTeam).toBe('CALL_CENTER');
  expect(userAccessProfile('SUPPORT','WAREHOUSE').role).toBe('WAREHOUSE');
 });
 it('técnicos resuelven sin asignar; gerentes no obtienen acceso global',()=>{
  for(const department of ['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'] as const){
   const technician=userAccessProfile('TECHNICIAN',department);expect(technician.permissions.canResolveTickets).toBe(true);expect(technician.permissions.canAssignTickets).toBe(false);
   const manager=userAccessProfile('DEPARTMENT_MANAGER',department);expect(manager.permissions.canManageDepartment).toBe(true);expect(manager.permissions.canManageUsers).toBe(false);
  }
 });
 it('ofrece todos los perfiles y permisos del catálogo',()=>{
  expect(accessProfiles).toHaveLength(9);
  const source=readFileSync('src/components/vite/AdminPanel.tsx','utf8');
  for(const key of editablePermissionKeys)expect(allPermissionCatalog.some(permission=>permission.key===key)).toBe(true);
  expect(source).toContain('useState<UserAccessProfile>("SUPPORT")');
  expect(source).toContain('if(savingUserRef.current)return');
 });
 for(const [department] of userDepartments)it(`administrativo conserva exactamente sus permisos en ${department}`,()=>{
  const profile=userAccessProfile('ADMINISTRATIVE',department);
  const administration=userAccessProfile('ADMINISTRATIVE','ADMINISTRATIVE');
  expect(profile.permissions.canManageUsers).toBe(false);
  expect(profile.permissions.canManageDepartment).toBe(false);
  expect(profile.permissions.canCreateTickets).toBe(true);
  expect(profile.permissions.canViewDocuments).toBe(true);
  expect(profile.permissions.canUploadDocuments).toBe(true);
  expect(profile.permissions.canRequestWarehouse).toBe(true);
  for(const key of editablePermissionKeys)expect(profile.permissions[key]).toBe(administration.permissions[key]);
  if(department==='ADMINISTRATIVE'){
   expect(profile.role).toBe('ADMINISTRATIVE');expect(profile.supportTeam).toBeNull();
  }else{
   expect(profile.role).toBe('DEPARTMENT');expect(profile.supportTeam).toBe(department);
  }
 });
 it('permite elegir el departamento con el único perfil Administrativo',()=>{
  const profile=userAccessProfile('ADMINISTRATIVE','TECHNOLOGY');
  expect(profile.role).toBe('DEPARTMENT');expect(profile.supportTeam).toBe('TECHNOLOGY');
  const workspace=readFileSync('src/components/vite/UsersPermissionsWorkspace.tsx','utf8');
  expect(workspace).toContain("'SUPERVISOR'?'SUPERVISION':nextDepartment");
  expect(workspace).toContain("['ADMINISTRATOR','SUPERVISOR','FISCALIZADOR','VIEWER'].includes(profile)");
  expect(workspace).not.toContain("['ADMINISTRATOR','SUPERVISOR','ADMINISTRATIVE'");
  const api=readFileSync('api/Program.cs','utf8');
  expect(api).toContain('"SUPERVISOR","ADMINISTRATIVE","FISCALIZADOR"');
  expect(api).not.toContain('"DEPARTMENT_ADMINISTRATIVE"');
 });
 it('selección del supervisor abre directamente el formulario',()=>{
  const source=readFileSync('src/components/vite/TicketCenter.tsx','utf8');
    expect(source).toContain('if (reportAgency || session.role === "GroupAdministrator" || session.role === "Department")');
  });
 it('el perfil Supervisor siempre incluye la auditoría de campo',()=>{
  for(const [department] of userDepartments){
   const supervisor=userAccessProfile('SUPERVISOR',department);
   expect(supervisor.role).toBe('GROUP_ADMIN');
   expect(supervisor.permissions.canAudit).toBe(true);
  }
  const api=readFileSync('api/Program.cs','utf8');
  expect(api).toContain('if (account.Role == "GroupAdministrator")');
  expect(api).toContain('dictionary["canAudit"] = true;');
 });
 for(const department of ['TREASURY','ACCOUNTING','COMMERCIALIZATION','COMMUNICATION','ADMINISTRATIVE','SUPERVISION'] as const)it(`soporte completo sin privilegios globales: ${department}`,()=>{
  const support=userAccessProfile('SUPPORT',department);
  expect(support.role).toBe('DEPARTMENT');expect(support.supportTeam).toBe(department);
  for(const key of ['canCreateTickets','canAssignTickets','canResolveTickets','canViewSupportFindings','canUseSupportChat','canViewTicketNotifications'])expect(support.permissions[key]).toBe(true);
  expect(support.permissions.canManageUsers).toBe(false);
  const technician=userAccessProfile('TECHNICIAN',department);expect(technician.permissions.canResolveTickets).toBe(true);expect(technician.permissions.canAssignTickets).toBe(false);
 });
});
