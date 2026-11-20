import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {describe,it,expect} from 'vitest';
import {administrativeDateMatches,isGasolineRecord} from '../src/lib/administrative-fuel';
import {administrativeColumns} from '../src/lib/administrative-form-excel';
import {prepareAdministrativeForm} from '../src/lib/administrative-form-data';
import {assignedAdministrativeForms} from '../src/lib/administrative-profiles';
import AdministrativeRequestComposer from '../src/components/vite/AdministrativeRequestComposer';
import WarehouseRequestComposer,{weekendLoanPeriodError} from '../src/components/vite/WarehouseRequestComposer';
import WeekendLoanDetail from '../src/components/vite/WeekendLoanDetail';
import type {PortalSession} from '../src/lib/session';
import type {DocumentDetail} from '../src/components/vite/DocumentSystemModule';
import {canRequestModemChange} from '../src/lib/internet-ticket-request';
const session={id:'sandra',email:'sandra.ramirez@grupotejeda.local',role:'Administrative',displayName:'Sandra',permissions:{}} as PortalSession;
describe('fuel split and weekend loans',()=>{
 it('validates the same weekend period rules in the form and the server',()=>{
  expect(weekendLoanPeriodError('2026-11-06','2026-11-08','2026-11-09')).toBe('');
  expect(weekendLoanPeriodError('2026-11-07','2026-11-08','2026-11-15')).toBe('');
  expect(weekendLoanPeriodError('2026-11-07','2026-11-09','2026-11-10')).toContain('terminar el domingo');
  expect(weekendLoanPeriodError('2026-11-07','2026-11-08','2026-11-16')).toContain('entre 1 y 7 días');
 });
 it('removes budget, tickets and credit from Sandra in the form, Excel and PDF data',()=>{
  const html=renderToStaticMarkup(<AdministrativeRequestComposer session={session} initialKind="fuel_log" department="Administración" onClose={()=>{}} onSaved={async()=>{}}/>);
  for(const label of ['Presupuesto','Tickets físicos','Crédito disponible','Responsable de recepción'])expect(html).not.toContain(label);
  const keys=administrativeColumns('fuel_log').map(x=>x.key);expect(keys).not.toContain('fuelBudget');expect(keys).not.toContain('physicalTickets');expect(keys).not.toContain('fuelCredit');
  const form=prepareAdministrativeForm('fuel_log',{fuelPeriod:'2026-10',fuelBudget:'1',physicalTickets:'2',fuelCredit:'3'},[{date:'2026-10-28',plate:'001',amount:'1250'}],[],'Administración',session);
  expect(form.fields.some(x=>/tickets|presupuesto|crédito/i.test(x.label))).toBe(false);expect(form.fields.find(x=>x.label==='Total RD$')?.value).toBe('1250.00');
 });
 it('separates Sulay gas with authorization from Sandra gasoline',()=>{
  expect(assignedAdministrativeForms(session.email)).toEqual(['fuel_log']);expect(assignedAdministrativeForms('whilmaly.sulay')).toContain('gas_log');expect(assignedAdministrativeForms('whilmaly.sulay')).not.toContain('fuel_log');
  expect(administrativeColumns('gas_log').map(x=>x.key)).toContain('fuelBudget');
 });
 it('filters consumption dates inclusively rather than upload dates',()=>{
  const d={createdUtc:'2026-10-28T12:00:00Z',administrativeRequest:{formType:'FUEL_LOG',formData:{rows:[{date:'2026-09-23'},{date:'2026-09-25'}]}}} as DocumentDetail;
  expect(isGasolineRecord(d)).toBe(true);expect(administrativeDateMatches(d,'2026-09-23','2026-09-23')).toBe(true);expect(administrativeDateMatches(d,'2026-10-01','2026-10-31')).toBe(false);expect(administrativeDateMatches(d,'2026-09-26','2026-09-20')).toBe(false);
 });
 it('forces weekend requests to department and includes return dates',()=>{
  const html=renderToStaticMarkup(<WarehouseRequestComposer session={{...session,role:'Technology',supportTeam:'TECHNICAL_FAILURE'}} department="TECHNOLOGY" agencies={[]} weekendLoan onClose={()=>{}} onSaved={()=>{}}/>);
  expect(html).toContain('Devolución de sobrantes');expect(html).toContain('Departamento de Tecnología');expect(html).not.toContain('Para una agencia');
 });
 it('shows remaining units and confirmed closure without another return button',()=>{
  const item={id:1,isOwner:false,warehouseRequest:{weekendLoan:{from:'2026-10-30',to:'2026-11-01',returnBy:'2026-11-02'}},warehouseDelivery:{},warehouseAssigned:[{name:'Router',serial:'CONTROL POR CANTIDAD',quantity:10}],weekendLoanSettlement:{uses:[{assignedIndex:0,quantity:6,agencyId:'a',agencyLabel:'001 · Agencia'}],note:'Se devuelven cuatro',confirmedBy:'Ana',confirmedAt:'2026-11-02T12:00:00Z'}} as DocumentDetail;
  const html=renderToStaticMarkup(<WeekendLoanDetail item={item} warehouse onChanged={async()=>{}}/>);expect(html).toContain('<td>4</td>');expect(html).toContain('devolución confirmada por Ana');expect(html).not.toContain('Confirmar recepción');
 });
 it('shows modem replacement on open internal Technology tickets to the main administrator',()=>{
  const ticket={ticketType:'INTERNAL',assignedDepartment:'TECHNOLOGY',status:'OPEN'};
  expect(canRequestModemChange({role:'Administrator',supportTeam:null},ticket)).toBe(true);
  expect(canRequestModemChange({role:'Technology',supportTeam:'TECHNICAL_FAILURE'},ticket)).toBe(true);
  expect(canRequestModemChange({role:'Technology',supportTeam:'CALL_CENTER'},ticket)).toBe(false);
  expect(canRequestModemChange({role:'Administrator',supportTeam:null},{...ticket,status:'RESOLVED'})).toBe(false);
 });
});
