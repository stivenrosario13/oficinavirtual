import {describe,it,expect} from 'vitest';
import {renderDeferred} from './render-deferred';
import type {PortalSession} from '../src/lib/session';
import AdministrativePortal from '../src/components/vite/AdministrativePortal';
import WarehouseSupport from '../src/components/vite/WarehouseSupport';
const session=(email:string)=>({authenticated:true,role:'Administrative',email,displayName:'QA',permissions:{canCreateTickets:true},supportTeam:null} as PortalSession);
describe('Emma department portal',()=>{
 it('opens operational audit and inventory for the administrative manager',async()=>{
  const gledys={...session('gledys.tejeda'),permissions:{...session('gledys.tejeda').permissions,canManageDepartment:true,canManageAdministrativeAssets:true}};
  const audit=await renderDeferred(<AdministrativePortal session={gledys} section="audit"/>);
  expect(audit).toContain('Auditoría operativa');expect(audit).not.toContain('no tiene acceso');
  const inventory=await renderDeferred(<AdministrativePortal session={gledys} section="inventory"/>);
  expect(inventory).toContain('Inventario de flotas y módems');
  const restricted=await renderDeferred(<AdministrativePortal session={session('sandra.ramirez')} section="audit"/>);
  expect(restricted).toContain('no tiene acceso');
 });
 it('shows only warehouse requests in the supervisor requests section',async()=>{
  const html=await renderDeferred(<AdministrativePortal session={{...session('supervisor'),role:'GroupAdministrator'}}/>);
  expect(html).toContain('Nueva solicitud');
  expect(html).not.toContain('Formularios administrativos');
 });
 it('opens forms without duplicating sidebar navigation for Emma',async()=>{
  const html=await renderDeferred(<AdministrativePortal session={session('emma.mendoza@grupotejeda.local')}/>);
  expect(html).toContain('Formularios administrativos');
  expect(html).not.toContain('Secciones de Administrativo');
  expect(html).not.toContain('administrative-portal-nav');
 });
 it('opens the file library for every administrative profile instead of warehouse requests',async()=>{
  const html=await renderDeferred(<AdministrativePortal session={session('sandra.ramirez')} section="files"/>);
  expect(html).toContain('Biblioteca de Administración');
  expect(html).toContain('Carpetas compartidas');
  expect(html).toContain('Papelera');
  expect(html).not.toContain('Solicitudes de equipo');
  expect(html).not.toContain('Solicitudes a Taller');
 });
 it('offers the three requested support destinations',async()=>{
  const html=await renderDeferred(<WarehouseSupport session={session('emma.mendoza')}/>);
  for(const label of ['Tecnología, Servicios Generales o Recursos Humanos','Administración','Reportar incidencia'])expect(html).toContain(label);
 });
 it('shows only the ticket creation card in Administrative support',async()=>{
  const html=await renderDeferred(<WarehouseSupport session={session('sandra.ramirez')} createOnly/>);
  expect(html).toContain('Crear ticket');
  expect(html).not.toContain('Mis tickets');
  expect(html).not.toContain('Actualizar');
 });
 it.each([
  ['ADMINISTRATIVE','Administración'],['COMMUNICATION','Comunicación'],['COMMERCIALIZATION','Comercialización'],
  ['TREASURY','Tesorería'],['ACCOUNTING','Contabilidad'],['SUPERVISION','Supervisión'],
 ])('shows the same isolated creation card for %s', async (code,name)=>{
  const html=await renderDeferred(<WarehouseSupport session={{...session('department@example.com'),role:'Department',assignedDepartment:code}} createOnly/>);
  expect(html).toContain(`Soporte de ${name}`);
  expect(html).toContain('Crear ticket');
  expect(html).not.toContain('Mis tickets');
 });
 it('keeps the Warehouse ticket list separate from its new creation card',async()=>{
  const warehouse={...session('warehouse@example.com'),role:'Warehouse'};
  const create=await renderDeferred(<WarehouseSupport session={warehouse} createOnly/>);
  const existing=await renderDeferred(<WarehouseSupport session={warehouse}/>);
  expect(create).toContain('Crear ticket');
  expect(create).not.toContain('Mis tickets');
  expect(existing).toContain('Mis tickets');
 });
});
