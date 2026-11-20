export const isPendingCase = (status:string) => ['OPEN','PENDING','IN_PROGRESS'].includes(status);
export const isSolvedCase = (status:string) => ['RESOLVED','CLOSED'].includes(status);
export function caseCounts(tickets:ReadonlyArray<{status:string}>,findings:ReadonlyArray<{status:string}>){
  return {tickets:tickets.filter(item=>isPendingCase(item.status)).length,findings:findings.filter(item=>isPendingCase(item.status)).length,solutions:tickets.filter(item=>isSolvedCase(item.status)).length+findings.filter(item=>isSolvedCase(item.status)).length};
}
