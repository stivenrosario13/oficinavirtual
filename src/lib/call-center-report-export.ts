import {realLogoData} from '../components/vite/institutionalDocumentAssets';
import {agentDetailCells,agentDetailHeaders, type buildAgentReport} from './call-center-report';
type Report=ReturnType<typeof buildAgentReport>;
const headers=['Responsable','Total de casos','Resueltos','En proceso','% resuelto','Evidencias'];
const summary=(r:Report)=>r.agents.map(a=>[a.name,a.total,a.resolved,a.inProgress,`${Math.round(a.resolved/a.total*100)}%`,a.evidence]);
const period=(r:Report)=>`Mesa de Servicios · ${r.from} al ${r.to} · Horario de Santo Domingo`;
const filename=(r:Report)=>`Reporte_mantenimiento_por_agente_${r.from}_${r.to}`;
function download(blob:Blob,name:string){const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),60000);}
export async function exportAgentExcel(r:Report,category:(key:string)=>string){
 const {Workbook}=await import('exceljs');const w=new Workbook();w.creator='Grupo Tejeda';const logo=await realLogoData();const image=w.addImage({base64:logo,extension:'png'});
 function sheet(name:string,title:string,cols:string[],rows:(string|number)[][],headerRow:number,widths:number[]){
  const s=w.addWorksheet(name,{views:[{state:'frozen',ySplit:headerRow,showGridLines:false}],pageSetup:{orientation:'landscape',paperSize:9,fitToPage:true,fitToWidth:1,fitToHeight:0}});
  // ExcelJS accepts coordinate objects for two-cell anchors; its declarations require its internal Anchor class.
  s.columns=widths.map(width=>({width}));s.getRow(1).height=30;s.getCell(1,2).value='Grupo Tejeda';s.getCell(1,2).font={name:'Aptos',size:18,bold:true,color:{argb:'FF123D59'}};s.getCell(2,2).value='OFICINA VIRTUAL';s.addImage(image,{tl:{col:0,row:0},br:{col:85/(widths[0]*7+5),row:0.8},editAs:'oneCell'} as unknown as import('exceljs').ImageRange);s.mergeCells(3,1,3,cols.length);s.getCell(3,1).value=title;s.getCell(3,1).fill={type:'pattern',pattern:'solid',fgColor:{argb:'FF123D59'}};s.getCell(3,1).font={name:'Aptos',size:19,bold:true,color:{argb:'FFFFFFFF'}};s.getRow(3).height=36;
  s.mergeCells(4,1,4,cols.length);s.getCell(4,1).value=period(r);s.getRow(4).height=24;
  s.getRow(headerRow).values=cols; s.getRow(headerRow).height=30;
  s.getRow(headerRow).eachCell(c=>{c.fill={type:'pattern',pattern:'solid',fgColor:{argb:'FF176A83'}};c.font={name:'Aptos',bold:true,color:{argb:'FFFFFFFF'}};c.alignment={wrapText:true,vertical:'middle'};});
  rows.forEach((values,i)=>{const row=s.getRow(headerRow+i+1);row.values=values;row.height=Math.max(25,...values.map((v,j)=>Math.ceil(String(v).length/Math.max(8,widths[j]-3))*14+10));row.eachCell(c=>{c.font={name:'Aptos',size:11,color:{argb:'FF243746'}};c.alignment={wrapText:true,vertical:'top'};c.border={bottom:{style:'thin',color:{argb:'FFEAF0F5'}}};});});
  s.autoFilter={from:{row:headerRow,column:1},to:{row:headerRow,column:cols.length}};s.pageSetup.printTitlesRow=`${headerRow}:${headerRow}`;s.pageSetup.printArea=`A1:${s.getColumn(cols.length).letter}${Math.max(headerRow,headerRow+rows.length)}`;return s;
 }
 const s=sheet('Resumen por agente','MANTENIMIENTO · RESUMEN POR AGENTE',headers,summary(r),8,[34,20,18,18,18,18]);
 ['Total de casos','Resueltos','En proceso',...(r.cancelled?['Anulados']:[])].forEach((v,i)=>{const c=s.getCell(5,i+1);c.value=v;c.fill={type:'pattern',pattern:'solid',fgColor:{argb:'FFEAF0F5'}};s.getCell(6,i+1).value=[r.total,r.resolved,r.inProgress,r.cancelled][i];s.getCell(6,i+1).font={name:'Aptos',size:26,bold:true,color:{argb:'FF123D59'}};});s.getRow(6).height=38;
 r.agents.forEach((a,i)=>{s.getCell(i+9,5).value=a.resolved/a.total;s.getCell(i+9,5).numFmt='0%';});
 sheet('Detalle de casos','REPORTE DE MANTENIMIENTO POR AGENTE',agentDetailHeaders,r.rows.map(row=>agentDetailCells(row,category)),6,[12,40,28,24,18,15,28,25,25,18,15,28,65]);
 const bytes=await w.xlsx.writeBuffer();download(new Blob([new Uint8Array(bytes)],{type:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'}),filename(r)+'.xlsx');
}
export async function exportAgentPdf(r:Report,category:(key:string)=>string){
 const {jsPDF}=await import('jspdf');const logo=await realLogoData();const pdf=new jsPDF({orientation:'landscape',unit:'mm',format:'a3'});const margin=12,width=396,bottom=282;let y=12;
 function title(text:string){pdf.addImage(logo,'PNG',margin,y,23,8.6);pdf.setFont('helvetica','bold');pdf.setFontSize(15);pdf.setTextColor('#123D59');pdf.text('Grupo Tejeda',margin+29,y+4);pdf.setFontSize(8);pdf.text('OFICINA VIRTUAL',margin+29,y+9);y+=15;pdf.setFillColor('#123D59');pdf.rect(margin,y,width,14,'F');pdf.setTextColor('#FFFFFF');pdf.setFont('helvetica','bold');pdf.setFontSize(18);pdf.text(text,margin+4,y+9);y+=21;pdf.setTextColor('#243746');pdf.setFont('helvetica','normal');pdf.setFontSize(10);pdf.text(period(r),margin,y);y+=9;}
 function table(cols:string[],rows:(string|number)[][],widths:number[]){
  function head(){let x=margin;pdf.setFont('helvetica','bold');pdf.setFontSize(8);cols.forEach((c,i)=>{pdf.setFillColor('#176A83');pdf.rect(x,y,widths[i],12,'F');pdf.setTextColor('#FFFFFF');pdf.text(pdf.splitTextToSize(c,widths[i]-4),x+2,y+4);x+=widths[i];});y+=12;pdf.setFont('helvetica','normal');pdf.setTextColor('#243746');}
  head();for(const row of rows){const lines=row.map((v,i)=>pdf.splitTextToSize(String(v),widths[i]-4) as string[]);while(lines.some(a=>a.length)){
   if(y+8>bottom){pdf.addPage();y=12;head();}
   const maxLines=Math.max(...lines.map(a=>a.length)),capacity=Math.max(1,Math.floor((bottom-y-4)/3.5));
   if(maxLines>capacity&&maxLines*3.5+4<240&&y>25){pdf.addPage();y=12;head();continue;}
   const count=Math.min(maxLines,capacity),height=count*3.5+4;let x=margin;
   lines.forEach((a,i)=>{pdf.setDrawColor('#D6E1E8');pdf.rect(x,y,widths[i],height);pdf.text(a.splice(0,count),x+2,y+4,{lineHeightFactor:1.24});x+=widths[i];});y+=height;
  }}
 }
 title('MANTENIMIENTO · RESUMEN POR AGENTE');pdf.text(`Total de casos: ${r.total}    Resueltos: ${r.resolved}    En proceso: ${r.inProgress}${r.cancelled?`    Anulados: ${r.cancelled}`:''}`,margin,y);y+=9;
 table(headers,summary(r),[126,54,54,54,54,54]);pdf.addPage();y=12;title('REPORTE DE MANTENIMIENTO POR AGENTE');
 table(agentDetailHeaders,r.rows.map(row=>agentDetailCells(row,category)),[15,44,29,25,22,20,29,29,29,19,18,32,85]);
 for(let n=1;n<=pdf.getNumberOfPages();n++){pdf.setPage(n);pdf.setFontSize(8);pdf.setTextColor('#243746');pdf.text(`${r.from} al ${r.to} · Página ${n} de ${pdf.getNumberOfPages()}`,margin,290);}
 pdf.save(filename(r)+'.pdf');
}
