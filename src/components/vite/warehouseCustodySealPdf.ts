import {rgb,type PDFPage,type PDFFont,type PDFImage} from 'pdf-lib';

type CustodySeal={status:'ENTREGADO'|'RECIBIDO';name:string;login:string;time:string;number:string};
// Same institutional header, logo, double border and confirmation icon as
// the authorization stamp, deliberately without a QR image.
export function drawWarehouseCustodySeal(page:PDFPage,seal:CustodySeal,x:number,y:number,width:number,height:number,font:PDFFont,bold:PDFFont,brand:PDFImage,department='ALMACEN'){
 const blue=rgb(.025,.34,.65),navy=rgb(.045,.13,.21),muted=rgb(.35,.44,.50),white=rgb(1,1,1),signatureWidth=72;
 const safe=(value:string)=>value.replace(/[^\x20-\x7e\xa0-\xff]/g,'-');
 const contentWidth=width-signatureWidth-24;
 const fitted=(value:string,size:number,min=5.5)=>{let text=safe(value);while(size>min&&font.widthOfTextAtSize(text,size)>contentWidth)size-=.25;if(font.widthOfTextAtSize(text,size)>contentWidth){while(text.length&&font.widthOfTextAtSize(text+'...',size)>contentWidth)text=text.slice(0,-1);text+='...'}return {text,size}};
 page.drawRectangle({x,y,width,height,color:white,borderColor:blue,borderWidth:2});
 page.drawRectangle({x:x+3,y:y+3,width:width-6,height:height-6,borderColor:blue,borderWidth:.7});
 page.drawRectangle({x:x+5,y:y+height-32,width:width-10,height:25,color:rgb(.94,.975,1)});
 page.drawImage(brand,{x:x+10,y:y+height-27,width:36,height:13.5});
 page.drawText('GRUPO TEJEDA',{x:x+51,y:y+height-18,size:9,font:bold,color:navy});
 page.drawText('CONTROL INSTITUCIONAL',{x:x+51,y:y+height-27,size:5.6,font:bold,color:blue});
 const iconX=x+width-86,iconY=y+height-19;
 page.drawCircle({x:iconX,y:iconY,size:7.5,color:blue});
 page.drawLine({start:{x:iconX-4,y:iconY},end:{x:iconX-1,y:iconY-3},thickness:1.8,color:white});
 page.drawLine({start:{x:iconX-1,y:iconY-3},end:{x:iconX+5,y:iconY+4},thickness:1.8,color:white});
 page.drawText(seal.status,{x:iconX+12,y:y+height-22,size:8.6,font:bold,color:blue});
 page.drawLine({start:{x:x+8,y:y+height-38},end:{x:x+width-8,y:y+height-38},thickness:1.1,color:blue});
 page.drawText(seal.status==='ENTREGADO'?`ENTREGADO POR ${department}`:'RECIBIDO POR',{x:x+10,y:y+height-48,size:6,font:bold,color:blue});
 const name={text:safe(seal.name),size:8.5};let remaining=name.text,lines:string[]=[];
 while(remaining){let line=remaining;while(bold.widthOfTextAtSize(line,name.size)>contentWidth&&line.length>1)line=line.slice(0,-1);lines.push(line);remaining=remaining.slice(line.length)}
 // Full human names can use two lines, leaving the login, date and code intact.
 if(lines.length>2){lines=lines.slice(0,2);while(lines[1].length&&bold.widthOfTextAtSize(lines[1]+'...',name.size)>contentWidth)lines[1]=lines[1].slice(0,-1);lines[1]+='...'}
 lines.forEach((line,index)=>page.drawText(line,{x:x+10,y:y+height-59-index*9,size:name.size,font:bold,color:navy}));
 const login=fitted(seal.login,7.2),time=fitted(`Fecha y hora: ${seal.time}`,6.5),number=fitted(`Código: ${seal.number}`,6.1);
 page.drawText(login.text,{x:x+10,y:y+height-79,size:login.size,font,color:muted});
 page.drawText(time.text,{x:x+10,y:y+height-89,size:time.size,font,color:blue});
 page.drawText(number.text,{x:x+10,y:y+8,size:number.size,font:bold,color:navy});
 const signatureX=x+width-signatureWidth;
 page.drawLine({start:{x:signatureX,y:y+8},end:{x:signatureX,y:y+height-42},thickness:.65,color:blue});
 page.drawLine({start:{x:signatureX+8,y:y+25},end:{x:x+width-9,y:y+25},thickness:.8,color:blue});
 page.drawText('FIRMA MANUAL',{x:signatureX+8,y:y+15,size:5.4,font:bold,color:blue});
}
