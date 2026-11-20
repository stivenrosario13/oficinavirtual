import {afterEach,expect,it,vi} from 'vitest';
import {createTicketResourceQueue,type TicketResourceState} from '../src/lib/ticket-resource-queue';
afterEach(()=>vi.useRealTimers());
const flush=async()=>{for(let i=0;i<12;i++)await Promise.resolve();};
it('runs independent resources at most two at a time, deduplicates and caches successful directories',async()=>{
 const states:Record<string,TicketResourceState>={},done:Array<()=>void>=[];
 const queue=createTicketResourceQueue((key,state)=>states[key]=state,()=>{});
 const read=vi.fn(()=>new Promise<void>(resolve=>done.push(resolve)));
 const jobs=['a','b','c'].map(key=>({key,label:key,maxAgeMs:300000,read}));
 jobs.forEach(job=>queue.refresh(job));queue.refresh(jobs[0],true);
 expect(read).toHaveBeenCalledTimes(2);done[1]();await flush();
 expect(states.b.hasData).toBe(true);expect(states.a.pending).toBe(true);expect(read).toHaveBeenCalledTimes(3);
 done[0]();done[2]();await flush();jobs.forEach(job=>queue.refresh(job));expect(read).toHaveBeenCalledTimes(3);
 queue.cancel();
});
it('retains previous data and named errors until that resource succeeds; failed reads back off',async()=>{
 vi.useFakeTimers();let fail=false;const states:Record<string,TicketResourceState>={};
 const queue=createTicketResourceQueue((key,state)=>states[key]=state,()=>{});
 const read=vi.fn(async()=>{if(fail)throw new Error('Sin conexión');});const job={key:'catalog',label:'Catálogo',maxAgeMs:0,read};
 queue.refresh(job);await flush();fail=true;queue.refresh(job);await flush();
 expect(states.catalog).toMatchObject({hasData:true,pending:false});expect(states.catalog.error).toContain('Catálogo');
 queue.refresh({key:'other',label:'Otro',maxAgeMs:0,read:async()=>{}});await flush();expect(states.catalog.error).toContain('Sin conexión');
 queue.refresh(job);expect(read).toHaveBeenCalledTimes(2);
 await vi.advanceTimersByTimeAsync(15000);queue.refresh(job);await flush();expect(read).toHaveBeenCalledTimes(3);
 fail=false;queue.refresh(job,true);await flush();expect(states.catalog.error).toBe('');queue.cancel();
});
it('cancels departed workspace reads and never publishes their results into a new account',async()=>{
 const notify=vi.fn();let finish!:()=>void;let signal!:AbortSignal;
 const queue=createTicketResourceQueue(notify,()=>{});
 queue.refresh({key:'catalog',label:'Catálogo',maxAgeMs:300000,read:async s=>{signal=s;await new Promise<void>(r=>finish=r);}});
 queue.cancel();expect(signal.aborted).toBe(true);finish();await flush();expect(notify).toHaveBeenCalledTimes(1);
 const nextRead=vi.fn(async()=>{});const next=createTicketResourceQueue(notify,()=>{});next.refresh({key:'catalog',label:'Catálogo',maxAgeMs:300000,read:nextRead});await flush();expect(nextRead).toHaveBeenCalledTimes(1);next.cancel();
});
