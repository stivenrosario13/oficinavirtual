import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {afterEach,expect,it,vi} from 'vitest';
import WarehouseLiveQueue from '../src/components/vite/WarehouseLiveQueue';
import {isWarehouseRequest} from '../src/lib/warehouse-live-queue';
import type {DocumentItem} from '../src/components/vite/DocumentSystemModule';

afterEach(()=>vi.useRealTimers());
it('exige destino Almacén, no departamento remitente ni título',()=>{
 expect(isWarehouseRequest({recipientDepartment:'Almacén'})).toBe(true);
 for(const recipientDepartment of ['Administración','Tecnología','',undefined,null])expect(isWarehouseRequest({recipientDepartment})).toBe(false);
});
it('excluye administrativas de las tres columnas y del historial sin eliminar envíos de Administración a Almacén',()=>{
 vi.useFakeTimers();vi.setSystemTime(new Date(2026,8,24,19,20));
 const items=['pending','approved','rejected'].flatMap((status,index)=>[
  {id:index+1,status,recipientDepartment:'Almacén',department:'Administración',title:`ALMACEN-VALIDA-${status}`},
  {id:index+11,status,recipientDepartment:'Administración',department:'Tecnología',title:`ADMIN-NO-MOSTRAR-${status}`},
 ]).map(row=>({...row,createdUtc:new Date(2026,8,24,18,0).toISOString(),decisionUtc:new Date(2026,8,24,18,30).toISOString(),ownerUserName:'qa',originalFileName:'qa.pdf'} as DocumentItem));
 const html=renderToStaticMarkup(<WarehouseLiveQueue items={items} loading={false} onOpen={()=>{}}/>);
 for(const status of ['pending','approved','rejected']){
  expect(html.split(`ALMACEN-VALIDA-${status}`).length-1).toBe(2);
  expect(html).not.toContain(`ADMIN-NO-MOSTRAR-${status}`);
 }
 expect(items).toHaveLength(6);
});
