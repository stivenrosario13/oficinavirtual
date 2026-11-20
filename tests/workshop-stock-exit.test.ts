import {it,expect} from 'vitest';import {readFileSync} from 'node:fs';import {workshopReplacementSerial} from '../src/lib/workshop-ticket-serial';
it('restores only the stock-exit serial in the resolution field',()=>{
 expect(workshopReplacementSerial({history:[{action:'WORKSHOP_STOCK_OUT',comment:JSON.stringify({serial:'SN-101',equipment:'Monitor'})},{action:'CREATED',comment:'other'}]})).toBe('SN-101');
 expect(workshopReplacementSerial({history:null})).toBe('');expect(workshopReplacementSerial({history:[{action:'WORKSHOP_STOCK_OUT',comment:'invalid'}]})).toBe('');
});
it('keeps stock exit in the normal ticket transaction and validates workshop permissions',()=>{
 const db=readFileSync('api/DatabaseSqlServerScoped.cs','utf8'),start=db.indexOf('if(workshopStockId is not null)await RecordWorkshopStockExit');expect(start).toBeGreaterThan(0);expect(db.slice(start,start+250)).toContain('await transaction.CommitAsync(ct)');expect(db).toContain('!new WorkshopActor(userId,role,supportTeam,displayName,username).CanOperate');
 expect(readFileSync('api/Program.cs','utf8')).toContain('if(!string.IsNullOrWhiteSpace(workshopSerial))replacementSerial=workshopSerial');
 expect(db).toContain("d.code IN('TECHNOLOGY','GENERAL_SERVICES')");expect(db).toContain("'NEW_AGENCY',N'Agencia nueva'");
});
it('uses the existing internal-ticket composer and normal ticket endpoint',()=>{
 const wrapper=readFileSync('src/components/vite/WorkshopStockExitForm.tsx','utf8'),ui=readFileSync('src/components/vite/TicketCenter.tsx','utf8'),api=readFileSync('api/Program.cs','utf8');
 expect(wrapper).toContain('<TicketCenter session={session}');expect(wrapper).not.toContain('<form');
 expect(ui).toContain("ticketType:'INTERNAL' as const,workshopStockId:workshopStockExit.id");expect(ui).toContain('request<Ticket>("/api/tickets"');expect(api).toContain('supervisorEscalationReason,ct,body.WorkshopStockId');expect(api).toContain('body.TicketType!="INTERNAL"');
});
