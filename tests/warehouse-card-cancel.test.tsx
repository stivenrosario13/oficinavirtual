import {expect,it} from 'vitest';
import {renderToStaticMarkup} from 'react-dom/server';
import WarehouseRequestCancelButton,{canCancelWarehouseRequest} from '../src/components/vite/WarehouseRequestCancelButton';
const item={id:1,status:'received',isOwner:true,recipientDepartment:'Almacén',warehouseRequest:{number:'QA'} as any};
it('muestra Anular solicitud directamente en la tarjeta del solicitante',()=>{expect(renderToStaticMarkup(<WarehouseRequestCancelButton item={item} canManage={false} onCancelled={async()=>{}}/>)).toContain('Anular solicitud')});
it('permite Almacén y administrador, pero no compañeros con acceso de lectura',()=>{expect(canCancelWarehouseRequest({...item,isOwner:false},true)).toBe(true);expect(canCancelWarehouseRequest({...item,isOwner:false},false)).toBe(false)});
it.each(['pending','received','approved','correction_requested'])('permite solicitud activa %s',status=>expect(canCancelWarehouseRequest({...item,status},false)).toBe(true));
it.each(['cancelled','rejected','draft'])('no ofrece anulación para %s',status=>expect(canCancelWarehouseRequest({...item,status},true)).toBe(false));
it('oculta anulación de solicitudes entregadas o administrativas',()=>{expect(canCancelWarehouseRequest({...item,warehouseDelivery:{}},true)).toBe(false);expect(canCancelWarehouseRequest({...item,recipientDepartment:'Administración'},true)).toBe(false)});
