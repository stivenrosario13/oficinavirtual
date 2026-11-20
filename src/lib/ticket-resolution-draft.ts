// Keep the selector, evidence fields and submit action on the same state.
export function startTicketResolution<T extends {status:string}>(draft:T):T {
 return {...draft,status:draft.status==='CLOSED'?'CLOSED':'RESOLVED'};
}
