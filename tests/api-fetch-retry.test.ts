import {it,expect,vi} from 'vitest';
import {createApiFetch} from '../src/lib/api-fetch';
const json=(status=200)=>new Response('{}',{status,headers:{'content-type':'application/json'}});
it('recovers one transient read without resending writes or authorization failures',async()=>{
 for(const failure of [408,502,503,504,'network']){
  const native=vi.fn().mockImplementationOnce(async()=>{if(failure==='network')throw new TypeError('Failed to fetch');return json(failure as number);}).mockResolvedValue(json());
  expect((await createApiFetch(native,{retryDelayMs:1})('/api/test')).status).toBe(200);expect(native).toHaveBeenCalledTimes(2);
 }
 for(const status of [400,401,403,429,500]){
  const native=vi.fn(async()=>json(status));expect((await createApiFetch(native,{retryDelayMs:1})('/api/test')).status).toBe(status);expect(native).toHaveBeenCalledTimes(1);
 }
 for(const method of ['POST','PUT','DELETE']){const native=vi.fn(async()=>json(503));await createApiFetch(native,{retryDelayMs:1})('/api/test',{method});expect(native).toHaveBeenCalledTimes(1);}
});
it('respects server backoff and bounds repeated errors',async()=>{
 const native=vi.fn(async()=>json(503));expect((await createApiFetch(native,{retryDelayMs:1})('/api/test')).status).toBe(503);expect(native).toHaveBeenCalledTimes(2);
 const backoff=vi.fn(async()=>new Response('{}',{status:503,headers:{'content-type':'application/json','retry-after':'60'}}));await createApiFetch(backoff)('/api/test');expect(backoff).toHaveBeenCalledTimes(1);
});
it('retains the original deadline and cancellation while waiting to retry',async()=>{
 vi.useFakeTimers();try{
  const native=vi.fn(async()=>json(503));const api=createApiFetch(native,{retryDelayMs:100,readTimeoutMs:40});
  const result=expect(api('/api/test')).rejects.toMatchObject({code:'REQUEST_TIMEOUT'});await vi.advanceTimersByTimeAsync(40);await result;expect(native).toHaveBeenCalledTimes(1);expect(vi.getTimerCount()).toBe(0);
  const c=new AbortController();const cancelled=expect(api('/api/test',{signal:c.signal})).rejects.toMatchObject({name:'AbortError'});await vi.advanceTimersByTimeAsync(1);c.abort();await cancelled;expect(vi.getTimerCount()).toBe(0);
 }finally{vi.useRealTimers();}
});
