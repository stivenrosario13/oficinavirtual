import type {WorkshopCase} from './workshopWorkflow';
import {workshopLabels} from './workshopWorkflow';
import {buildWarehouseRequestDocument} from './warehouseRequestPdf';




// Share Almacén's actual layout; only Taller-specific content and the seal differ.
export async function buildWorkshopFormPdf(items:WorkshopCase[],logo:string){
 const [{PDFDocument,StandardFonts,rgb},{default:QRCode},{drawWarehouseCustodySeal}]=await Promise.all([import('pdf-lib'),import('qrcode'),import('./warehouseCustodySealPdf')]);
 const item=items[0];if(!item)throw new Error('No hay equipos para generar el formulario.');
 const work=[...item.events].reverse().find(e=>e.action==='WORK');
 const delivered=item.events.find(e=>e.action==='DELIVER'),received=item.events.find(e=>e.action==='WAREHOUSE_RECEIVE');
 const route=item.status==='DISCHARGED'?'DISCHARGED':work?.status||[...item.events].reverse().find(e=>['REPAIR','PART','REPLACE'].includes(e.action))?.status||item.status;
 const latest=work||[...item.events].reverse().find(e=>!['DELIVER','WAREHOUSE_RECEIVE'].includes(e.action));
 const title=item.status==='SENT'||item.status==='RECEIVED'?'ENTRADA A TALLER':route==='DISCHARGED'?'SIN REPARACIÓN':route==='PART_REPLACEMENT'||route==='REPLACEMENT'?'REEMPLAZO DE PIEZAS':'REPARACIÓN';
 const date=(value?:string)=>value?new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value)?value:value+'Z'):undefined;
 const time=(value?:string)=>date(value)?.toLocaleString('es-DO',{timeZone:'America/Santo_Domingo',hour12:false})||'';
 const origin=typeof window==='undefined'?'https://realagencias.runasp.net':window.location.origin;
 const pdf=await buildWarehouseRequestDocument({
  number:item.documentNumber,department:item.department,agency:{codigo:item.agencyCode||'Sin agencia',terminal:item.agencyName||'Solicitud del departamento',grupo:''},
  priority:'MEDIUM',notes:[item.failureCause,item.notes,delivered&&work?.note?'Trabajo: '+work.note:''].filter(Boolean).join('\n'),
  lines:items.map(i=>({kind:'EQUIPMENT',name:i.equipmentType,quantity:i.quantity,serial:i.serialNumber,note:''})),
  createdBy:item.senderName,createdByLogin:'',createdAt:date(item.sentAt),logoData:logo,origin,destination:'Taller',
  workshop:{title,handoff:!!delivered,status:received?'Recibido en Almacén':item.pendingWarehouse?'Almacén · pendiente de recibir':workshopLabels[item.status],receivedBy:delivered?(received?.actorName||'Pendiente en Almacén'):item.receivedByName||'',receivedAt:time(delivered?received?.occurredAt:item.receivedAt),work:work?.note||latest?.note||''},
 });
 // Same fixed reception box as Almacén: no overlay over observations or work.
 const navy:[number,number,number]=[12,32,51];
 const color:[number,number,number]=[0,128,92];
 const fit=(text:string,width:number,size:number,min=4.3)=>{pdf.setFontSize(size);while(pdf.getTextWidth(text)>width&&size>min){size-=.2;pdf.setFontSize(size)}return pdf.splitTextToSize(text,width).slice(0,1)};
 const status={SENT:'PENDIENTE',RECEIVED:'RECIBIDO',REPAIR:'EN REPARACIÓN',PART_REPLACEMENT:'CAMBIO DE PIEZA',REPLACEMENT:'REEMPLAZO',DISCHARGED:'SIN REPARACIÓN',INVENTORY:'EN INVENTARIO',DELIVERED:'ENTREGADO'}[route];
 const qr=await QRCode.toDataURL(origin+'/?equipment='+encodeURIComponent(item.qrToken),{margin:1,width:300});
 if(route==='PART_REPLACEMENT'){
 // Exactly the same 90 x 38 mm outer size as the two custody seals below.
 // This box encloses the original reception frame and ends before section 05.
 pdf.setFillColor(255,255,255);pdf.setDrawColor(...color);pdf.setLineWidth(.7);pdf.rect(108,191.5,90,38,'FD');pdf.setLineWidth(.25);pdf.rect(109,192.5,88,36);
 pdf.setFillColor(239,247,251);pdf.rect(110,193.5,86,9,'F');pdf.addImage(logo,'PNG',111.5,195.5,12.7,4.75);
 pdf.setTextColor(...navy);pdf.setFont('helvetica','bold');pdf.setFontSize(9);pdf.text('GRUPO TEJEDA',126,198);
 pdf.setTextColor(...color);pdf.setFontSize(5.6);pdf.text('CONTROL INSTITUCIONAL',126,201.2);
 pdf.setFillColor(...color);pdf.circle(168,198,2.65,'F');pdf.setTextColor(255,255,255);pdf.setFontSize(5.5);pdf.text('R',168,198.7,{align:'center'});
 pdf.setTextColor(...color);pdf.setFontSize(7.5);pdf.text(['CAMBIO','DE PIEZA'],173,197);
 pdf.setDrawColor(...color);pdf.line(110,204.5,196,204.5);
 pdf.setTextColor(...navy);pdf.text(fit(latest?.actorName||item.senderName,61,8.5),111.5,209);
 pdf.setFont('helvetica','normal');pdf.setTextColor(...color);pdf.text(fit('Estado: '+workshopLabels[route],61,6.5),111.5,214);
 pdf.text(fit(latest?.actorLogin||'Pendiente de recibir',61,6.5),111.5,218.5);
 pdf.text(fit('Fecha y hora: '+time(latest?.occurredAt||item.sentAt),61,6.2),111.5,222.5);
 pdf.setTextColor(...navy);pdf.setFont('helvetica','bold');pdf.text(fit('Código: '+item.documentNumber,61,6.1),111.5,227);
 pdf.addImage(qr,'PNG',176,207,20,20);
 }else{
 pdf.setFillColor(255,255,255);pdf.setDrawColor(...color);pdf.setLineWidth(.55);pdf.rect(110,194,87,33,'FD');pdf.setLineWidth(.2);pdf.rect(111,195,85,31);
 pdf.setFillColor(239,247,251);pdf.rect(112,196,83,9,'F');pdf.addImage(logo,'PNG',113,198,12,4.5);
 pdf.setTextColor(...navy);pdf.setFont('helvetica','bold');pdf.setFontSize(7);pdf.text('GRUPO TEJEDA',127,199.5);pdf.setTextColor(...color);pdf.setFontSize(4.3);pdf.text('CONTROL INSTITUCIONAL',127,202.6);
 pdf.setFillColor(...color);pdf.circle(166,200,2.5,'F');pdf.setTextColor(255,255,255);pdf.setFontSize(5);pdf.text(item.status==='SENT'?'P':'R',166,200.7,{align:'center'});
 pdf.setTextColor(...color);pdf.setFontSize(5.5);pdf.text(pdf.splitTextToSize(status,23).slice(0,2),170,199.4);
 pdf.setDrawColor(...color);pdf.line(112,207,195,207);
 pdf.setTextColor(...navy);pdf.setFont('helvetica','bold');pdf.text(fit(latest?.actorName||item.senderName,61,6.4),113,211);
 pdf.setFont('helvetica','normal');pdf.setTextColor(...color);pdf.text(fit('Estado: '+workshopLabels[route],61,5.6),113,215);
 pdf.text(fit(latest?.actorLogin||'Pendiente de recibir',61,5.2),113,219);
 pdf.text(fit('Fecha y hora: '+time(latest?.occurredAt||item.sentAt),61,5),113,222);
 pdf.setTextColor(...navy);pdf.setFont('helvetica','bold');pdf.text(fit('Código: '+item.documentNumber,61,4.7),113,225);
 pdf.addImage(qr,'PNG',177,208,17.5,17.5);
 }
 if(!delivered)return pdf;
 const stamped=await PDFDocument.load(pdf.output('arraybuffer')),page=stamped.getPages()[0],font=await stamped.embedFont(StandardFonts.Helvetica),bold=await stamped.embedFont(StandardFonts.HelveticaBold),brand=await stamped.embedPng(logo);
 const mm=72/25.4,y=page.getHeight()-278*mm;
 drawWarehouseCustodySeal(page,{status:'ENTREGADO',name:delivered.actorName,login:delivered.actorLogin,time:time(delivered.occurredAt),number:item.documentNumber},12*mm,y,90*mm,38*mm,font,bold,brand,'TALLER');
 if(received)drawWarehouseCustodySeal(page,{status:'RECIBIDO',name:received.actorName,login:received.actorLogin,time:time(received.occurredAt),number:item.documentNumber},108*mm,y,90*mm,38*mm,font,bold,brand);
 else {page.drawRectangle({x:108*mm,y,width:90*mm,height:38*mm,borderWidth:.7,borderColor:rgb(.74,.81,.85)});page.drawText('PENDIENTE DE RECIBIR EN ALMACEN',{x:112*mm,y:y+29*mm,size:8,font:bold,color:rgb(.35,.44,.5)});page.drawText('Se completara al confirmar la recepcion por QR.',{x:112*mm,y:y+22*mm,size:7,font,color:rgb(.35,.44,.5)});}
 const bytes=await stamped.save();
 return {output:(_type:'arraybuffer')=>bytes.slice().buffer,getNumberOfPages:()=>stamped.getPageCount(),save:(name:string)=>{const url=URL.createObjectURL(new Blob([bytes.slice().buffer],{type:'application/pdf'})),a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),60000);}};
}
