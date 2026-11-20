import {renderToStaticMarkup} from 'react-dom/server';
import {expect,it,vi} from 'vitest';
import type {PortalSession} from '../src/lib/session';
import WarehousePdfRequests from '../src/components/vite/WarehousePdfRequests';

const {rows}=vi.hoisted(()=>({rows:['Tecnología','Servicios Generales','Recursos Humanos','Almacén'].flatMap((department,i)=>
 ['pending','received','approved','rejected','cancelled'].map((status,j)=>({
  id:i*10+j+1,title:`FORMULARIO ${department} ${status}`,department,recipientDepartment:'Almacén',status,
  ownerUserName:'qa',originalFileName:'qa.pdf',createdUtc:'2026-10-19T12:00:00Z',
 })))}));

// Seed the loaded inbox with mixed departments; render the real component and counters.
vi.mock('react',async importOriginal=>{
 const react=await importOriginal<typeof import('react')>();
 return {...react,useState:(initial:unknown)=>react.useState(Array.isArray(initial)?rows:initial)};
});

const session={role:'Warehouse',permissions:{canAuthorizeWarehouseRequests:true}} as PortalSession;
const departments={TECHNOLOGY:'Tecnología',GENERAL_SERVICES:'Servicios Generales',HUMAN_RESOURCES:'Recursos Humanos',WAREHOUSE:'Almacén'};

it.each(Object.entries(departments))('muestra solo las solicitudes y cantidades de %s', (department,name)=>{
 for(const embedded of [true,false]){
  const html=renderToStaticMarkup(<WarehousePdfRequests session={session} department={department} embedded={embedded}/>);
  for(const row of rows){
   if(row.department===name)expect(html).toContain(row.title);
   else expect(html).not.toContain(row.title);
  }
  expect(html).toContain('<strong>5</strong><small>Solicitudes registradas</small>');
  expect(html).not.toContain('Todos los departamentos');
  expect(html).not.toContain('Ver todos');
 }
});

it('conserva la bandeja general cuando no se selecciona un departamento',()=>{
 const html=renderToStaticMarkup(<WarehousePdfRequests session={session} department=""/>);
 for(const row of rows)expect(html).toContain(row.title);
 expect(html).toContain('<strong>20</strong><small>Solicitudes registradas</small>');
 expect(html).toContain('Todos los departamentos');
});

it('también aísla las solicitudes para un solicitante departamental',()=>{
 const html=renderToStaticMarkup(<WarehousePdfRequests session={session} warehouseAccess={false} department="TECHNOLOGY" embedded/>);
 expect(html).toContain('FORMULARIO Tecnología pending');
 for(const row of rows.filter(row=>row.department!=='Tecnología'))expect(html).not.toContain(row.title);
 expect(html).toContain('<strong>5</strong><small>Solicitudes registradas</small>');
});
