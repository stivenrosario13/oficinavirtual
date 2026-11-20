import {afterEach,describe,expect,it,vi} from 'vitest';
import {loadWarehouseGroups,normalizeWarehouseGroups} from '../src/lib/warehouse-request-groups';
afterEach(()=>vi.unstubAllGlobals());
describe('destino de solicitud por grupo',()=>{
 it('ordena, elimina duplicados y omite grupos vacíos o nulos',()=>{
  expect(normalizeWarehouseGroups([' Z ','A','A',null,'',undefined,'  '])).toEqual(['A','Z']);
 });
 it('consulta los grupos sin cargar agencias ni el catálogo de soporte',async()=>{
  const fetch=vi.fn().mockResolvedValue(Response.json({groups:['Grupo QA']}));vi.stubGlobal('fetch',fetch);
  await expect(loadWarehouseGroups()).resolves.toEqual(['Grupo QA']);
  expect(fetch.mock.calls[0][0]).toBe('/api/institutional-documents/warehouse-groups');
  expect(fetch).toHaveBeenCalledTimes(1);
 });
 it('no oculta un error de permisos',async()=>{
  vi.stubGlobal('fetch',vi.fn().mockResolvedValue(Response.json({error:'Acceso denegado'},{status:403})));
  await expect(loadWarehouseGroups()).rejects.toThrow('Acceso denegado');
 });
 it('detecta una respuesta incorrecta y permite reintentar',async()=>{
  const fetch=vi.fn().mockResolvedValueOnce(new Response('<html>')).mockResolvedValueOnce(Response.json({groups:['QA']}));vi.stubGlobal('fetch',fetch);
  await expect(loadWarehouseGroups()).rejects.toThrow('listado de grupos');
  await expect(loadWarehouseGroups()).resolves.toEqual(['QA']);
 });
});
