import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import WarehouseRequestComposer,{warehouseDestinationOptions} from '../src/components/vite/WarehouseRequestComposer';
import type {PortalSession} from '../src/lib/session';
const session={authenticated:true,role:'Warehouse',supportTeam:'WAREHOUSE',email:'qa@example.invalid',displayName:'Almacén QA'} as PortalSession;
it('solicitud interna no obliga a seleccionar una agencia',()=>{
 const html=renderToStaticMarkup(<WarehouseRequestComposer session={session} department="WAREHOUSE" agencies={[]} onClose={()=>{}} onSaved={()=>{}}/>);
 expect(html).toContain('Departamento de Almacén');expect(html).toContain('No requiere agencia ni ticket');
 expect(html).not.toContain('Seleccionar agencia');expect(html).toContain('Prioridad');
});
it('conserva la selección de agencia en solicitudes departamentales',()=>{
 const html=renderToStaticMarkup(<WarehouseRequestComposer session={session} department="TECHNOLOGY" agencies={[]} onClose={()=>{}} onSaved={()=>{}}/>);
 expect(html).toContain('Agencia de la solicitud');expect(html).not.toContain('No requiere agencia ni ticket');
 expect(html).not.toContain('Agencia para artículo 1');expect(html).toContain('Para varias agencias');
 expect(html).toContain('Agregar artículo');expect(html).toContain('Todos los artículos se enviarán para esa agencia');
});
it('ordena las etapas recepción, autorización, asignación y retiro',()=>{
 const file=readFileSync('src/components/vite/WorkshopBoard.tsx','utf8');
 const steps=file.slice(file.indexOf('const requestSteps'),file.indexOf('const regularSteps'));
 expect(steps.indexOf('decisionAction')).toBeLessThan(steps.indexOf('DISPATCHED'));
 expect(steps.indexOf('DISPATCHED')).toBeLessThan(steps.indexOf('WITHDRAWN'));
 expect(file).toContain('/api/warehouse/fulfillment/track?code=');
});
it.each(['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'])('exige agencia para nueva solicitud de %s incluso con el antiguo modo departamental',department=>{
 const html=renderToStaticMarkup(<WarehouseRequestComposer session={session} department={department} departmentOnly agencies={[]} onClose={()=>{}} onSaved={()=>{}}/>);
 expect(html).not.toContain('No requiere agencia ni ticket');expect(html).toContain('Agencia de la solicitud');
});
it('Administración comienza mostrando agencias y permite elegir departamento',()=>{const html=renderToStaticMarkup(<WarehouseRequestComposer session={session} department="ADMINISTRATIVE" agencies={[]} onClose={()=>{}} onSaved={()=>{}}/>);expect(html).toContain('Agencia de la solicitud');expect(html).toContain('Para mi departamento');expect(html).toContain('Para varias agencias');expect(html).toContain('Para un grupo');});
it('el perfil Administrativo conserva departamento y recupera una o varias agencias',()=>{
 const administrative={...session,role:'Administrative',supportTeam:null,permissions:{...session.permissions,'profile:ADMINISTRATIVE':true}} as PortalSession;
 const html=renderToStaticMarkup(<WarehouseRequestComposer session={administrative} department="ADMINISTRATIVE" departmentOnly agencies={[]} onClose={()=>{}} onSaved={()=>{}}/>);
 expect(html).toContain('Para mi departamento');expect(html).toContain('Para una agencia');expect(html).toContain('Para varias agencias');
 expect(warehouseDestinationOptions(administrative).map(option=>option.value)).toEqual(['GROUP','AGENCY','MULTI_AGENCY','DEPARTMENT']);
});
it('el perfil Soporte puede pedir para una o varias agencias aunque reciba el modo departamental antiguo',()=>{
 const support={...session,role:'Department',supportTeam:'GENERAL_SERVICES',permissions:{...session.permissions,'profile:SUPPORT':true}} as PortalSession;
 const html=renderToStaticMarkup(<WarehouseRequestComposer session={support} department="GENERAL_SERVICES" departmentOnly agencies={[]} onClose={()=>{}} onSaved={()=>{}}/>);
 expect(html).toContain('Para mi departamento');expect(html).toContain('Para una agencia');expect(html).toContain('Para varias agencias');expect(html).toContain('Agencia de la solicitud');
});
it('un ticket conserva su agencia aunque se indique modo departamental',()=>{
 const html=renderToStaticMarkup(<WarehouseRequestComposer session={session} department="TECHNOLOGY" departmentOnly agencies={[]} linkedTicket={{id:'qa',ticketNumber:15,codigo:'123',terminal:'Agencia QA',grupo:'QA',subject:'Monitor'}} onClose={()=>{}} onSaved={()=>{}}/>);
 expect(html).toContain('Agencia del ticket');expect(html).toContain('Vinculada al ticket');
});
