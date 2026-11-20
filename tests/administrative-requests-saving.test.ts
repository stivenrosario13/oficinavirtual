import {afterEach,describe,expect,it,vi} from 'vitest';
import {administrativeDepartmentName,belongsToAdministrativeRequests,saveAdministrativeRequest} from '../src/lib/administrative-requests';

afterEach(()=>vi.unstubAllGlobals());
describe('solicitudes administrativas',()=>{
  const saved={id:42,recipientDepartment:'Administración',department:'Administración',isOwner:true};
  it('muestra una solicitud guardada al volver a cargar el departamento administrativo',()=>{
    expect(administrativeDepartmentName('ADMINISTRATIVE')).toBe('Administración');
    expect([saved].filter(row=>belongsToAdministrativeRequests(row,'ADMINISTRATIVE'))).toEqual([saved]);
  });
  it.each(['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','SUPERVISION'])('conserva el ámbito propio de %s',department=>{
    const row={...saved,department:administrativeDepartmentName(department)};
    expect(belongsToAdministrativeRequests(row,department)).toBe(true);
    expect(belongsToAdministrativeRequests({...row,isOwner:false},department)).toBe(false);
    expect(belongsToAdministrativeRequests(saved,department)).toBe(false);
  });
  it('conserva la bandeja central y excluye otros destinos',()=>{
    expect(belongsToAdministrativeRequests({...saved,isOwner:false})).toBe(true);
    expect(belongsToAdministrativeRequests({...saved,recipientDepartment:'Almacén'})).toBe(false);
    expect(belongsToAdministrativeRequests({...saved,isOwner:false},'ADMINISTRATIVE')).toBe(false);
  });
  it('acepta únicamente una confirmación con identificador persistido',async()=>{
    const fetch=vi.fn().mockResolvedValue(Response.json(saved));vi.stubGlobal('fetch',fetch);
    const body=new FormData();
    await expect(saveAdministrativeRequest('/api/institutional-documents',body)).resolves.toBeUndefined();
    expect(fetch).toHaveBeenCalledWith('/api/institutional-documents',{method:'POST',credentials:'same-origin',body,signal:expect.any(AbortSignal)});
  });
  it.each(['<!doctype html>',JSON.stringify({ok:true}),JSON.stringify({id:0})])('no anuncia éxito ante respuesta inválida %s',async body=>{
    vi.stubGlobal('fetch',vi.fn().mockResolvedValue(new Response(body)));
    await expect(saveAdministrativeRequest('/api/institutional-documents',new FormData())).rejects.toThrow('no confirmó');
  });
  it('conserva el error del servidor',async()=>{
    vi.stubGlobal('fetch',vi.fn().mockResolvedValue(Response.json({message:'Sin permiso'},{status:403})));
    await expect(saveAdministrativeRequest('/api/institutional-documents',new FormData())).rejects.toThrow('Sin permiso');
  });
});
