import {regionForFinding} from "@/components/vite/findingAreaFilters";

export type FindingCategoryInput={department?:string|null;findingKey?:string|null;title?:string|null};

export const automaticFindingCategory=(finding:FindingCategoryInput):{key:string;title:string}=>{
  const department=String(finding.department||"").trim();
  const findingKey=String(finding.findingKey||"").trim();
  const mappings:Record<string,Record<string,{key:string;title:string}>>={
    TECHNOLOGY:{
      PRINTER_NO_MAINTENANCE:{key:"PRINTERS",title:"Impresoras y mantenimiento"},
      DAMAGE_SCREENS:{key:"SCREENS",title:"Pantallas y monitores"},
      DAMAGE_CONNECTIVITY:{key:"CONNECTIVITY",title:"Conectividad e internet"},
      DAMAGE_WIRING:{key:"NETWORK",title:"Redes y cableado de datos"},
      DAMAGE_REPORTED:{key:"EQUIPMENT",title:"Equipos tecnológicos"},
    },
    GENERAL_SERVICES:{
      NOT_PAINTED:{key:"PAINTING",title:"Pintura"},
      DAMAGE_PAINTING:{key:"PAINTING",title:"Pintura"},
      NO_INVERTER:{key:"INVERTER",title:"Inversor"},
      DAMAGE_INVERTER:{key:"INVERTER",title:"Inversor"},
      NO_BATTERY:{key:"BATTERIES",title:"Baterías"},
      DAMAGE_BATTERIES:{key:"BATTERIES",title:"Baterías"},
      DAMAGE_FURNITURE:{key:"RESTRUCTURING",title:"Reestructuración física / Sheetrock"},
      DAMAGE_RESTRUCTURING:{key:"RESTRUCTURING",title:"Reestructuración física / Sheetrock"},
      DAMAGE_LIGHTING:{key:"LIGHTING",title:"Iluminación"},
      DAMAGE_METALWORK:{key:"METALWORK",title:"Herrería"},
      DAMAGE_GENERATOR_REQUEST:{key:"GENERATOR_REQUEST",title:"Requisición planta"},
      DAMAGE_ELECTRICAL:{key:"ELECTRICAL",title:"Avería eléctrica"},
      DAMAGE_SHUTTER:{key:"SHUTTER",title:"Falla en el shutter"},
      NO_RAZA_STICKER:{key:"GENERAL_OTHER",title:"Otro servicio general"},
      NO_REAL_STICKER:{key:"GENERAL_OTHER",title:"Otro servicio general"},
      LOTEKA_NOT_REMOVED:{key:"GENERAL_OTHER",title:"Otro servicio general"},
      DAMAGE_GENERAL_OTHER:{key:"GENERAL_OTHER",title:"Otro servicio general"},
    },
    HUMAN_RESOURCES:{
      STAFF_SHORTAGE:{key:"STAFFING",title:"Personal y vacantes"},
      LEAVE_REQUEST:{key:"LEAVE",title:"Licencias y ausencias"},
      SALES_INCENTIVE_CLAIM:{key:"PAYROLL",title:"Nómina e incentivos"},
      PAYROLL_CLAIM:{key:"PAYROLL",title:"Nómina e incentivos"},
    },
  };
  return mappings[department]?.[findingKey]||{key:findingKey||"OTHER",title:String(finding.title||"").trim()||"Otros hallazgos"};
};

export type LiveFindingInput=FindingCategoryInput&{codigo?:string|null;grupo?:string|null;status?:string|null;provincia?:string|null;municipio?:string|null};
export type LiveFindingCategory={key:string;title:string;department:string;total:number;open:number;inProgress:number};

const normalizedPlace=(value:string|null|undefined)=>String(value||"").normalize("NFD").replace(/[\u0300-\u036f]/g,"").trim().toLocaleLowerCase("es");
const isSantoDomingo=(value:string|null|undefined)=>{const place=normalizedPlace(value);return place==="santo domingo"||place.startsWith("santo domingo ")};

export function summarizeSantoDomingoFindings(findings:LiveFindingInput[],agencyRegions:ReadonlyMap<string,string>):LiveFindingCategory[]{
  const rows=new Map<string,LiveFindingCategory>();
  findings.filter(finding=>["OPEN","IN_PROGRESS"].includes(String(finding.status||"").trim().toUpperCase())&&[regionForFinding(finding,agencyRegions),finding.provincia,finding.municipio].some(isSantoDomingo)).forEach(finding=>{
    const status=String(finding.status||"").trim().toUpperCase();
    const department=String(finding.department||"OTHER").trim()||"OTHER";
    const category=automaticFindingCategory(finding),key=`${department}:${category.key}`;
    const current=rows.get(key)||{key,title:category.title,department,total:0,open:0,inProgress:0};
    current.total++;if(status==="IN_PROGRESS")current.inProgress++;else current.open++;rows.set(key,current);
  });
  return [...rows.values()].sort((left,right)=>right.total-left.total||left.title.localeCompare(right.title,"es"));
}
