import {describe, expect, it} from 'vitest';
import {supervisorTracksTicket, ticketAssignedToSession} from '../src/lib/supervisor-support';

const session = {id: 'supervisor-1', email: 'supervisor@example.test'};
const report = {status: 'OPEN', isCreatedByCurrentUser: true, isAssignedToCurrentUser: false, assignedTechnicianId: null};
describe('supervisor follows reports sent to departments', () => {
  it('keeps a new, unassigned report in the process inbox', () => {
    expect(supervisorTracksTicket(report, session, 'process')).toBe(true);
    expect(ticketAssignedToSession(report, session)).toBe(false);
    expect(supervisorTracksTicket(report, session, 'resolved')).toBe(false);
  });
  it('moves the report to cases resolved by technicians only when completed', () => {
    for (const status of ['OPEN', 'IN_PROGRESS', 'PENDING']) {
      const ticket = {...report, status, assignedTechnicianId: 'technician-2'};
      expect(supervisorTracksTicket(ticket, session, 'process')).toBe(true);
      expect(supervisorTracksTicket(ticket, session, 'resolved')).toBe(false);
    }
    for (const status of ['RESOLVED', 'CLOSED']) {
      const ticket = {...report, status, assignedTechnicianId: 'technician-2'};
      expect(supervisorTracksTicket(ticket, session, 'process')).toBe(false);
      expect(supervisorTracksTicket(ticket, session, 'resolved')).toBe(true);
    }
  });
  it('does not count cancelled, unrelated or personally assigned cases as technician resolutions', () => {
    expect(supervisorTracksTicket({...report, status:'CANCELLED'}, session, 'resolved')).toBe(false);
    expect(supervisorTracksTicket({...report, status:'CANCELLED'}, session, 'process')).toBe(false);
    expect(supervisorTracksTicket({...report, isCreatedByCurrentUser:false}, session, 'process')).toBe(false);
    expect(supervisorTracksTicket({...report, status:'RESOLVED', isAssignedToCurrentUser:true}, session, 'resolved')).toBe(false);
    expect(supervisorTracksTicket({...report, status:'RESOLVED', isCreatedByCurrentUser:false, createdByUsername:session.email}, session, 'resolved')).toBe(false);
  });
});
