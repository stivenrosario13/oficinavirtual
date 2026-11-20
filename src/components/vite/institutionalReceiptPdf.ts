import {isGasolineRecord} from '@/lib/administrative-fuel';
import type {PDFPage} from 'pdf-lib';

import type {DocumentDetail} from './DocumentSystemModule';


import {administrativeSealLayout,administrativeCustodyLayout,administrativeFormSealSpaces} from './administrativeSealLayout';
import {buildWarehouseRequestDocument} from './warehouseRequestPdf';

export async function buildReceiptCopy(original:Uint8Array,item:DocumentDetail,logo:string,origin:string){
 if(isGasolineRecord(item)){
  const [{administrativeDraftFromDocument},{prepareAdministrativeForm},{buildAdministrativeFormPdf}]=await Promise.all([import('@/lib/administrative-form-save'),import('@/lib/administrative-form-data'),import('./administrativeFormPdf')]);
  const draft=administrativeDraftFromDocument(item),prepared=prepareAdministrativeForm('fuel_log',draft.values,draft.rows,draft.phones,item.department,{displayName:item.ownerDisplayName});
  return new Uint8Array(await buildAdministrativeFormPdf({registrationOnly:true,number:item.administrativeRequest!.number,title:prepared.title,subtitle:prepared.subtitle,department:item.department,createdBy:item.ownerDisplayName,createdByLogin:item.ownerLogin||'',responsible:draft.values.fuelOwner||item.ownerDisplayName,target:prepared.target,fields:prepared.fields,tables:prepared.tables,bodySections:prepared.bodySections,notes:prepared.reason,logoData:logo,origin,createdAt:new Date(item.createdUtc)}));
 }
 const [{PDFDocument,StandardFonts,rgb},{default:QRCode},{drawWarehouseAssignedSerials},{drawWarehouseCustodySeal}]=await Promise.all([import('pdf-lib'),import('qrcode'),import('./warehouseAssignedSerialPdf'),import('./warehouseCustodySealPdf')]);
 let source=original;
 if(item.administrativeRequest?.formType==='INCIDENT_REPORT'){
  const [{administrativeDraftFromDocument},{prepareAdministrativeForm},{buildAdministrativeFormPdf}]=await Promise.all([import('@/lib/administrative-form-save'),import('@/lib/administrative-form-data'),import('./administrativeFormPdf')]);
  const draft=administrativeDraftFromDocument(item),prepared=prepareAdministrativeForm('incident_report',draft.values,draft.rows,draft.phones,item.department,{displayName:item.ownerDisplayName});
  source=new Uint8Array(await buildAdministrativeFormPdf({detailsOnlyAfterFirstPage:true,hideCustodySpaces:true,number:item.administrativeRequest.number,title:prepared.title,subtitle:prepared.subtitle,department:item.department,createdBy:item.ownerDisplayName,createdByLogin:item.ownerLogin||'',responsible:draft.values.preparedBy||item.ownerDisplayName,target:prepared.target,fields:prepared.fields,tables:prepared.tables,bodySections:prepared.bodySections,notes:prepared.reason,logoData:logo,origin,createdAt:new Date(item.createdUtc)}));
 }
 const pdf=await PDFDocument.load(source);
 const subject=pdf.getSubject()||'';
 const administrativeRequest=!!item.administrativeRequest||/REAL-ADMINISTRATIVE-(?:REQUEST-V372|FORM-V376)/.test(subject);
 const warehouseRequest=!administrativeRequest&&/REAL-WAREHOUSE-REQUEST-V319/.test(subject),structuredRequest=warehouseRequest||administrativeRequest;
 const warehouseSinglePage=warehouseRequest||!!item.warehouseRequest&&item.recipientDepartment==='Almacén';
 const saveCopy=async()=>new Uint8Array(await pdf.save());
 if(warehouseSinglePage)while(pdf.getPageCount()>1)pdf.removePage(pdf.getPageCount()-1);
 if(warehouseSinglePage&&item.warehouseRequest&&item.warehouseRequest.lines.length>1){
  const request=item.warehouseRequest;
  const breakdown=await buildWarehouseRequestDocument({number:request.number,department:item.department,agency:{codigo:request.agencyCode,terminal:request.agencyName,grupo:request.agencyGroup},priority:request.priority,notes:'',createdBy:item.ownerDisplayName||item.ownerUserName,createdByLogin:item.ownerUserName,logoData:logo,origin,lines:request.lines.map((line,index)=>({...line,kind:line.kind==='RETURN'?'RETURN':line.kind==='COMPONENT'?'COMPONENT':'EQUIPMENT',serial:item.warehouseAssigned?.filter(a=>a.lineIndex===index).map(a=>a.serial).filter(Boolean).join(' / ')||line.serial||'',note:line.note||''}))});
  const annex=await PDFDocument.load(breakdown.output('arraybuffer'));
  for(const extra of await pdf.copyPages(annex,annex.getPageIndices().slice(1)))pdf.addPage(extra);
 }
 if(!item.receivedUtc&&!structuredRequest)return warehouseSinglePage?await saveCopy():original.slice();
 const font=await pdf.embedFont(StandardFonts.Helvetica),bold=await pdf.embedFont(StandardFonts.HelveticaBold);
 let page=pdf.getPages()[0];
 const {width,height}=page.getSize(),mm=72/25.4,navy=rgb(.045,.13,.21),muted=rgb(.35,.44,.50);
 if(warehouseRequest)drawWarehouseAssignedSerials(pdf,item,font);
 if(warehouseRequest){
  page.drawRectangle({x:190*mm,y:15*mm,width:10*mm,height:5*mm,color:rgb(1,1,1)});
  page.drawText(`1 / ${pdf.getPageCount()}`,{x:193*mm,y:17*mm,size:5,font,color:muted});
   if((item.warehouseRequest?.lines.length||0)>1){
    page.drawRectangle({x:13*mm,y:(297-141.5)*mm,width:20*mm,height:8*mm,color:rgb(1,1,1)});
    page.drawText('Anexo',{x:16*mm,y:(297-138.5)*mm,size:5.5,font,color:navy});
    page.drawRectangle({x:120*mm,y:(297-141.5)*mm,width:20*mm,height:8*mm,color:rgb(1,1,1)});
    page.drawText(String(item.warehouseRequest!.lines.slice(1).reduce((total,line)=>total+Number(line.quantity||0),0)),{x:129*mm,y:(297-138.5)*mm,size:5.5,font,color:navy});
   page.drawRectangle({x:35*mm,y:(297-141.5)*mm,width:83*mm,height:8*mm,color:rgb(1,1,1)});
   page.drawText('Consultar el desglose de los demás artículos en la página 2',{x:38*mm,y:(297-138.5)*mm,size:5.5,font,color:navy});
   page.drawRectangle({x:142*mm,y:(297-141.5)*mm,width:55*mm,height:8*mm,color:rgb(1,1,1)});
   page.drawText('Detalle en página 2',{x:145*mm,y:(297-138.5)*mm,size:6,font,color:navy});
  }
  const requester=item.ownerDisplayName||item.ownerUserName||'Solicitante autenticado',requesterLogin=item.ownerUserName||'';
  // Recompone la zona de recepción y firmas para que los PDF históricos no
  // impriman textos de estado ni nombres ocupando el espacio de firma física.
  page.drawRectangle({x:110*mm,y:(297-227)*mm,width:87*mm,height:33*mm,color:rgb(1,1,1)});
  page.drawRectangle({x:111*mm,y:(297-180)*mm,width:82*mm,height:24*mm,color:rgb(1,1,1)});
  page.drawText('PERSONA QUE RECIBE',{x:112*mm,y:(297-159)*mm,size:6.4,font:bold,color:muted});
  page.drawLine({start:{x:112*mm,y:(297-165)*mm},end:{x:192*mm,y:(297-165)*mm},thickness:.55,color:rgb(.75,.82,.87)});
  page.drawText('RECEPCION REGISTRADA',{x:112*mm,y:(297-172)*mm,size:6.4,font:bold,color:muted});
  page.drawLine({start:{x:112*mm,y:(297-178)*mm},end:{x:192*mm,y:(297-178)*mm},thickness:.55,color:rgb(.75,.82,.87)});
  for(const x of [12,108]){page.drawRectangle({x:(x+1)*mm,y:(297-265)*mm,width:88*mm,height:22*mm,color:rgb(1,1,1)});page.drawText(x===12?'FIRMA DE QUIEN SOLICITA':'FIRMA DE QUIEN RECIBE',{x:(x+4)*mm,y:(297-248)*mm,size:6.7,font:bold,color:navy});page.drawLine({start:{x:(x+7)*mm,y:(297-260)*mm},end:{x:(x+83)*mm,y:(297-260)*mm},thickness:.55,color:rgb(.75,.82,.87)})}
  page.drawRectangle({x:12*mm,y:(297-278)*mm,width:90*mm,height:11*mm,color:rgb(1,1,1)});
  page.drawText(`Solicitado por: ${requester}`.slice(0,66),{x:12*mm,y:(297-271)*mm,size:6.5,font:bold,color:navy});
  if(requesterLogin)page.drawText(`Usuario: ${requesterLogin}`.slice(0,78),{x:12*mm,y:(297-275)*mm,size:5.8,font,color:muted});
  page.drawRectangle({x:108*mm,y:(297-278)*mm,width:90*mm,height:11*mm,color:rgb(1,1,1)});
 }
 if(!item.receivedUtc&&item.status!=='cancelled')return await saveCopy();
 const verificationUrl=item.verificationToken?`${origin}/api/institutional-documents/verify/${item.verificationToken}`:item.administrativeRequest?.number?`${origin}/api/institutional-documents/lookup/${encodeURIComponent(item.administrativeRequest.number)}`:`${origin}/?equipment=${encodeURIComponent("REAL-MNT|"+(item.warehouseRequest?.number||""))}`;
 const brand=await pdf.embedPng(logo),qr=await pdf.embedPng(await QRCode.toDataURL(verificationUrl,{width:220,margin:1}));
 const w=Math.min(87*mm,width-16),h=33*mm;
 // sealX and sealY are the visual center, matching the on-screen placement tool.
 // Warehouse requests always use the reserved reception rectangle on page 1.
 const x=structuredRequest?110*mm:Math.max(8,Math.min(width-w-8,(item.sealX??65)/100*width-w/2)),y=structuredRequest?(297-227)*mm:Math.max(8,Math.min(height-h-8,height-(item.sealY??72)/100*height-h/2));
 const safe=(value:string)=>value.replace(/[^\x20-\x7e\xa0-\xff]/g,'-');
 const receivedTime=new Date(item.receivedUtc||item.decisionUtc||item.createdUtc).toLocaleString('es-DO',{timeZone:'America/Santo_Domingo'}),blue=rgb(.025,.34,.65);
 const finalDecision=item.status==='approved'||item.status==='rejected'||item.status==='cancelled';
 const administrativeWithdrawal=administrativeRequest&&item.administrativeRequest?.formType!=='INCIDENT_REPORT';
 const sealLabel=item.status==='cancelled'?'ANULADO':item.status==='approved'&&(item.warehouseAssigned?.length||administrativeWithdrawal)?'AUTORIZADO A RETIRAR':item.status==='approved'?'AUTORIZADO':item.status==='rejected'?'RECHAZADO':'RECIBIDO';
 const sealColor=item.status==='approved'?rgb(.035,.48,.32):(item.status==='rejected'||item.status==='cancelled')?rgb(.68,.10,.18):blue;
 const pale=item.status==='approved'?rgb(.93,.99,.96):(item.status==='rejected'||item.status==='cancelled')?rgb(1,.95,.96):rgb(.96,.985,1);
 const sealTime=new Date(finalDecision&&item.decisionUtc?item.decisionUtc:(item.receivedUtc||item.createdUtc)).toLocaleString('es-DO',{timeZone:'America/Santo_Domingo'});
 const sealActor=finalDecision?(item.decisionBy||item.receiverDisplayName||'Usuario no registrado'):(item.receiverDisplayName||'Nombre no registrado');
 const fit=(value:string,size:number,max:number)=>{let result=safe(value);while(font.widthOfTextAtSize(result,size)>max&&result.length>1)result=result.slice(0,-1);return result};
 const drawRequestStatusSeal=(target:PDFPage,sealX:number,sealY:number,sealW:number,sealH:number)=>{
  target.drawRectangle({x:sealX,y:sealY,width:sealW,height:sealH,color:rgb(1,1,1),borderColor:sealColor,borderWidth:2});target.drawRectangle({x:sealX+3,y:sealY+3,width:sealW-6,height:sealH-6,borderColor:sealColor,borderWidth:.7});
  target.drawRectangle({x:sealX+5,y:sealY+sealH-32,width:sealW-10,height:25,color:pale});target.drawImage(brand,{x:sealX+10,y:sealY+sealH-27,width:40,height:15});
  target.drawText('GRUPO TEJEDA',{x:sealX+56,y:sealY+sealH-18,size:10.5,font:bold,color:navy});target.drawText('CONTROL INSTITUCIONAL',{x:sealX+56,y:sealY+sealH-27,size:6.2,font:bold,color:sealColor});
  const iconX=sealX+sealW-78,iconY=sealY+sealH-19;target.drawCircle({x:iconX,y:iconY,size:8,color:sealColor});
  if(item.status==='approved'){target.drawLine({start:{x:iconX-4,y:iconY},end:{x:iconX-1,y:iconY-3},thickness:2,color:rgb(1,1,1)});target.drawLine({start:{x:iconX-1,y:iconY-3},end:{x:iconX+5,y:iconY+4},thickness:2,color:rgb(1,1,1)})}
  else if(item.status==='rejected'||item.status==='cancelled'){target.drawLine({start:{x:iconX-4,y:iconY-4},end:{x:iconX+4,y:iconY+4},thickness:2,color:rgb(1,1,1)});target.drawLine({start:{x:iconX-4,y:iconY+4},end:{x:iconX+4,y:iconY-4},thickness:2,color:rgb(1,1,1)})}
  else target.drawText('R',{x:iconX-3.4,y:iconY-3.5,size:9,font:bold,color:rgb(1,1,1)});
  if(sealLabel==='AUTORIZADO A RETIRAR'){
   const labelX=iconX+12,labelWidth=sealX+sealW-8-labelX;
   const size=Math.min(8.5,labelWidth/bold.widthOfTextAtSize('AUTORIZADO',1));
   target.drawText('AUTORIZADO',{x:labelX,y:sealY+sealH-17,size,font:bold,color:sealColor});
   target.drawText('A RETIRAR',{x:labelX,y:sealY+sealH-27,size,font:bold,color:sealColor});
  }else{
   const sealSize=sealLabel.length>8?9:11.5;
   target.drawText(sealLabel,{x:iconX+12,y:sealY+sealH-22,size:sealSize,font:bold,color:sealColor});
  }
  target.drawLine({start:{x:sealX+8,y:sealY+sealH-38},end:{x:sealX+sealW-8,y:sealY+sealH-38},thickness:1.1,color:sealColor});
  target.drawText(fit(sealActor,8.5,sealW-70),{x:sealX+10,y:sealY+sealH-52,size:8.5,font:bold,color:navy});
  target.drawText(fit(finalDecision?`Decisión: ${sealLabel}`:`Usuario: ${item.receiverLogin||'No registrado'}`,7.4,sealW-70),{x:sealX+10,y:sealY+sealH-66,size:7.4,font,color:sealColor});
  target.drawText(fit(`Fecha y hora: ${sealTime}`,7.2,sealW-70),{x:sealX+10,y:sealY+sealH-79,size:7.2,font,color:sealColor});
  target.drawText(fit(`Código: ${item.receiptCode||''}`,6.4,sealW-66),{x:sealX+10,y:sealY+8,size:6.4,font:bold,color:navy});target.drawImage(qr,{x:sealX+sealW-54,y:sealY+7,width:44,height:44});
 };

 if(warehouseRequest)drawRequestStatusSeal(page,x,y,w,h);

 const letterMarker=pdf.getSubject()?.match(/REAL-LETTER-V(?:274|383)\|SIGNATURE_PAGE=(\d+)/),requestMarker=pdf.getSubject()?.match(/REAL-(?:WAREHOUSE-REQUEST-V319|ADMINISTRATIVE-(?:REQUEST-V372|FORM-V376))\|RECEIPT_SIGNATURE_PAGE=(\d+)/);
 const signaturePage=requestMarker?pdf.getPages()[Number(requestMarker[1])-1]:letterMarker?pdf.getPages()[Number(letterMarker[1])-1]:undefined;
 if(item.receiptSignature&&signaturePage&&!administrativeRequest){
  const sign=await pdf.embedPng(item.receiptSignature);
  if(warehouseRequest){signaturePage.drawRectangle({x:108*mm,y:(297-278)*mm,width:90*mm,height:36*mm,color:rgb(1,1,1)});signaturePage.drawRectangle({x:108*mm,y:(297-266)*mm,width:90*mm,height:24*mm,borderColor:rgb(.75,.82,.87),borderWidth:.7});signaturePage.drawText('FIRMA DE QUIEN RECIBE',{x:112*mm,y:(297-248)*mm,size:6.7,font:bold,color:navy});signaturePage.drawImage(sign,{x:116*mm,y:(297-258)*mm,width:74*mm,height:9*mm});signaturePage.drawLine({start:{x:115*mm,y:(297-260)*mm},end:{x:191*mm,y:(297-260)*mm},thickness:.5,color:navy});signaturePage.drawText(fit(item.receiverDisplayName||'',7.2,235),{x:108*mm,y:(297-271)*mm,size:7.2,font:bold,color:navy});signaturePage.drawText(fit(`${item.receiverLogin||''} · ${receivedTime}`,5.7,245),{x:108*mm,y:(297-275)*mm,size:5.7,font,color:blue})}
  else{signaturePage.drawRectangle({x:113*mm,y:(297-275)*mm,width:79*mm,height:28*mm,color:rgb(1,1,1)});signaturePage.drawImage(sign,{x:115*mm,y:(297-264)*mm,width:74*mm,height:17*mm});signaturePage.drawText(fit(item.receiverDisplayName||'',7,210),{x:114*mm,y:(297-269)*mm,size:7,font});signaturePage.drawText(fit(item.receiverLogin||'',6,210),{x:114*mm,y:(297-274)*mm,size:6,font})}
 }

 // The approved copy carries Warehouse's actual inventory assignment, not a serial typed by the requester.
 if(warehouseRequest){
  if(item.warehouseDelivery){const delivery=item.warehouseDelivery,time=new Date(delivery.at.endsWith('Z')?delivery.at:delivery.at+'Z').toLocaleString('es-DO',{timeZone:'America/Santo_Domingo'});for(const[index,seal]of[{status:'ENTREGADO' as const,name:delivery.deliveredBy,login:delivery.deliveredLogin},{status:'RECIBIDO' as const,name:delivery.receivedBy,login:'Retiro de equipo'}].entries())drawWarehouseCustodySeal(page,{...seal,time,number:delivery.number},(index===0?12:108)*mm,(297-278)*mm,90*mm,36*mm,font,bold,brand)}
  return await saveCopy();
 }

 if(administrativeRequest){
  const reserved=administrativeSealLayout(subject,pdf.getPages().map(p=>p.getSize()));
  let custody=administrativeCustodyLayout(subject,pdf.getPages().map(p=>p.getSize()));
  const delivery=item.administrativeDelivery;
  if(reserved?.page===0&&(!delivery||custody)){
   drawRequestStatusSeal(page,reserved.x,reserved.y,reserved.width,reserved.height);
  }else{
   // Historical PDFs keep their body and original footer. Insert the stamp
   // section inside the first-page form, above that footer, never below it.
   const footerHeight=15*mm,bodyHeight=height-footerHeight;
   const body=await pdf.embedPage(page,{left:0,bottom:footerHeight,right:width,top:height});
   const footer=await pdf.embedPage(page,{left:0,bottom:0,right:width,top:footerHeight});
   const scale=170*mm/bodyHeight;
   pdf.removePage(0);page=pdf.insertPage(0,[width,height]);
   page.drawPage(body,{x:(width-width*scale)/2,y:height-170*mm,width:width*scale,height:170*mm});
   page.drawPage(footer,{x:0,y:0,width,height:footerHeight});
   page.drawRectangle({x:12*mm,y:height-181.5*mm,width:186*mm,height:7.5*mm,color:rgb(.937,.969,.984)});
   page.drawText('CONTROL DE AUTORIZACION, ENTREGA Y RECEPCION',{x:16*mm,y:height-179*mm,size:7,font:bold,color:navy});
   const boxes=Object.fromEntries(Object.entries(administrativeFormSealSpaces).map(([key,b])=>[key,{page:0,x:b.x*mm,y:height-(b.top+b.height)*mm,width:b.width*mm,height:b.height*mm}]));
   for(const key of ['delivery','receipt']){const b=boxes[key];page.drawRectangle({...b,borderColor:rgb(.75,.81,.85),borderWidth:.7});page.drawText(key==='delivery'?'SELLO DE ENTREGA':'SELLO DE RECIBIDO',{x:b.x+9*mm,y:b.y+b.height/2,size:7,font,color:muted});}
   custody={delivery:boxes.delivery,receipt:boxes.receipt};
   const b=boxes.status;drawRequestStatusSeal(page,b.x,b.y,b.width,b.height);
  }
  if(delivery){
   const time=new Date(delivery.at).toLocaleString('es-DO',{timeZone:'America/Santo_Domingo'});
   for(const[index,seal]of[{status:'ENTREGADO' as const,name:delivery.deliveredBy,login:delivery.deliveredLogin},{status:'RECIBIDO' as const,name:delivery.receivedBy,login:'Custodia administrativa'}].entries()){const b=index===0?custody!.delivery:custody!.receipt;drawWarehouseCustodySeal(page,{...seal,time,number:delivery.number},b.x,b.y,b.width,b.height,font,bold,brand,'ADMINISTRACION');}
   // Asset details keep their annexes; all stamps belong to the first page.
   const identifiers=delivery.assetIdentifiers?.length?delivery.assetIdentifiers:delivery.assetIdentifier?[delivery.assetIdentifier]:[];
   let annex:PDFPage|undefined;
   const heading=(target:PDFPage)=>{target.drawImage(brand,{x:12*mm,y:272*mm,width:22*mm,height:8*mm});target.drawText('CONSTANCIA DE ENTREGA',{x:40*mm,y:275*mm,size:14,font:bold,color:navy});target.drawText(fit(`Solicitud ${delivery.number}`,10,180*mm),{x:12*mm,y:258*mm,size:10,font:bold,color:navy});};
   let lineY=240*mm;
   for(const[index,identifier]of identifiers.entries()){
    if(!annex||lineY<25*mm){annex=pdf.addPage([210*mm,297*mm]);heading(annex);lineY=240*mm;}
    annex.drawText(fit(`${index+1}. ${identifier}`,10,180*mm),{x:12*mm,y:lineY,size:10,font,color:navy});lineY-=20;
   }
  }
  return await saveCopy();
 }

 const receipt=pdf.addPage([595,842]);receipt.drawImage(brand,{x:40,y:777,width:80,height:30});receipt.drawText('Grupo Tejeda | Constancia de recepción',{x:140,y:790,size:16,font:bold,color:navy});let lineY=738;
 for(const text of [`Documento: ${item.title}`,`Código: ${item.receiptCode}`,`Recibido por: ${item.receiverDisplayName}`,`Login: ${item.receiverLogin}`,`Fecha de recepción (República Dominicana): ${receivedTime}`,`Sello actual: ${sealLabel}`,finalDecision?`Decisión registrada por: ${sealActor} · ${sealTime}`:`Estado actual: ${item.status}`,`Verificación con sesión autorizada:`,`${origin}/api/institutional-documents/verify/${item.verificationToken}`]){let rest=safe(text);while(rest){const line=fit(rest,10,510);receipt.drawText(line,{x:40,y:lineY,size:10,font,color:navy});rest=rest.slice(line.length);lineY-=16}lineY-=10}
 // Los documentos generales reciben el sello en esta página exclusiva de
 // constancia. La posición es fija y nunca puede cubrir el texto original.
 const fixedX=40,fixedY=74,fixedW=515,fixedH=126;
 receipt.drawRectangle({x:fixedX,y:fixedY,width:fixedW,height:fixedH,color:rgb(1,1,1),borderColor:sealColor,borderWidth:2.4});
 receipt.drawRectangle({x:fixedX+4,y:fixedY+4,width:fixedW-8,height:fixedH-8,borderColor:sealColor,borderWidth:.9});
 receipt.drawRectangle({x:fixedX+7,y:fixedY+fixedH-39,width:fixedW-14,height:30,color:pale});
 receipt.drawImage(brand,{x:fixedX+14,y:fixedY+fixedH-34,width:72,height:27});
 receipt.drawText('GRUPO TEJEDA',{x:fixedX+99,y:fixedY+fixedH-22,size:13,font:bold,color:navy});
 receipt.drawText('CONTROL INSTITUCIONAL',{x:fixedX+99,y:fixedY+fixedH-34,size:7.5,font:bold,color:sealColor});
 receipt.drawCircle({x:fixedX+fixedW-153,y:fixedY+fixedH-22,size:10,color:sealColor});
 receipt.drawText(item.status==='approved'?'A':item.status==='rejected'?'X':'R',{x:fixedX+fixedW-157,y:fixedY+fixedH-26,size:11,font:bold,color:rgb(1,1,1)});
 receipt.drawText(sealLabel,{x:fixedX+fixedW-132,y:fixedY+fixedH-28,size:15,font:bold,color:sealColor});
 receipt.drawLine({start:{x:fixedX+10,y:fixedY+fixedH-47},end:{x:fixedX+fixedW-10,y:fixedY+fixedH-47},thickness:1.2,color:sealColor});
 receipt.drawText(fit(sealActor,10.5,fixedW-145),{x:fixedX+14,y:fixedY+fixedH-67,size:10.5,font:bold,color:navy});
 receipt.drawText(fit(finalDecision?`Decisión: ${sealLabel}`:`Usuario: ${item.receiverLogin||'No registrado'}`,8.5,fixedW-145),{x:fixedX+14,y:fixedY+fixedH-84,size:8.5,font,color:sealColor});
 receipt.drawText(fit(`Fecha y hora: ${sealTime}`,8.5,fixedW-145),{x:fixedX+14,y:fixedY+fixedH-100,size:8.5,font,color:sealColor});
 receipt.drawText(fit(`Código: ${item.receiptCode||''}`,7.3,fixedW-135),{x:fixedX+14,y:fixedY+12,size:7.3,font:bold,color:navy});
 receipt.drawImage(qr,{x:fixedX+fixedW-105,y:fixedY+12,width:84,height:84});
 if(item.receiptSignature){const sign=await pdf.embedPng(item.receiptSignature);receipt.drawImage(sign,{x:40,y:lineY-70,width:220,height:66});receipt.drawText('Firma de quien recibe',{x:40,y:lineY-85,size:10,font:bold,color:navy})}
 if(item.administrativeDelivery){const delivery=item.administrativeDelivery,time=new Date(delivery.at).toLocaleString('es-DO',{timeZone:'America/Santo_Domingo'});if(delivery.assetIdentifier)receipt.drawText(`Activo asignado: ${safe(delivery.assetIdentifier)}`.slice(0,90),{x:40,y:342,size:10,font:bold,color:navy});drawWarehouseCustodySeal(receipt,{status:'ENTREGADO',name:delivery.deliveredBy,login:delivery.deliveredLogin,time,number:delivery.number},40,205,250,120,font,bold,brand,'ADMINISTRACION');drawWarehouseCustodySeal(receipt,{status:'RECIBIDO',name:delivery.receivedBy,login:'Custodia administrativa',time,number:delivery.number},305,205,250,120,font,bold,brand,'ADMINISTRACION')}
 receipt.drawText('Copia con constancia interna. El archivo original se conserva sin modificaciones.',{x:40,y:40,size:8,font,color:navy});if(letterMarker){pdf.getPages().forEach((p,i)=>{p.drawRectangle({x:495,y:15,width:88,height:52,color:rgb(1,1,1)});p.drawText(`${i+1} / ${pdf.getPageCount()}`,{x:548,y:31,size:8,font,color:navy})})}return await saveCopy();
}
