import {expect,it} from 'vitest';
import {agentReportRange,agentReportDayCount,buildAgentReport,agentDetailCells,type AgentReportCase} from '../src/lib/call-center-report';
const ticket=(patch:Partial<AgentReportCase>={})=>({id:'1',ticketNumber:1,codigo:'01',terminal:'Agencia',grupo:'Grupo',assignedDepartment:'TECHNOLOGY',assignedTeam:'CALL_CENTER',status:'IN_PROGRESS',priority:'HIGH',createdAt:'2026-10-15T04:00:00Z',updatedAt:'2026-10-15T12:00:00Z',assignedTechnicianName:'Ana',category:'PRINTER_FAILURE',...patch});
it.each([[1,'2026-10-15'],[7,'2026-10-09'],[15,'2026-10-01'],[30,'2026-09-16']])('incluye exactamente %s días terminando en la fecha seleccionada',(days,from)=>expect(agentReportRange('2026-10-15',days as number)).toEqual({from,to:'2026-10-15'}));
it('maneja cambio de año y año bisiesto',()=>{expect(agentReportRange('2026-01-03',7).from).toBe('2025-12-28');expect(agentReportRange('2024-03-01',7).from).toBe('2024-02-24');});
it('acepta calendarios independientes Desde y Hasta',()=>{expect(agentReportRange('2026-10-01','2026-10-15')).toEqual({from:'2026-10-01',to:'2026-10-15'});expect(agentReportDayCount('2026-10-01','2026-10-15')).toBe(15);});
it('respeta medianoche de Santo Domingo y excluye días posteriores',()=>{
 const rows=['2026-10-15T03:59:59Z','2026-10-15T04:00:00Z','2026-10-16T03:59:59Z','2026-10-16T04:00:00Z'].map((createdAt,i)=>ticket({id:String(i),createdAt}));
 expect(buildAgentReport(rows,'2026-10-15',1).rows.map(r=>r.id)).toEqual(['1','2']);
});
it('resueltos por cierre, abiertos por creación, sin duplicados ni otros equipos',()=>{
 const rows=[ticket(),ticket(),ticket({id:'2',status:'RESOLVED',createdAt:'2026-09-01T12:00:00Z',closedAt:'2026-10-15T15:00:00Z',resolvedByName:'Bea',evidence:[{},{}]}),ticket({id:'3',assignedTeam:'TECHNICAL_FAILURE'}),ticket({id:'4',assignedDepartment:'GENERAL_SERVICES'})];
 const report=buildAgentReport(rows,'2026-10-15',1);expect(report.total).toBe(2);expect(report.resolved).toBe(1);expect(report.inProgress).toBe(1);expect(report.agents[1]).toMatchObject({name:'Bea',evidence:2,resolved:1});
 expect(agentDetailCells(rows[2],()=> 'Falla en printer')).toHaveLength(13);
});
it('no cuenta anulados como resueltos ni como en proceso',()=>{const r=buildAgentReport([ticket({status:'CANCELLED'})],'2026-10-15',1);expect([r.total,r.resolved,r.inProgress,r.cancelled]).toEqual([1,0,0,1]);});
it('rechaza fechas vacías, inválidas o invertidas',()=>{expect(()=>agentReportRange('',1)).toThrow();expect(()=>agentReportRange('2026-02-30',1)).toThrow();expect(()=>agentReportRange('2026-10-16','2026-10-15')).toThrow();});
