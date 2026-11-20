import React,{lazy,Suspense} from 'react';import {createRoot} from 'react-dom/client';import WorkshopWorkspace from '../src/components/vite/WorkshopWorkspace';
const TicketCenter=lazy(()=>import('../src/components/vite/TicketCenter'));
const session={id:'qa',role:'Administrator',email:'qa',displayName:'Administrador QA',supportTeam:null,permissions:{canCreateTickets:true,canAssignTickets:true,canViewAllSupportDepartments:true,canViewSupportDashboard:true}} as any;
createRoot(document.getElementById('root')!).render(location.search.includes('tickets')?<Suspense><TicketCenter session={session} onSessionExpired={()=>{throw Error('Expired')}} departmentEntry={{key:1,department:'GENERAL_SERVICES',section:'support'}}/></Suspense>:<WorkshopWorkspace session={session}/>);
