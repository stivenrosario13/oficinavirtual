import {createRoot} from 'react-dom/client';
import {useState} from 'react';
import AdministrativeRequests from '../src/components/vite/AdministrativeRequests';
import AdministrativeInternetRequestComposer from '../src/components/vite/AdministrativeInternetRequestComposer';
import WarehouseRequestEditor from '../src/components/vite/WarehouseRequestEditor';
import WarehousePdfRequests from '../src/components/vite/WarehousePdfRequests';
import WeekendLoanDetail from '../src/components/vite/WeekendLoanDetail';
import type {DocumentDetail} from '../src/components/vite/DocumentSystemModule';
import type {PortalSession} from '../src/lib/session';
import '../src/vite.css';
import '../src/components/vite/administrativeWorkspace.css';
import '../src/components/vite/administrativeFormsPolish.css';
import '../src/components/vite/warehouseFulfillment.css';
const mode=new URLSearchParams(location.search).get('mode');
const session={id:'vittini',email:'rosmarlin.vittini@grupotejeda.local',role:'Technology',supportTeam:'TECHNICAL_FAILURE',displayName:'Vittini',permissions:{canRequestWarehouse:true,canAuthorizeWarehouseRequests:true}} as PortalSession;
const request={id:12,title:'Solicitud QA',department:'Tecnología',ownerUserName:'vittini',ownerDisplayName:'Vittini',ownerLogin:session.email,isOwner:true,recipientDepartment:'Almacén',createdUtc:'2026-10-28T12:00:00Z',originalFileName:'SOL-ALM-QA.pdf',warehouseRevision:0,warehouseRequest:{number:'SOL-ALM-QA',agencyId:'00000000-0000-0000-0000-000000000000',agencyCode:'USO INTERNO',agencyName:'Tecnología',agencyGroup:'Grupo QA',priority:'MEDIUM',weekendLoan:{from:'2026-10-30',to:'2026-11-01',returnBy:'2026-11-02'},lines:[{kind:'EQUIPMENT',productId:'OLD',name:'Equipo sin existencia',quantity:5}]},warehouseAssigned:[{lineIndex:0,productId:'P1',name:'Router QA',serial:'CONTROL POR CANTIDAD',quantity:10}],warehouseDelivery:{number:'DEL-QA'}} as DocumentDetail;
function App(){const [item,setItem]=useState(request);const saved=async(d:DocumentDetail)=>{if(mode!=='edit')setItem(d);document.title='Guardado'};
 return <main className="administrative-workspace administrative-portal" style={{padding:20}}>{mode==='modem'?<AdministrativeInternetRequestComposer session={session} linkedTicket={{id:'22222222-2222-2222-2222-222222222222',ticketNumber:123,codigo:'001',terminal:'Agencia QA',grupo:'Grupo QA',subject:'El módem no conecta'}} onClose={()=>{}} onBack={()=>{}} onSaved={async()=>{document.title='Guardado'}}/>:mode==='edit'?<WarehouseRequestEditor item={item} onCancel={()=>{}} onSaved={saved}/>:mode==='loans'?<WarehousePdfRequests session={session} department="TECHNOLOGY"/>:mode==='loan'?<WeekendLoanDetail item={item} warehouse={false} onChanged={saved}/>:mode==='return'?<WeekendLoanDetail item={{...item,isOwner:false,weekendLoanSettlement:{uses:[{assignedIndex:0,quantity:6,agencyId:'11111111-1111-1111-1111-111111111111',agencyLabel:'001 · Agencia QA'}],note:'Devolución de cuatro equipos',reportedBy:'Vittini',reportedAt:'2026-11-02T12:00:00Z'}}} warehouse onChanged={saved}/>:<AdministrativeRequests session={{...session,id:'sandra',role:'Administrative',email:'sandra.ramirez@grupotejeda.local',displayName:'Sandra Lucia Ramirez Ortiz'}}/>}</main>
}
createRoot(document.getElementById('root')!).render(<App/>);
