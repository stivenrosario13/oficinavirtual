import type {PortalSession} from './session';
export function canDeleteWarehouseInventory(session:Pick<PortalSession,'authenticated'|'role'|'supportTeam'|'email'>&{permissions?:PortalSession['permissions']}){
 return session.authenticated&&(session.role==='Warehouse'||session.supportTeam==='WAREHOUSE')&&
  (session.permissions?.canDeleteWarehouseInventory??['diorly.abreu@grupotejeda.local','bryan.arias@grupotejeda.local'].includes(session.email.trim().toLowerCase()));
}
