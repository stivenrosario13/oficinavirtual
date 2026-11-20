import React from 'react';
import {createRoot} from 'react-dom/client';
import WarehouseRequestComposer from '../src/components/vite/WarehouseRequestComposer';
import type {PortalSession} from '../src/lib/session';
const department=new URLSearchParams(location.search).get('department')||'GENERAL_SERVICES';
const linked=new URLSearchParams(location.search).has('ticket');
const session={role:department==='WAREHOUSE'?'Warehouse':department==='GENERAL_SERVICES'?'GeneralServices':'Technology',supportTeam:department==='TECHNOLOGY'?'TECHNICAL_FAILURE':department,displayName:'QA',id:'qa'} as PortalSession;
const agencies=[{id:'11111111-1111-1111-1111-111111111111',codigo:'123',terminal:'Agencia QA',grupo:'Grupo Norte'}];
createRoot(document.getElementById('root')!).render(<WarehouseRequestComposer session={session} department={department} agencies={agencies} linkedTicket={linked?{id:'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',ticketNumber:42,codigo:'123',terminal:'Agencia QA',grupo:'Grupo Norte',subject:'Equipo requerido'}:null} onClose={()=>{}} onSaved={()=>{document.title='Solicitud enviada';}}/>);
