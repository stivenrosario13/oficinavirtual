import {it,expect} from 'vitest';
import {renderToStaticMarkup} from 'react-dom/server';
import AdministrativeNavigation from '../src/components/vite/AdministrativeNavigation';
import {isAdministrativeProfile} from '../src/lib/administrative-profiles';
import {userAccessProfile} from '../src/lib/user-access-profiles';
import {userDepartments} from '../src/lib/user-departments';
import {sessionRoleLabel,type PortalSession} from '../src/lib/session';

const administrativeSession=(department:string,permissions:Record<string,boolean>)=>({
 role:department==='ADMINISTRATIVE'?'Administrative':'Department',assignedDepartment:department,permissions,
} as PortalSession);

for(const [department,name] of userDepartments)it(`uses the administration sections in ${department}`,()=>{
 const preset=userAccessProfile('ADMINISTRATIVE',department);
 const session=administrativeSession(department,preset.permissions);
 expect(preset.permissions['profile:ADMINISTRATIVE']).toBe(true);
 expect(isAdministrativeProfile(session)).toBe(true);
 const navigation=(current:PortalSession)=>renderToStaticMarkup(<AdministrativeNavigation section="forms" canSupport={current.permissions.canCreateTickets} canAudit={!!current.permissions.canManageDepartment} canInventory={!!current.permissions.canManageDepartment&&!!current.permissions.canManageAdministrativeAssets} onSelect={()=>{}}/>);
 expect(navigation(session)).toBe(navigation(administrativeSession('ADMINISTRATIVE',userAccessProfile('ADMINISTRATIVE','ADMINISTRATIVE').permissions)));
 if(department!=='ADMINISTRATIVE')expect(sessionRoleLabel(session)).toBe(`Administrativo · ${name}`);
 const legacy={...preset.permissions};delete legacy['profile:ADMINISTRATIVE'];
 expect(isAdministrativeProfile(administrativeSession(department,legacy))).toBe(true);
 for(const profile of ['SUPPORT','TECHNICIAN','DEPARTMENT_MANAGER','WAREHOUSE_REQUESTER'] as const){
  const other=userAccessProfile(profile,department);
  expect(isAdministrativeProfile({role:'Department',permissions:other.permissions} as PortalSession)).toBe(false);
 }
});

it('keeps marked administrative identity with custom permissions and legacy markers',()=>{
 for(const marker of ['profile:ADMINISTRATIVE','profile:DEPARTMENT_ADMINISTRATIVE']){
  const session=administrativeSession('TREASURY',{[marker]:true,canCreateTickets:false});
  expect(isAdministrativeProfile(session)).toBe(true);
  expect(isAdministrativeProfile({...session,role:'Technology'})).toBe(false);
  expect(isAdministrativeProfile({...session,permissions:{...session.permissions,'profile:WAREHOUSE_REQUESTER':true}})).toBe(false);
 }
});
it('restores sidebar sections without granting extra permissions',()=>{
 const html=renderToStaticMarkup(<AdministrativeNavigation section="files" canSupport={false} canAudit={false} onSelect={()=>{}}/>);
 expect(html).toContain('Archivos y comunicaciones');expect(html).toContain('Formularios administrativos');expect(html).toContain('Solicitudes a Almacén');expect(html).not.toContain('>Soporte<');expect(html).not.toContain('Auditoría operativa');expect(html).toContain('aria-current="page"');
});
it('shows support and audit when granted',()=>{
 const html=renderToStaticMarkup(<AdministrativeNavigation section="support" canSupport canAudit onSelect={()=>{}}/>);
 expect(html).toContain('Soporte');expect(html).toContain('Auditoría operativa');
});
it('shows administrative inventory only when granted',()=>{
 const html=renderToStaticMarkup(<AdministrativeNavigation section="inventory" canSupport canAudit canInventory onSelect={()=>{}}/>);
 expect(html).toContain('Inventario administrativo');expect(html).toContain('aria-current="page"');
 const ordinary=renderToStaticMarkup(<AdministrativeNavigation section="forms" canSupport canAudit={false} onSelect={()=>{}}/>);
 expect(ordinary).not.toContain('Inventario administrativo');
});
