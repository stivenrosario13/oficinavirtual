import {jsPDF} from 'jspdf';
import type {WorkshopDailyMovement} from './workshopReportData';
import {santoDomingoDateKey,workshopReportDateLabel,workshopReportRangeLabel} from './workshopReportData';

export function buildWorkshopDailyReportPdf(date:string,rows:WorkshopDailyMovement[],logo:string){
 return buildWorkshopReportPdf({from:date,to:date,title:'Reporte diario de movimientos de Taller',emptyMessage:'No se registraron movimientos de Taller en la fecha seleccionada.',rows,logo});
}

export function buildWorkshopReportPdf({from,to,fromTime='00:00',toTime='23:59',title,emptyMessage,rows,logo}:{from:string;to:string;fromTime?:string;toTime?:string;title:string;emptyMessage:string;rows:WorkshopDailyMovement[];logo:string}){
 const pdf=new jsPDF({orientation:'landscape',unit:'mm',format:'a4',compress:true});
 const width=297,height=210,margin=12,contentWidth=width-margin*2;
 const columns=[{label:'Fecha / hora',width:26},{label:'Movimiento',width:36},{label:'Formulario',width:34},{label:'Equipo / serial',width:48},{label:'Responsable',width:42},{label:'Detalle',width:87}];
 const dateLabel=`${from===to?workshopReportDateLabel(from):workshopReportRangeLabel(from,to)} · ${fromTime} a ${toTime}`,generated=new Date().toLocaleString('es-DO',{timeZone:'America/Santo_Domingo',dateStyle:'medium',timeStyle:'short'});
 let y=0;
 const footer=()=>{pdf.setDrawColor(42,104,132);pdf.line(margin,height-11,width-margin,height-11);pdf.setFont('helvetica','normal');pdf.setFontSize(6.5);pdf.setTextColor(83,111,126);pdf.text(`Oficina Virtual Grupo Tejeda · Generado ${generated}`,margin,height-6);pdf.text(`Página ${pdf.getNumberOfPages()}`,width-margin,height-6,{align:'right'});};
 const header=()=>{pdf.setFillColor(4,28,47);pdf.rect(0,0,width,29,'F');pdf.addImage(logo,'PNG',margin,5,25,15,undefined,'FAST');pdf.setFont('helvetica','bold');pdf.setFontSize(14);pdf.setTextColor(255,255,255);pdf.text(title,44,12);pdf.setFontSize(7.5);pdf.setTextColor(111,218,237);pdf.text(`AVERÍA TÉCNICA · CONTROL DE TALLER · ${dateLabel.toUpperCase()}`,44,19);footer();y=35;};
 const tableHeader=()=>{pdf.setFillColor(10,75,101);pdf.rect(margin,y,contentWidth,8,'F');let x=margin;pdf.setFont('helvetica','bold');pdf.setFontSize(6.6);pdf.setTextColor(255,255,255);for(const column of columns){pdf.text(column.label,x+1.5,y+5.2);x+=column.width;}y+=8;};
 const newPage=()=>{if(pdf.getNumberOfPages()>0)pdf.addPage('a4','landscape');header();};
 header();
 const uniqueEquipment=new Set(rows.map(row=>`${row.documentNumber}|${row.serial}`)).size,uniquePeople=new Set(rows.map(row=>row.actor).filter(Boolean)).size;
 const cards=[['MOVIMIENTOS',String(rows.length)],['EQUIPOS INVOLUCRADOS',String(uniqueEquipment)],['RESPONSABLES',String(uniquePeople)]];
 cards.forEach(([label,value],index)=>{const x=margin+index*55;pdf.setFillColor(238,247,250);pdf.setDrawColor(47,137,166);pdf.roundedRect(x,y,50,17,2,2,'FD');pdf.setFont('helvetica','bold');pdf.setFontSize(13);pdf.setTextColor(9,91,119);pdf.text(value,x+4,y+7);pdf.setFontSize(6);pdf.setTextColor(67,94,108);pdf.text(label,x+4,y+13);});
 y+=23;tableHeader();
 if(!rows.length){pdf.setFillColor(246,249,251);pdf.rect(margin,y,contentWidth,18,'F');pdf.setFont('helvetica','normal');pdf.setFontSize(9);pdf.setTextColor(69,91,104);pdf.text(emptyMessage,margin+4,y+10);return new Uint8Array(pdf.output('arraybuffer'));}
 rows.forEach((row,index)=>{
  const cells=[`${santoDomingoDateKey(row.occurredAt)}\n${row.time}`,row.action,row.documentNumber,`${row.equipment}\n${row.serial}`,`${row.actor}${row.login?`\n${row.login}`:''}`,`${row.department} · ${row.agency}\n${row.detail}`];
  const lines=cells.map((cell,i)=>pdf.splitTextToSize(cell,columns[i].width-3));const rowHeight=Math.max(9,...lines.map(value=>value.length*3.2+3));
  if(y+rowHeight>height-15){newPage();tableHeader();}
  pdf.setFillColor(index%2?248:239,index%2?251:247,index%2?252:250);pdf.rect(margin,y,contentWidth,rowHeight,'F');let x=margin;
  pdf.setFont('helvetica','normal');pdf.setFontSize(6.4);pdf.setTextColor(31,55,69);lines.forEach((line,i)=>{pdf.text(line,x+1.5,y+4);x+=columns[i].width;});y+=rowHeight;
 });
 return new Uint8Array(pdf.output('arraybuffer'));
}
