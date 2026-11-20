import {describe,it,expect} from 'vitest';
import {startTicketResolution} from '../src/lib/ticket-resolution-draft';
describe('Opening ticket resolution for any department',()=>{
 for(const department of ['GENERAL_SERVICES','TECHNOLOGY','HUMAN_RESOURCES']){
  for(const status of ['OPEN','IN_PROGRESS','PENDING'])it(`${department} / ${status} enables evidence and submit together`,()=>{
   const draft={status,department,resolution:'Trabajo realizado',evidence:['photo'],assignedTechnicianId:'assigned-only',replacementSerial:'SER-1'};
   expect(startTicketResolution(draft)).toEqual({...draft,status:'RESOLVED'});
   expect(draft.status).toBe(status);
  });
 }
 it('preserves close state',()=>expect(startTicketResolution({status:'CLOSED'}).status).toBe('CLOSED'));
});
