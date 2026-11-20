import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {expect,it} from 'vitest';
import {canDeleteWarehouseInventory} from '../src/lib/warehouse-inventory-delete';
import WarehouseInventoryDeleteButton from '../src/components/vite/WarehouseInventoryDeleteButton';
import type {PortalSession} from '../src/lib/session';
const user=(email:string,role:PortalSession['role']='Warehouse')=>({authenticated:true,email,role,supportTeam:role==='Warehouse'?'WAREHOUSE':null} as PortalSession);
it.each(['diorly.abreu','bryan.arias'])('muestra eliminación solo para %s',login=>{
 const session=user(`${login}@grupotejeda.local`);
 expect(canDeleteWarehouseInventory(session)).toBe(true);
 expect(renderToStaticMarkup(<WarehouseInventoryDeleteButton session={session} productId="P001" name="Papel" quantity={20} department="TECHNOLOGY" onDeleted={async()=>{}}/>)).toContain('Eliminar artículo');
});
it.each(['ana.rodriguez@grupotejeda.local','admin@local.com','bryan.arias@otro.local','diorly.abreu.falso@grupotejeda.local'])('no permite %s',email=>{
 const session=user(email);
 expect(canDeleteWarehouseInventory(session)).toBe(false);
 expect(renderToStaticMarkup(<WarehouseInventoryDeleteButton session={session} productId="P001" name="Papel" quantity={20} department="TECHNOLOGY" onDeleted={async()=>{}}/>)).toBe('');
});
it('no concede acceso por el nombre sin rol ni sesión autenticada',()=>{
 expect(canDeleteWarehouseInventory(user('bryan.arias@grupotejeda.local','Administrator'))).toBe(false);
 expect(canDeleteWarehouseInventory({...user('bryan.arias@grupotejeda.local'),authenticated:false})).toBe(false);
});
