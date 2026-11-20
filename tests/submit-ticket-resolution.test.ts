import {describe,it,expect,vi} from 'vitest';
import {submitTicketResolution} from '../src/lib/submit-ticket-resolution';
describe('Ticket resolution feedback',()=>{
 it('requires server confirmation and sends evidence exactly once',async()=>{
  const form=new FormData();form.append('resolution','Reparación comprobada');
  const request=vi.fn().mockResolvedValue(new Response(JSON.stringify({ok:true}),{status:200}));
  await submitTicketResolution('ticket-id',form,request);
  expect(request).toHaveBeenCalledTimes(1);expect(request.mock.calls[0][1].body).toBe(form);
 });
 it.each([403,409,422,503])('shows the server error for HTTP %s',async status=>{
  const request=vi.fn().mockResolvedValue(new Response(JSON.stringify({error:'Motivo específico del servidor'}),{status}));
  await expect(submitTicketResolution('id',new FormData(),request)).rejects.toThrow('Motivo específico');
 });
 it('does not call an HTML login page a successful save',async()=>{
  const request=vi.fn().mockResolvedValue(new Response('<!doctype html> login',{status:200}));
  await expect(submitTicketResolution('id',new FormData(),request)).rejects.toThrow('No se confirmó');
 });
 it('explains oversized uploads',async()=>{
  const request=vi.fn().mockResolvedValue(new Response('',{status:413}));
  await expect(submitTicketResolution('id',new FormData(),request)).rejects.toThrow('fotografías');
 });
 it('bounds waiting without retrying a potentially committed write',async()=>{
  const request=vi.fn((_url,options)=>new Promise<Response>((_resolve,reject)=>options.signal.addEventListener('abort',()=>reject(new DOMException('Aborted','AbortError')))));
  await expect(submitTicketResolution('id',new FormData(),request as typeof fetch,10)).rejects.toThrow('comprobar si quedó resuelto');
  expect(request).toHaveBeenCalledTimes(1);
 });
});
