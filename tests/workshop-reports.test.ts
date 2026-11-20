import {describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {buildWorkshopReportRows} from '../src/components/vite/workshopReportData';
import type {WorkshopCase} from '../src/components/vite/workshopWorkflow';

const item:WorkshopCase={id:'equipment-1',documentNumber:'TAL-001',qrToken:'qr',equipmentType:'Impresora',serialNumber:'SER-001',quantity:1,department:'TECHNOLOGY',senderName:'María',returnName:'',sentAt:'2026-11-01T14:00:00Z',status:'DISCHARGED',failureCause:'No enciende',canOperate:true,events:[
 {id:'receive',action:'RECEIVE',status:'RECEIVED',actorName:'Juan',actorLogin:'juan',occurredAt:'2026-11-01T15:00:00Z'},
 {id:'repair',action:'REPAIR',status:'REPAIR',actorName:'Juan',actorLogin:'juan',occurredAt:'2026-11-02T15:00:00Z'},
 {id:'discharge',action:'DISCHARGE',status:'DISCHARGED',actorName:'Juan',actorLogin:'juan',occurredAt:'2026-11-03T15:00:00Z'},
 {id:'outside',action:'DELIVER',status:'DELIVERED',actorName:'Juan',actorLogin:'juan',occurredAt:'2026-11-05T15:00:00Z'},
]};

describe('reportes de Taller por rango',()=>{
 it('presenta los tres botones y los calendarios Desde y Hasta',()=>{
  const source=readFileSync('src/components/vite/WorkshopDailyReport.tsx','utf8');
  for(const label of ['Entradas y salidas','Equipos sin reparación','Movimientos','Fecha desde','Fecha hasta','Hora desde','Hora hasta'])expect(source).toContain(label);
 });
 it('limita inclusivamente el reporte por hora de Santo Domingo',()=>{
  expect(buildWorkshopReportRows([item],'2026-11-01','2026-11-01','MOVEMENTS','10:30','11:30').map(row=>row.action)).toEqual(['Recepción en Taller']);
  expect(buildWorkshopReportRows([item],'2026-11-01','2026-11-01','MOVEMENTS','09:59','10:01').map(row=>row.action)).toEqual(['Registro / envío a Taller']);
 });
 it('filtra inclusivamente desde y hasta para el reporte de movimientos',()=>{
  expect(buildWorkshopReportRows([item],'2026-11-02','2026-11-03','MOVEMENTS').map(row=>row.action)).toEqual(['Reparación','Salida sin reparación']);
 });
 it('separa entradas y salidas y genera el listado de equipos sin reparación',()=>{
  expect(buildWorkshopReportRows([item],'2026-11-01','2026-11-03','ENTRIES_EXITS').map(row=>row.action)).toEqual(['Registro / envío a Taller','Recepción en Taller','Salida sin reparación']);
  expect(buildWorkshopReportRows([item],'2026-11-01','2026-11-03','UNREPAIRED')).toHaveLength(1);
 });
});
