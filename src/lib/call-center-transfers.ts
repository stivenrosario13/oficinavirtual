export type CallCenterDestination = 'SUPERVISORS' | 'TECHNICAL_FAILURE';
type TransferTicket = {ticketType?:string; history?:Array<{action:string; fromTeam?:string|null; toTeam?:string|null}>|null};

/** The last transfer out of Call Center defines its tracking folder, even after reassignment or resolution. */
export function callCenterDestination(ticket:TransferTicket):CallCenterDestination|null {
  if(ticket.ticketType!=='INTERNAL')return null;
  for(const event of [...(ticket.history||[])].reverse()) {
    if(event.fromTeam!=='CALL_CENTER')continue;
    if(event.action==='ESCALATED_TO_SUPERVISOR'&&event.toTeam==='TECHNICIANS')return 'SUPERVISORS';
    if(event.action==='ESCALATED'&&event.toTeam==='TECHNICAL_FAILURE')return 'TECHNICAL_FAILURE';
  }
  return null;
}
