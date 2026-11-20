import {describe,expect,it} from 'vitest';
import {findWarehouseTicketAgency} from '../src/components/vite/WarehouseRequestComposer';

const agency={id:'correct',codigo:'123',terminal:'Agency',grupo:'Group A'};
const ticket={id:'ticket',ticketNumber:42,codigo:'123',terminal:'Agency',grupo:'Group A',subject:'Equipment'};
describe('ticket request agency identity',()=>{
 it('matches the group when different groups share an agency code',()=>{
  expect(findWarehouseTicketAgency([{...agency,id:'wrong',grupo:'Group B'},agency],ticket)?.id).toBe('correct');
 });
 it('handles the display fallback for a legacy empty group',()=>{
  expect(findWarehouseTicketAgency([{...agency,grupo:''}],{...ticket,grupo:'Sin grupo registrado'})?.id).toBe('correct');
 });
 it('does not silently pick an ambiguous or unrelated agency',()=>{
  expect(findWarehouseTicketAgency([agency,{...agency,id:'duplicate'}],ticket)).toBeUndefined();
  expect(findWarehouseTicketAgency([agency],{...ticket,grupo:'Other'})).toBeUndefined();
 });
});
