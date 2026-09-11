import {jsPDF} from "jspdf";
import QRCode from "qrcode";

export type WarehouseRequestLineKind="EQUIPMENT"|"COMPONENT"|"RETURN";
export type WarehouseRequestPdfLine={kind:WarehouseRequestLineKind;name:string;quantity:number;serial:string;note:string};
export type WarehouseRequestPdfInput={
  number:string;department:string;agency:{codigo:string;terminal:string;grupo:string};installer:string;
  priority:string;notes:string;lines:WarehouseRequestPdfLine[];createdBy:string;createdByLogin:string;
  createdAt?:Date;logoData?:string;origin?:string;
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
  const logo=input.logoData||await loadLogo(),pdf=new jsPDF({unit:"mm",format:"a4",compress:true});
  const navy:[number,number,number]=[12,32,51],cyan:[number,number,number]=[22,152,191],blue:[number,number,number]=[7,87,166],ink:[number,number,number]=[24,45,62],muted:[number,number,number]=[91,111,126],line:[number,number,number]=[190,207,218];
  const created=input.createdAt||new Date(),department=departmentNames[input.department]||input.department;
  const time=created.toLocaleString("es-DO",{timeZone:"America/Santo_Domingo",hour12:false});
  const textColor=(color:[number,number,number])=>pdf.setTextColor(...color);
  const split=(value:string,width:number,max=2)=>{const rows=pdf.splitTextToSize(value||"No registrado",width) as string[];return rows.slice(0,max)};
  const brand=(x:number,y:number,width:number)=>{
    const h=width*71/250;
    pdf.addImage(logo,"PNG",x+width*.075,y+h*.31,width*.25,width*.25*60/160,undefined,"FAST");
    pdf.setFont("helvetica","bold");pdf.setFontSize(width*.17);textColor(blue);pdf.text("Grupo Tejeda",x+width*.37,y+h*.47);
    pdf.setFontSize(width*.075);pdf.text("OFICINA VIRTUAL",x+width*.37,y+h*.70,{charSpace:width*.004});
  };
  const section=(title:string,y:number)=>{pdf.setFillColor(239,247,251);pdf.roundedRect(12,y,186,7,1.2,1.2,"F");pdf.setFont("helvetica","bold");pdf.setFontSize(7);textColor(navy);pdf.text(title,16,y+4.7)};
  const cell=(label:string,value:string,x:number,y:number,width:number,size=8,max=2)=>{pdf.setFont("helvetica","bold");pdf.setFontSize(6);textColor(muted);pdf.text(label.toUpperCase(),x,y);pdf.setFont("helvetica","normal");pdf.setFontSize(size);textColor(ink);pdf.text(split(value,width,max),x,y+4.5)};
  const trace=`REAL-ALMACEN|${input.number}`;
  const qrTarget=`${input.origin||"https://realagencias.runasp.net"}/?warehouseRequest=${encodeURIComponent(input.number)}`;
  const qr=await QRCode.toDataURL(qrTarget,{errorCorrectionLevel:"M",margin:1,width:500});

  pdf.setProperties({title:`Formulario ${input.number}`,subject:"REAL-WAREHOUSE-REQUEST-V319|RECEIPT_SIGNATURE_PAGE=1",author:"REAL - Grupo Tejeda",creator:"Oficina Virtual"});
  pdf.setFillColor(...cyan);pdf.rect(0,0,210,2.5,"F");brand(12,8,68);pdf.setDrawColor(...line);pdf.line(87,9,87,28);
  pdf.setFont("helvetica","bold");pdf.setFontSize(7);textColor(cyan);pdf.text("CONTROL INSTITUCIONAL DE ACTIVOS",94,12);pdf.setFontSize(13);textColor(navy);pdf.text("CONDUCE / FORMULARIO",94,20);pdf.setFont("helvetica","normal");pdf.setFontSize(7.2);textColor(muted);pdf.text("Solicitud, recepción y constancia de firma",94,26);pdf.setDrawColor(...line);pdf.line(12,33,198,33);

  pdf.setFillColor(...navy);pdf.roundedRect(12,37,186,23,2,2,"F");pdf.setFont("helvetica","bold");pdf.setFontSize(10.5);pdf.setTextColor(255,255,255);pdf.text("SOLICITUD A ALMACÉN",17,45);pdf.setFont("helvetica","normal");pdf.setFontSize(7);pdf.setTextColor(190,222,238);pdf.text(`${department} - Almacén central`,17,52);pdf.setFont("helvetica","bold");pdf.setFontSize(6);pdf.setTextColor(135,217,238);pdf.text(`PRIORIDAD: ${priorityNames[input.priority]||input.priority}`,17,57);pdf.setDrawColor(60,89,111);pdf.line(136,41,136,56);pdf.setFont("helvetica","bold");pdf.setFontSize(6);pdf.setTextColor(135,217,238);pdf.text("N.º DE FORMULARIO",142,43);pdf.setFontSize(7);pdf.setTextColor(255,255,255);pdf.text(split(input.number,51,2),142,48);pdf.setFontSize(5);pdf.setTextColor(135,217,238);pdf.text("FECHA Y HORA EFECTIVA",142,53);pdf.setFont("helvetica","normal");pdf.setFontSize(6.5);pdf.setTextColor(255,255,255);pdf.text(time,142,57);

  section("01 / DATOS DE LA OPERACIÓN",65);cell("Área operativa","Almacén central",16,78,40);cell("Departamento",department,60,78,49);cell("Destino","Almacén",113,78,44);cell("Agencia / origen",`${input.agency.codigo} - ${input.agency.terminal}`,16,93,85);cell("Grupo",input.agency.grupo,106,93,50);pdf.addImage(qr,"PNG",166,73,26,26,undefined,"FAST");pdf.setFont("helvetica","bold");pdf.setFontSize(5.5);textColor(muted);pdf.text("CONSULTA Y TRAZABILIDAD",179,103,{align:"center"});

  section("02 / EQUIPOS, COMPONENTES Y DEVOLUCIONES",107);pdf.setFillColor(...navy);pdf.rect(12,117,186,7,"F");pdf.setFontSize(6.2);pdf.setTextColor(255,255,255);pdf.text("TIPO",16,121.5);pdf.text("EQUIPO / ARTÍCULO",38,121.5);pdf.text("CANT.",123,121.5);pdf.text("SERIAL / DETALLE",145,121.5);pdf.setDrawColor(...line);pdf.rect(12,124,186,18);for(const x of [34,119,141])pdf.line(x,124,x,142);
  const preview=input.lines.length<=2?input.lines:[input.lines[0],{kind:"COMPONENT" as const,name:`Ver anexo: ${input.lines.length} renglones completos`,quantity:input.lines.reduce((sum,row)=>sum+row.quantity,0),serial:"Detalle en página 2",note:""}];
  preview.forEach((row,index)=>{const y=130+index*8.5;pdf.setFont("helvetica","normal");pdf.setFontSize(6.7);textColor(ink);pdf.text(kindNames[row.kind],16,y);pdf.text(split(`${row.name}${row.note?` - ${row.note}`:""}`,77,1),38,y);pdf.text(String(row.quantity),130,y,{align:"center"});pdf.text(split(row.serial||"Pendiente / no aplica",51,1),145,y)});

  section("03 / CADENA DE CUSTODIA",147);cell("Persona que entrega",input.createdBy,16,159,82,7.5,1);cell("Persona que recibe","Pendiente - usuario de Almacén",112,159,80,7.5,1);cell("Técnico instalador / responsable",input.installer,16,172,82,7,1);cell("Recepción","Pendiente de confirmar",112,172,80,7,1);

  section("04 / MOTIVO, OBSERVACIONES Y RECEPCIÓN",183);pdf.setDrawColor(...line);pdf.roundedRect(12,193,91,35,2,2);cell("Motivo / observaciones",input.notes||`Solicitud de ${input.lines.length} renglón(es) para la agencia indicada.`,16,199,82,7.1,5);pdf.roundedRect(109,193,89,35,2,2);pdf.setFont("helvetica","bold");pdf.setFontSize(8);textColor(muted);pdf.text("RECEPCIÓN PENDIENTE",153.5,206,{align:"center"});pdf.setFont("helvetica","normal");pdf.setFontSize(6.5);pdf.text("Se completa al pulsar Recibir, sellar y firmar.",153.5,215,{align:"center"});

  section("05 / IDENTIDAD DE ENTREGA Y FIRMA DE RECEPCIÓN",232);
  const identityBox=(x:number,label:string,name:string,login:string,pending=false)=>{pdf.setDrawColor(...line);pdf.roundedRect(x,242,90,36,2,2);pdf.setFont("helvetica","bold");pdf.setFontSize(6.5);textColor(navy);pdf.text(label,x+4,248);pdf.setFont("helvetica",pending?"normal":"bold");pdf.setFontSize(pending?6.5:8);textColor(pending?muted:ink);pdf.text(pending?"Firma pendiente":split(name,78,1),x+45,pending?258:256,{align:pending?"center":"center"});pdf.setDrawColor(...line);pdf.line(x+6,265,x+84,265);pdf.setFont("helvetica","normal");pdf.setFontSize(5.7);textColor(muted);pdf.text(split(pending?"Se identifica al recibir":`Usuario: ${login}`,81,1),x+4,271)};
  identityBox(12,"REMITENTE AUTENTICADO",input.createdBy,input.createdByLogin);identityBox(108,"FIRMA DE QUIEN RECIBE","","",true);

  pdf.setFillColor(239,247,251);pdf.rect(0,282,210,15,"F");pdf.setFont("helvetica","normal");pdf.setFontSize(5.8);textColor(muted);pdf.text(split(`Trazabilidad: ${trace}`,119,1),12,287);pdf.text(`Generado: ${time}`,198,287,{align:"right"});pdf.text(split(`Registrado por: ${input.createdBy} - ${input.createdByLogin}`,180,1),12,290);pdf.setFont("helvetica","bold");pdf.setFontSize(5.5);textColor(navy);pdf.text("Documento de control interno. No es factura fiscal. La firma dibujada no es un certificado digital.",105,294,{align:"center"});

  if(input.lines.length>2){
    pdf.addPage();pdf.setFillColor(...cyan);pdf.rect(0,0,210,2.5,"F");brand(12,8,68);pdf.setFont("helvetica","bold");pdf.setFontSize(12);textColor(navy);pdf.text("ANEXO - DETALLE COMPLETO DE LA SOLICITUD",94,18);pdf.setFontSize(7);textColor(muted);pdf.text(input.number,94,25);let y=39;
    const header=()=>{pdf.setFillColor(...navy);pdf.rect(12,y,186,8,"F");pdf.setTextColor(255,255,255);pdf.setFontSize(6.3);pdf.text("TIPO",16,y+5);pdf.text("EQUIPO / COMPONENTE / DEVOLUCIÓN",40,y+5);pdf.text("CANT.",131,y+5);pdf.text("SERIAL / DETALLE",150,y+5);y+=8};header();
    for(const row of input.lines){const description=pdf.splitTextToSize(`${row.name}${row.note?` - ${row.note}`:""}`,82) as string[],serial=pdf.splitTextToSize(row.serial||"Pendiente / no aplica",42) as string[],height=Math.max(12,Math.max(description.length,serial.length)*4+5);if(y+height>275){pdf.addPage();brand(12,8,55);y=31;header()}pdf.setDrawColor(...line);pdf.rect(12,y,186,height);pdf.setFont("helvetica","normal");pdf.setFontSize(7);textColor(ink);pdf.text(kindNames[row.kind],16,y+6);pdf.text(description,40,y+6);pdf.text(String(row.quantity),136,y+6,{align:"center"});pdf.text(serial,150,y+6);y+=height}
    for(let page=2;page<=pdf.getNumberOfPages();page++){pdf.setPage(page);pdf.setFontSize(5.5);textColor(muted);pdf.text(`Formulario ${input.number} - ${input.createdBy}`,12,289);pdf.text(`${page} / ${pdf.getNumberOfPages()}`,198,289,{align:"right"})}
  }
  pdf.setPage(1);pdf.setFont("helvetica","normal");pdf.setFontSize(5);textColor(muted);pdf.text(`1 / ${pdf.getNumberOfPages()}`,198,280,{align:"right"});return pdf.output("arraybuffer");
}
