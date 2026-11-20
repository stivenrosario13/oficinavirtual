import {PDFDocument,rgb,type PDFFont,type PDFImage,type PDFPage} from 'pdf-lib';
import type {DocumentDetail} from './DocumentSystemModule';
import {drawWarehouseCustodySeal} from './warehouseCustodySealPdf';

export function appendWarehouseFulfillment(pdf:PDFDocument,item:DocumentDetail,font:PDFFont,bold:PDFFont,brand:PDFImage,qr:PDFImage){
 const navy=rgb(.045,.13,.21),muted=rgb(.35,.44,.50);
 const safe=(text:string)=>text.replace(/[^\x20-\x7e\xa0-\xff]/g,'-');
 const fit=(text:string,size:number,width:number)=>{let result=safe(text);while(result.length&&font.widthOfTextAtSize(result,size)>width)result=result.slice(0,-1);return result};
 const text=(page:PDFPage,value:string,x:number,y:number,size=10,strong=false,width=510)=>page.drawText(fit(value,size,width),{x,y,size,font:strong?bold:font,color:navy});
 const wrap=(value:string,size:number,width:number)=>{let rest=safe(value);const result:string[]=[];while(rest){const line=fit(rest,size,width);result.push(line);rest=rest.slice(line.length)}return result.length?result:['']};
 const page=(title:string)=>{const result=pdf.addPage([595,842]);result.drawImage(brand,{x:40,y:776,width:80,height:30});text(result,title,145,787,14,true,410);text(result,`Solicitud: ${item.warehouseRequest?.number||item.title}`,40,742,10,true);text(result,`Departamento: ${item.department}`,40,720);text(result,`Agencia: ${item.warehouseRequest?.agencyCode||''} · ${item.warehouseRequest?.agencyName||''}`,40,698);text(result,`Grupo: ${item.warehouseRequest?.agencyGroup||''}`,40,676);return result};
 const table=(title:string,delivery=false)=>{
  let current=page(title),y=621;
  const header=()=>{text(current,'Equipo del inventario',40,y,9,true,275);text(current,'Cantidad',330,y,9,true,70);text(current,'Serial asignado',408,y,9,true,147);y-=23};
  header();
  for(const assigned of item.warehouseAssigned||[]){const names=wrap(assigned.name,9,270),serials=wrap(assigned.serial,9,140),height=Math.max(23,Math.max(names.length,serials.length)*13+10);if(y-height<(delivery?400:100)){current=page(`${title} · CONTINUACION`);y=621;header()}names.forEach((name,index)=>text(current,name,40,y-index*13,9,false,275));text(current,String(assigned.quantity),338,y,9,false,60);serials.forEach((serial,index)=>text(current,serial,408,y-index*13,9,true,147));current.drawLine({start:{x:40,y:y-height+10},end:{x:555,y:y-height+10},thickness:.5,color:rgb(.75,.82,.87)});y-=height}
  return current;
 };
 if(item.status==='approved'&&item.warehouseAssigned?.length){const approved=table('EQUIPOS AUTORIZADOS POR ALMACEN');text(approved,'Pendientes de retiro hasta escanear el QR del sello autorizado.',40,70,9);text(approved,`Autorizado por: ${item.decisionBy||''}`,40,50,9,true)}
 if(item.warehouseDelivery){const delivery=item.warehouseDelivery,receipt=table('FORMULARIO DE ENTREGA DE EQUIPO',true),time=new Date(delivery.at.endsWith('Z')?delivery.at:`${delivery.at}Z`).toLocaleString('es-DO',{timeZone:'America/Santo_Domingo'});text(receipt,`Comprobante de entrega: ${delivery.number}`,40,377,10,true);text(receipt,`Fecha y hora: ${time}`,40,358,9);
  for(const[index,seal]of[{status:'ENTREGADO' as const,name:delivery.deliveredBy,login:delivery.deliveredLogin},{status:'RECIBIDO' as const,name:delivery.receivedBy,login:'Recepción de equipo'}].entries())drawWarehouseCustodySeal(receipt,{...seal,time,number:delivery.number},index===0?40:305,230,250,105,font,bold,brand);receipt.drawImage(qr,{x:40,y:80,width:100,height:100});text(receipt,'Entrega vinculada al QR autorizado.',160,145,9,false,390);text(receipt,'La salida se registra una sola vez en el inventario.',160,127,9,false,390);receipt.drawText('Constancia interna de entrega y recepción.',{x:40,y:40,size:8,font,color:muted});
 }
}
