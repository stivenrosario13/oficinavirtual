import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {expect,it} from 'vitest';
import TicketCenter from '../src/components/vite/TicketCenter';
import type {PortalSession} from '../src/lib/session';

it.each([
 ['Technology','TECHNOLOGY','TECHNICIANS'],
 ['GeneralServices','GENERAL_SERVICES',null],
])('restores the V311 home for %s and respects chat permission',(role,department,team)=>{
 const session={authenticated:true,id:'qa',email:'qa@example.invalid',displayName:'Técnico QA',role,supportDepartment:department,supportTeam:team,permissions:{canAssignTickets:false,canUseSupportChat:true}} as PortalSession;
 const render=()=>renderToStaticMarkup(<TicketCenter session={session} onSessionExpired={()=>{}}/>);
 const html=render();
 for(const label of ['Mis tickets asignados','Mis casos resueltos','Mis estadísticas','Chat de soporte'])expect(html).toContain(label);
 session.permissions.canUseSupportChat=false;
 expect(render()).not.toContain('class="technician-chat-entry"');
});

it('shows the supervisor assigned inbox, resolved cases and evidence/history entry',()=>{
 const session={authenticated:true,id:'supervisor-qa',email:'supervisor@example.invalid',displayName:'Supervisor QA',role:'GroupAdministrator',supportDepartment:null,supportTeam:null,permissions:{canAssignTickets:false,canUseSupportChat:false,canViewSupportFindings:true}} as PortalSession;
 const html=renderToStaticMarkup(<TicketCenter session={session} onSessionExpired={()=>{}}/>);
 for(const label of ['Casos del supervisor','Mis tickets asignados','Mis casos resueltos','Evidencias e historial','Mis hallazgos asignados','Mis tickets en proceso','Mis casos resueltos por técnicos']) expect(html).toContain(label);
 expect(html).not.toContain('Configurar soporte');
});
