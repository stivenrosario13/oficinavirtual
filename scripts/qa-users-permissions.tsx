import WarehouseRequesterWorkspace from '../src/components/vite/WarehouseRequesterWorkspace';
import React from 'react';
import {createRoot} from 'react-dom/client';
import UsersPermissionsWorkspace from '../src/components/vite/UsersPermissionsWorkspace';
import RequestHubCards from '../src/components/vite/RequestHubCards';
import type {PortalSession} from '../src/lib/session';
const session={id:'admin-qa',role:'Administrator',authenticated:true,permissions:{canManageUsers:true}} as PortalSession;
createRoot(document.getElementById('root')!).render(location.search.includes('hub')?<section className="administrative-workspace"><RequestHubCards department="TREASURY" name="Tesorería" canWorkshop={false} onSelect={()=>{}}/></section>:location.search.includes('requester')?<WarehouseRequesterWorkspace session={{...session,role:'GeneralServices',email:'solicitante.qa@grupotejeda.local',displayName:'Solicitante QA',supportDepartment:'GENERAL_SERVICES',permissions:{...session.permissions,canManageUsers:false,'profile:WAREHOUSE_REQUESTER':true}}} onLogout={()=>{}} onSession={()=>{}}/>:<UsersPermissionsWorkspace session={session}/>);
