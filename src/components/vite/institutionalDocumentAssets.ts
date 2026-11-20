import { apiFetch as fetch } from "@/lib/api-fetch";
export type InstitutionalTemplateType='TEXT'|'LETTER'|'COMMUNICATION'|'FORM'|'RECEIPT'|'INVOICE';
export type LetterForm={documentType:InstitutionalTemplateType;departmentHeading:string;referenceCode:string;documentDate:string;recipientName:string;recipientTitle:string;viaName:string;viaTitle:string;subject:string;body:string;closing:string;signerName:string;signerTitle:string;initials:string};
export const newLetterForm=():LetterForm=>({documentType:'COMMUNICATION',departmentHeading:'Departamento',referenceCode:`REAL-${new Date().getFullYear()}-`,documentDate:new Date().toLocaleDateString('es-DO',{timeZone:'America/Santo_Domingo'}),recipientName:'',recipientTitle:'',viaName:'',viaTitle:'',subject:'',body:'',closing:'Atentamente,',signerName:'',signerTitle:'',initials:''});
export function parseLetterForm(value:string):LetterForm{const next=newLetterForm();try{const parsed=JSON.parse(value);const types:InstitutionalTemplateType[]=['TEXT','LETTER','COMMUNICATION','FORM','RECEIPT','INVOICE'];for(const k of Object.keys(next) as (keyof LetterForm)[]){if(k==='documentType'){if(types.includes(parsed?.[k]))next.documentType=parsed[k];continue}if(typeof parsed?.[k]==='string')next[k]=parsed[k]}}catch{if(value)next.body=value}return next}

let logoPromise: Promise<string> | undefined;
async function loadRealLogo(){const r=await fetch('/loto-real-logo-transparent.png');if(!r.ok)throw new Error('No se pudo cargar el logo REAL.');const blob=await r.blob();return await new Promise<string>((resolve,reject)=>{const reader=new FileReader();reader.onload=()=>resolve(String(reader.result));reader.onerror=reject;reader.readAsDataURL(blob)})}
export function downloadDocument(data:Uint8Array,name:string){const url=URL.createObjectURL(new Blob([data.slice().buffer],{type:'application/pdf'}));const a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),10000)}

// Share only this public, immutable logo. A failure is retryable; user documents are never cached here.
export function realLogoData(){return logoPromise ??= loadRealLogo().catch(error=>{logoPromise=undefined;throw error})}
