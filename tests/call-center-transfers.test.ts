import {expect,it} from 'vitest';
import {callCenterDestination} from '../src/lib/call-center-transfers';
it('tracks the transfer origin through resolution and later reassignment',()=>{
 const history=[{action:'ESCALATED',fromTeam:'CALL_CENTER',toTeam:'TECHNICAL_FAILURE'},{action:'ASSIGNED',fromTeam:'TECHNICAL_FAILURE',toTeam:'TECHNICIANS'},{action:'RESOLVED',fromTeam:'TECHNICIANS',toTeam:'TECHNICIANS'}];
 expect(callCenterDestination({ticketType:'INTERNAL',history})).toBe('TECHNICAL_FAILURE');
 history.push({action:'ESCALATED_TO_SUPERVISOR',fromTeam:'CALL_CENTER',toTeam:'TECHNICIANS'});
 expect(callCenterDestination({ticketType:'INTERNAL',history})).toBe('SUPERVISORS');
});
it('does not classify unrelated cases as Call Center transfers',()=>{
 expect(callCenterDestination({ticketType:'INTERNAL',history:null})).toBeNull();
 expect(callCenterDestination({ticketType:'SUPPORT',history:[{action:'ESCALATED',fromTeam:'CALL_CENTER',toTeam:'TECHNICAL_FAILURE'}]})).toBeNull();
 expect(callCenterDestination({ticketType:'INTERNAL',history:[{action:'ASSIGNED',fromTeam:'TECHNICAL_FAILURE',toTeam:'TECHNICIANS'}]})).toBeNull();
});
