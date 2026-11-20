import React from 'react';
import {createRoot} from 'react-dom/client';
import AdministrativePortal from '../src/components/vite/AdministrativePortal';
import WarehouseRequestComposer from '../src/components/vite/WarehouseRequestComposer';
import type {PortalSession} from '../src/lib/session';
const supervisor=new URLSearchParams(location.search).has('supervisor');
const session={role:supervisor?'GroupAdministrator':'Administrative',displayName:'QA',email:'emma.mendoza@grupotejeda.local',id:'qa',permissions:{canCreateTickets:true}} as PortalSession;
createRoot(document.getElementById('root')!).render(new URLSearchParams(location.search).has('composer')?<WarehouseRequestComposer session={session} department="SUPERVISION" agencies={[{id:'qa',codigo:'100',terminal:'Agencia de prueba',grupo:'Grupo QA'}]} onClose={()=>{}} onSaved={()=>{}}/>:<AdministrativePortal session={session}/>);
