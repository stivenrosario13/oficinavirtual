export type MaintenanceDocumentMovement={id:string;agencyId?:string|null;codigo:string;terminal:string;grupo:string;ticketId?:string|null;department:string;technicianUserId?:string|null;technicianName:string;movementType:string;equipmentType:string;componentType?:string|null;failureCause:string;serialNumber?:string|null;quantity:number;notes?:string|null;createdByName:string;createdAt:string;operationalArea:"WORKSHOP"|"WAREHOUSE";documentNumber:string;qrToken:string;deliveredByName?:string|null;receivedByName?:string|null;destinationName?:string|null;signatureData?:string|null;occurredAt:string;receivedByLogin?:string|null};
export type DocumentSignature={data:string;name:string;login:string;signedAt:string};
export type SignatureRole="DELIVERY"|"RECEIPT";
export type DocumentMarks={receivedAt?:string|null;receiverName?:string|null;receiverLogin?:string|null;stampX:number;stampY:number;signatureData?:string|null;signerName?:string|null;signerLogin?:string|null;signedAt?:string|null;deliverySignature?:DocumentSignature|null;receiptSignature?:DocumentSignature|null;canReceive?:boolean;canMove?:boolean;canSignDelivery?:boolean;canSignReceipt?:boolean;stampLabel?:string;requiresBothSignatures?:boolean};
export const departmentLabels:Record<string,string>={TECHNOLOGY:"Tecnología",GENERAL_SERVICES:"Servicios Generales",HUMAN_RESOURCES:"Recursos Humanos"};
const movementLabels:Record<string,string>={ENTRY:"Entrada",EXIT:"Salida / entrega",REQUEST:"Solicitud de equipo",NEW_DELIVERY:"Entrega de equipo nuevo",DAMAGED_RETURN:"Recepción de equipo dañado",TRANSFER_TO_WORKSHOP:"Envío a Taller",REPLACEMENT:"Reemplazo",COMPONENT_REPLACEMENT:"Cambio de componente",REPAIR:"Reparación",DISCHARGE:"Descargo definitivo"};
export function formatDocumentTime(value:string,utc=true){const date=new Date(utc&&!/[zZ]|[+-]\d{2}:\d{2}$/.test(value)?value+"Z":value);return Number.isNaN(date.getTime())?"Fecha no disponible":date.toLocaleString("es-DO",{timeZone:"America/Santo_Domingo",hour12:false});}

export async function buildMaintenancePdf(item:MaintenanceDocumentMovement,marks:DocumentMarks,assets?:{realLogo?:string;template?:boolean}){
 const [{jsPDF},QRCode]=await Promise.all([import("jspdf"),import("qrcode")]);
 const template=!!assets?.template;
 const logoResponse=assets?.realLogo?null:await fetch("/loto-real-logo-transparent.png");
 if(logoResponse&&!logoResponse.ok)throw new Error("No se pudo cargar el logo institucional.");
 const logoBlob=await logoResponse?.blob();
 const realLogo=assets?.realLogo||await new Promise<string>((resolve,reject)=>{const reader=new FileReader();reader.onload=()=>resolve(String(reader.result));reader.onerror=()=>reject(new Error("No se pudo preparar el logo institucional."));reader.readAsDataURL(logoBlob!);});
 const pdf=new jsPDF({unit:"mm",format:"a4",compress:true});
 const navy:[number,number,number]=[12,32,51],cyan:[number,number,number]=[22,152,191],blue:[number,number,number]=[7,87,166],ink:[number,number,number]=[24,45,62],muted:[number,number,number]=[91,111,126],line:[number,number,number]=[190,207,218];
 const textColor=(color:[number,number,number])=>pdf.setTextColor(...color);
 const overflow:{label:string;value:string;image?:string}[]=[];
 const fit=(value:string,width:number,count=2,label="Detalle completo")=>{
  const lines=pdf.splitTextToSize(value||"No registrado",width) as string[];
  if(lines.length<=count)return lines;
  overflow.push({label,value});return [...lines.slice(0,Math.max(0,count-1)),"Ver anexo"];
 };
 // Composición institucional de la referencia: REAL + Grupo Tejeda / Oficina Virtual.
 const brandLockup=(x:number,y:number,width:number)=>{
  const h=width*71/250;
  pdf.addImage(realLogo,"PNG",x+width*.075,y+h*.31,width*.25,width*.25*60/160,undefined,"FAST");
  pdf.setFont("helvetica","bold");pdf.setFontSize(width*.17);textColor(blue);pdf.text("Grupo Tejeda",x+width*.37,y+h*.47);
  pdf.setFontSize(width*.075);pdf.text("OFICINA VIRTUAL",x+width*.37,y+h*.70,{charSpace:width*.004});
 };
 const section=(title:string,y:number)=>{pdf.setFillColor(239,247,251);pdf.roundedRect(12,y,186,7,1.2,1.2,"F");pdf.setFont("helvetica","bold");pdf.setFontSize(7);textColor(navy);pdf.text(title,16,y+4.7);};
 const cell=(label:string,value:string,x:number,y:number,width:number,size=8,count=2)=>{
  pdf.setFont("helvetica","bold");pdf.setFontSize(6);textColor(muted);pdf.text(label.toUpperCase(),x,y);
  pdf.setFont("helvetica","normal");pdf.setFontSize(size);textColor(ink);pdf.text(fit(value,width,count,label),x,y+4.5);
 };
 const blank="____________________________";
 const occurred=template?"____ / ____ / ______  ____:____":formatDocumentTime(item.occurredAt||item.createdAt,false);
 const area=template?"Taller / Almacén":item.operationalArea==="WORKSHOP"?"Taller técnico":"Almacén central";
 const dept=template?"Departamento: __________________":departmentLabels[item.department]||item.department;
 const movement=template?"ENTREGA Y RECEPCIÓN DE EQUIPOS":movementLabels[item.movementType]||item.movementType;
 const stampLabel=(marks.stampLabel||(["EXIT","NEW_DELIVERY"].includes(item.movementType)?"ENTREGADO":"RECIBIDO")).toUpperCase();
 pdf.setProperties({title:template?"Plantilla institucional - entrega y recepción":`Formulario ${item.documentNumber}`,author:"REAL - Grupo Tejeda",subject:movement,creator:"Oficina Virtual"});
 pdf.setFillColor(...cyan);pdf.rect(0,0,210,2.5,"F");
 brandLockup(12,8,68);
 pdf.setDrawColor(...line);pdf.line(87,9,87,28);
 pdf.setFont("helvetica","bold");pdf.setFontSize(7);textColor(cyan);pdf.text("CONTROL INSTITUCIONAL DE ACTIVOS",94,12);
 pdf.setFontSize(13);textColor(navy);pdf.text("CONDUCE / FORMULARIO",94,20);
 pdf.setFont("helvetica","normal");pdf.setFontSize(7.2);textColor(muted);pdf.text("Entrega, recepción y constancia de firmas",94,26);
 pdf.setDrawColor(...line);pdf.line(12,33,198,33);

 pdf.setFillColor(...navy);pdf.roundedRect(12,37,186,23,2,2,"F");
 pdf.setFont("helvetica","bold");pdf.setFontSize(10.5);pdf.setTextColor(255,255,255);pdf.text(movement.toUpperCase(),17,45);
 pdf.setFont("helvetica","normal");pdf.setFontSize(7);pdf.setTextColor(190,222,238);pdf.text(`${area} - ${dept}`,17,53);
 pdf.setDrawColor(60,89,111);pdf.line(136,41,136,56);
 pdf.setFont("helvetica","bold");pdf.setFontSize(6);pdf.setTextColor(135,217,238);pdf.text("N.º DE FORMULARIO",142,43);
 pdf.setFontSize(7);pdf.setTextColor(255,255,255);pdf.text(fit(template?blank:item.documentNumber,51,2,"Número del formulario"),142,48);
 pdf.setFont("helvetica","bold");pdf.setFontSize(5);pdf.setTextColor(135,217,238);pdf.text("FECHA Y HORA EFECTIVA",142,53);
 pdf.setFont("helvetica","normal");pdf.setFontSize(6.5);pdf.setTextColor(255,255,255);pdf.text(occurred,142,57);

 section("01 / DATOS DE LA OPERACIÓN",65);
 cell("Área operativa",area,16,78,40);cell("Departamento",template?blank:dept,60,78,49);
 cell("Destino",template?blank:item.destinationName||"No especificado",113,78,44);
 cell("Agencia / origen",template?blank:`${item.codigo} - ${item.terminal}`,16,93,85);
 cell("Grupo",template?blank:item.grupo,106,93,50);
 if(!template){const token=item.qrToken||`REAL-MNT|${item.documentNumber}`;const target=typeof window!=="undefined"?window.location.origin+"/?equipment="+encodeURIComponent(token):token;const qr=await QRCode.toDataURL(target,{errorCorrectionLevel:"M",margin:1,width:500});pdf.addImage(qr,"PNG",166,73,26,26,undefined,"FAST");}
 else {pdf.setDrawColor(...line);pdf.roundedRect(166,74,26,25,1,1);pdf.setFontSize(6);textColor(muted);pdf.text(["QR disponible","al registrar el","formulario"],179,82,{align:"center"});}
 pdf.setFont("helvetica","bold");pdf.setFontSize(5.5);textColor(muted);pdf.text("CONSULTA Y TRAZABILIDAD",179,103,{align:"center"});

 section("02 / DETALLE DEL EQUIPO O ARTÍCULO",107);
 pdf.setFillColor(...navy);pdf.rect(12,117,186,7,"F");pdf.setFontSize(6.5);pdf.setTextColor(255,255,255);pdf.text("CANT.",16,121.5);pdf.text("EQUIPO / ARTÍCULO",31,121.5);pdf.text("COMPONENTE",102,121.5);pdf.text("SERIAL / CÓDIGO",146,121.5);
 pdf.setDrawColor(...line);pdf.rect(12,124,186,18);for(const x of [27,98,142])pdf.line(x,124,x,142);
 pdf.setFont("helvetica","normal");pdf.setFontSize(8);textColor(ink);pdf.text(template?"____":String(item.quantity),16,131);
 pdf.text(fit(template?blank:item.equipmentType,63,2,"Equipo / artículo"),31,131);pdf.text(fit(template?"____________":item.componentType||"No aplica",34,2,"Componente"),102,131);
 pdf.setFontSize(7);pdf.text(fit(template?"________________":item.serialNumber||"No registrado",47,3,"Serial completo"),146,131);

 section("03 / CADENA DE CUSTODIA",147);
 const deliveryName=marks.deliverySignature?.name||item.deliveredByName||"No registrado";
 const receiptName=marks.receiptSignature?.name||marks.receiverName||item.receivedByName||"No registrado";
 cell("Persona que entrega",template?blank:deliveryName,16,159,85);
 cell("Persona que recibe",template?blank:receiptName,112,159,80);
 cell("Responsable del equipo",template?blank:item.technicianName,16,172,85,7,1);
 cell("Recepción",template?blank:marks.receivedAt?formatDocumentTime(marks.receivedAt):"Pendiente de confirmar",112,172,80,7,1);

 section("04 / MOTIVO, OBSERVACIONES Y RECEPCIÓN",183);
 pdf.setDrawColor(...line);pdf.roundedRect(12,193,91,35,2,2);
 const legacy=marks.signatureData||item.signatureData;
 cell("Motivo / observaciones",template?"________________________________________________\n________________________________________________\n________________________________________________":`${item.failureCause}\n${item.notes||"Sin observaciones adicionales"}`,16,199,82,7.4,legacy&&!template?2:6);
 if(legacy&&!template){pdf.setFontSize(5.5);textColor(blue);pdf.text("FIRMA REGISTRADA ANTERIORMENTE (SIN ROL)",16,215);const props=pdf.getImageProperties(legacy),scale=Math.min(79/props.width,10/props.height);pdf.addImage(legacy,"PNG",16,216,props.width*scale,props.height*scale);}
 if(marks.receivedAt&&!template){
  const x=Math.max(4,Math.min(117,marks.stampX??109)),y=Math.max(4,Math.min(256,marks.stampY??191));
  pdf.setDrawColor(...blue);pdf.setLineWidth(.6);pdf.rect(x,y,89,37);pdf.setLineWidth(.2);pdf.rect(x+1,y+1,87,35);
  brandLockup(x+2,y+1,45);pdf.setFont("helvetica","bold");pdf.setFontSize(11);textColor(blue);pdf.text(stampLabel,x+84,y+9,{align:"right"});
  pdf.setDrawColor(...blue);pdf.line(x+4,y+13,x+85,y+13);
  pdf.setFontSize(6.5);pdf.text(fit(dept.toUpperCase(),81,1,"Departamento del sello"),x+4,y+17);
  pdf.setFontSize(7);pdf.text(fit(marks.receiverName||"Nombre no registrado",81,1,"Nombre del receptor"),x+4,y+22);
  pdf.setFont("helvetica","normal");pdf.setFontSize(6);pdf.text(fit(`Usuario: ${marks.receiverLogin||"No registrado"}`,81,1,"Login receptor"),x+4,y+27);
  pdf.setFontSize(6.5);pdf.text(`Fecha / hora (RD): ${formatDocumentTime(marks.receivedAt)}`,x+4,y+33);
 }else if(template){
  const x=109,y=191;pdf.setDrawColor(...blue);pdf.setLineWidth(.6);pdf.rect(x,y,89,37);pdf.setLineWidth(.2);pdf.rect(x+1,y+1,87,35);
  brandLockup(x+2,y+1,45);textColor(blue);pdf.setFont("helvetica","bold");pdf.setFontSize(6.5);pdf.text("SELLO DE RECIBIDO / ENTREGADO",x+84,y+9,{align:"right"});
  pdf.line(x+4,y+13,x+85,y+13);pdf.setFont("helvetica","normal");pdf.setFontSize(6.5);
  pdf.text("Departamento: __________________________________",x+4,y+18);pdf.text("Nombre: _______________________________________",x+4,y+23);
  pdf.text("Usuario: _______________________________________",x+4,y+28);pdf.text("Fecha / hora (RD): ______________________________",x+4,y+33);
 }else{
  pdf.setDrawColor(...line);pdf.roundedRect(109,193,89,35,2,2);pdf.setFont("helvetica","bold");pdf.setFontSize(8);textColor(muted);pdf.text(template?"SELLO DE RECIBIDO":"RECEPCIÓN PENDIENTE",153.5,206,{align:"center"});pdf.setFont("helvetica","normal");pdf.setFontSize(6.5);pdf.text(template?"Fecha / hora: __________________": "Se confirma desde el botón Recibido.",153.5,215,{align:"center"});
 }
 section("05 / FIRMAS DE ENTREGA Y RECEPCIÓN",232);
 const signatureBox=(x:number,label:string,signature:DocumentSignature|null|undefined,name:string)=>{
  pdf.setDrawColor(...line);pdf.roundedRect(x,242,90,36,2,2);
  pdf.setFont("helvetica","bold");pdf.setFontSize(6.5);textColor(navy);pdf.text(label,x+4,248);
  if(signature&&!template){const props=pdf.getImageProperties(signature.data);const scale=Math.min(79/props.width,14/props.height);const w=props.width*scale,h=props.height*scale;pdf.addImage(signature.data,"PNG",x+(90-w)/2,250+(14-h)/2,w,h,undefined,"FAST");}
  else{pdf.setFont("helvetica","normal");pdf.setFontSize(6.5);textColor(muted);if(!template)pdf.text("Firma pendiente",x+45,258,{align:"center"});}
  pdf.setDrawColor(...line);pdf.line(x+6,265,x+84,265);
  pdf.setFont("helvetica","normal");pdf.setFontSize(6);textColor(ink);
  pdf.text(fit(template?blank:signature?.name||name,81,1,label+" - nombre"),x+4,269);
  pdf.setFontSize(5.5);textColor(muted);pdf.text(fit(template?"Nombre / identificación":signature?.login||"Debe firmar la persona indicada",81,1,label+" - login"),x+4,272.5);
  if(signature&&!template){pdf.setFontSize(5);pdf.text(formatDocumentTime(signature.signedAt),x+4,276);}
 };
 signatureBox(12,"FIRMA DE QUIEN ENTREGA",marks.deliverySignature,deliveryName);
 signatureBox(108,"FIRMA DE QUIEN RECIBE",marks.receiptSignature,receiptName);
 if(legacy&&!template)overflow.push({label:"Firma histórica - rol no identificado",value:`Firma conservada del formulario anterior y visible en la primera página. No se asigna automáticamente a quien entrega ni a quien recibe. ${marks.signerName||""} ${marks.signerLogin||""}`,image:legacy});
 pdf.setFillColor(239,247,251);pdf.rect(0,282,210,15,"F");pdf.setFont("helvetica","normal");pdf.setFontSize(5.8);textColor(muted);
 pdf.text(fit(template?"Plantilla institucional - completar antes de utilizar":`Trazabilidad: ${item.qrToken}`,119,1,"Código de trazabilidad"),12,287);
 pdf.text(template?"REAL / Grupo Tejeda":`Generado: ${formatDocumentTime(new Date().toISOString())}`,198,287,{align:"right"});
 pdf.text(fit(template?"Documento para entrega y recepción de activos":`Registrado por: ${item.createdByName}`,180,1,"Registrado por"),12,290);
 pdf.setFont("helvetica","bold");pdf.setFontSize(5.5);textColor(navy);pdf.text("Documento de control interno. No es factura fiscal. Las firmas dibujadas no son certificados digitales.",105,294,{align:"center"});
 if(overflow.length){let y=32;const next=()=>{pdf.addPage();pdf.setFont("helvetica","bold");pdf.setFontSize(11);textColor(navy);pdf.text("ANEXO - DETALLES DEL FORMULARIO",12,15);pdf.setFont("helvetica","normal");pdf.setFontSize(8);pdf.text(item.documentNumber,12,22);y=34;};next();for(const part of overflow){if(y>245)next();pdf.setFont("helvetica","bold");pdf.setFontSize(8);textColor(navy);pdf.text(part.label.toUpperCase(),12,y);y+=6;pdf.setFont("helvetica","normal");pdf.setFontSize(9);for(const row of pdf.splitTextToSize(part.value,182) as string[]){if(y>271)next();pdf.setFont("helvetica","normal");pdf.setFontSize(9);textColor(ink);pdf.text(row,12,y);y+=4.5;}if(part.image){if(y>244)next();pdf.addImage(part.image,"PNG",16,y,80,24);y+=29;}y+=8;}}
 for(let page=1;page<=pdf.getNumberOfPages();page++){pdf.setPage(page);pdf.setFont("helvetica","normal");pdf.setFontSize(5);textColor(muted);pdf.text(`${page} / ${pdf.getNumberOfPages()}`,198,280,{align:"right"});}
 pdf.setPage(1);return pdf;
}


