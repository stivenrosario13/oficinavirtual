import {afterEach,describe,expect,it,vi} from 'vitest';
import {saveWarehouseRequest} from '../src/lib/save-warehouse-request';
const number='SOL-ALM-20261008120000-ABCD';
const saved={id:123,warehouseRequest:{number}};
afterEach(()=>vi.unstubAllGlobals());
describe('confirmación de solicitudes de Almacén',()=>{
 it('muestra el código SQL del POST cuando la recuperación no encuentra la solicitud',async()=>{
  const fetch=vi.fn().mockResolvedValueOnce(Response.json({error:'Consulta rechazada',code:'MSSQL_229'},{status:503})).mockResolvedValueOnce(new Response(null,{status:404}));vi.stubGlobal('fetch',fetch);
  await expect(saveWarehouseRequest(new FormData(),number)).rejects.toThrow('Consulta rechazada (HTTP 503 · MSSQL_229)');
  expect(fetch.mock.calls.filter(call=>call[1]?.method==='POST')).toHaveLength(1);
 });
 it('acepta la confirmación normal sin otra consulta',async()=>{
  const fetch=vi.fn().mockResolvedValue(Response.json(saved));vi.stubGlobal('fetch',fetch);
  await expect(saveWarehouseRequest(new FormData(),number)).resolves.toEqual(saved);expect(fetch).toHaveBeenCalledTimes(1);
 });
 for(const [name,result] of [['HTML',new Response('<!doctype html>')],['sin cuerpo',new Response(null,{status:204})],['sin id',Response.json({ok:true})],['error posterior al commit',Response.json({error:'SQL temporal'},{status:503})]] as const){
  it(`recupera por folio tras respuesta ${name}`,async()=>{
   const fetch=vi.fn().mockResolvedValueOnce(result).mockResolvedValueOnce(Response.json(saved));vi.stubGlobal('fetch',fetch);
   await expect(saveWarehouseRequest(new FormData(),number)).resolves.toEqual(saved);
   expect(fetch.mock.calls.filter(call=>call[1]?.method==='POST')).toHaveLength(1);
   expect(fetch.mock.calls[1][0]).toContain(`/warehouse-submission/${number}`);
  });
 }
 it('recupera una respuesta perdida sin reenviar el POST',async()=>{
  const fetch=vi.fn().mockRejectedValueOnce(new TypeError('network')).mockResolvedValueOnce(Response.json(saved));vi.stubGlobal('fetch',fetch);
  await expect(saveWarehouseRequest(new FormData(),number)).resolves.toEqual(saved);expect(fetch).toHaveBeenCalledTimes(2);
 });
 it('no simula éxito ni acepta un folio distinto',async()=>{
  const fetch=vi.fn().mockResolvedValueOnce(Response.json({id:0})).mockResolvedValueOnce(Response.json({...saved,warehouseRequest:{number:'OTHER'}}));vi.stubGlobal('fetch',fetch);
  await expect(saveWarehouseRequest(new FormData(),number)).rejects.toThrow(number);
 });
 it('conserva errores de validación, permisos y conflicto',async()=>{
  for(const status of [401,403,409,422,429]){
   const fetch=vi.fn().mockResolvedValue(Response.json({message:'Mensaje real'},{status}));vi.stubGlobal('fetch',fetch);
   await expect(saveWarehouseRequest(new FormData(),number)).rejects.toThrow('Mensaje real');expect(fetch).toHaveBeenCalledTimes(1);
  }
 });
});
