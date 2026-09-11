import {PDFDocument,StandardFonts,rgb} from 'pdf-lib';
import QRCode from 'qrcode';
import type {DocumentDetail} from './DocumentSystemModule';

export async function buildReceiptCopy(original:Uint8Array,item:DocumentDetail,logo:string,origin:string){
 const pdf=await PDFDocument.load(original);if(!item.receivedUtc)return original.slice();
 const font=await pdf.embedFont(StandardFonts.Helvetica),bold=await pdf.embedFont(StandardFonts.HelveticaBold),brand=await pdf.embedPng(logo),qr=await pdf.embedPng(await QRCode.toDataURL(`${origin}/api/institutional-documents/verify/${item.verificationToken}`,{width:220,margin:1}));
 const page=pdf.getPages()[0],{width,height}=page.getSize(),warehouseRequest=/REAL-WAREHOUSE-REQUEST-V319/.test(pdf.getSubject()||'');
 const w=Math.min(warehouseRequest?252:220,width-16),h=warehouseRequest?105:107;
 // sealX and sealY are the visual center, matching the on-screen placement tool.
 const x=Math.max(8,Math.min(width-w-8,(item.sealX??65)/100*width-w/2)),y=Math.max(8,Math.min(height-h-8,height-(item.sealY??72)/100*height-h/2));
 const safe=(value:string)=>value.replace(/[^\x20-\x7e\xa0-\xff]/g,'-');
 const time=new Date(item.receivedUtc).toLocaleString('es-DO',{timeZone:'America/Santo_Domingo'}),blue=rgb(.025,.34,.65),navy=rgb(.045,.13,.21),pale=rgb(.96,.985,1);
 const fit=(value:string,size:number,max:number)=>{let result=safe(value);while(font.widthOfTextAtSize(result,size)>max&&result.length>1)result=result.slice(0,-1);return result};

 if(warehouseRequest){
  page.drawRectangle({x,y,width:w,height:h,color:rgb(1,1,1),borderColor:blue,borderWidth:2});page.drawRectangle({x:x+3,y:y+3,width:w-6,height:h-6,borderColor:blue,borderWidth:.7});
  page.drawRectangle({x:x+5,y:y+h-34,width:w-10,height:27,color:pale});page.drawImage(brand,{x:x+10,y:y+h-29,width:38,height:15});
  page.drawText('GRUPO TEJEDA',{x:x+54,y:y+h-20,size:10,font:bold,color:navy});page.drawText('CONTROL INSTITUCIONAL',{x:x+54,y:y+h-29,size:6,font:bold,color:blue});
  page.drawText('RECIBIDO',{x:x+w-63,y:y+h-21,size:12,font:bold,color:blue});page.drawLine({start:{x:x+8,y:y+h-40},end:{x:x+w-8,y:y+h-40},thickness:1.2,color:blue});
  page.drawText(fit(item.receiverDisplayName||'Nombre no registrado',8,w-78),{x:x+10,y:y+h-55,size:8,font:bold,color:navy});
  page.drawText(fit(`Usuario: ${item.receiverLogin||'No registrado'}`,7,w-78),{x:x+10,y:y+h-68,size:7,font,color:blue});
  page.drawText(fit(`Fecha / hora RD: ${time}`,7,w-78),{x:x+10,y:y+h-81,size:7,font,color:blue});
  page.drawText(fit(`Código: ${item.receiptCode||''}`,6,w-78),{x:x+10,y:y+9,size:6,font:bold,color:navy});page.drawImage(qr,{x:x+w-66,y:y+7,width:56,height:56});
 }else{
  const color=rgb(.06,.24,.36);page.drawRectangle({x,y,width:w,height:h,color:rgb(1,1,1),borderColor:color,borderWidth:1.5});page.drawImage(brand,{x:x+8,y:y+h-22,width:35,height:13});page.drawText('GRUPO TEJEDA',{x:x+49,y:y+h-17,size:10,font:bold,color});page.drawText('RECIBIDO',{x:x+8,y:y+h-39,size:14,font:bold,color});page.drawImage(qr,{x:x+w-53,y:y+5,width:48,height:48});for(const[n,text]of[time,item.receiverDisplayName||'',item.receiverLogin||'',item.receiptCode||''].entries())page.drawText(fit(text,7,w-65),{x:x+8,y:y+h-53-n*12,size:7,font,color});
 }

 const letterMarker=pdf.getSubject()?.match(/REAL-LETTER-V274\|SIGNATURE_PAGE=(\d+)/),warehouseMarker=pdf.getSubject()?.match(/REAL-WAREHOUSE-REQUEST-V319\|RECEIPT_SIGNATURE_PAGE=(\d+)/);
 const signaturePage=warehouseMarker?pdf.getPages()[Number(warehouseMarker[1])-1]:letterMarker?pdf.getPages()[Number(letterMarker[1])-1]:undefined;
 if(item.receiptSignature&&signaturePage){
  const mm=72/25.4,sign=await pdf.embedPng(item.receiptSignature);
  if(warehouseMarker){signaturePage.drawRectangle({x:110*mm,y:(297-277)*mm,width:86*mm,height:28*mm,color:rgb(1,1,1)});signaturePage.drawImage(sign,{x:116*mm,y:(297-263)*mm,width:74*mm,height:13*mm});signaturePage.drawLine({start:{x:114*mm,y:(297-265)*mm},end:{x:192*mm,y:(297-265)*mm},thickness:.5,color:navy});signaturePage.drawText(fit(item.receiverDisplayName||'',7,210),{x:114*mm,y:(297-270)*mm,size:7,font:bold,color:navy});signaturePage.drawText(fit(`${item.receiverLogin||''} - ${time}`,5.5,220),{x:114*mm,y:(297-275)*mm,size:5.5,font,color:blue})}
  else{signaturePage.drawRectangle({x:113*mm,y:(297-275)*mm,width:79*mm,height:28*mm,color:rgb(1,1,1)});signaturePage.drawImage(sign,{x:115*mm,y:(297-264)*mm,width:74*mm,height:17*mm});signaturePage.drawText(fit(item.receiverDisplayName||'',7,210),{x:114*mm,y:(297-269)*mm,size:7,font});signaturePage.drawText(fit(item.receiverLogin||'',6,210),{x:114*mm,y:(297-274)*mm,size:6,font})}
 }

 // Warehouse request PDFs already contain their custody, stamp and signature sections.
 if(warehouseRequest)return new Uint8Array(await pdf.save());

 const receipt=pdf.addPage([595,842]);receipt.drawImage(brand,{x:40,y:777,width:80,height:30});receipt.drawText('Grupo Tejeda | Constancia de recepción',{x:140,y:790,size:16,font:bold,color:navy});let lineY=738;
 for(const text of [`Documento: ${item.title}`,`Código: ${item.receiptCode}`,`Recibido por: ${item.receiverDisplayName}`,`Login: ${item.receiverLogin}`,`Fecha y hora (República Dominicana): ${time}`,`Estado actual: ${item.status}`,`Verificación con sesión autorizada:`,`${origin}/api/institutional-documents/verify/${item.verificationToken}`]){let rest=safe(text);while(rest){const line=fit(rest,10,510);receipt.drawText(line,{x:40,y:lineY,size:10,font,color:navy});rest=rest.slice(line.length);lineY-=16}lineY-=10}
 if(item.receiptSignature){const sign=await pdf.embedPng(item.receiptSignature);receipt.drawImage(sign,{x:40,y:lineY-70,width:220,height:66});receipt.drawText('Firma de quien recibe',{x:40,y:lineY-85,size:10,font:bold,color:navy})}receipt.drawImage(qr,{x:430,y:80,width:110,height:110});receipt.drawText('Copia con constancia interna. El archivo original se conserva sin modificaciones.',{x:40,y:40,size:8,font,color:navy});if(letterMarker){pdf.getPages().forEach((p,i)=>{p.drawRectangle({x:507,y:20,width:58,height:22,color:rgb(1,1,1)});p.drawText(`${i+1} / ${pdf.getPageCount()}`,{x:525,y:31,size:8,font,color:navy})})}return new Uint8Array(await pdf.save());
}
