import {it,expect} from 'vitest';
import {caseCounts} from '../src/lib/ticket-status-counts';
it('keeps history and cancellations out of pending cards and counts closed solutions',()=>{
 const tickets=['OPEN','IN_PROGRESS','PENDING','RESOLVED','CLOSED','CANCELLED'].map(status=>({status}));
 const findings=['OPEN','RESOLVED','CLOSED','CANCELLED'].map(status=>({status}));
 expect(caseCounts(tickets,findings)).toEqual({tickets:3,findings:1,solutions:4});
 tickets[0].status='RESOLVED';findings[0].status='RESOLVED';
 expect(caseCounts(tickets,findings)).toEqual({tickets:2,findings:0,solutions:6});
});
