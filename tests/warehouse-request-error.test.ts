import {describe,it,expect} from 'vitest';
import {warehouseRequestError} from '../src/lib/warehouse-request-error';
describe('warehouse request diagnostics',()=>{
 it('preserves SQL diagnostics',async()=>{
  const response=Response.json({error:'Consulta rechazada',code:'MSSQL_2628'},{status:503});
  expect((await warehouseRequestError(response,'Error')).message).toBe('Consulta rechazada (HTTP 503 · MSSQL_2628)');
 });
 it('preserves permission errors',async()=>{
  const response=Response.json({message:'No tienes permiso'},{status:403});
  expect((await warehouseRequestError(response,'Error')).message).toContain('No tienes permiso (HTTP 403)');
 });
 it('does not display HTML error pages',async()=>{
  expect((await warehouseRequestError(new Response('<html>Error</html>',{status:500}),'No se pudo cargar')).message).toBe('No se pudo cargar (HTTP 500)');
 });
});
