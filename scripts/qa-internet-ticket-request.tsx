import React from 'react';
import {createRoot} from 'react-dom/client';
import AdministrativeInternetRequestComposer from '../src/components/vite/AdministrativeInternetRequestComposer';
import type {PortalSession} from '../src/lib/session';

const session={role:'Administrator',supportTeam:null,displayName:'Administrador principal',email:'admin@local.com',id:'admin',permissions:{}} as PortalSession;
const linkedTicket={id:'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',ticketNumber:2799,codigo:'632084',terminal:'632084 | Villa Mella Antiguo 400',grupo:'GRUPO SANTOS',subject:'Falla en módem'};
createRoot(document.getElementById('root')!).render(<AdministrativeInternetRequestComposer session={session} linkedTicket={linkedTicket} onClose={()=>{}} onBack={()=>{}} onSaved={async()=>{}}/>);
