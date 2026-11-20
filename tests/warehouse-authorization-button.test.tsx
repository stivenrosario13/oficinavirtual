import {expect,it} from 'vitest';
import {renderToStaticMarkup} from 'react-dom/server';
import InstitutionalDocumentDetail from '../src/components/vite/InstitutionalDocumentDetail';
import type {DocumentDetail} from '../src/components/vite/DocumentSystemModule';
const item={id:1,title:'Solicitud de equipo',status:'received',channel:'requirements',contentType:'application/pdf',recipientDepartment:'Almacén',isRecipient:true,receivedUtc:'2026-10-17T12:00:00Z',warehouseRequest:{number:'QA',lines:[{kind:'EQUIPMENT',name:'Monitor',quantity:1}]},warehouseAssigned:[]} as unknown as DocumentDetail;
const render=(allowed:boolean,status='received')=>renderToStaticMarkup(<InstitutionalDocumentDetail item={{...item,status}} onBack={()=>{}} onChanged={async()=>{}} warehouseAccess warehouseAuthorizeAccess={allowed}/>);
it('muestra Autorizar y colocar sello para solicitudes recibidas con permiso restaurado',()=>expect(render(true)).toContain('Autorizar y colocar sello'));
it('conserva la restricción para operadores sin permiso',()=>expect(render(false)).not.toContain('Autorizar y colocar sello'));
it.each(['pending','approved','rejected'])('no autoriza fuera de la etapa recibida: %s',status=>expect(render(true,status)).not.toContain('Autorizar y colocar sello'));
it('autoriza desde el historial aunque la pantalla no pase permisos al visor',()=>{
 const html=renderToStaticMarkup(<InstitutionalDocumentDetail item={{...item,canAuthorizeWarehouseRequest:true}} onBack={()=>{}} onChanged={async()=>{}}/>);
 expect(html).toContain('Autorizar y colocar sello');
});
it('la denegación actual del servidor prevalece sobre los permisos antiguos de la pantalla',()=>{
 const html=renderToStaticMarkup(<InstitutionalDocumentDetail item={{...item,canAuthorizeWarehouseRequest:false}} onBack={()=>{}} onChanged={async()=>{}} warehouseAccess warehouseAuthorizeAccess/>);
 expect(html).not.toContain('Autorizar y colocar sello');
});
