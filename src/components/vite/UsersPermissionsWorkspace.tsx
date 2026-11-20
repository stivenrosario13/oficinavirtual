import { apiFetch as fetch } from "@/lib/api-fetch";
import {useEffect,useMemo,useRef,useState} from 'react';
import {ArrowLeft,Check,LoaderCircle,Plus,RefreshCw,Save,Search,ShieldCheck,UserRound,X} from 'lucide-react';
import type {PortalSession} from '@/lib/session';
import {permissionsForUserSave} from '@/lib/user-permission-edit';
import {allPermissionCatalog} from '@/lib/permission-catalog';
import {accessProfiles,userAccessProfile,type AccessProfile} from '@/lib/user-access-profiles';
import {assignedUserDepartment,userDepartments,type UserDepartment} from '@/lib/user-departments';
import './usersAccessWorkspace.css';

type User={id:string;email:string;displayName:string;role:string;supportTeam?:string|null;isActive:boolean;region?:string|null;contact?:string|null;groups:string[];permissions:Record<string,boolean>;effectivePermissions?:Record<string,boolean>;password?:string};
type SavedProfile={department:UserDepartment;profile:AccessProfile;permissions:Record<string,boolean>};
export {allPermissionCatalog};
const normalize=(value:string)=>value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().trim();
const departmentName=(code:string)=>userDepartments.find(([key])=>key===code)?.[1]||'Sin departamento';
function profileOf(user:User):AccessProfile{
 if(user.permissions['profile:DEPARTMENT_ADMINISTRATIVE'])return 'ADMINISTRATIVE';
 const marked=accessProfiles.find(([profile])=>user.permissions['profile:'+profile]);if(marked)return marked[0];
 if(user.role==='ADMINISTRATOR')return 'ADMINISTRATOR';if(user.role==='GROUP_ADMIN')return 'SUPERVISOR';
 if(user.role==='ADMINISTRATIVE')return 'ADMINISTRATIVE';if(user.role==='FISCALIZADOR')return 'FISCALIZADOR';if(user.role==='VIEWER')return 'VIEWER';
 if(user.permissions.canManageDepartment)return 'DEPARTMENT_MANAGER';
 if(user.role==='TECHNOLOGY'&&user.supportTeam==='CALL_CENTER')return 'SUPPORT';
 return user.supportTeam==='TECHNICIANS'||user.permissions.canResolveTickets&&!user.permissions.canAssignTickets?'TECHNICIAN':'SUPPORT';
}
function withProfile(user:User,profile:AccessProfile,department:UserDepartment,savedProfiles:SavedProfile[]=[]):User{
 const preset=userAccessProfile(profile,department);
 const saved=profile==='SUPPORT'&&department==='CALL_CENTER'?undefined:savedProfiles.find(item=>item.department===department&&item.profile===profile);
 return {...user,...preset,permissions:{...preset.permissions,...saved?.permissions,['profile:'+profile]:true},groups:profile==='SUPERVISOR'?user.groups:[]};
}
async function request(path:string,init?:RequestInit){
 const response=await fetch(path,{credentials:'same-origin',...init});const data=await response.json().catch(()=>({}));
 if(!response.ok)throw Error(data.error||data.message||`No se pudo completar la operación (${response.status}).`);return data;
}
export default function UsersPermissionsWorkspace({session,onClose}:{session:PortalSession;onClose?:()=>void}){
 const [users,setUsers]=useState<User[]>([]),[groups,setGroups]=useState<string[]>([]),[loading,setLoading]=useState(true),[error,setError]=useState(''),[notice,setNotice]=useState('');
 const [query,setQuery]=useState(''),[filter,setFilter]=useState(''),[status,setStatus]=useState('active');
 const [savedProfiles,setSavedProfiles]=useState<SavedProfile[]>([]),[profileNotice,setProfileNotice]=useState('');
 const [draft,setDraft]=useState<User|null>(null),[profile,setProfile]=useState<AccessProfile>('SUPPORT'),[department,setDepartment]=useState<UserDepartment>('TECHNOLOGY');
 const [permissionQuery,setPermissionQuery]=useState(''),[groupQuery,setGroupQuery]=useState(''),[tab,setTab]=useState<'identity'|'permissions'|'groups'>('identity'),[busy,setBusy]=useState(false);
 const saving=useRef(false),initial=useRef(''),editor=useRef<HTMLFormElement>(null);
 const originalPermissions=useRef<Record<string,boolean>>({}),initialPermissions=useRef<Record<string,boolean>>({}),replacedProfile=useRef(false);
 const [profileFilter,setProfileFilter]=useState(''),[page,setPage]=useState(1);
 useEffect(()=>setPage(1),[query,filter,status,profileFilter]);
 const load=async()=>{setLoading(true);setError('');try{const [u,g,p]=await Promise.all([request('/api/admin/users'),request('/api/admin/groups-catalog'),request('/api/admin/access-profiles')]);setUsers(u.users);setGroups(g.groups);setSavedProfiles(p.profiles);}catch(e){setError((e as Error).message);}finally{setLoading(false);}};
 useEffect(()=>{if(session.permissions.canManageUsers)void load();else setLoading(false);},[session.id]);
 const editing=!!draft;
 useEffect(()=>{
  if(!editing)return;
  const focused=document.activeElement as HTMLElement|null,overflow=document.body.style.overflow;
  document.body.style.overflow='hidden';editor.current?.querySelector<HTMLButtonElement>('button')?.focus();
  return()=>{document.body.style.overflow=overflow;focused?.focus();};
 },[editing]);
 const visible=useMemo(()=>users.filter(user=>(!filter||assignedUserDepartment(user)===filter)&&(status==='all'||user.isActive===(status==='active'))&&(!profileFilter||profileOf(user)===profileFilter)&&normalize(`${user.displayName} ${user.email} ${departmentName(assignedUserDepartment(user))} ${user.groups.join(' ')}`).includes(normalize(query))),[users,filter,status,query,profileFilter]);
 const pageCount=Math.max(1,Math.ceil(visible.length/25)),currentPage=Math.min(page,pageCount),pageUsers=visible.slice((currentPage-1)*25,currentPage*25);
 const dirty=!!draft&&JSON.stringify(draft)!==initial.current;
 const close=()=>{if(!busy&&(!dirty||window.confirm('Hay cambios sin guardar. ¿Descartarlos?')))setDraft(null);};
 const open=(user?:User)=>{
  if(dirty&&!window.confirm('Hay cambios sin guardar. ¿Descartarlos?'))return;
  const dept=(userDepartments.some(([code])=>code===assignedUserDepartment(user||{role:''}))?assignedUserDepartment(user!):'TECHNOLOGY') as UserDepartment;
  const selected=user?profileOf(user):'SUPPORT';
  const next=user?{...user,groups:[...user.groups],permissions:{...user.permissions,...user.effectivePermissions},password:''}:withProfile({id:'',email:'',displayName:'',role:'TECHNOLOGY',isActive:true,region:'',contact:'',groups:[],permissions:{},password:'123456'},selected,dept,savedProfiles);
  originalPermissions.current={...user?.permissions};initialPermissions.current={...next.permissions};replacedProfile.current=!user;
  initial.current=JSON.stringify(next);setDraft(next);setProfile(selected);setDepartment(dept);setTab('identity');setPermissionQuery('');setGroupQuery('');setError('');setNotice('');setProfileNotice('');
 };
 const applyProfile=(nextProfile:AccessProfile,nextDepartment:UserDepartment)=>{if(!draft)return;nextDepartment=nextProfile==='SUPERVISOR'?'SUPERVISION':nextDepartment;replacedProfile.current=true;setProfile(nextProfile);setDepartment(nextDepartment);setProfileNotice('');setDraft(withProfile(draft,nextProfile,nextDepartment,savedProfiles));};
 const saveProfile=async()=>{
  if(!draft||saving.current||!window.confirm('¿Guardar estos permisos para los nuevos usuarios de este departamento y perfil? Las cuentas existentes no se modificarán.'))return;
  saving.current=true;setBusy(true);setError('');setProfileNotice('');
  try{
   const preset={department,profile,permissions:Object.fromEntries(allPermissionCatalog.map(item=>[item.key,!!draft.permissions[item.key]]))};
   await request('/api/admin/access-profiles',{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(preset)});
   setSavedProfiles(current=>[...current.filter(item=>item.department!==department||item.profile!==profile),preset]);setProfileNotice('Perfil guardado para las nuevas cuentas.');
  }catch(e){setError((e as Error).message);}finally{saving.current=false;setBusy(false);}
 };
 const editorKeys=(event:React.KeyboardEvent)=>{
  if(event.key==='Escape'){event.preventDefault();close();}
  if(event.key!=='Tab')return;
  const nodes=Array.from(editor.current?.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled),select:not(:disabled)')||[]);
  const first=nodes[0],last=nodes.at(-1);
  if(event.shiftKey&&document.activeElement===first){event.preventDefault();last?.focus();}
  else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first?.focus();}
 };
 const save=async(event:React.FormEvent)=>{
  event.preventDefault();if(!draft||saving.current)return;
  if(!draft.displayName.trim()||!draft.email.trim()){setError('Completa el nombre y el usuario.');setTab('identity');return;}
  saving.current=true;setBusy(true);setError('');setNotice('');
  try{
   const {effectivePermissions:_effectivePermissions,...payload}=draft;
   const saved=await request(draft.id?`/api/admin/users/${draft.id}`:'/api/admin/users',{method:draft.id?'PUT':'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({...payload,permissions:permissionsForUserSave(originalPermissions.current,initialPermissions.current,payload.permissions,replacedProfile.current),email:draft.email.includes('@')?draft.email.trim():draft.email.trim()+'@grupotejeda.local'})});
   setUsers(current=>draft.id?current.map(user=>user.id===draft.id?saved:user):[...current,saved]);setDraft(null);setNotice('Usuario y permisos guardados.');
  }catch(e){setError((e as Error).message);}finally{saving.current=false;setBusy(false);}
 };
 const permissions=allPermissionCatalog.filter(item=>normalize(`${item.title} ${item.section}`).includes(normalize(permissionQuery)));
 const sections=[...new Set(permissions.map(item=>item.section))];
 if(!session.permissions.canManageUsers)return <p role="alert">No tienes permiso para administrar usuarios.</p>;
 return <section className="users-access-workspace">
  <header className="users-access-heading"><div>{onClose&&<button type="button" className="access-icon" title="Volver" aria-label="Volver" onClick={onClose}><ArrowLeft/></button>}<div><h1>Usuarios y permisos</h1><p>{users.filter(user=>user.isActive).length} activos · {userDepartments.length} departamentos · {allPermissionCatalog.length} permisos</p></div></div><button className="access-primary" type="button" onClick={()=>open()}><Plus/>Nuevo usuario</button></header>
  {error&&<p role="alert" className="access-error">{error}</p>}{notice&&<p role="status" className="access-notice"><Check/>{notice}</p>}
  <div className="access-summary" aria-label="Resumen de usuarios"><article><span>Cuentas activas</span><strong>{users.filter(user=>user.isActive).length}</strong><small>Con acceso al sistema</small></article><article><span>Técnicos</span><strong>{users.filter(user=>user.isActive&&profileOf(user)==='TECHNICIAN').length}</strong><small>Tickets asignados</small></article><article><span>Supervisores</span><strong>{users.filter(user=>user.isActive&&profileOf(user)==='SUPERVISOR').length}</strong><small>Grupos y tareas asignadas</small></article><article><span>Cuentas inactivas</span><strong>{users.filter(user=>!user.isActive).length}</strong><small>Historial conservado</small></article></div>
  <div className="access-filters"><label className="access-search"><Search/><input aria-label="Buscar usuarios" type="search" placeholder="Nombre, usuario o grupo" value={query} onChange={e=>setQuery(e.target.value)}/></label><select aria-label="Filtrar departamento" value={filter} onChange={e=>setFilter(e.target.value)}><option value="">Todos los departamentos</option>{userDepartments.map(([code,name])=><option key={code} value={code}>{name}</option>)}</select><select aria-label="Filtrar perfil" value={profileFilter} onChange={e=>setProfileFilter(e.target.value)}><option value="">Todos los perfiles</option>{accessProfiles.map(([key,name])=><option key={key} value={key}>{name}</option>)}</select><select aria-label="Estado de usuarios" value={status} onChange={e=>setStatus(e.target.value)}><option value="active">Activos</option><option value="inactive">Inactivos</option><option value="all">Todos los estados</option></select><button className="access-icon" title="Actualizar usuarios" aria-label="Actualizar usuarios" type="button" disabled={loading} onClick={()=>void load()}><RefreshCw className={loading?'spin':''}/></button></div>
  <div className="access-table-wrap"><table><thead><tr><th>Usuario</th><th>Departamento</th><th>Perfil</th><th>Estado</th><th>Permisos</th></tr></thead><tbody>{pageUsers.map(user=><tr key={user.id}><td><button type="button" className="access-user-link" onClick={()=>open(user)}><UserRound/><span><strong>{user.displayName}</strong><small>{user.email.split('@')[0]}</small></span></button></td><td data-label="Departamento">{departmentName(assignedUserDepartment(user))}</td><td data-label="Perfil"><span className="access-profile-badge">{accessProfiles.find(([key])=>key===profileOf(user))?.[1]}</span>{user.role==='GROUP_ADMIN'&&<small className="access-group-count">{user.groups.length} grupos asignados</small>}</td><td data-label="Estado"><span className={user.isActive?'access-active':'access-inactive'}>{user.isActive?'Activo':'Inactivo'}</span></td><td data-label="Permisos"><button type="button" className="access-icon" aria-label={`Editar permisos de ${user.displayName}`} title="Editar permisos" onClick={()=>{open(user);setTab('permissions');}}><ShieldCheck/></button></td></tr>)}</tbody></table>{loading?<p role="status">Cargando usuarios…</p>:!visible.length&&<p className="access-empty">No hay usuarios para estos filtros.</p>}</div>
  <div className="access-pagination"><span>{visible.length} usuarios encontrados · Página {currentPage} de {pageCount}</span><div><button type="button" disabled={currentPage===1} onClick={()=>setPage(currentPage-1)}>Anterior</button><button type="button" disabled={currentPage===pageCount} onClick={()=>setPage(currentPage+1)}>Siguiente</button></div></div>
  {draft&&<div className="access-editor-backdrop"><form ref={editor} onKeyDown={editorKeys} onSubmit={save} className="access-editor" role="dialog" aria-modal="true" aria-labelledby="access-editor-title"><header><div><h2 id="access-editor-title">{draft.id?draft.displayName:'Nuevo usuario'}</h2><p>{departmentName(department)} · {accessProfiles.find(([key])=>key===profile)?.[1]}{dirty?' · Cambios sin guardar':''}</p></div><button type="button" className="access-icon" title="Cerrar" aria-label="Cerrar usuario" disabled={busy} onClick={close}><X/></button></header>
   <nav aria-label="Configuración de usuario">{(['identity','permissions',...(draft.role==='GROUP_ADMIN'?['groups']:[])] as typeof tab[]).map(value=><button type="button" key={value} aria-current={tab===value?'page':undefined} onClick={()=>setTab(value)}>{value==='identity'?'Cuenta y perfil':value==='permissions'?`Permisos (${allPermissionCatalog.filter(item=>draft.permissions[item.key]).length})`:'Grupos asignados'}</button>)}</nav>
   <fieldset disabled={busy} className="access-editor-body">{error&&<p role="alert" className="access-error">{error}</p>}{profileNotice&&<p role="status" className="access-notice"><Check/>{profileNotice}</p>}
    <div className="access-preset-toolbar"><span>{profile==='SUPPORT'?(department==='CALL_CENTER'?'Perfil principal: Call Center · Soporte':'Perfil de Soporte del departamento'):profile==='WAREHOUSE_REQUESTER'?'Solo envío y consulta de solicitudes propias':savedProfiles.some(item=>item.department===department&&item.profile===profile)?'Perfil personalizado':'Perfil del sistema'}</span><button type="button" disabled={busy||loading||profile==='SUPPORT'} onClick={()=>void saveProfile()}><Save/>Guardar como perfil</button></div>
    {tab==='identity'&&<><div className="access-fields"><label>Nombre completo<input value={draft.displayName} maxLength={160} onChange={e=>setDraft({...draft,displayName:e.target.value})}/></label><label>Usuario<input value={draft.email.split('@')[0]} autoComplete="off" placeholder="nombre.apellido" onChange={e=>setDraft({...draft,email:e.target.value})}/></label><label>{draft.id?'Nueva contraseña (opcional)':'Contraseña temporal'}<input type="password" autoComplete="new-password" minLength={6} value={draft.password||''} onChange={e=>setDraft({...draft,password:e.target.value})}/></label><label>Contacto<input value={draft.contact||''} onChange={e=>setDraft({...draft,contact:e.target.value})}/></label><label>Región<input value={draft.region||''} onChange={e=>setDraft({...draft,region:e.target.value})}/></label><label className="access-toggle"><input type="checkbox" checked={draft.isActive} disabled={draft.id===session.id} onChange={e=>setDraft({...draft,isActive:e.target.checked})}/>Cuenta activa</label></div><div className="access-profile-fields"><label>Departamento<select aria-label="Departamento" value={department} disabled={['ADMINISTRATOR','SUPERVISOR','FISCALIZADOR','VIEWER'].includes(profile)} onChange={e=>applyProfile(profile,e.target.value as UserDepartment)}>{userDepartments.map(([code,name])=><option key={code} value={code}>{name}</option>)}</select></label><label>Perfil predeterminado<select aria-label="Perfil predeterminado" value={profile} onChange={e=>applyProfile(e.target.value as AccessProfile,department)}>{accessProfiles.map(([key,name])=><option key={key} value={key}>{name}</option>)}</select></label><button type="button" onClick={()=>applyProfile(profile,department)}><RefreshCw/>Restablecer perfil</button></div></>}
    {tab==='permissions'&&<><label className="access-search"><Search/><input type="search" aria-label="Buscar permisos" placeholder="Buscar permiso o módulo" value={permissionQuery} onChange={e=>setPermissionQuery(e.target.value)}/></label><p className="access-scope">Alcance: {draft.role==='ADMINISTRATOR'?'todo el sistema':draft.role==='GROUP_ADMIN'?'grupos asignados':departmentName(department)}. Se conservan las restricciones de propiedad y del rol.</p>{sections.map(section=><section className="access-permission-section" key={section}><h3>{section}</h3>{permissions.filter(item=>item.section===section).map(item=><label className="access-permission-row" key={item.key}><span><strong>{item.title}</strong>{item.description&&<small>{item.description}</small>}</span><input type="checkbox" aria-label={item.title} checked={!!draft.permissions[item.key]} disabled={busy||draft.role==='ADMINISTRATOR'&&item.key==='canManageUsers'} onChange={e=>setDraft({...draft,permissions:{...draft.permissions,[item.key]:e.target.checked,usesExplicitPermissions:true}})}/></label>)}</section>)}{!permissions.length&&<p>No hay permisos que coincidan.</p>}</>}
    {tab==='groups'&&<><label className="access-search"><Search/><input type="search" aria-label="Buscar grupos asignables" value={groupQuery} onChange={e=>setGroupQuery(e.target.value)}/></label><p>{draft.groups.length} grupos asignados</p>{groups.filter(group=>normalize(group).includes(normalize(groupQuery))).map(group=><label className="access-permission-row" key={group}><span>{group}</span><input type="checkbox" checked={draft.groups.includes(group)} onChange={e=>setDraft({...draft,groups:e.target.checked?[...draft.groups,group]:draft.groups.filter(value=>value!==group)})}/></label>)}</>}
   </fieldset><footer><button type="button" disabled={busy} onClick={close}>Cancelar</button><button type="submit" className="access-primary" disabled={busy}>{busy?<LoaderCircle className="spin"/>:<Save/>}{busy?'Guardando…':'Guardar usuario'}</button></footer>
  </form></div>}
 </section>;
}
