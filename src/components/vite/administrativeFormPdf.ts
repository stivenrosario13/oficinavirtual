

import {administrativeFormSealSpaces} from './administrativeSealLayout';

export type AdministrativePdfField={label:string;value:string};
export type AdministrativePdfTable={title:string;headers:string[];rows:string[][];widths?:number[]};
export type AdministrativePdfSection={title:string;text:string};
export type AdministrativeFormPdfInput={
  number:string;title:string;subtitle:string;department:string;createdBy:string;createdByLogin:string;
  responsible:string;target:string;fields:AdministrativePdfField[];tables?:AdministrativePdfTable[];notes?:string;
  bodySections?:AdministrativePdfSection[];
  registrationOnly?:boolean;detailsOnlyAfterFirstPage?:boolean;hideCustodySpaces?:boolean;signatureLabels?:string[];createdAt?:Date;logoData:string;origin?:string;
};

export async function buildAdministrativeFormPdf(input:AdministrativeFormPdfInput){
 const [{jsPDF},{default:QRCode}]=await Promise.all([import('jspdf'),import('qrcode')]);
  const pdf=new jsPDF({unit:'mm',format:'a4',compress:true});
  const navy:[number,number,number]=[12,32,51],cyan:[number,number,number]=[22,152,191],blue:[number,number,number]=[7,87,166],ink:[number,number,number]=[24,45,62],muted:[number,number,number]=[91,111,126],line:[number,number,number]=[190,207,218],pale:[number,number,number]=[239,247,251];
  const created=input.createdAt||new Date(),time=created.toLocaleString('es-DO',{timeZone:'America/Santo_Domingo',hour12:false});
  const qr=await QRCode.toDataURL(`${input.origin||'https://realagencias.runasp.net'}/api/institutional-documents/lookup/${encodeURIComponent(input.number)}`,{errorCorrectionLevel:'M',margin:1,width:500});
  let page=0,y=0;
  const color=(value:[number,number,number])=>pdf.setTextColor(...value);
  const split=(value:string,width:number,max=8)=>(pdf.splitTextToSize(value||'No registrado',width) as string[]).slice(0,max);
  const brand=()=>{pdf.addImage(input.logoData,'PNG',15,10,17,6.4,undefined,'FAST');pdf.setFont('helvetica','bold');pdf.setFontSize(14);color(blue);pdf.text('Grupo Tejeda',36,15);pdf.setFontSize(6.8);pdf.text('OFICINA VIRTUAL',36,20,{charSpace:.6});};
  const footer=()=>{pdf.setFillColor(...pale);pdf.rect(0,282,210,15,'F');pdf.setFont('helvetica','normal');pdf.setFontSize(5.6);color(muted);pdf.text(`Trazabilidad: REAL-ADMINISTRACION|${input.number}`,12,288);pdf.text(`Generado: ${time}`,198,288,{align:'right'});pdf.setFont('helvetica','bold');pdf.setFontSize(5.2);color(navy);pdf.text('Documento de control interno. Las firmas y sellos quedan vinculados al historial institucional.',105,293,{align:'center'});};
  const newPage=(continuation=false)=>{if(page)footer();if(page)pdf.addPage();page+=1;pdf.setFillColor(...cyan);pdf.rect(0,0,210,2.5,'F');brand();if(!continuation)pdf.addImage(qr,'PNG',176,8,18,18,undefined,'FAST');pdf.setDrawColor(...line);pdf.line(12,31,198,31);if(continuation){pdf.setFont('helvetica','bold');pdf.setFontSize(7);color(cyan);pdf.text('CONTINUACIÓN DEL FORMULARIO',12,40);pdf.setFontSize(11);color(navy);pdf.text(input.title,12,48);pdf.setFont('helvetica','normal');pdf.setFontSize(6.5);color(muted);pdf.text(input.number,12,54);y=63;}else{pdf.setFillColor(...navy);pdf.roundedRect(12,36,186,29,2,2,'F');pdf.setFont('helvetica','bold');pdf.setFontSize(6.5);pdf.setTextColor(135,217,238);pdf.text(input.registrationOnly?'REGISTRO DIARIO DE GASOLINA':'SOLICITUD ADMINISTRATIVA',17,43);pdf.setFontSize(13);pdf.setTextColor(255,255,255);pdf.text(split(input.title.toUpperCase(),112,2),17,51);pdf.setFont('helvetica','normal');pdf.setFontSize(6.7);pdf.setTextColor(190,222,238);pdf.text(split(input.subtitle,112,1),17,61);pdf.setDrawColor(60,89,111);pdf.line(143,40,143,61);pdf.setFont('helvetica','bold');pdf.setFontSize(5.5);pdf.setTextColor(135,217,238);pdf.text('N.º DE FORMULARIO',149,43);pdf.setFontSize(6.7);pdf.setTextColor(255,255,255);pdf.text(split(input.number,43,2),149,49);pdf.setFontSize(5);pdf.setTextColor(135,217,238);pdf.text('FECHA EFECTIVA',149,56);pdf.setFont('helvetica','normal');pdf.setFontSize(5.8);pdf.setTextColor(255,255,255);pdf.text(time,149,61);y=72;}};
  const contentBottom=()=>page===1&&!input.registrationOnly?170:276;
  const need=(height:number)=>{if(y+height>contentBottom())newPage(true)};
  const section=(title:string)=>{need(12);pdf.setFillColor(...pale);pdf.roundedRect(12,y,186,7.5,1.2,1.2,'F');pdf.setFont('helvetica','bold');pdf.setFontSize(7);color(navy);pdf.text(title,16,y+5);y+=12;};
  let fieldRowHeight=14;
  const field=(item:AdministrativePdfField,index:number,next?:AdministrativePdfField)=>{const width=86,x=index%2===0?16:108;const lines=split(item.value,width,4),height=Math.max(14,lines.length*4+9);if(index%2===0){fieldRowHeight=Math.max(height,next?split(next.value,width,4).length*4+9:14);need(fieldRowHeight);}pdf.setFont('helvetica','bold');pdf.setFontSize(5.8);color(muted);pdf.text(item.label.toUpperCase(),x,y);pdf.setFont('helvetica','normal');pdf.setFontSize(7.8);color(ink);pdf.text(lines,x,y+5);pdf.setDrawColor(...line);pdf.line(x,y+height-3,x+86,y+height-3);if(index%2===1)y+=fieldRowHeight;return height;};
  newPage();section(input.registrationOnly?'01 / REGISTRO DE CONSUMO':'01 / CONTROL Y ENRUTAMIENTO');
  const control=[{label:input.registrationOnly?'Departamento':'Departamento solicitante',value:input.department},{label:'Creado por',value:input.createdBy},{label:input.registrationOnly?'Responsable del registro':'Responsable de recepción',value:input.responsible},{label:'Destino o beneficiario',value:input.target}];
  for(let index=0;index<control.length;index++)field(control[index],index,control[index+1]);if(control.length%2)y+=fieldRowHeight;
  section(input.registrationOnly?'02 / DATOS DEL REGISTRO':'02 / DATOS DE LA SOLICITUD');
  for(let index=0;index<input.fields.length;index++)field(input.fields[index],index,input.fields[index+1]);if(input.fields.length%2)y+=fieldRowHeight;
  for(const table of input.tables||[]){
    need(29);section(table.title);const widths=table.widths||table.headers.map(()=>186/table.headers.length);
    const header=()=>{pdf.setFillColor(...navy);pdf.rect(12,y,186,8,'F');let x=12;pdf.setFont('helvetica','bold');pdf.setFontSize(5.6);pdf.setTextColor(255,255,255);table.headers.forEach((label,index)=>{pdf.text(split(label,widths[index]-4,2),x+2,y+5);x+=widths[index]});y+=8;};
    need(20);header();
    for(const row of table.rows){
      pdf.setFont('helvetica','normal');pdf.setFontSize(6.2);
      const cells=row.map((value,index)=>pdf.splitTextToSize(value||'No registrado',widths[index]-4) as string[]);
      const height=Math.max(9,...cells.map(lines=>lines.length*3.5+4));
      if(y+height>contentBottom()){newPage(true);section(table.title+' (continuación)');header();}
      let x=12;pdf.setDrawColor(...line);pdf.rect(12,y,186,height);pdf.setFont('helvetica','normal');pdf.setFontSize(6.2);color(ink);
      cells.forEach((lines,index)=>{if(index)pdf.line(x,y,x,y+height);pdf.text(lines,x+2,y+5);x+=widths[index]});y+=height;
    }
  }
  for(const body of input.bodySections||[]){section(body.title);const lines=pdf.splitTextToSize(body.text||'No registrado',176) as string[];for(let offset=0;offset<lines.length;){const available=Math.max(1,Math.floor((contentBottom()-y-8)/4));if(available<2){newPage(true);continue;}const chunk=lines.slice(offset,offset+available),height=chunk.length*4+7;pdf.setFont('helvetica','normal');pdf.setFontSize(7.4);color(ink);pdf.text(chunk,17,y+5);y+=height;offset+=chunk.length;if(offset<lines.length)newPage(true);}}
  // Incident reports reserve every workflow stamp on page one. Continuation
  // pages contain only ticket details and the institutional footer.
  if(!input.detailsOnlyAfterFirstPage){
    pdf.setFont('helvetica','normal');pdf.setFontSize(7.6);
    const noteLines=pdf.splitTextToSize(input.notes||'Sin observaciones adicionales.',81) as string[];
    if(noteLines.length>6){
      section('03 / MOTIVO Y OBSERVACIONES');
      const fullLines=pdf.splitTextToSize(input.notes||'',176) as string[];
      for(const text of fullLines){need(5);pdf.setFont('helvetica','normal');pdf.setFontSize(7.6);color(ink);pdf.text(text,17,y+4);y+=5;}
      y+=4;
    }
    need(95);section(input.registrationOnly?'03 / OBSERVACIONES':'03 / OBSERVACIONES Y RECEPCIÓN');
    pdf.setDrawColor(...line);pdf.roundedRect(12,y,186,35,2,2);
    pdf.setFont('helvetica','normal');pdf.setFontSize(7.6);color(ink);
    pdf.text(noteLines.length>6?'Motivo y observaciones detallados arriba.':noteLines,17,y+7);
    y+=37;
    const signatures=input.registrationOnly?['Registrado por']:input.signatureLabels||['Firma de quien solicita','Firma de quien autoriza','Firma de quien recibe'];section('04 / IDENTIDAD Y FIRMAS');need(34);const gap=4,width=(186-gap*2)/3;signatures.slice(0,3).forEach((label,index)=>{const x=12+index*(width+gap);pdf.setDrawColor(...line);pdf.roundedRect(x,y,width,24,2,2);pdf.line(x+5,y+15,x+width-5,y+15);pdf.setFont('helvetica','bold');pdf.setFontSize(5.7);color(navy);pdf.text(split(label.toUpperCase(),width-8,2),x+4,y+21)});pdf.setFont('helvetica','normal');pdf.setFontSize(5.4);color(muted);pdf.text(split(`${input.createdBy} · ${input.createdByLogin}`,180,1),12,y+31);
  }
  footer();
  const sealSpaces=input.hideCustodySpaces?{status:administrativeFormSealSpaces.status}:administrativeFormSealSpaces;
  if(!input.registrationOnly){pdf.setPage(1);pdf.setFillColor(...pale);pdf.roundedRect(12,174,186,7.5,1.2,1.2,'F');pdf.setFont('helvetica','bold');pdf.setFontSize(7);color(navy);pdf.text(input.hideCustodySpaces?'CONTROL DE RECEPCIÓN / AUTORIZACIÓN':'CONTROL DE AUTORIZACIÓN, ENTREGA Y RECEPCIÓN',16,179);
  for(const [key,box] of Object.entries(sealSpaces)){
    pdf.setDrawColor(...line);pdf.roundedRect(box.x,box.top,box.width,box.height,2,2);
    pdf.setFontSize(6);color(muted);pdf.text(key==='status'?'SELLO DE RECEPCIÓN / AUTORIZACIÓN':key==='delivery'?'SELLO DE ENTREGA':'SELLO DE RECIBIDO',box.x+box.width/2,box.top+box.height/2,{align:'center'});
  }
  }
  const markers=input.registrationOnly?'|REGISTRATION_ONLY=1':Object.entries(sealSpaces).map(([key,b])=>`|${key==='status'?'ADMIN_SEAL':key==='delivery'?'ADMIN_DELIVERY':'ADMIN_RECEIPT'}=1,${b.x},${b.top},${b.width},${b.height}`).join('');
  for(let current=1;current<=pdf.getNumberOfPages();current++){pdf.setPage(current);pdf.setFont('helvetica','normal');pdf.setFontSize(5);color(muted);pdf.text(`${current} / ${pdf.getNumberOfPages()}`,198,279,{align:'right'});}pdf.setProperties({title:`${input.title} ${input.number}`,subject:`REAL-ADMINISTRATIVE-FORM-V376|RECEIPT_SIGNATURE_PAGE=${page}${markers}`,author:'REAL - Grupo Tejeda',creator:'Oficina Virtual'});return pdf.output('arraybuffer');
}
