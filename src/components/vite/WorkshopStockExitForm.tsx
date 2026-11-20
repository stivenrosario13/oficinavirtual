import {lazy,Suspense} from 'react';
import type {PortalSession} from '@/lib/session';
const TicketCenter=lazy(()=>import('./TicketCenter'));
export default function WorkshopStockExitForm({session,item,onClose,onSaved}:{session:PortalSession;item:{id:string;equipment:string;serial:string};onClose:()=>void;onSaved:(number:number)=>void}){
 return <Suspense fallback={<p>Cargando Crear ticket interno…</p>}><TicketCenter session={session} onSessionExpired={()=>window.location.reload()} workshopStockExit={{...item,onClose,onSaved}}/></Suspense>;
}
