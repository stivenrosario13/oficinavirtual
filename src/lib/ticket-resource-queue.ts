export type TicketResourceState = {label:string; pending:boolean; hasData:boolean; error:string};
type Task = {key:string; label:string; maxAgeMs:number; read:(signal:AbortSignal)=>Promise<void>};
type Entry = {updatedAt:number; retryAfter:number; failures:number; pending:boolean; state:TicketResourceState};

/** Per-mounted-workspace state. No cross-account data cache and no write requests. */
export function createTicketResourceQueue(notify:(key:string,state:TicketResourceState)=>void, unauthorized:()=>void, concurrency=2) {
  const entries=new Map<string,Entry>();
  const controllers=new Set<AbortController>();
  const waiting:Array<()=>Promise<void>>=[];
  let active=0,generation=0;
  const pump=()=>{
    while(active<concurrency&&waiting.length){
      active++;
      void waiting.shift()!().finally(()=>{active--;pump();});
    }
  };
  return {
    refresh(task:Task,force=false) {
      let entry=entries.get(task.key);
      if(!entry){entry={updatedAt:0,retryAfter:0,failures:0,pending:false,state:{label:task.label,pending:false,hasData:false,error:''}};entries.set(task.key,entry);}
      const row=entry,now=Date.now();
      if(row.pending||!force&&(now<row.retryAfter||row.state.hasData&&now-row.updatedAt<task.maxAgeMs))return;
      row.pending=true;row.state={...row.state,pending:true};notify(task.key,row.state);
      const current=generation;
      waiting.push(async()=>{
        if(current!==generation)return;
        const controller=new AbortController();controllers.add(controller);
        try {
          await task.read(controller.signal);
          if(current!==generation)return;
          row.updatedAt=Date.now();row.retryAfter=0;row.failures=0;
          row.state={label:task.label,pending:false,hasData:true,error:''};
        } catch(reason) {
          if(current!==generation||controller.signal.aborted)return;
          const error=reason as Error&{status?:number};
          if(error.status===401)unauthorized();
          row.failures++;
          row.retryAfter=Date.now()+(error.status===401||error.status===403?300000:Math.min(120000,15000*2**(row.failures-1)));
          const detail=error.status===403?'Tu cuenta no tiene permiso para esta consulta.':error.message||'No se pudo completar la consulta.';
          row.state={...row.state,pending:false,error:`${task.label}: ${detail} ${row.state.hasData?'Se conservan los datos anteriores.':'Los datos todavía no están disponibles.'}`};
        } finally {
          controllers.delete(controller);
          if(current===generation){row.pending=false;row.state={...row.state,pending:false};notify(task.key,row.state);}
        }
      });
      pump();
    },
    cancel() {generation++;waiting.length=0;controllers.forEach(controller=>controller.abort());entries.clear();},
  };
}
