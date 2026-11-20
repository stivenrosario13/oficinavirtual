import {describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {agentReportRange} from '../src/lib/call-center-report';
import {incidentTicketGroups} from '../src/lib/incident-ticket-report';

describe('reporte restringido de incidentes V518',()=>{
 it('agrupa decisiones y compensaciones por grupo',()=>{
  const groups=incidentTicketGroups([
   {documentId:1,documentNumber:'A',ticketDate:'2026-11-14',group:'Grupo A',agencyCode:'1',agency:'Uno',ticketCode:'T1',agentContact:'Ana',status:'APPROVED',compensationAmount:500,supervisor:'Steven',subject:'Caso'},
   {documentId:1,documentNumber:'A',ticketDate:'2026-11-14',group:'Grupo A',agencyCode:'1',agency:'Uno',ticketCode:'T2',agentContact:'Ana',status:'REJECTED',supervisor:'Steven',subject:'Caso'},
  ]);
  expect(groups).toEqual([{group:'Grupo A',total:2,approved:1,rejected:1,pending:0,compensation:500}]);
 });
 it('ofrece los periodos diario, 7, 15 y 30 días',()=>{
  expect(agentReportRange('2026-11-14',1)).toEqual({from:'2026-11-14',to:'2026-11-14'});
  expect(agentReportRange('2026-11-14',7)).toEqual({from:'2026-11-08',to:'2026-11-14'});
  expect(agentReportRange('2026-11-14',15)).toEqual({from:'2026-10-31',to:'2026-11-14'});
  expect(agentReportRange('2026-11-14',30)).toEqual({from:'2026-10-16',to:'2026-11-14'});
 });
 it('restringe la API a Yessica Mercedes y administrador principal',()=>{
  const api=readFileSync('api/Program.cs','utf8'),center=readFileSync('src/components/vite/TicketCenter.tsx','utf8'),report=readFileSync('src/components/vite/CallCenterAgentReport.tsx','utf8');
  expect(api).toContain('/api/incident-ticket-report');
  expect(api).toContain('login is not ("yessica.mercedes" or "yesica.mercedes")');
  expect(api).toContain('context.User.IsInRole("Administrator")');
  expect(center).toContain("session.role==='Administrator'||['yessica.mercedes','yesica.mercedes']");
  expect(report).toContain('Disponible únicamente para Yessica Mercedes y el Administrador principal.');
 });
});
