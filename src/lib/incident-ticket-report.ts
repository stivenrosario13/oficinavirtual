import {realLogoData} from '@/components/vite/institutionalDocumentAssets';

export type IncidentTicketReportRow={documentId:number;documentNumber:string;ticketDate:string;group:string;agencyCode:string;agency:string;ticketCode:string;agentContact:string;status:string;compensationAmount?:number|null;supervisor:string;subject:string};
export type IncidentTicketGroup={group:string;total:number;approved:number;rejected:number;pending:number;compensation:number};

const approved=(status:string)=>status.toUpperCase()==='APPROVED';
const rejected=(status:string)=>status.toUpperCase()==='REJECTED';
export const incidentStatusLabel=(status:string)=>approved(status)?'Aprobado':rejected(status)?'Rechazado':'Pendiente';
export const incidentTicketGroups=(rows:IncidentTicketReportRow[])=>{
 const groups=new Map<string,IncidentTicketGroup>();
 for(const row of rows){const key=row.group||'Sin grupo',group=groups.get(key)||{group:key,total:0,approved:0,rejected:0,pending:0,compensation:0};group.total++;if(approved(row.status)){group.approved++;group.compensation+=Number(row.compensationAmount)||0}else if(rejected(row.status))group.rejected++;else group.pending++;groups.set(key,group)}
 return [...groups.values()].sort((a,b)=>a.group.localeCompare(b.group,'es'));
};
export const incidentTicketHeaders=['Fecha','Grupo','Agencia','Ticket','Contacto','Estado','Monto de compensación RD$','Supervisor','Informe'];
export const incidentTicketCells=(row:IncidentTicketReportRow)=>([row.ticketDate.slice(0,10),row.group,`${row.agencyCode} · ${row.agency}`,row.ticketCode,row.agentContact,incidentStatusLabel(row.status),approved(row.status)?Number(row.compensationAmount||0).toFixed(2):'—',row.supervisor,row.documentNumber] as (string|number)[]);
const filename=(from:string,to:string)=>`Tickets_de_incidentes_por_grupo_${from}_${to}`;
function download(blob:Blob,name:string){const url=URL.createObjectURL(blob),anchor=document.createElement('a');anchor.href=url;anchor.download=name;anchor.click();setTimeout(()=>URL.revokeObjectURL(url),60000)}

export async function exportIncidentTicketsExcel(rows:IncidentTicketReportRow[],from:string,to:string){
 const {Workbook}=await import('exceljs'),workbook=new Workbook();workbook.creator='Grupo Tejeda';const logo=await realLogoData(),image=workbook.addImage({base64:logo,extension:'png'}),groups=incidentTicketGroups(rows);
 const summary=workbook.addWorksheet('Resumen por grupo',{views:[{state:'frozen',ySplit:6,showGridLines:false}]});summary.columns=[34,16,16,16,16,25].map(width=>({width}));summary.addImage(image,{tl:{col:0,row:0},br:{col:0.9,row:1.2},editAs:'oneCell'} as unknown as import('exceljs').ImageRange);summary.mergeCells('B1:F1');summary.getCell('B1').value='Grupo Tejeda · Tickets de incidentes por grupo';summary.getCell('B1').font={size:18,bold:true,color:{argb:'FF123D59'}};summary.mergeCells('A3:F3');summary.getCell('A3').value=`Período: ${from} al ${to}`;summary.getRow(6).values=['Grupo','Total','Aprobados','Rechazados','Pendientes','Compensación RD$'];groups.forEach((group,index)=>summary.getRow(7+index).values=[group.group,group.total,group.approved,group.rejected,group.pending,group.compensation]);summary.getColumn(6).numFmt='#,##0.00';
 const detail=workbook.addWorksheet('Detalle',{views:[{state:'frozen',ySplit:4,showGridLines:false}]});detail.columns=[14,28,42,22,25,18,26,28,30].map(width=>({width}));detail.mergeCells(1,1,1,incidentTicketHeaders.length);detail.getCell(1,1).value='TICKETS DE INCIDENTES POR GRUPO';detail.mergeCells(2,1,2,incidentTicketHeaders.length);detail.getCell(2,1).value=`${from} al ${to}`;detail.getRow(4).values=incidentTicketHeaders;rows.forEach((row,index)=>detail.getRow(5+index).values=incidentTicketCells(row));detail.getColumn(7).numFmt='#,##0.00';
 for(const sheet of [summary,detail]){const header=sheet===summary?6:4;sheet.getRow(header).eachCell(cell=>{cell.fill={type:'pattern',pattern:'solid',fgColor:{argb:'FF176A83'}};cell.font={bold:true,color:{argb:'FFFFFFFF'}};cell.alignment={wrapText:true}});sheet.eachRow((row,index)=>{if(index>header){row.alignment={wrapText:true,vertical:'top'};row.height=25}});sheet.pageSetup={orientation:'landscape',fitToPage:true,fitToWidth:1,fitToHeight:0}}
 const bytes=await workbook.xlsx.writeBuffer();download(new Blob([new Uint8Array(bytes)],{type:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'}),filename(from,to)+'.xlsx');
}

export async function exportIncidentTicketsPdf(rows:IncidentTicketReportRow[],from:string,to:string){
 const {jsPDF}=await import('jspdf'),pdf=new jsPDF({orientation:'landscape',unit:'mm',format:'a3'}),logo=await realLogoData(),groups=incidentTicketGroups(rows),margin=12,bottom=280;let y=12;
 const header=(title:string)=>{pdf.addImage(logo,'PNG',margin,y,24,9);pdf.setFont('helvetica','bold');pdf.setTextColor('#123D59');pdf.setFontSize(16);pdf.text(title,42,y+6);y+=17;pdf.setFontSize(9);pdf.text(`${from} al ${to} · Acceso restringido a Yessica Mercedes y Administrador principal`,margin,y);y+=9};
 const table=(headers:string[],data:(string|number)[][],widths:number[])=>{const head=()=>{let x=margin;headers.forEach((value,index)=>{pdf.setFillColor('#176A83');pdf.setTextColor('#FFFFFF');pdf.rect(x,y,widths[index],11,'F');pdf.setFontSize(7);pdf.text(pdf.splitTextToSize(value,widths[index]-3),x+1.5,y+4);x+=widths[index]});y+=11};head();for(const row of data){if(y+12>bottom){pdf.addPage();y=12;head()}let x=margin;const lines=row.map((value,index)=>pdf.splitTextToSize(String(value),widths[index]-3) as string[]),height=Math.max(8,Math.max(...lines.map(value=>value.length))*3.2+3);lines.forEach((value,index)=>{pdf.setDrawColor('#D6E1E8');pdf.setTextColor('#243746');pdf.rect(x,y,widths[index],height);pdf.text(value,x+1.5,y+4);x+=widths[index]});y+=height}};
 header('TICKETS DE INCIDENTES POR GRUPO');table(['Grupo','Total','Aprobados','Rechazados','Pendientes','Compensación RD$'],groups.map(group=>[group.group,group.total,group.approved,group.rejected,group.pending,group.compensation.toFixed(2)]),[105,45,45,45,45,70]);pdf.addPage();y=12;header('DETALLE DE TICKETS DE INCIDENTES');table(incidentTicketHeaders,rows.map(incidentTicketCells),[22,35,58,29,36,25,40,38,48]);
 for(let page=1;page<=pdf.getNumberOfPages();page++){pdf.setPage(page);pdf.setFontSize(7);pdf.setTextColor('#526F80');pdf.text(`Página ${page} de ${pdf.getNumberOfPages()}`,390,290,{align:'right'})}pdf.save(filename(from,to)+'.pdf');
}
