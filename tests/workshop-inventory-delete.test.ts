import {expect,it} from 'vitest';
import {readFileSync} from 'node:fs';

const ui=readFileSync('src/components/vite/WorkshopAvailableInventory.tsx','utf8');
const endpoints=readFileSync('api/WorkshopWorkflow.cs','utf8');
const database=readFileSync('api/WorkshopAvailableInventory.cs','utf8');
const board=readFileSync('src/components/vite/WorkshopBoard.tsx','utf8');
const intake=readFileSync('api/WorkshopIntake.cs','utf8');
const workspace=readFileSync('src/components/vite/WorkshopWorkspace.tsx','utf8');

it('busca los equipos existentes por todos sus datos y permite limpiar el filtro',()=>{
  expect(ui).toContain('Buscar por equipo, serial, origen, usuario u observación');
  expect(ui).toContain('normalizeSearch([i.equipment,i.serial,i.notes,i.createdBy');
  expect(ui).toContain("normalize('NFD')");
  expect(ui).toContain('aria-label="Limpiar búsqueda"');
});

it('muestra el botón habilitado en todos los procesos para el administrador principal y Vittini',()=>{
  expect(board).toContain('(canDeleteWorkshop||item.canDelete)&&<button className="wt-delete" disabled={!!busy}');
  expect(board).toContain('Eliminar este equipo de Taller');
  expect(board).toContain('maintenanceApi(`/api/workshop/${item.id}`,"DELETE"');
  expect(endpoints).toContain('MapDelete("/api/workshop/{id:guid}"');
  expect(endpoints).toContain('isManualEntry');
  expect(endpoints).toContain('canDelete');
  expect(intake).toContain('INSERT dbo.workshop_intake_deletions');
  expect(intake).toContain('m.movement_type=\'TRANSFER_TO_WORKSHOP\'');
  expect(intake).toContain('Solo el administrador principal y Vittini');
  expect(endpoints).toContain('public bool CanDeleteWorkshop=>Role=="Administrator"||IsVittini');
  expect(endpoints).toContain('CAST(CASE WHEN @deleteOperator=1 THEN 1 ELSE 0 END AS bit) canDelete');
});

it('no depende de la API antigua para mostrar el botón al administrador principal o Vittini',()=>{
  expect(workspace).toContain("session.role==='Administrator'");
  expect(workspace).toContain("sessionLogin==='rosmarlin.vittini'");
  expect(workspace).toContain('canDeleteWorkshop={canDeleteWorkshop}');
  expect(board).toContain('(canDeleteWorkshop||item.canDelete)&&<button className="wt-delete"');
  expect(board).toContain('if(busy||(!canDeleteWorkshop&&!item.canDelete))return');
});

it('solo ofrece eliminar registros directos y exige una confirmación con motivo',()=>{
  expect(ui).toContain("i.origin==='DIRECT'?");
  expect(ui).toContain('Eliminar equipo agregado por error');
  expect(ui).toContain('minLength={5}');
  expect(ui).toContain("'DELETE',{reason:deleteReason}");
  expect(ui).toContain('Historial protegido');
});

it('protege la eliminación con permiso, auditoría y bloqueo de equipos con salida',()=>{
  expect(endpoints).toContain('MapDelete("/api/workshop/available-inventory/{id:guid}"');
  expect(endpoints).toContain('if(!actor.CanOperate)return Results.Forbid()');
  expect(endpoints).toContain('body.Reason.Trim().Length<5');
  expect(endpoints).toContain('workshop_available_inventory_deletions');
  expect(database).toContain('workshop_stock_exits WITH(UPDLOCK,HOLDLOCK)');
  expect(database).toContain('INSERT dbo.workshop_available_inventory_deletions');
  expect(database).toContain('DELETE dbo.workshop_available_inventory WHERE id=@id');
  expect(database).toContain('Los equipos reparados conservan su historial');
});
