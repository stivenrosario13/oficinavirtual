export function workshopReplacementSerial(ticket:{history?:{action:string;comment?:string|null}[]|null}){
 return [...new Set((ticket.history||[]).filter(h=>h.action==='WORKSHOP_STOCK_OUT').flatMap(h=>{const match=h.comment?.match(/Serial del equipo reemplazado: (.+)/);if(match)return [match[1].trim()];try{const value=JSON.parse(h.comment||'{}');return typeof value.serial==='string'?[value.serial]:[]}catch{return []}}))].join(', ');
}
