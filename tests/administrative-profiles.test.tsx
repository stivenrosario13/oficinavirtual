import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {describe,it,expect} from 'vitest';
import AdministrativeRequestComposer from '../src/components/vite/AdministrativeRequestComposer';
import AdministrativeRequests from '../src/components/vite/AdministrativeRequests';
import WarehouseRequestComposer from '../src/components/vite/WarehouseRequestComposer';
import {assignedAdministrativeForms} from '../src/lib/administrative-profiles';
import type {PortalSession} from '../src/lib/session';
const session=(login:string)=>({email:login+'@grupotejeda.local',role:'Administrative',displayName:login} as PortalSession);
describe('assigned administrative forms',()=>{
 it('gives Gledys the complete existing administrative catalog',()=>{
  const forms=assignedAdministrativeForms(' GLEDYS.TEJEDA@grupotejeda.local ')!;
  expect(forms).toHaveLength(17);
  for(const login of ['emma.mendoza','derihan.alisha','whilmaly.sulay','sandra.ramirez','emely.figueroa','diana.ventura'])
   for(const kind of assignedAdministrativeForms(login)!)expect(forms).toContain(kind);
  const html=renderToStaticMarkup(<AdministrativeRequestComposer session={session('gledys.tejeda')} department="Administración" onClose={()=>{}} onSaved={async()=>{}}/>);
  for(const title of ['Asignación de flota','Entrega de módem','Solicitud general','Relación de combustible'])expect(html).toContain(title);
 });
 it('separates all six profiles and preserves primary admin catalog',()=>{
  for(const login of ['emma.mendoza','derihan.alisha','whilmaly.sulay','sandra.ramirez','emely.figueroa','diana.ventura']){
   const html=renderToStaticMarkup(<AdministrativeRequestComposer session={session(login)} department="Administración" onClose={()=>{}} onSaved={async()=>{}}/>);
   const assigned=assignedAdministrativeForms(login)!;
   const extraPayroll=['labor_benefits_request','blank_checks_delivery','signed_benefits_checks_delivery'].filter(kind=>!assigned.includes(kind)).length;
   expect((html.match(/<strong>/g)||[]).length).toBe(assigned.length+extraPayroll+1);
   if(login==='emma.mendoza'){expect(html).toContain('Asignación de flota');expect(html).not.toContain('Entrega de módem');}
   if(login==='derihan.alisha'){expect(html).toContain('Entrega de módem');expect(html).not.toContain('Solicitud general');}
  }
  expect(assignedAdministrativeForms('admin@local.com')).toBeUndefined();
 });
 it('shows the three payroll forms to every administrative account',()=>{
  for(const login of ['gledys.tejeda','emma.mendoza','derihan.alisha','whilmaly.sulay','sandra.ramirez','emely.figueroa','diana.ventura']){
   const html=renderToStaticMarkup(<AdministrativeRequestComposer session={session(login)} department="Administración" onClose={()=>{}} onSaved={async()=>{}}/>);
   for(const title of ['Solicitud de pago de prestaciones en cheque','Entrega de cheques en blanco a Contabilidad','Entrega de cheques firmados de prestaciones a Caja'])expect(html).toContain(title);
  }
 });
 it('keeps the payroll forms out of the main page and only in the new-request modal',()=>{
  const html=renderToStaticMarkup(<AdministrativeRequests session={session('gledys.tejeda')}/>);
  expect(html).not.toContain('FLUJO DE PRESTACIONES LABORALES');
  for(const title of ['Solicitud de pago de prestaciones en cheque','Entrega de cheques en blanco a Contabilidad','Entrega de cheques firmados de prestaciones a Caja'])expect(html).not.toContain(title);
 });
 it('always shows the three payroll forms to the primary administrator in the administrative catalog',()=>{
  const administrator={...session('admin'),role:'Administrator'} as PortalSession;
  const html=renderToStaticMarkup(<AdministrativeRequestComposer session={administrator} department="ADMINISTRATIVE" onClose={()=>{}} onSaved={async()=>{}}/>);
  for(const title of ['Solicitud de pago de prestaciones en cheque','Entrega de cheques en blanco a Contabilidad','Entrega de cheques firmados de prestaciones a Caja'])expect(html).toContain(title);
 });
 it('limits payroll forms to Human Resources and Administration departments',()=>{
  const humanResources={...session('rrhh'),role:'HumanResources'} as PortalSession;
  const hrHtml=renderToStaticMarkup(<AdministrativeRequestComposer session={humanResources} department="HUMAN_RESOURCES" onClose={()=>{}} onSaved={async()=>{}}/>);
  expect(hrHtml).toContain('Solicitud de pago de prestaciones en cheque');
  expect(hrHtml).not.toContain('Entrega de cheques en blanco a Contabilidad');
  expect(hrHtml).not.toContain('Entrega de cheques firmados de prestaciones a Caja');
  const otherDepartment={...session('contabilidad'),role:'Department'} as PortalSession;
  const otherHtml=renderToStaticMarkup(<AdministrativeRequestComposer session={otherDepartment} department="ACCOUNTING" onClose={()=>{}} onSaved={async()=>{}}/>);
  for(const title of ['Solicitud de pago de prestaciones en cheque','Entrega de cheques en blanco a Contabilidad','Entrega de cheques firmados de prestaciones a Caja'])expect(otherHtml).not.toContain(title);
 });
 it('keeps own department and restores agency destinations for administrative profiles',()=>{
  const html=renderToStaticMarkup(<WarehouseRequestComposer session={session('sandra.ramirez')} department="ADMINISTRATIVE" agencies={[]} onClose={()=>{}} onSaved={()=>{}}/>);
  expect(html).toContain('Agencia de la solicitud');
  for(const label of ['Para un grupo','Para una agencia','Para varias agencias','Para mi departamento'])expect(html).toContain(label);
 });
 it('retains all three warehouse destinations for supervisors and support',()=>{
  for(const department of ['TECHNOLOGY','GENERAL_SERVICES','SUPERVISION','WAREHOUSE']){
   const html=renderToStaticMarkup(<WarehouseRequestComposer session={session('qa')} department={department} agencies={[]} onClose={()=>{}} onSaved={()=>{}}/>);
   for(const label of ['Para un grupo','Para una agencia','Para mi departamento'])expect(html).toContain(label);
  }
 });
});
