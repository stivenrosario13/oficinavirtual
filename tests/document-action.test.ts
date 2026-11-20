import {afterEach,expect,it,vi} from 'vitest';
import {submitDocumentAction} from '../src/lib/document-action';
afterEach(()=>vi.unstubAllGlobals());
it('returns the persisted document without a second request',async()=>{
 const document={id:123,status:'approved',decisionBy:'Administrador principal'};
 const fetch=vi.fn().mockResolvedValue(Response.json({ok:true,document}));vi.stubGlobal('fetch',fetch);
 expect(await submitDocumentAction(123,'approve',{note:'autorizar'})).toEqual(document);
 expect(fetch).toHaveBeenCalledTimes(1);
 expect(fetch).toHaveBeenCalledWith('/api/institutional-documents/123/actions/approve',expect.objectContaining({method:'POST',redirect:'error',body:'{"note":"autorizar"}'}));
});
it.each([new Response('<!doctype html>',{headers:{'Content-Type':'text/html'}}),new Response('<!doctype html>',{headers:{'Content-Type':'application/json'}}),Response.json({})])('rejects HTML or unconfirmed success',async(response)=>{
 vi.stubGlobal('fetch',vi.fn().mockResolvedValue(response));
 await expect(submitDocumentAction(123,'approve',{note:'autorizar'})).rejects.toThrow('respuesta no válida');
});
it.each([{status:403,message:'No tienes permiso'},{status:409,message:'La solicitud no está pendiente'},{status:503,error:'Conexión no disponible'}])('preserves server errors',async({status,...body})=>{
 vi.stubGlobal('fetch',vi.fn().mockResolvedValue(Response.json(body,{status})));
 await expect(submitDocumentAction(123,'approve',{note:'autorizar'})).rejects.toThrow(body.message||body.error);
});
