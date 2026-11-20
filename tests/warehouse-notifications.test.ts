import {expect,it,vi} from 'vitest';
import {readFileSync} from 'node:fs';
import {canReceiveWarehouseNotifications,isWarehouseNotificationUser,playWarehouseAlert} from '../src/lib/warehouse-alert';
it('permite al administrador ver solicitudes pero reserva la alerta fuerte a Almacén',()=>{
 expect(canReceiveWarehouseNotifications(null)).toBe(false);
 expect(canReceiveWarehouseNotifications({role:'Administrator'})).toBe(true);
 expect(canReceiveWarehouseNotifications({role:'Warehouse'})).toBe(true);
 expect(canReceiveWarehouseNotifications({role:'Technology',supportTeam:'WAREHOUSE'})).toBe(true);
 for(const role of ['Technology','Technician','Support','GeneralServices'])expect(canReceiveWarehouseNotifications({role})).toBe(false);
 expect(isWarehouseNotificationUser(null)).toBe(false);
 for(const role of ['Administrator','Technology','Technician','Support','GeneralServices'])expect(isWarehouseNotificationUser({role})).toBe(false);
 expect(isWarehouseNotificationUser({role:'Warehouse'})).toBe(true);
 expect(isWarehouseNotificationUser({role:'Technology',supportTeam:'WAREHOUSE'})).toBe(true);
 expect(isWarehouseNotificationUser({role:'Administrator',supportTeam:'WAREHOUSE'})).toBe(true);
 const app=readFileSync('src/App.tsx','utf8');
 expect(app).toContain('receivesWarehouseNotifications=canReceiveWarehouseNotifications(session)');
 expect(app).toContain('usesWarehouseAlert=isWarehouseNotificationUser(session)');
 expect(app).toContain('if(usesWarehouseAlert&&newItems.some');
 expect(app).toContain('notificationTimer=usesWarehouseAlert?');
 expect(app).toContain('else playNotificationChime()');
 expect(app).toContain('{usesWarehouseAlert&&<div');
});
it('respeta el bloqueo de audio del navegador',()=>{expect(playWarehouseAlert(null)).toBe(false);expect(playWarehouseAlert({state:'suspended'} as AudioContext)).toBe(false);});
it('reproduce tres tonos fuertes y finitos sin bucle continuo',()=>{
 const param=()=>({setValueAtTime:vi.fn(),exponentialRampToValueAtTime:vi.fn()});
 const oscillators:Array<{type:string;frequency:ReturnType<typeof param>;connect:ReturnType<typeof vi.fn>;start:ReturnType<typeof vi.fn>;stop:ReturnType<typeof vi.fn>;disconnect:ReturnType<typeof vi.fn>}> = [];
 const gains:ReturnType<typeof param>[]=[];
 const audio={state:'running',currentTime:0,destination:{},createOscillator:()=>{const oscillator={type:'',frequency:param(),connect:vi.fn(),start:vi.fn(),stop:vi.fn(),disconnect:vi.fn()};oscillators.push(oscillator);return oscillator;},createGain:()=>{const gain=param();gains.push(gain);return {gain,connect:vi.fn(),disconnect:vi.fn()};}};
 expect(playWarehouseAlert(audio as unknown as AudioContext)).toBe(true);expect(oscillators).toHaveLength(3);expect(oscillators[2].stop).toHaveBeenCalledWith(1.83);expect(gains[0].exponentialRampToValueAtTime).toHaveBeenCalledWith(.5,.03);
});
it('conecta la bandeja con el formulario y persiste los leídos',()=>{
 const app=readFileSync('src/App.tsx','utf8');expect(app).toContain('/api/warehouse/fulfillment/notifications');expect(app).toContain('setWarehouseNotificationId(Number(item.id))');expect(app).toContain('Activar y probar alerta de Almacén');expect(app).toContain('WAREHOUSE_REQUEST');
 const db=readFileSync('api/DatabaseSqlServerScoped.cs','utf8');expect(db).toContain('key.StartsWith("WAREHOUSE_REQUEST:"');
});
it('oculta los accesos antiguos en Servicios Generales sin retirar el módulo nuevo',()=>{
 const center=readFileSync('src/components/vite/TicketCenter.tsx','utf8');
 const section=center.slice(center.indexOf('workspaceDepartment !== "GENERAL_SERVICES"'),center.indexOf('className="mw-section-cards"'));
 expect(section).toContain('session.supportDepartment !== "GENERAL_SERVICES"');expect(center).toContain('<AdministrativeWorkspace');
});
