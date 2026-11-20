import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {expect,it,vi} from 'vitest';
import WarehouseRequestComposer from '../src/components/vite/WarehouseRequestComposer';
import type {PortalSession} from '../src/lib/session';
vi.mock('../src/components/vite/warehouseRequestPdf',()=>({buildWarehouseRequestPdf:vi.fn()}));
const session={role:'GroupAdministrator',id:'supervisor',displayName:'Supervisor',permissions:{canRequestWarehouse:true}} as PortalSession;
it('supervisor puede elegir una agencia o varias, pero no un grupo completo',()=>{
 const html=renderToStaticMarkup(<WarehouseRequestComposer session={session} department="SUPERVISION" agencies={[]} onClose={()=>{}} onSaved={()=>{}}/>);
 for(const option of ['Para una agencia','Para varias agencias'])expect(html).toContain(option);
 expect(html).not.toContain('Para un grupo');
 expect(html).not.toContain('Para mi departamento');
 expect(html).toContain('Agencia de la solicitud');
 expect(html).not.toContain('Agencia para artículo 1');
});
it('solicitud vinculada a un ticket conserva su agencia fija',()=>{
 const html=renderToStaticMarkup(<WarehouseRequestComposer session={session} department="SUPERVISION" agencies={[{id:'qa',codigo:'001',terminal:'Agencia QA',grupo:'Grupo QA'}]} linkedTicket={{id:'ticket',ticketNumber:1,codigo:'001',terminal:'Agencia QA',grupo:'Grupo QA',subject:'Equipo'}} onClose={()=>{}} onSaved={()=>{}}/>);
 expect(html).not.toContain('aria-label="Destino de la solicitud"');
 expect(html).not.toContain('Para varias agencias');
});
