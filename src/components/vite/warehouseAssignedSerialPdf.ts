import {rgb,type PDFDocument,type PDFFont} from 'pdf-lib';
import type {DocumentDetail} from './DocumentSystemModule';

// Update only the serial cells in the original request's preview table.
// Complete identifiers remain in the system; warehouse PDFs use only one page.
export function drawWarehouseAssignedSerials(pdf:PDFDocument,item:DocumentDetail,font:PDFFont){
 const request=item.warehouseRequest;if(!request||!item.warehouseAssigned?.length)return;
 const page=pdf.getPages()[0],mm=72/25.4,height=page.getHeight();
 const wrap=(value:string,size:number)=>{const lines:string[]=[];let line='';for(const character of value.replace(/[^\x20-\x7e\xa0-\xff]/g,'-')){if(line&&font.widthOfTextAtSize(line+character,size)>49*mm){lines.push(line);line=''}line+=character}if(line)lines.push(line);return lines};
 const count=request.lines.length===1?1:request.lines.length===2?2:1;
 for(let index=0;index<count;index++){
  const line=request.lines[index];if(line.kind==='RETURN')continue;
  const assigned=item.warehouseAssigned.filter(row=>row.lineIndex===index).map(row=>row.serial);if(!assigned.length)continue;
  const top=124+index*8.5,cellHeight=request.lines.length===1?18:8.5,size=request.lines.length===1?6:5,lineHeight=size+1.2;
  const capacity=Math.max(1,Math.floor(((cellHeight-6)*mm-2)/lineHeight)+1);let lines=wrap(assigned.join(' / '),size);
  if(lines.length>capacity){lines=wrap(assigned[0],size);if(lines.length<capacity)lines.push('Ver todos los seriales en el sistema');else if(lines.length>capacity)lines=['Ver serial completo en el sistema'];}
  // Never truncate identifiers or draw beyond the original table cell.
  page.drawRectangle({x:141.4*mm,y:height-(top+cellHeight-.4)*mm,width:56.2*mm,height:(cellHeight-.8)*mm,color:rgb(1,1,1)});
  lines.forEach((text,at)=>page.drawText(text,{x:145*mm,y:height-(top+6)*mm-at*lineHeight,size,font,color:rgb(.094,.176,.243)}));
 }
}
