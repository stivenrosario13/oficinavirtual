import {describe,it,expect} from 'vitest';
import {readFileSync} from 'node:fs';

describe('recepción de Taller en Almacén',()=>{
 it('no exige el permiso de operar Taller para confirmar una recepción',()=>{
  const permissions=readFileSync('api/OperationalPermissions.cs','utf8');
  expect(permissions.indexOf('path.EndsWith("/warehouse-receipt")')).toBeLessThan(permissions.indexOf('if(path.StartsWith("/api/workshop"))'));
  expect(permissions).toContain('path.EndsWith("/warehouse-receipt"))return null;');
 });
 it('reconoce el rol Almacén incluso sin support_team y conserva la restricción de pendiente',()=>{
  const workflow=readFileSync('api/WorkshopWorkflow.cs','utf8');
  const receipt=readFileSync('api/WorkshopWarehouseReceipt.cs','utf8');
  expect(workflow).toContain('Role=="Warehouse"||Team=="WAREHOUSE"');
  expect(workflow).toContain('(@admin=1 OR @warehouse=1)');
  expect(receipt).toContain("@status<>'DELIVERED'");
  expect(receipt).toContain("action='WAREHOUSE_RECEIVE'");
 });
});
