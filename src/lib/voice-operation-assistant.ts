export type VoiceAgency={id:string;codigo:string;terminal:string;grupo:string};
export type VoiceCatalogItem={code:string;name:string};
export type VoiceWarehouseItem={productId:string;name:string};
export type VoiceWarehouseBatchLine={productId:string;name:string;quantity:number};
export type VoiceWarehouseBatchTarget={agencyId:string;agencyCode:string;agencyName:string;agencyGroup:string;lines:VoiceWarehouseBatchLine[]};
export type VoiceWarehouseBatch={transcript:string;targets:VoiceWarehouseBatchTarget[]};
export function prepareVoiceWarehouseBatch(batch:VoiceWarehouseBatch,agencyIds:Iterable<string>,productIds:Iterable<string>){
  const agencies=new Set(agencyIds),products=new Set(productIds),multi=batch.targets.length>1;
  const valid=batch.targets.length>0&&batch.targets.every(target=>agencies.has(target.agencyId)&&target.lines.length>0&&target.lines.every(line=>products.has(line.productId)&&Number.isInteger(line.quantity)&&line.quantity>0&&line.quantity<=10000)&&target.lines.reduce((total,line)=>total+line.quantity,0)<=10000);
  return{valid,multi,lines:batch.targets.flatMap(target=>target.lines.map(line=>({...line,agencyId:multi?target.agencyId:''})))};
}

export const normalizeVoiceText=(value:string)=>value.normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es").replace(/[^a-z0-9\s]/g," ").replace(/\s+/g," ").trim();

const numberWords:Record<string,number>={un:1,uno:1,una:1,dos:2,tres:3,cuatro:4,cinco:5,seis:6,siete:7,ocho:8,nueve:9,diez:10,once:11,doce:12,trece:13,catorce:14,quince:15,veinte:20};
const words=(value:string)=>normalizeVoiceText(value).split(" ").filter(Boolean);
const score=(source:string,target:string)=>{
  const sourceWords=new Set(words(source)),targetWords=words(target);
  return targetWords.reduce((total,word)=>total+(sourceWords.has(word)?word.length+2:0),0);
};

export function findVoiceAgency(text:string,agencies:VoiceAgency[]){
  const normalized=normalizeVoiceText(text);
  const exact=agencies.find(item=>normalized.includes(normalizeVoiceText(item.codigo))&&normalizeVoiceText(item.codigo).length>=3);
  if(exact)return exact;
  const numeric=normalized.split(' ').filter(token=>/^\d{2,}$/.test(token));
  const partial=agencies.filter(item=>numeric.some(token=>normalizeVoiceText(item.codigo).includes(token)));
  if(partial.length===1)return partial[0];
  if(partial.length>1){const ranked=partial.map(item=>({item,score:score(normalized,`${item.terminal} ${item.grupo}`)})).sort((a,b)=>b.score-a.score);if(ranked[0].score>ranked[1].score)return ranked[0].item;}
  return agencies.map(item=>({item,score:score(normalized,`${item.terminal} ${item.grupo}`)})).sort((a,b)=>b.score-a.score)[0]?.score>=5?agencies.map(item=>({item,score:score(normalized,`${item.terminal} ${item.grupo}`)})).sort((a,b)=>b.score-a.score)[0].item:undefined;
}

export function findVoiceCatalogItem(text:string,items:VoiceCatalogItem[]){
  const ranked=items.map(item=>({item,score:score(text,item.name)})).sort((a,b)=>b.score-a.score);
  return ranked[0]?.score>=4?ranked[0].item:undefined;
}

export function voiceSubject(text:string){
  const cleaned=text.trim().replace(/^(crea|crear|necesito|quiero|registra|registrar)\s+(un\s+|una\s+)?(ticket|solicitud)(\s+intern[oa])?\s*/i,"");
  const boundary=cleaned.search(/\b(descripci[oó]n|detalle|motivo|para la agencia|agencia)\b/i);
  const subject=(boundary>12?cleaned.slice(0,boundary):cleaned).trim();
  return (subject||"Solicitud creada por voz").slice(0,180);
}

export function parseVoiceWarehouseLines(text:string,items:VoiceWarehouseItem[]){
  const normalized=normalizeVoiceText(text);
  const found:{productId:string;name:string;quantity:number;manual:boolean}[]=[];
  for(const item of items){
    const itemWords=words(item.name).filter(word=>word.length>2);
    if(!itemWords.length||!itemWords.every(word=>normalized.includes(word)))continue;
    const sameName=items.filter(candidate=>normalizeVoiceText(candidate.name)===normalizeVoiceText(item.name));
    if(sameName.length>1&&!normalized.includes(normalizeVoiceText(item.productId)))continue;
    const first=normalized.indexOf(itemWords[0]);
    const prefix=normalized.slice(Math.max(0,first-20),first).trim().split(" ").pop()||"";
    const quantity=Number(prefix)||numberWords[prefix]||1;
    found.push({productId:item.productId,name:item.name,quantity:Math.max(1,quantity),manual:false});
  }
  if(found.length)return found.slice(0,30);
  const match=normalized.match(/(?:solicita|necesito|quiero|pedido|articulo)\s+(?:(\d+|un|una|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez)\s+)?(.+?)(?:\s+para\s+la\s+agencia|\s+agencia|\s+motivo|$)/);
  if(!match)return [];
  return [{productId:"",name:match[2].trim().slice(0,100),quantity:Number(match[1])||numberWords[match[1]]||1,manual:true}];
}
