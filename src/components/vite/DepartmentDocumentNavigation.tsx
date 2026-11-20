import {FileSignature,FolderOpen,Hammer,Warehouse} from 'lucide-react';
export type DepartmentDocumentSection='files'|'forms'|'warehouse'|'workshop';
export default function DepartmentDocumentNavigation({section,onSelect,canWorkshop=false}:{section:DepartmentDocumentSection|null;onSelect:(section:DepartmentDocumentSection)=>void;canWorkshop?:boolean}){
 const links=[{key:'files',label:'Archivos y comunicaciones',icon:FolderOpen},{key:'forms',label:'Formularios administrativos',icon:FileSignature},{key:'warehouse',label:'Solicitudes a Almacén',icon:Warehouse},...(canWorkshop?[{key:'workshop' as const,label:'Solicitudes a Taller',icon:Hammer}]:[])] as const;
 return <>{links.map(({key,label,icon:Icon})=><button key={key} type="button" className={section===key?'active':''} aria-current={section===key?'page':undefined} title={label} onClick={()=>onSelect(key as DepartmentDocumentSection)}><Icon/><span>{label}</span></button>)}</>;
}
