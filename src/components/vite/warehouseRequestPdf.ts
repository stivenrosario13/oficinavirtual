import { apiFetch as fetch } from "@/lib/api-fetch";



export type WarehouseRequestLineKind="EQUIPMENT"|"COMPONENT"|"RETURN";
export type WarehouseRequestPdfLine={kind:WarehouseRequestLineKind;name:string;quantity:number;serial:string;note:string};
export type WarehouseRequestPdfInput={
  number:string;department:string;agency:{codigo:string;terminal:string;grupo:string};installer?:string;
  priority:string;notes:string;lines:WarehouseRequestPdfLine[];createdBy:string;createdByLogin:string;
  createdAt?:Date;logoData?:string;origin?:string;destination?:'Almacén'|'Administración'|'Taller';
  workshop?:{title?:string;handoff?:boolean;status:string;receivedBy:string;receivedAt:string;work:string};
  internetDetails?:{serviceKind:string;serviceNumber:string;company:string;modemQuantity:number;itemKind:string;reason:string};
};

const departmentNames:Record<string,string>={TECHNOLOGY:"Tecnología",GENERAL_SERVICES:"Servicios Generales"};
const kindNames:Record<WarehouseRequestLineKind,string>={EQUIPMENT:"Equipo",COMPONENT:"Componente",RETURN:"Devolución"};
const priorityNames:Record<string,string>={LOW:"Baja",MEDIUM:"Media",HIGH:"Alta",CRITICAL:"Crítica"};

async function loadLogo(){
  const response=await fetch("/loto-real-logo-transparent.png");
  if(!response.ok)throw new Error("No se pudo cargar el logo institucional REAL.");
  const blob=await response.blob();
  return await new Promise<string>((resolve,reject)=>{const reader=new FileReader();reader.onload=()=>resolve(String(reader.result));reader.onerror=reject;reader.readAsDataURL(blob)});
}

export async function buildWarehouseRequestPdf(input:WarehouseRequestPdfInput){
  return (await buildWarehouseRequestDocument(input)).output('arraybuffer');
}

export async function buildWarehouseRequestDocument(input:WarehouseRequestPdfInput){
 const [{jsPDF},{default:QRCode}]=await Promise.all([import('jspdf'),import('qrcode')]);
  const logo=input.logoData||await loadLogo(),pdf=new jsPDF({unit:"mm",format:"a4",compress:true});
  const navy:[number,number,number]=[12,32,51],cyan:[number,number,number]=[22,152,191],blue:[number,number,number]=[7,87,166],ink:[number,number,number]=[24,45,62],muted:[number,number,number]=[91,111,126],line:[number,number,number]=[190,207,218];
  const created=input.createdAt||new Date(),department=departmentNames[input.department]||input.department,destination=input.destination||'Almacén',administrative=destination==='Administración',workshop=destination==='Taller'&&!!input.workshop;
  const time=created.toLocaleString("es-DO",{timeZone:"America/Santo_Domingo",hour12:false});
  const textColor=(color:[number,number,number])=>pdf.setTextColor(...color);
  const split=(value:string,width:number,max=2)=>{const rows=pdf.splitTextToSize(value||"No registrado",width) as string[];return rows.slice(0,max)};
  const brand=(x:number,y:number,width:number)=>{
    const h=width*71/250;
    pdf.addImage(logo,"PNG",x+width*.075,y+h*.31,width*.25,width*.25*60/160,undefined,"FAST");
    pdf.setFont("helvetica","bold");pdf.setFontSize(width*.17);textColor(blue);pdf.text("Grupo Tejeda",x+width*.37,y+h*.47);
    pdf.setFontSize(width*.075);pdf.text("OFICINA VIRTUAL",x+width*.37,y+h*.70,{charSpace:width*.004});
  };
  const section=(title:string,y:number)=>{pdf.setFillColor(239,247,251);pdf.roundedRect(12,y,186,7.5,1.2,1.2,"F");pdf.setFont("helvetica","bold");pdf.setFontSize(7.5);textColor(navy);pdf.text(title,16,y+5.1)};
  const cell=(label:string,value:string,x:number,y:number,width:number,size=8.3,max=2)=>{pdf.setFont("helvetica","bold");pdf.setFontSize(6.4);textColor(muted);pdf.text(label.toUpperCase(),x,y);pdf.setFont("helvetica","normal");pdf.setFontSize(size);textColor(ink);pdf.text(split(value,width,max),x,y+4.8)};
  const emptyCell=(label:string,x:number,y:number,width:number)=>{pdf.setFont("helvetica","bold");pdf.setFontSize(6.4);textColor(muted);pdf.text(label.toUpperCase(),x,y);pdf.setDrawColor(...line);pdf.line(x,y+6,x+width,y+6)};
  const trace=`REAL-${workshop?'TALLER':administrative?'ADMINISTRACION':'ALMACEN'}|${input.number}`;
  const qrTarget=administrative?`${input.origin||"https://realagencias.runasp.net"}/api/institutional-documents/lookup/${encodeURIComponent(input.number)}`:`${input.origin||"https://realagencias.runasp.net"}/?equipment=${encodeURIComponent(`REAL-MNT|${input.number}`)}`;
  const qr=await QRCode.toDataURL(qrTarget,{errorCorrectionLevel:"M",margin:1,width:500});

  pdf.setProperties({title:`Formulario ${input.number}`,subject:workshop?"REAL-WORKSHOP-WAREHOUSE-FORM-V418":administrative?"REAL-ADMINISTRATIVE-REQUEST-V372|RECEIPT_SIGNATURE_PAGE=1|ADMIN_SEAL=1,110,194,87,33|ADMIN_DELIVERY=1,12,242,90,36|ADMIN_RECEIPT=1,108,242,90,36":"REAL-WAREHOUSE-REQUEST-V319|RECEIPT_SIGNATURE_PAGE=1",author:"REAL - Grupo Tejeda",creator:"Oficina Virtual"});
  pdf.setFillColor(...cyan);pdf.rect(0,0,210,2.5,"F");brand(12,8,68);pdf.setDrawColor(...line);pdf.line(87,9,87,28);
  pdf.setFont("helvetica","bold");pdf.setFontSize(7);textColor(cyan);pdf.text("CONTROL INSTITUCIONAL DE ACTIVOS",94,12);pdf.setFontSize(13);textColor(navy);pdf.text("CONDUCE / FORMULARIO",94,20);pdf.setFont("helvetica","normal");pdf.setFontSize(7.2);textColor(muted);pdf.text(workshop?"Entrada, recepción y seguimiento de equipos":"Solicitud, recepción y constancia de firma",94,26);pdf.setDrawColor(...line);pdf.line(12,33,198,33);

  pdf.setFillColor(...navy);pdf.roundedRect(12,37,186,23,2,2,"F");pdf.setFont("helvetica","bold");pdf.setFontSize(10.5);pdf.setTextColor(255,255,255);pdf.text(workshop?(input.workshop!.title||"REPARACIÓN"):administrative?"SOLICITUD A ADMINISTRACIÓN":"SOLICITUD A ALMACÉN",17,45);pdf.setFont("helvetica","normal");pdf.setFontSize(7);pdf.setTextColor(190,222,238);pdf.text(`${department} - ${workshop?'Taller':administrative?'Departamento Administrativo':'Almacén central'}`,17,52);pdf.setFont("helvetica","bold");pdf.setFontSize(6);pdf.setTextColor(135,217,238);pdf.text(workshop?`ESTADO: ${input.workshop!.status}`:`PRIORIDAD: ${priorityNames[input.priority]||input.priority}`,17,57);pdf.setDrawColor(60,89,111);pdf.line(136,41,136,56);pdf.setFont("helvetica","bold");pdf.setFontSize(6);pdf.setTextColor(135,217,238);pdf.text("N.º DE FORMULARIO",142,43);pdf.setFontSize(7);pdf.setTextColor(255,255,255);pdf.text(split(input.number,51,2),142,48);pdf.setFontSize(5);pdf.setTextColor(135,217,238);pdf.text("FECHA Y HORA EFECTIVA",142,53);pdf.setFont("helvetica","normal");pdf.setFontSize(6.5);pdf.setTextColor(255,255,255);pdf.text(time,142,57);

  section("01 / DATOS DE LA OPERACIÓN",65);cell("Área operativa",workshop?'Taller':administrative?'Administración':'Almacén central',16,78,40);cell("Departamento",department,60,78,49);cell("Destino",input.workshop?.handoff?"Almacén":destination,113,78,44);cell("Agencia / origen",`${input.agency.codigo} - ${input.agency.terminal}`,16,93,85);cell("Grupo",input.agency.grupo,106,93,50);pdf.addImage(qr,"PNG",166,73,26,26,undefined,"FAST");pdf.setFont("helvetica","bold");pdf.setFontSize(5.5);textColor(muted);pdf.text("CONSULTA Y TRAZABILIDAD",179,103,{align:"center"});

  section(workshop?"02 / EQUIPOS Y SERIALES":"02 / EQUIPOS SOLICITADOS",107);pdf.setFillColor(...navy);pdf.rect(12,117,186,7,"F");pdf.setFontSize(6.2);pdf.setTextColor(255,255,255);pdf.text("TIPO",16,121.5);pdf.text("EQUIPO / ARTÍCULO",38,121.5);pdf.text("CANT.",123,121.5);pdf.text("SERIAL / DETALLE",145,121.5);pdf.setDrawColor(...line);pdf.rect(12,124,186,18);for(const x of [34,119,141])pdf.line(x,124,x,142);
  const warehouseBreakdown=!administrative&&!workshop&&input.lines.length>1;
  const preview=warehouseBreakdown?[input.lines[0],{kind:"COMPONENT" as const,name:"Consultar el desglose de los demás artículos en la página 2",quantity:input.lines.slice(1).reduce((sum,row)=>sum+row.quantity,0),serial:"Detalle en página 2",note:""}]:input.lines.length<=2?input.lines:[input.lines[0],{kind:"COMPONENT" as const,name:administrative?`Ver anexo: ${input.lines.length} renglones completos`:`${input.lines.length} renglones: consultar en el sistema`,quantity:input.lines.reduce((sum,row)=>sum+row.quantity,0),serial:administrative?"Detalle en página 2":"Detalle en el sistema",note:""}];
  preview.forEach((row,index)=>{const y=130+index*8.5;pdf.setFont("helvetica","normal");pdf.setFontSize(warehouseBreakdown&&index===1?5.5:6.7);textColor(ink);const maxLines=workshop?(preview.length===1?4:2):1;pdf.text(warehouseBreakdown&&index===1?"Anexo":kindNames[row.kind],16,y);pdf.text(split(`${row.name}${row.note?` - ${row.note}`:""}`,77,maxLines),38,y);pdf.text(String(row.quantity),130,y,{align:"center"});pdf.text(split(row.serial||"Pendiente / no aplica",51,maxLines),145,y)});

  section("03 / DATOS DE LA SOLICITUD",147);cell("Persona que solicita",input.createdBy,16,159,82,8,1);if(workshop)cell("Persona que recibe",input.workshop!.receivedBy||"Pendiente de recibir",112,159,80,8,1);else emptyCell(`Persona que recibe en ${destination}`,112,159,80);cell("Departamento solicitante",department,16,172,82,7.6,1);if(workshop)cell("Recepción registrada",input.workshop!.receivedAt||"Pendiente de recibir",112,172,80,7.6,1);else emptyCell("Recepción registrada",112,172,80);

  section("04 / MOTIVO, OBSERVACIONES Y RECEPCIÓN",183);pdf.setDrawColor(...line);pdf.roundedRect(12,193,91,35,2,2);cell("Motivo / observaciones",input.notes||`Solicitud de ${input.lines.length} renglón(es) para la agencia indicada.`,16,199,82,7.5,5);pdf.roundedRect(109,193,89,35,2,2);

  if(workshop&&input.workshop!.handoff){
    section("05 / ENTREGA DE TALLER Y RECEPCIÓN EN ALMACÉN",230);
  }else if(workshop){
    section("05 / TRABAJO REALIZADO Y RESULTADO",232);
    pdf.setDrawColor(...line);pdf.roundedRect(12,242,186,24,2,2);
    cell("Diagnóstico / piezas / pruebas",input.workshop!.work||"Pendiente de registrar trabajo.",16,248,176,7.2,3);
  }else{
  section(administrative?"05 / SELLOS DE ENTREGA Y RECEPCIÓN":"05 / IDENTIDAD DE ENTREGA Y FIRMA DE RECEPCIÓN",232);
  const signatureBox=(x:number,label:string)=>{pdf.setDrawColor(...line);pdf.roundedRect(x,242,90,administrative?36:24,2,2);pdf.setFont("helvetica","bold");pdf.setFontSize(6.7);textColor(navy);pdf.text(label,x+4,248);pdf.setDrawColor(...line);pdf.line(x+7,260,x+83,260)};
  signatureBox(12,administrative?"ESPACIO PARA EL SELLO DE ENTREGA":"FIRMA DE QUIEN SOLICITA");signatureBox(108,administrative?"ESPACIO PARA EL SELLO DE RECIBIDO":"FIRMA DE QUIEN RECIBE");
  }
  if(!administrative&&!input.workshop?.handoff){pdf.setFont("helvetica","bold");pdf.setFontSize(6.5);textColor(ink);pdf.text(split(`Solicitado por: ${input.createdBy}`,87,1),12,271);pdf.setFont("helvetica","normal");pdf.setFontSize(5.8);textColor(muted);pdf.text(split(`Usuario: ${input.createdByLogin}`,87,1),12,275);}

  pdf.setFillColor(239,247,251);pdf.rect(0,282,210,15,"F");pdf.setFont("helvetica","normal");pdf.setFontSize(5.8);textColor(muted);pdf.text(split(`Trazabilidad: ${trace}`,119,1),12,287);pdf.text(`Generado: ${time}`,198,287,{align:"right"});pdf.text(split(`Registrado por: ${input.createdBy} - ${input.createdByLogin}`,180,1),12,290);pdf.setFont("helvetica","bold");pdf.setFontSize(5.5);textColor(navy);pdf.text(workshop?"Documento de control interno. Consulta el QR para verificar la etapa actual del equipo.":"Documento de control interno. No es factura fiscal. La firma dibujada no es un certificado digital.",105,294,{align:"center"});

  if(administrative&&input.lines.length>2||warehouseBreakdown){
    pdf.addPage();pdf.setFillColor(...cyan);pdf.rect(0,0,210,2.5,"F");brand(12,8,68);pdf.setFont("helvetica","bold");pdf.setFontSize(12);textColor(navy);pdf.text("ANEXO - DETALLE COMPLETO DE LA SOLICITUD",94,18);pdf.setFontSize(7);textColor(muted);pdf.text(input.number,94,25);let y=39;
    const header=()=>{pdf.setFillColor(...navy);pdf.rect(12,y,186,8,"F");pdf.setTextColor(255,255,255);pdf.setFontSize(6.3);pdf.text("TIPO",16,y+5);pdf.text("EQUIPO / COMPONENTE / DEVOLUCIÓN",40,y+5);pdf.text("CANT.",131,y+5);pdf.text("SERIAL / DETALLE",150,y+5);y+=8};header();
    for(const row of input.lines){const description=pdf.splitTextToSize(`${row.name}${row.note?` - ${row.note}`:""}`,82) as string[],serial=pdf.splitTextToSize(row.serial||"Pendiente / no aplica",42) as string[],height=Math.max(12,Math.max(description.length,serial.length)*4+5);if(y+height>275){pdf.addPage();brand(12,8,55);y=31;header()}pdf.setDrawColor(...line);pdf.rect(12,y,186,height);pdf.setFont("helvetica","normal");pdf.setFontSize(7);textColor(ink);pdf.text(kindNames[row.kind],16,y+6);pdf.text(description,40,y+6);pdf.text(String(row.quantity),136,y+6,{align:"center"});pdf.text(serial,150,y+6);y+=height}
    for(let page=2;page<=pdf.getNumberOfPages();page++){pdf.setPage(page);pdf.setFontSize(5.5);textColor(muted);pdf.text(`Formulario ${input.number} - ${input.createdBy}`,12,289);pdf.text(`${page} / ${pdf.getNumberOfPages()}`,198,289,{align:"right"})}
  }
  if(administrative&&input.internetDetails){
    const details=input.internetDetails;pdf.addPage();pdf.setFillColor(...cyan);pdf.rect(0,0,210,2.5,"F");brand(12,8,68);pdf.setDrawColor(...line);pdf.line(12,33,198,33);
    pdf.setFont("helvetica","bold");pdf.setFontSize(13);textColor(navy);pdf.text("ANEXO · EQUIPO DE INTERNET",12,44);pdf.setFontSize(7);textColor(muted);pdf.text(`Formulario ${input.number} · ${department} · ${time}`,12,51);
    section("01 / AGENCIA DE INSTALACIÓN",60);cell("Código",input.agency.codigo,16,73,45);cell("Agencia",input.agency.terminal,66,73,80);cell("Grupo",input.agency.grupo,16,88,110);
    section("02 / SERVICIO DE CONECTIVIDAD",104);cell("Tipo de servicio",details.serviceKind,16,117,75);cell("Número de internet, banda ancha o línea fija",details.serviceNumber,99,117,92);cell("Compañía",details.company,16,136,75);cell("Equipo o SIM",details.itemKind,99,136,75);cell("Cantidad de módems",String(details.modemQuantity),16,155,75);
    section("03 / MOTIVO DEL CAMBIO",170);pdf.setDrawColor(...line);pdf.roundedRect(12,181,186,52,2,2);pdf.setFont("helvetica","normal");pdf.setFontSize(8);textColor(ink);const reasonLines=pdf.splitTextToSize(details.reason,175) as string[];pdf.text(reasonLines.slice(0,10),17,188);
    pdf.setDrawColor(...line);pdf.line(15,261,91,261);pdf.line(119,261,195,261);pdf.setFont("helvetica","bold");pdf.setFontSize(7);textColor(navy);pdf.text("FIRMA DE QUIEN SOLICITA",15,266);pdf.text("FIRMA DE QUIEN RECIBE",119,266);pdf.setFont("helvetica","normal");pdf.setFontSize(6);textColor(muted);pdf.text(`Solicitado por ${input.createdBy} · ${input.createdByLogin}`,12,283);
    for(let offset=10;offset<reasonLines.length;offset+=46){pdf.addPage();pdf.setFillColor(...cyan);pdf.rect(0,0,210,2.5,"F");brand(12,8,68);pdf.setFont("helvetica","bold");pdf.setFontSize(12);textColor(navy);pdf.text("CONTINUACIÓN · MOTIVO DEL CAMBIO",12,43);pdf.setFont("helvetica","normal");pdf.setFontSize(8);textColor(ink);pdf.text(reasonLines.slice(offset,offset+46),12,54);pdf.setFontSize(6);textColor(muted);pdf.text(input.number,12,286)}
  }
  pdf.setPage(1);pdf.setFont("helvetica","normal");pdf.setFontSize(5);textColor(muted);pdf.text(`1 / ${pdf.getNumberOfPages()}`,198,280,{align:"right"});return pdf;
}
