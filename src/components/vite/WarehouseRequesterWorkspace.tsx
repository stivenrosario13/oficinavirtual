import {useState} from 'react';
import type {PortalSession} from '@/lib/session';
import {userDepartments} from '@/lib/user-departments';
import WarehousePdfRequests from './WarehousePdfRequests';
import ProfileSettings from './ProfileSettings';
import {FileText,UserRound,LogOut,Menu,X} from 'lucide-react';
import './maintenanceCommunications.css';
import './administrativeWorkspace.css';
import './administrativePortal.css';
import './departmentWorkspacePolish.css';
import './warehouseRequesterWorkspace.css';

export default function WarehouseRequesterWorkspace({session,onLogout,onSession}:{session:PortalSession;onLogout:()=>void;onSession:(session:PortalSession)=>void}){
 const [profile,setProfile]=useState(false),[menu,setMenu]=useState(false);
 const operationalTeam=['TECHNICAL_FAILURE','CALL_CENTER','WORKSHOP'].includes(session.supportTeam||'')?session.supportTeam:null;
 const department=session.assignedDepartment||(session.role==='Warehouse'||session.supportTeam==='WAREHOUSE'?'WAREHOUSE':operationalTeam||session.supportDepartment)||'ADMINISTRATIVE';
 const name=userDepartments.find(([code])=>code===department)?.[1]||department;
 return <div className="warehouse-requester-shell">
  <header className="warehouse-requester-topbar"><button type="button" className="requester-menu-toggle" aria-label={menu?'Cerrar menú':'Abrir menú'} onClick={()=>setMenu(!menu)}>{menu?<X/>:<Menu/>}</button><div><small>OFICINA VIRTUAL</small><strong>Solicitudes a Almacén</strong></div><button type="button" onClick={()=>setProfile(true)}><UserRound/><span>{session.displayName}</span></button></header>
  {menu&&<button type="button" className="requester-sidebar-backdrop" aria-label="Cerrar navegación" onClick={()=>setMenu(false)}/>}
  <aside className={`requester-sidebar${menu?' is-open':''}`}><div className="requester-brand"><strong>Grupo Tejeda</strong><small>DEPARTAMENTO DE {name.toUpperCase()}</small></div><nav aria-label="Secciones de solicitudes"><button type="button" className="active" aria-current="page" onClick={()=>{setProfile(false);setMenu(false)}}><FileText/>Solicitudes a Almacén</button><button type="button" onClick={()=>{setProfile(true);setMenu(false)}}><UserRound/>Mi cuenta</button><button type="button" onClick={onLogout}><LogOut/>Cerrar sesión</button></nav></aside>
  <main className="requester-main"><section className="mc-hub administrative-workspace administrative-portal department-document-workspace"><header className="mc-page-heading"><div><small>DEPARTAMENTO DE {name.toUpperCase()}</small><h2>Solicitudes a Almacén</h2><p>Solicita equipos y consulta su recepción, autorización y entrega.</p></div></header><div className="department-section-content"><WarehousePdfRequests embedded session={session} department={department} warehouseAccess={false}/></div></section></main>
  {(profile||session.mustChangePassword)&&<ProfileSettings session={session} forced={session.mustChangePassword} onClose={()=>setProfile(false)} onSession={updated=>{onSession(updated);if(!updated.mustChangePassword)setProfile(false)}}/>}
 </div>;
}
