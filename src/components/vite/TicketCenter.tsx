import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  BarChart3,
  Barcode,
  Boxes,
  Building2,
  Camera,
  CheckCircle2,
  ChevronDown,
  Clock3,
  Download,
  FileText,
  Hammer,
  Eye,
  History,
  LayoutGrid,
  LifeBuoy,
  List,
  LoaderCircle,
  MapPin,
  MessageCircle,
  PackageSearch,
  PackageCheck,
  PenTool,
  Pencil,
  Plus,
  RefreshCw,
  RotateCcw,
  Search,
  ScanBarcode,
  QrCode,
  Send,
  Share2,
  ShieldCheck,
  ShoppingCart,
  ClipboardList,
  Truck,
  Upload,
  Trash2,
  UserRoundCheck,
  UsersRound,
  Wrench,
  Warehouse,
  X,
} from "lucide-react";
import type { PortalSession } from "@/lib/session";
import type { SupportNavigationTarget } from "@/App";
import SupportChat from "@/components/vite/SupportChat";
import MaintenanceDocumentEditor from "./MaintenanceDocumentEditor";
import MaintenanceOverview from "./MaintenanceOverview";
import WorkshopBoard,{EquipmentTracking} from "./WorkshopBoard";
import {workshopCode} from "./workshopWorkflow";
import "./maintenanceModern.css";
import MaintenanceCommunications from "./MaintenanceCommunications";
import type {MaintenanceChannel} from "./MaintenanceCommunications";
import WarehouseRequestComposer from "./WarehouseRequestComposer";
import SentWarehouseRequestHistory from "./SentWarehouseRequestHistory";

type TicketAgency = {
  id: string;
  codigo: string;
  terminal: string;
  grupo: string;
  direccion?: string | null; sector?: string | null; municipio?: string | null; provincia?: string | null;
  latitude?: number | null; longitude?: number | null;
};
type AgencyDirectoryDraft = {
  codigo: string; terminal: string; grupo: string;
  direccion: string; sector: string; municipio: string; provincia: string;
  latitude: string; longitude: string;
};
type AgencyDirectoryImportRow = {
  rowNumber: number; codigo: string; terminal: string; grupo: string;
  region: string | null; direccion: string | null; sector: string | null;
  municipio: string | null; provincia: string | null;
  latitude: number | null; longitude: number | null;
};
type AgencyDirectoryImportPreview = {
  fileName: string; rows: AgencyDirectoryImportRow[]; errors: string[];
};
type AgencyDirectoryImportResult = {
  total: number; created: number; skipped: number; duplicates: string[];
};
type MaintenanceInventoryImportRow = {
  rowNumber:number; department:string; equipmentType:string; serialNumber:string;
  quantity:number; destinationName:string; notes:string;
};
type MaintenanceInventoryImportPreview = {
  fileName:string; rows:MaintenanceInventoryImportRow[]; errors:string[]; duplicates:string[];
};
type TechnologyTeam = "CALL_CENTER" | "TECHNICAL_FAILURE" | "TECHNICIANS";
type MaintenanceOperatorTeam = "WAREHOUSE" | "WORKSHOP";
type Technician = {
  id: string;
  name: string;
  department: string;
  role?: string | null;
  groups?: string[] | null;
  isAgencySupervisor?: boolean;
  supportTeam?: TechnologyTeam | MaintenanceOperatorTeam | null;
  isTechnician: boolean;
};
type TicketEvidence = {
  id: string;
  fileName: string;
  contentType: string;
  capturedAt: string;
  url?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  accuracyMeters?: number | null;
  locationCapturedAt?: string | null;
};
type TicketHistoryEvent = {
  id: string;
  action: string;
  actorName: string;
  comment?: string | null;
  fromDepartment?: string | null;
  toDepartment?: string | null;
  fromTeam?: TechnologyTeam | null;
  toTeam?: TechnologyTeam | null;
  createdAt: string;
};
type SupportCategory = {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  defaultPriority: string;
  requiresDetail: boolean;
  detailLabel?: string | null;
  options?: string[] | null;
  displayOrder: number;
  isActive: boolean;
};
type SupportDepartment = {
  code: string;
  name: string;
  description?: string | null;
  accentColor: string;
  displayOrder: number;
  isActive: boolean;
  categories: SupportCategory[];
};
type Ticket = {
  id: string;
  ticketNumber: number;
  codigo: string;
  terminal: string;
  grupo: string;
  category: string;
  assignedDepartment: string;
  priority: string;
  status: string;
  subject: string;
  description: string;
  resolution?: string | null;
  createdByName: string;
  createdByUsername: string;
  createdAt: string;
  updatedAt: string;
  closedAt?: string | null;
  isAutomatic: boolean;
  ticketType: "SUPPORT" | "INTERNAL";
  isAssignedToCurrentUser?: boolean;
  assignedTechnicianId?: string | null; assignedTechnicianName?: string | null;
  assignedTeam?: TechnologyTeam | null;
  sharedWithDepartments?: string[] | null;
  evidence?: TicketEvidence[] | null;
  history?: TicketHistoryEvent[] | null;
  resolvedByName?: string | null;
  resolutionMinutes?: number | null;
  direccion?: string | null; sector?: string | null; municipio?: string | null; provincia?: string | null;
  latitude?: number | null; longitude?: number | null;
};
type AutomaticFinding = {
  id: string;
  profileId: string;
  codigo: string;
  terminal: string;
  grupo: string;
  submittedAt: string;
  findingKey: string;
  title: string;
  detail: string;
  department: string;
  priority: string;
  status: string;
  diagnosis?: string | null;
  resolution?: string | null;
  verification?: string | null;
  recommendations?: string | null;
  resolvedAt?: string | null;
  resolutionMinutes?: number | null;
  resolvedByName?: string | null;
  evidence: Array<{
    id: string;
    fileName: string;
    contentType: string;
    capturedAt: string;
    latitude?: number | null; longitude?: number | null; accuracyMeters?: number | null;
  }>;
  assignedTechnicianId?: string | null; assignedTechnicianName?: string | null;
  direccion?: string | null; sector?: string | null; municipio?: string | null; provincia?: string | null;
  latitude?: number | null; longitude?: number | null;
};
type FindingWorkDraft = {
  diagnosis: string;
  resolution: string;
  verification: string;
  recommendations: string;
  photos: File[];
};
type TicketManagementDraft = {
  status: string;
  priority: string;
  resolution: string;
  assignedTechnicianId: string;
  assignedDepartment: string;
  assignedTeam: TechnologyTeam | "";
  sharedWithDepartments: string[];
  routingComment: string;
  evidence: File[];
};
type TicketEvidenceLocation = {
  latitude: number;
  longitude: number;
  accuracyMeters: number;
  capturedAt: string;
  distanceMeters?: number | null;
};
type TicketStatistics = {
  from: string;
  to: string;
  active: number;
  resolved: number;
  byTechnician: Array<{
    technicianId?: string | null;
    technicianName: string;
    department: string;
    resolved: number;
    closed: number;
    totalResolved: number;
    averageResolutionHours: number;
  }>;
  byCategory: Array<{
    department: string;
    category: string;
    resolved: number;
    closed: number;
    totalResolved: number;
    averageResolutionHours: number;
  }>;
  byDepartment: Array<{
    department: string;
    open: number;
    inProgress: number;
    resolved: number;
    closed: number;
    total: number;
    averageResolutionHours: number;
  }>;
  history: Array<{ date: string; department: string; resolved: number }>;
};
type AgencyTransitionEvidence={id:string;fileName:string;contentType:string;capturedAt:string;url:string;latitude?:number|null;longitude?:number|null;accuracyMeters?:number|null;locationCapturedAt?:string|null;uploadedByName?:string|null};
type AgencyTransitionProgress = { id:string; note:string; progressStatus:"IN_PROGRESS"|"COMPLETED"|"EDITED"; entryType?:"ADVANCE"|"STAGE_EDIT"|"STAGE_COMPLETION"|"ASSIGNMENT"; createdByName:string; createdAt:string; updatedAt?:string|null; evidence?:AgencyTransitionEvidence[] };
type AgencyTransitionAssignment={assignmentType:"TECHNICIAN"|"CONTRACTOR";assignedUserId?:string|null;assignedName:string;targetProgressId:string};
const transitionProgressType=(entry:AgencyTransitionProgress):"ADVANCE"|"STAGE_EDIT"|"STAGE_COMPLETION"|"ASSIGNMENT"=>entry.entryType||(entry.progressStatus==="EDITED"?"STAGE_EDIT":entry.progressStatus==="COMPLETED"?"STAGE_COMPLETION":"ADVANCE");
const transitionAssignmentEntry=(entry:AgencyTransitionProgress):AgencyTransitionAssignment|null=>{if(transitionProgressType(entry)!=="ASSIGNMENT")return null;try{const value=JSON.parse(entry.note.replace(/^__ASSIGNMENT__/,"")) as Partial<AgencyTransitionAssignment>;return (value.assignmentType==="TECHNICIAN"||value.assignmentType==="CONTRACTOR")&&!!value.assignedName&&!!value.targetProgressId?{assignmentType:value.assignmentType,assignedUserId:value.assignedUserId||null,assignedName:value.assignedName,targetProgressId:value.targetProgressId}:null;}catch{return null;}};
const transitionRecordAssignment=(stage:AgencyTransitionStage,progressId:string):AgencyTransitionAssignment|null=>{const entries=(stage.progress||[]).map(transitionAssignmentEntry).filter((entry):entry is AgencyTransitionAssignment=>!!entry&&entry.targetProgressId===progressId);return entries.length?entries[entries.length-1]:null;};
type AgencyTransitionStage = { id:string; department:string; stageOrder:number; status:string; notes?:string|null; startedAt?:string|null; completedAt?:string|null; completedByName?:string|null; updatedAt:string; progress:AgencyTransitionProgress[] };
type AgencyTransition = { id:string; agencyId:string; codigo:string; terminal:string; grupo:string; projectType:"CONSTRUCTION"|"RESTRUCTURING"; status:string; currentStage:string; notes?:string|null; createdByName:string; createdAt:string; updatedAt:string; completedAt?:string|null; stages:AgencyTransitionStage[] };
type TransitionEvidenceViewer={project:AgencyTransition;stage:AgencyTransitionStage;entry:AgencyTransitionProgress;recordNumber:number};
type MaintenanceArea="WORKSHOP"|"WAREHOUSE";
type MaintenanceMovement={id:string;agencyId?:string|null;codigo:string;terminal:string;grupo:string;ticketId?:string|null;department:string;technicianUserId?:string|null;technicianName:string;movementType:string;equipmentType:string;componentType?:string|null;failureCause:string;serialNumber?:string|null;quantity:number;notes?:string|null;createdByName:string;createdAt:string;operationalArea:MaintenanceArea;documentNumber:string;qrToken:string;deliveredByName?:string|null;receivedByName?:string|null;destinationName?:string|null;signatureData?:string|null;occurredAt:string;receivedByLogin?:string|null};
type MaintenanceProduct={id:string;department:string;scanCode:string;productName:string;componentType?:string|null;updatedAt:string};
type MaintenanceDraft={agencyId:string;ticketId:string;technicianUserId:string;technicianName:string;movementType:string;equipmentType:string;componentType:string;failureCause:string;serialNumber:string;quantity:number;notes:string;operationalArea:MaintenanceArea;documentNumber:string;qrToken:string;deliveredByName:string;receivedByName:string;destinationName:string;signatureData:string;occurredAt:string};
type MaintenanceSupplier={id:string;name:string;taxId?:string|null;contactName?:string|null;phone?:string|null;email?:string|null;isActive:boolean;createdAt:string};
type MaintenanceRequisition={id:string;requisitionNumber:string;department:string;productName:string;quantityRequested:number;quantityFulfilled:number;priority:string;status:string;requestedByName:string;createdAt:string;updatedAt:string;notes?:string|null};
type MaintenancePurchaseOrder={id:string;orderNumber:string;supplierId:string;supplierName:string;requisitionId?:string|null;department:string;productName:string;quantityOrdered:number;quantityReceived:number;unitCost:number;status:string;expectedAt?:string|null;createdByName:string;createdAt:string;updatedAt:string;notes?:string|null};
type MaintenanceReturn={id:string;returnNumber:string;supplierId?:string|null;supplierName?:string|null;purchaseOrderId?:string|null;department:string;productName:string;serialNumber:string;quantity:number;reason:string;status:string;createdByName:string;createdAt:string;notes?:string|null};
type MaintenanceProcurement={suppliers:MaintenanceSupplier[];requisitions:MaintenanceRequisition[];purchaseOrders:MaintenancePurchaseOrder[];returns:MaintenanceReturn[]};

const technologyTeams: Record<TechnologyTeam, string> = {
  CALL_CENTER: "Call Center",
  TECHNICAL_FAILURE: "Avería Técnica",
  TECHNICIANS: "Técnicos",
};
const maintenanceMovementLabels:Record<string,string>={ENTRY:"Entrada",EXIT:"Salida / entrega",REQUEST:"Solicitud de equipo",NEW_DELIVERY:"Entrega de equipo nuevo",DAMAGED_RETURN:"Recepción de equipo dañado",TRANSFER_TO_WORKSHOP:"Envío a Taller",REPLACEMENT:"Reemplazo de equipo",COMPONENT_REPLACEMENT:"Cambio de componente",REPAIR:"Reparación",DISCHARGE:"Descargo definitivo"};

const TICKET_AGENCY_RADIUS_METERS=75;
const TICKET_GPS_TARGET_ACCURACY_METERS=12;
const TICKET_GPS_MAX_ACCURACY_METERS=50;
const TICKET_GPS_MIN_SAMPLES=3;
const ticketHaversine=(lat1:number,lon1:number,lat2:number,lon2:number)=>{const radius=6_371_000,p=Math.PI/180,dLat=(lat2-lat1)*p,dLon=(lon2-lon1)*p;const value=Math.sin(dLat/2)**2+Math.cos(lat1*p)*Math.cos(lat2*p)*Math.sin(dLon/2)**2;return radius*2*Math.atan2(Math.sqrt(value),Math.sqrt(1-value));};
const captureTicketGps=()=>new Promise<TicketEvidenceLocation>((resolve,reject)=>{
  if(!window.isSecureContext||!navigator.geolocation){reject(new Error("La evidencia necesita HTTPS y un dispositivo con ubicación disponible."));return;}
  let best:GeolocationPosition|null=null,watchId=0,timer=0,samples=0,done=false;const startedAt=Date.now();
  const finish=(force=false)=>{if(done||(!force&&(samples<TICKET_GPS_MIN_SAMPLES||Date.now()-startedAt<3500)))return;done=true;navigator.geolocation.clearWatch(watchId);window.clearTimeout(timer);if(!best){reject(new Error("No se obtuvo una coordenada GPS para la evidencia."));return;}if(best.coords.accuracy>TICKET_GPS_MAX_ACCURACY_METERS){reject(new Error(`La señal GPS está imprecisa (±${Math.round(best.coords.accuracy)} m). Acércate a una ventana o sal al exterior de la agencia y vuelve a intentarlo.`));return;}resolve({latitude:best.coords.latitude,longitude:best.coords.longitude,accuracyMeters:best.coords.accuracy,capturedAt:new Date(best.timestamp||Date.now()).toISOString()});};
  watchId=navigator.geolocation.watchPosition(position=>{const fresh=Date.now()-position.timestamp<=15000,valid=Number.isFinite(position.coords.latitude)&&Number.isFinite(position.coords.longitude)&&Number.isFinite(position.coords.accuracy)&&position.coords.accuracy>0&&!(position.coords.latitude===0&&position.coords.longitude===0);if(!fresh||!valid)return;samples+=1;if(!best||position.coords.accuracy<best.coords.accuracy)best=position;if(best.coords.accuracy<=TICKET_GPS_TARGET_ACCURACY_METERS)finish();},error=>{if(done)return;done=true;navigator.geolocation.clearWatch(watchId);window.clearTimeout(timer);reject(new Error(error.code===1?"Permite la ubicación desde el candado del navegador y vuelve a tomar la evidencia.":"No se obtuvo señal GPS. Activa Ubicación y Wi-Fi e inténtalo desde la agencia."));},{enableHighAccuracy:true,maximumAge:0,timeout:20000});
  timer=window.setTimeout(()=>finish(true),15000);
});
const normalizeMaintenanceSerial=(value:string)=>value.trim().replace(/[\r\n\t]/g,"").toLocaleUpperCase("es").slice(0,120);
const maintenanceProductSuggestion=(value:string)=>{const clean=value.trim().replace(/[_-]+/g," ").replace(/\s+/g," ");return clean.length>=3&&clean.length<=60&&/[a-záéíóúñ]/i.test(clean)?clean.toLocaleLowerCase("es").replace(/(^|\s)\p{L}/gu,letter=>letter.toLocaleUpperCase("es")):"";};
const maintenanceLocalNow=()=>{const date=new Date();date.setMinutes(date.getMinutes()-date.getTimezoneOffset());return date.toISOString().slice(0,16);};
const createMaintenanceIdentity=(area:MaintenanceArea)=>{const now=new Date();const stamp=`${now.getFullYear()}${String(now.getMonth()+1).padStart(2,"0")}${String(now.getDate()).padStart(2,"0")}-${String(now.getHours()).padStart(2,"0")}${String(now.getMinutes()).padStart(2,"0")}${String(now.getSeconds()).padStart(2,"0")}`;const suffix=crypto.randomUUID().replace(/-/g,"").slice(0,5).toUpperCase();const documentNumber=`${area==="WORKSHOP"?"TAL":"ALM"}-${stamp}-${suffix}`;return{documentNumber,qrToken:`REAL-MNT|${documentNumber}`};};
const newMaintenanceDraft=(area:MaintenanceArea="WORKSHOP",movementType="ENTRY"):MaintenanceDraft=>{const identity=createMaintenanceIdentity(area);return{agencyId:"",ticketId:"",technicianUserId:"",technicianName:"",movementType,equipmentType:"",componentType:"",failureCause:movementType==="ENTRY"?"Registro inicial de activo":"",serialNumber:"",quantity:1,notes:"",operationalArea:area,documentNumber:identity.documentNumber,qrToken:identity.qrToken,deliveredByName:"",receivedByName:"",destinationName:"",signatureData:"",occurredAt:maintenanceLocalNow()};};
const technologyEquipment=["Router","Módem","Switch de red","Pantalla","Computadora","Impresora","Escáner","Cámara","Cableado de datos","Máquina 2Connect (Todo en uno)","Impresora POS 2Connect 80-01","Impresora POS V6","Impresora POS V7","Impresora POS V8","Escáner 2Connect","Escáner Witek","Módem Internet Altice","Módem Internet Claro","TV exterior / Rapidita","Monitor de venta","CPU Dell compacto","Mouse","Teclado","DS / Cajita de Raza o Perros","Otro equipo tecnológico"];
const technologyComponents=["Fuente de poder","Disco/SSD","Memoria RAM","Tarjeta de red","Pantalla/Panel","Cable HDMI","Cable de red","Cable VGA","Adaptador DP a VGA","Adaptador DP a HDMI","Fuente de impresora POS","Fuente de escáner 2Connect","Fuente de escáner Witek","Regleta","Cable de corriente / Power cord","Conector","Ventilador","Batería interna","Otro componente"];
const technologyCauses=["Equipo no enciende","Sin internet","Daño eléctrico","Sobrevoltaje","Componente quemado","Pantalla rota","Falla de cableado","Desgaste","Daño por humedad","Mantenimiento preventivo","Otra causa"];
const generalEquipment=["Planta eléctrica","Inversor","Banco de baterías","Bombillo/Lámpara","Interruptor","Tomacorriente","Breaker","Cable eléctrico","Aire acondicionado","Puerta/Cerradura/Shutter","Plomería","Pintura","Mobiliario","Sheetrock/Estructura","Otro activo de Servicios Generales"];
const generalComponents=["Motor/alternador","Batería","Tarjeta de inversor","Breaker","Cable eléctrico","Lámpara","Interruptor","Compresor","Tubería","Cerradura","Pieza de mobiliario","Panel de sheetrock","Otro componente"];
const generalCauses=["Planta eléctrica fuera de servicio","Inversor averiado","Batería agotada o dañada","Factura de energía no pagada","Bombillo quemado","Cortocircuito","Falla de cableado eléctrico","Breaker disparado o dañado","Fuga de agua","Aire acondicionado averiado","Daño estructural","Desgaste","Mantenimiento preventivo","Otra causa"];
const humanResourcesEquipment=["Expediente de personal","Reloj ponchador/Biométrico","Carné de empleado","Uniforme o EPP","Mobiliario de personal","Equipo de capacitación","Archivo físico","Otro recurso de RR. HH."];
const humanResourcesComponents=["Documento o formulario","Lector biométrico","Tarjeta/Carné","Uniforme","Accesorio de protección","Pieza de mobiliario","Material de capacitación","Otro componente"];
const humanResourcesCauses=["Expediente incompleto o deteriorado","Reloj ponchador fuera de servicio","Carné dañado o perdido","Uniforme o EPP deteriorado","Mobiliario dañado","Material de capacitación deteriorado","Reposición preventiva","Otra causa"];
const maintenanceCatalog:Record<string,{equipment:string[];components:string[];causes:string[]}>= {
  TECHNOLOGY:{equipment:technologyEquipment,components:technologyComponents,causes:technologyCauses},
  GENERAL_SERVICES:{equipment:generalEquipment,components:generalComponents,causes:generalCauses},
  HUMAN_RESOURCES:{equipment:humanResourcesEquipment,components:humanResourcesComponents,causes:humanResourcesCauses},
};
const automaticFindingCategory=(finding:AutomaticFinding):{key:string;title:string}=>{
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
  return mappings[finding.department]?.[finding.findingKey]||{key:finding.findingKey||"OTHER",title:finding.title||"Otros hallazgos"};
};

const categories: Record<string, string> = {
  EQUIPMENT: "Equipos",
  CONNECTIVITY: "Conectividad",
  INFRASTRUCTURE: "Infraestructura",
  PRINTER: "Impresora",
  SECURITY: "Seguridad",
  OTHER: "Otro",
};
const priorities: Record<string, string> = {
  LOW: "Baja",
  MEDIUM: "Media",
  HIGH: "Alta",
  CRITICAL: "Crítica",
};
const ticketPriorityForCategory = (department: string, category: string, fallback = "MEDIUM") =>
  department === "TECHNOLOGY" && category === "CPU_FAILURE" ? "CRITICAL" : fallback;
const statuses: Record<string, string> = {
  OPEN: "Abierto",
  IN_PROGRESS: "En proceso",
  PENDING: "Pendiente",
  RESOLVED: "Resuelto",
  CLOSED: "Cerrado",
  CANCELLED: "Anulado",
};
const missingTicketText = (value: unknown) => {
  const text = String(value ?? "").trim();
  return !text || ["undefined", "null", "nan"].includes(text.toLowerCase());
};
const ticketText = (value: unknown, fallback: string) => {
  const text = String(value ?? "").trim();
  return missingTicketText(text) ? fallback : text;
};
// SQL Server devuelve datetime2 en UTC pero sin el sufijo de zona. Si se deja
// como una fecha "naive", el navegador la interpreta como hora local y los
// tickets aparecen desplazados. Marcamos explícitamente esos valores como UTC;
// toLocaleString() se encarga después de mostrarlos en la zona del dispositivo.
const deviceDate = (value: unknown, fallback = new Date().toISOString()) => {
  const text = ticketText(value, "").trim();
  if (!text) return fallback;
  const zoned = /(?:z|[+-]\d{2}:?\d{2})$/i.test(text) ? text : `${text}Z`;
  return Number.isFinite(Date.parse(zoned)) ? zoned : fallback;
};
const ticketDate = (value: unknown) => deviceDate(value);
const departments: Record<string, string> = {
  TECHNOLOGY: "Tecnología",
  GENERAL_SERVICES: "Servicios Generales",
  HUMAN_RESOURCES: "Recursos Humanos",
};
const transitionDepartmentLabels: Record<string, string> = {
  GENERAL_SERVICES: "Servicios Generales",
  TECHNOLOGY: "Tecnología",
  HUMAN_RESOURCES: "Recursos Humanos",
};
const transitionDepartmentResponsibilities: Record<string, string> = {
  GENERAL_SERVICES: "Remodelación y adecuación de la agencia",
  TECHNOLOGY: "Instalación tecnológica y ejecución del trabajo técnico",
  HUMAN_RESOURCES: "Contratación y preparación del personal",
};
const resolvedStatuses = new Set(["RESOLVED", "CLOSED", "CANCELLED"]);
const successfulStatuses = new Set(["RESOLVED", "CLOSED"]);
const normalizeSupportTicket = (raw: Partial<Ticket>): Ticket => {
  const rawTeam = ticketText(raw.assignedTeam, "");
  const assignedTeam = (["CALL_CENTER", "TECHNICAL_FAILURE", "TECHNICIANS"] as const).includes(rawTeam as TechnologyTeam)
    ? rawTeam as TechnologyTeam
    : null;
  const rawPriority = ticketText(raw.priority, "MEDIUM").toUpperCase();
  const rawStatus = ticketText(raw.status, "OPEN").toUpperCase();
  return {
    ...raw,
    id: ticketText(raw.id, `ticket-${Number(raw.ticketNumber) || Date.now()}`),
    ticketNumber: Number.isFinite(Number(raw.ticketNumber)) ? Number(raw.ticketNumber) : 0,
    codigo: ticketText(raw.codigo, "Sin código"),
    terminal: ticketText(raw.terminal, "Agencia sin nombre"),
    grupo: ticketText(raw.grupo, "Sin grupo registrado"),
    category: ticketText(raw.category, "OTHER"),
    assignedDepartment: ticketText(raw.assignedDepartment, "TECHNOLOGY").toUpperCase(),
    priority: Object.prototype.hasOwnProperty.call(priorities, rawPriority) ? rawPriority : "MEDIUM",
    status: Object.prototype.hasOwnProperty.call(statuses, rawStatus) ? rawStatus : "OPEN",
    subject: ticketText(raw.subject, "Ticket sin asunto"),
    description: ticketText(raw.description, "Sin detalle registrado"),
    resolution: missingTicketText(raw.resolution) ? null : String(raw.resolution).trim(),
    createdByName: ticketText(raw.createdByName, ticketText(raw.createdByUsername, "Usuario")),
    createdByUsername: ticketText(raw.createdByUsername, ticketText(raw.createdByName, "usuario")),
    createdAt: ticketDate(raw.createdAt),
    updatedAt: ticketDate(missingTicketText(raw.updatedAt) ? raw.createdAt : raw.updatedAt),
    closedAt: missingTicketText(raw.closedAt) ? null : deviceDate(raw.closedAt, ""),
    isAutomatic: raw.isAutomatic === true,
    ticketType: raw.ticketType === "INTERNAL" ? "INTERNAL" : "SUPPORT",
    assignedTechnicianId: missingTicketText(raw.assignedTechnicianId) ? null : String(raw.assignedTechnicianId).trim(),
    assignedTechnicianName: missingTicketText(raw.assignedTechnicianName) ? null : String(raw.assignedTechnicianName).trim(),
    assignedTeam,
    sharedWithDepartments: Array.isArray(raw.sharedWithDepartments) ? raw.sharedWithDepartments.filter(Boolean).map(String) : [],
    evidence: Array.isArray(raw.evidence) ? raw.evidence.map((item) => ({
      ...item,
      capturedAt: deviceDate(item.capturedAt),
      locationCapturedAt: item.locationCapturedAt ? deviceDate(item.locationCapturedAt, "") : null,
    })) : [],
    history: Array.isArray(raw.history) ? raw.history.map((item) => ({ ...item, createdAt: deviceDate(item.createdAt) })) : [],
    resolvedByName: missingTicketText(raw.resolvedByName) ? null : String(raw.resolvedByName).trim(),
  };
};
const effectiveTechnologyTeam = (ticket: Ticket): TechnologyTeam | null => {
  if (ticket.assignedDepartment !== "TECHNOLOGY") return null;
  if (ticket.assignedTeam) return ticket.assignedTeam;
  if ((ticket.history || []).some((entry) => entry.toTeam === "TECHNICAL_FAILURE" || entry.fromTeam === "TECHNICAL_FAILURE")) return "TECHNICAL_FAILURE";
  if ((ticket.history || []).some((entry) => entry.toTeam === "CALL_CENTER" || entry.fromTeam === "CALL_CENTER")) return "CALL_CENTER";
  return "TECHNICAL_FAILURE";
};
const ticketBelongsToTechnologyTeam=(ticket:Ticket,team:"ALL"|TechnologyTeam)=>team==="ALL"||effectiveTechnologyTeam(ticket)===team||(ticket.status==="IN_PROGRESS"&&!!ticket.assignedTechnicianId&&(team==="CALL_CENTER"||team==="TECHNICAL_FAILURE")&&(ticket.history||[]).some(entry=>entry.fromTeam===team||entry.toTeam===team));
const ticketMinutes = (ticket: Ticket) =>
  ticket.resolutionMinutes ??
  Math.max(
    0,
    Math.round(
      (new Date(ticket.closedAt || ticket.updatedAt).getTime() -
        new Date(ticket.createdAt).getTime()) /
        60000,
    ),
  );
const compactDuration = (minutes: number) => {
  if (minutes < 60) return `${minutes} min`;
  if (minutes < 1440)
    return `${Math.floor(minutes / 60)} h ${minutes % 60} min`;
  return `${Math.floor(minutes / 1440)} d ${Math.floor((minutes % 1440) / 60)} h`;
};
const historyActionLabels: Record<string, string> = {
  CREATED: "Ticket creado",
  SUPERVISOR_ESCALATED: "Supervisor justificó el escalamiento",
  ASSIGNED: "Técnico asignado",
  REASSIGNED: "Ticket reasignado",
  ROUTED: "Ticket transferido",
  ESCALATED: "Escalado a Avería Técnica",
  SHARED: "Compartido con otro departamento",
  IN_PROGRESS: "Atención iniciada",
  PENDING: "Caso pendiente",
  RESOLVED: "Caso resuelto",
  CLOSED: "Caso cerrado",
};
const elapsedCaseTime = (start: string, end?: string | null) => {
  const minutes = Math.max(
    0,
    Math.floor(
      (new Date(end || Date.now()).getTime() - new Date(start).getTime()) /
        60000,
    ),
  );
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  const remaining = minutes % 60;
  return [
    days ? `${days} día${days === 1 ? "" : "s"}` : "",
    hours ? `${hours} hora${hours === 1 ? "" : "s"}` : "",
    `${remaining} minuto${remaining === 1 ? "" : "s"}`,
  ]
    .filter(Boolean)
    .join(", ");
};

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(path, options);
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(
      data.error || "No fue posible completar la solicitud.",
    ) as Error & { status?: number };
    error.status = response.status;
    throw error;
  }
  return data as T;
}

const agencyEvidenceMime = (file: File) => {
  const declared = file.type.trim().toLowerCase();
  if (declared) return declared;
  const extension = file.name.split(".").pop()?.toLowerCase();
  return extension === "jpg" || extension === "jpeg"
    ? "image/jpeg"
    : extension === "png"
      ? "image/png"
      : extension === "webp"
        ? "image/webp"
        : extension === "heic"
          ? "image/heic"
          : extension === "heif"
            ? "image/heif"
            : "";
};

async function prepareAgencyEvidence(file: File): Promise<File> {
  const mime = agencyEvidenceMime(file);
  const directlySupported = new Set(["image/jpeg", "image/jpg", "image/png", "image/webp"]);
  // Las fotos pequeñas se conservan; las tomadas por el teléfono se reducen antes de usar la red móvil.
  if (file.size <= 1200 * 1024 && directlySupported.has(mime)) {
    return file.type ? file : new File([file], file.name, { type: mime, lastModified: file.lastModified });
  }
  if (file.size > 24 * 1024 * 1024) throw new Error(`${file.name} supera 24 MB y no puede prepararse desde el teléfono.`);
  const imageUrl = URL.createObjectURL(file);
  try {
    const image = await new Promise<HTMLImageElement>((resolve, reject) => {
      const candidate = new Image();
      candidate.onload = () => resolve(candidate);
      candidate.onerror = () => reject(new Error(`No se pudo leer ${file.name}. Usa JPG, PNG, WebP, HEIC o HEIF.`));
      candidate.src = imageUrl;
    });
    const maxEdge = 1600;
    const scale = Math.min(1, maxEdge / Math.max(image.naturalWidth, image.naturalHeight));
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
    canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
    const context = canvas.getContext("2d");
    if (!context) throw new Error("El navegador no pudo preparar la fotografía.");
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, "image/jpeg", .78));
    if (!blob || blob.size <= 0 || blob.size > 8 * 1024 * 1024) throw new Error(`${file.name} no pudo reducirse al tamaño permitido.`);
    const baseName = file.name.replace(/\.[^.]+$/, "") || "evidencia";
    return new File([blob], `${baseName}.jpg`, { type: "image/jpeg", lastModified: Date.now() });
  } finally {
    URL.revokeObjectURL(imageUrl);
  }
}

export default function TicketCenter({
  session,
  onSessionExpired,
  navigationTarget,
  supervisorInboxRequestKey,
  maintenanceEntry,
  reportAgency,
  onCloseReport,
}: {
  session: PortalSession;
  onSessionExpired: () => void;
  navigationTarget?: SupportNavigationTarget | null;
  supervisorInboxRequestKey?: number;
  maintenanceEntry?: MaintenanceArea | null;
  reportAgency?: TicketAgency | null;
  onCloseReport?: () => void;
}) {
  const onSessionExpiredRef = useRef(onSessionExpired);
  useEffect(() => {
    onSessionExpiredRef.current = onSessionExpired;
  }, [onSessionExpired]);

  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [findings, setFindings] = useState<AutomaticFinding[]>([]);
  const [agencies, setAgencies] = useState<TicketAgency[]>([]);
  const [catalog, setCatalog] = useState<SupportDepartment[]>([]);
  const [technicians, setTechnicians] = useState<Technician[]>([]);
  const [_ticketStatistics, setTicketStatistics] =
    useState<TicketStatistics | null>(null);
  const [agencyTransitions,setAgencyTransitions]=useState<AgencyTransition[]>([]);
  const [maintenanceMovements,setMaintenanceMovements]=useState<MaintenanceMovement[]>([]);
  const [maintenanceLoading,setMaintenanceLoading]=useState(true);
  const [trackingCode,setTrackingCode]=useState<string|null>(null);
  const [maintenanceProducts,setMaintenanceProducts]=useState<MaintenanceProduct[]>([]);
  const [maintenanceProcurement,setMaintenanceProcurement]=useState<MaintenanceProcurement>({suppliers:[],requisitions:[],purchaseOrders:[],returns:[]});
  const [maintenanceProcurementMode,setMaintenanceProcurementMode]=useState<"supplier"|"requisition"|"order"|"receive"|"return"|null>(null);
  const [maintenanceProcurementTarget,setMaintenanceProcurementTarget]=useState<string>("");
  const [maintenanceProcurementError,setMaintenanceProcurementError]=useState("");
  const [maintenanceProcurementDraft,setMaintenanceProcurementDraft]=useState({supplierId:"",supplierName:"",requisitionId:"",productName:"",quantity:1,unitCost:0,priority:"MEDIUM",expectedAt:"",serialNumber:"",receivedByName:"",reason:"",taxId:"",contactName:"",phone:"",email:"",notes:""});
  const [maintenanceFormOpen,setMaintenanceFormOpen]=useState(false);
  const [warehouseRequestOpen,setWarehouseRequestOpen]=useState(false);
  const [maintenanceFormError,setMaintenanceFormError]=useState("");
  const [maintenanceDraft,setMaintenanceDraft]=useState<MaintenanceDraft>(()=>newMaintenanceDraft());
  const [maintenanceArea,setMaintenanceArea]=useState<MaintenanceArea|null>(maintenanceEntry??null);
  const [maintenanceSelectedDepartment,setMaintenanceSelectedDepartment]=useState<string|null>(null);
  const [documentToEdit,setDocumentToEdit]=useState<MaintenanceMovement|null>(null);
  const [warehousePanel,setWarehousePanel]=useState<MaintenanceChannel|null>(null);
  useEffect(()=>{setWarehousePanel(null);},[maintenanceArea]);
  const [maintenanceView,setMaintenanceView]=useState<"dashboard"|"inventory"|"history"|"procurement">("dashboard");
  const [maintenanceQuery,setMaintenanceQuery]=useState("");
  const [maintenanceMovementFilter,setMaintenanceMovementFilter]=useState("ALL");
  const [warehouseRequestHistoryCount,setWarehouseRequestHistoryCount]=useState(0);
  const [maintenanceScannerActive,setMaintenanceScannerActive]=useState(false);
  const [maintenanceScanFeedback,setMaintenanceScanFeedback]=useState<{kind:"ready"|"new"|"existing";message:string}|null>(null);
  const [maintenanceScannedMovement,setMaintenanceScannedMovement]=useState<MaintenanceMovement|null>(null);
  const [maintenanceLoadError,setMaintenanceLoadError]=useState("");
  const [maintenanceInventoryImport,setMaintenanceInventoryImport]=useState<MaintenanceInventoryImportPreview|null>(null);
  const [maintenanceInventoryImportOpen,setMaintenanceInventoryImportOpen]=useState(false);
  const [maintenanceInventoryImportProgress,setMaintenanceInventoryImportProgress]=useState(0);
  const maintenanceSerialInputRef=useRef<HTMLInputElement|null>(null);
  const maintenanceInventoryFileRef=useRef<HTMLInputElement|null>(null);
  const maintenanceScannerBufferRef=useRef({value:"",lastAt:0});
  const loadInFlightRef=useRef(false);
  const loadHasDataRef=useRef(false);
  const maintenanceLoadInFlightRef=useRef(false);
  const maintenanceRetryAfterRef=useRef(0);
  const [transitionFormOpen,setTransitionFormOpen]=useState(false);
  const [transitionDraft,setTransitionDraft]=useState({agencyId:"",projectType:"RESTRUCTURING",notes:""});
  const [transitionAgencyQuery,setTransitionAgencyQuery]=useState("");
  const [transitionStatusFilter,setTransitionStatusFilter]=useState<"ACTIVE"|"PENDING"|"COMPLETED">("ACTIVE");
  const [transitionListQuery,setTransitionListQuery]=useState("");
  const [transitionNotes,setTransitionNotes]=useState<Record<string,string>>({});
  const [selectedTransitionId,setSelectedTransitionId]=useState<string|null>(null);
  const [selectedTransitionStageId,setSelectedTransitionStageId]=useState<string|null>(null);
  const [transitionEditMode,setTransitionEditMode]=useState(false);
  const [transitionStageEditNotes,setTransitionStageEditNotes]=useState("");
  const [transitionProgressEditId,setTransitionProgressEditId]=useState<string|null>(null);
  const [transitionProgressEditNote,setTransitionProgressEditNote]=useState("");
  const [transitionRecordDecisionStageId,setTransitionRecordDecisionStageId]=useState<string|null>(null);
  const [transitionAssignmentRecordId,setTransitionAssignmentRecordId]=useState<string|null>(null);
  const [transitionEvidenceUploadingId,setTransitionEvidenceUploadingId]=useState<string|null>(null);
  const [transitionEvidenceFeedback,setTransitionEvidenceFeedback]=useState<{entryId:string;kind:"working"|"success"|"error";message:string}|null>(null);
  const [transitionEvidenceViewer,setTransitionEvidenceViewer]=useState<TransitionEvidenceViewer|null>(null);
  const [expandedTransitionRecords,setExpandedTransitionRecords]=useState<Record<string,boolean>>({});
  const [transitionEvidenceLocations,setTransitionEvidenceLocations]=useState<Record<string,{latitude:number;longitude:number;accuracyMeters:number;capturedAt:string}>>({});
  const [transitionAssignmentDraft,setTransitionAssignmentDraft]=useState<{assignmentType:"TECHNICIAN"|"CONTRACTOR";technicianUserId:string;contractorName:string}>({assignmentType:"TECHNICIAN",technicianUserId:"",contractorName:""});
  const transitionNewRecordRef=useRef<HTMLTextAreaElement|null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [query, setQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("ACTIVE");
  const [priorityFilter, setPriorityFilter] = useState("ALL");
  const [categoryFilter, setCategoryFilter] = useState("ALL");
  const [technicianFilter, setTechnicianFilter] = useState("ALL");
  const [ticketWorkspaceView, setTicketWorkspaceView] = useState<
    "menu" | "active" | "process" | "history" | "reports"
  >("menu");
  const [technologyTeamFilter, setTechnologyTeamFilter] = useState<
    "ALL" | TechnologyTeam
  >(session.supportTeam === "CALL_CENTER" || session.supportTeam === "TECHNICAL_FAILURE" || session.supportTeam === "TECHNICIANS" ? session.supportTeam : "ALL");
  const [assignedToMeOnly, setAssignedToMeOnly] = useState(false);
  const [personalAssignmentView,setPersonalAssignmentView]=useState(false);
  const [supervisorHistoryView,setSupervisorHistoryView]=useState(false);
  const [teamViewChosen, setTeamViewChosen] = useState(false);
  const [technicianPickerOpen,setTechnicianPickerOpen]=useState(false);
  const [personnelKind,setPersonnelKind]=useState<"TECHNICIANS"|"SUPERVISORS"|null>(null);
  const [personnelQuery,setPersonnelQuery]=useState("");
  const [personnelLayout,setPersonnelLayout]=useState<"GRID"|"LIST">("GRID");
  const [assignmentScope, setAssignmentScope] = useState<"ALL" | "UNASSIGNED" | "MINE">("ALL");
  const [responsibleQuery, setResponsibleQuery] = useState("");
  const [reportDimension, setReportDimension] = useState<
    "technician" | "category" | "department" | "group"
  >("technician");
  const [reportPeriod,setReportPeriod]=useState<"DAY"|"WEEK"|"MONTH"|"ALL">("MONTH");
  const [activeReportModule,setActiveReportModule]=useState<"overview"|"productivity"|"department"|"rankings"|"personnel"|"resolved"|"findings"|null>(null);
  const [reportOwner,setReportOwner]=useState("ALL");
  const [expandedHistoryTicketId, setExpandedHistoryTicketId] = useState<
    string | null
  >(null);
  const [expandedTicketId, setExpandedTicketId] = useState<string | null>(null);
  const [directTicketFocus, setDirectTicketFocus] = useState<{
    id?: string;
    ticketNumber?: number;
  } | null>(null);
  const directTicketFocusRef = useRef(false);
  const supervisorInboxOpeningRef = useRef(false);
  const [ticketActionMode, setTicketActionMode] = useState<
    "evidence" | "transfer" | "assign" | "resolve" | "cancel" | null
  >(null);
  const [ticketPage, setTicketPage] = useState(1);
  const [adminControlOpen, setAdminControlOpen] = useState(false);
  const [ticketTypeFilter, setTicketTypeFilter] = useState<
    "SUPPORT" | "INTERNAL"
  >(session.supportTeam === "CALL_CENTER" ? "INTERNAL" : "SUPPORT");
  const [supportTab, setSupportTab] = useState<"tickets" | "findings">(
    "tickets",
  );
  const [supportStage, setSupportStage] = useState<
    "home" | "tickets" | "findings" | "transitions" | "maintenance" | "agencyDirectory"
  >(maintenanceEntry?"maintenance":"home");
  const [findingGroup, setFindingGroup] = useState("ALL");
  const [findingCategoryOpen,setFindingCategoryOpen]=useState<string|null>(null);
  const [findingView, setFindingView] = useState<
    "menu" | "pending" | "resolved" | "tracking"
  >("menu");
  const [findingLayout,setFindingLayout]=useState<"GRID"|"LIST">("GRID");
  const [selectedFindingId,setSelectedFindingId]=useState<string|null>(null);
  const [findingActionMode,setFindingActionMode]=useState<"details"|"assign"|"resolve"|"evidence">("details");
  const [findingAssigneeKind,setFindingAssigneeKind]=useState<"ALL"|"TECHNICIANS"|"SUPERVISORS">("ALL");
  const [findingAssigneeFilter,setFindingAssigneeFilter]=useState("ALL");
  const [lastSync, setLastSync] = useState<Date | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [supervisorEscalationReason, setSupervisorEscalationReason] = useState("");
  const [supervisorCreationEvidence, setSupervisorCreationEvidence] = useState<File[]>([]);
  const [returnToTicketCreation, setReturnToTicketCreation] = useState(false);
  const [duplicateCreationConfirmed, setDuplicateCreationConfirmed] = useState("");
  const [configOpen, setConfigOpen] = useState(false);
  const [workspaceDepartment, setWorkspaceDepartment] = useState<string | null>(
    session.role === "Administrator" || session.role === "GroupAdministrator"
      ? null
      : session.supportDepartment,
  );
  const workspaceDepartmentRef = useRef(workspaceDepartment);
  workspaceDepartmentRef.current = workspaceDepartment;
  const [categoryEdit, setCategoryEdit] = useState<
    Partial<SupportCategory> & { departmentCode?: string }
  >({
    defaultPriority: "MEDIUM",
    requiresDetail: true,
    isActive: true,
    displayOrder: 100,
  });
  const [departmentEdit, setDepartmentEdit] = useState({
    code: "",
    name: "",
    description: "",
    accentColor: "#38BDF8",
    displayOrder: 100,
    isActive: true,
  });
  const [agencyQuery, setAgencyQuery] = useState("");
  const [agencyDirectoryQuery,setAgencyDirectoryQuery]=useState("");
  const [agencyDirectoryEditId,setAgencyDirectoryEditId]=useState<string|null>(null);
  const [agencyDirectoryDraft,setAgencyDirectoryDraft]=useState<AgencyDirectoryDraft|null>(null);
  const [agencyDirectoryImport,setAgencyDirectoryImport]=useState<AgencyDirectoryImportPreview|null>(null);
  const [agencyDirectoryImportResult,setAgencyDirectoryImportResult]=useState<AgencyDirectoryImportResult|null>(null);
  const agencyDirectoryFileRef=useRef<HTMLInputElement>(null);
  const [draft, setDraft] = useState({
    agencyId: "",
    category: "",
    assignedDepartment: "",
    priority: "MEDIUM",
    subject: "",
    description: "",
    ticketType: "SUPPORT" as "SUPPORT" | "INTERNAL",
    assignedTechnicianId: "",
    assignedTeam: "" as TechnologyTeam | "",
    detail: "",
    option: "",
  });
  useEffect(() => {
    if (session.role !== "GroupAdministrator" || !supervisorInboxRequestKey) return;
    supervisorInboxOpeningRef.current = workspaceDepartmentRef.current !== null;
    setConfigOpen(false);
    setSupportTab("tickets");
    setSupportStage("tickets");
    setWorkspaceDepartment(null);
    setTicketTypeFilter("SUPPORT");
    setTicketWorkspaceView("active");
    setStatusFilter("ACTIVE");
    setPriorityFilter("ALL");
    setCategoryFilter("ALL");
    setTechnicianFilter("ALL");
    setAssignmentScope("MINE");
    setAssignedToMeOnly(true);
    setPersonalAssignmentView(true);
    setTeamViewChosen(true);
    setQuery("");
  }, [session.role, supervisorInboxRequestKey]);
  const [selectedCases, setSelectedCases] = useState<
    Array<{ department: string; category: string }>
  >([]);
  const [caseDetails, setCaseDetails] = useState<
    Record<string, { option: string; detail: string }>
  >({});
  const [management, setManagement] = useState<
    Record<string, TicketManagementDraft>
  >({});
  const [ticketEvidenceLocations,setTicketEvidenceLocations]=useState<Record<string,TicketEvidenceLocation>>({});
  const [findingWork, setFindingWork] = useState<
    Record<string, FindingWorkDraft>
  >({});
  const canCreateTickets = session.permissions.canCreateTickets;
  const isSupportTechnician = session.permissions.canViewSupportDashboard;
  const isWarehouseOperator=session.supportTeam==="WAREHOUSE";
  const isWorkshopOperator=session.supportTeam==="WORKSHOP";
  const isMaintenanceOperator=isWarehouseOperator||isWorkshopOperator;
  const isTechnologyDepartmentManager=Boolean(
    session.supportDepartment==="TECHNOLOGY"&&
    !session.supportTeam&&
    session.permissions.canAssignTickets
  );
  // Tecnología solicita equipos a Almacén; no necesita entrar al portal de
  // Taller ni a las operaciones de inventario que administra el almacenero.
  // El administrador principal también usa esta vista al abrir Tecnología;
  // antes V315 lo enviaba por error al selector general Taller / Almacén.
  const isTechnologyWarehouseRequester=Boolean(
    !maintenanceEntry&&
    !isMaintenanceOperator&&
    (isTechnologyDepartmentManager||
      (session.role==="Administrator"&&workspaceDepartment==="TECHNOLOGY"))
  );
  const isDedicatedWarehousePortal=isWarehouseOperator||(session.role==="Administrator"&&maintenanceEntry==="WAREHOUSE");
  const isDedicatedWorkshopPortal=isWorkshopOperator||(session.role==="Administrator"&&maintenanceEntry==="WORKSHOP");
  const isDedicatedMaintenancePortal=isDedicatedWarehousePortal||isDedicatedWorkshopPortal;
  const sessionRoutingTeam:TechnologyTeam|"ALL"=session.supportTeam==="CALL_CENTER"||session.supportTeam==="TECHNICAL_FAILURE"||session.supportTeam==="TECHNICIANS"?session.supportTeam:"ALL";
  const isTechnologyAreaSupport=session.role==="Technology"&&(session.supportTeam==="CALL_CENTER"||session.supportTeam==="TECHNICAL_FAILURE");
  const showFullSupportHome=!isTechnologyAreaSupport||session.supportTeam==="TECHNICAL_FAILURE";
  const lockedTechnologyTeam:TechnologyTeam|null=session.supportTeam==="CALL_CENTER"||session.supportTeam==="TECHNICIANS"?session.supportTeam:null;
  const allowedTechnologyTeams:TechnologyTeam[]=session.supportTeam==="CALL_CENTER"?["CALL_CENTER"]:session.supportTeam==="TECHNICAL_FAILURE"?["TECHNICAL_FAILURE","TECHNICIANS"]:session.supportTeam==="TECHNICIANS"?["TECHNICIANS"]:["CALL_CENTER","TECHNICAL_FAILURE","TECHNICIANS"];
  const strictTechnologyTechnician=
    session.role !== "Administrator" &&
    !!session.supportDepartment &&
    !isMaintenanceOperator &&
    // El equipo Técnicos siempre usa su bandeja personal. Esto también cubre
    // cuentas antiguas que conservaron por error el permiso de asignación.
    (session.supportTeam === "TECHNICIANS" || !session.permissions.canAssignTickets);
  const maintenanceDepartment=isDedicatedMaintenancePortal?(maintenanceSelectedDepartment||""):session.supportDepartment||workspaceDepartment||"";
  const isWarehouseDepartmentRequester=!isDedicatedWarehousePortal&&!isWarehouseOperator&&["TECHNOLOGY","GENERAL_SERVICES"].includes(maintenanceDepartment);
  useEffect(()=>{let cancelled=false;const load=async()=>{if(!isWarehouseDepartmentRequester){if(!cancelled)setWarehouseRequestHistoryCount(0);return}const name=maintenanceDepartment==="TECHNOLOGY"?"Tecnología":"Servicios Generales";try{const response=await fetch(`/api/institutional-documents?area=communications&channel=requirements&view=sent&department=${encodeURIComponent(name)}`,{credentials:"same-origin",cache:"no-store"});if(response.ok&&!cancelled)setWarehouseRequestHistoryCount((await response.json()).length)}catch{if(!cancelled)setWarehouseRequestHistoryCount(0)}};void load();const refresh=()=>void load();window.addEventListener("support-live-refresh",refresh);return()=>{cancelled=true;window.removeEventListener("support-live-refresh",refresh)}},[isWarehouseDepartmentRequester,maintenanceDepartment]);
  const maintenanceDepartmentCatalog=maintenanceCatalog[maintenanceDepartment]||maintenanceCatalog.TECHNOLOGY;
  const maintenanceAvailableMovements=maintenanceDraft.operationalArea==="WAREHOUSE"?["ENTRY","REQUEST","NEW_DELIVERY","DAMAGED_RETURN","TRANSFER_TO_WORKSHOP","EXIT","DISCHARGE"]:["ENTRY","REPAIR","REPLACEMENT","COMPONENT_REPLACEMENT","EXIT"];
  const maintenanceOperationalCauses=[...new Set([...maintenanceDepartmentCatalog.causes,...(maintenanceDraft.operationalArea==="WAREHOUSE"?["Solicitud de Tecnología","Recepción de equipos nuevos","Entrega de equipo nuevo","Recepción por daño o reemplazo","Transferencia controlada a Taller","Despacho a agencia o departamento","Descargo por obsolescencia","Descargo por daño irreparable","Ajuste de inventario"]:["Recepción desde Almacén","Recepción desde agencia","Entrega posterior a reparación","Reemplazo autorizado","Diagnóstico y reparación","Equipo no reparable"])])];
  const departmentMaintenanceMovements=maintenanceMovements.filter(item=>!maintenanceDepartment||item.department===maintenanceDepartment);
  const maintenanceAreaMovements=departmentMaintenanceMovements.filter(item=>!maintenanceArea||item.operationalArea===maintenanceArea||(maintenanceArea==="WORKSHOP"&&item.movementType==="TRANSFER_TO_WORKSHOP"));
  const workshopMovements=departmentMaintenanceMovements.filter(item=>item.operationalArea==="WORKSHOP");
  const warehouseMovements=departmentMaintenanceMovements.filter(item=>item.operationalArea==="WAREHOUSE");
  const maintenanceAssetMap=new Map<string,MaintenanceMovement>();
  maintenanceAreaMovements.forEach(item=>{const serial=normalizeMaintenanceSerial(item.serialNumber||"");if(serial&&!maintenanceAssetMap.has(serial))maintenanceAssetMap.set(serial,item);});
  const maintenanceAssets=[...maintenanceAssetMap.entries()].map(([serial,movement])=>({serial,movement,status:(movement.movementType==="DISCHARGE"?"DISCHARGED":movement.movementType==="EXIT"?"OUT":"ACTIVE") as "OUT"|"ACTIVE"|"DISCHARGED"}));
  const maintenanceNeedle=maintenanceQuery.trim().normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es");
  const matchesMaintenanceQuery=(item:MaintenanceMovement)=>!maintenanceNeedle||`${item.serialNumber||""} ${item.documentNumber||""} ${item.equipmentType} ${item.componentType||""} ${item.codigo} ${item.terminal} ${item.grupo} ${item.technicianName} ${item.deliveredByName||""} ${item.receivedByName||""} ${item.destinationName||""} ${item.failureCause}`.normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es").includes(maintenanceNeedle);
  const visibleMaintenanceAssets=maintenanceAssets.filter(item=>matchesMaintenanceQuery(item.movement));
  const visibleMaintenanceMovements=maintenanceAreaMovements.filter(item=>(maintenanceMovementFilter==="ALL"||item.movementType===maintenanceMovementFilter)&&matchesMaintenanceQuery(item));
  const activeMaintenanceAssets=maintenanceAssets.filter(item=>item.status==="ACTIVE").length;
  const maintenanceRepairs=maintenanceAreaMovements.filter(item=>item.movementType==="REPAIR"||item.movementType==="COMPONENT_REPLACEMENT").length;
  const maintenanceStockByProduct=new Map<string,{name:string;stock:number;consumption:number}>();
  departmentMaintenanceMovements.filter(item=>item.operationalArea==="WAREHOUSE").forEach(item=>{const key=item.equipmentType.trim().toLocaleLowerCase("es");const row=maintenanceStockByProduct.get(key)||{name:item.equipmentType,stock:0,consumption:0};if(["ENTRY","DAMAGED_RETURN"].includes(item.movementType))row.stock+=item.quantity;if(["NEW_DELIVERY","EXIT","TRANSFER_TO_WORKSHOP","DISCHARGE"].includes(item.movementType)){row.stock-=item.quantity;row.consumption+=item.quantity;}maintenanceStockByProduct.set(key,row);});
  const maintenanceStockRows=[...maintenanceStockByProduct.values()].map(item=>({...item,stock:Math.max(0,item.stock)})).sort((a,b)=>a.stock-b.stock||b.consumption-a.consumption);
  const maintenanceCriticalStock=maintenanceStockRows.filter(item=>item.stock<=3);
  const maintenanceHighConsumption=[...maintenanceStockRows].filter(item=>item.consumption>0).sort((a,b)=>b.consumption-a.consumption).slice(0,6);
  const maintenanceDepartmentRequisitions=maintenanceProcurement.requisitions.filter(item=>!maintenanceDepartment||item.department===maintenanceDepartment);
  const maintenancePendingRequisitions=maintenanceDepartmentRequisitions.filter(item=>!["FULFILLED","CANCELLED","REJECTED"].includes(item.status));
  const maintenanceDepartmentOrders=maintenanceProcurement.purchaseOrders.filter(item=>!maintenanceDepartment||item.department===maintenanceDepartment);
  const maintenanceOpenOrders=maintenanceDepartmentOrders.filter(item=>!["RECEIVED","CANCELLED"].includes(item.status));
  const maintenancePhysicalReceipts=departmentMaintenanceMovements.filter(item=>item.movementType==="ENTRY"&&item.failureCause==="Recepción física contra orden de compra");
  const maintenanceWorkshopPending=[...maintenanceAssetMap.values()].filter(item=>item.movementType==="TRANSFER_TO_WORKSHOP").length;
  const canViewMaintenance =
    session.supportDepartment === "TECHNOLOGY" ||
    session.supportDepartment === "GENERAL_SERVICES" ||
    session.supportDepartment === "HUMAN_RESOURCES" ||
    (session.role === "Administrator" &&
      (workspaceDepartment === "TECHNOLOGY" ||
        workspaceDepartment === "GENERAL_SERVICES" ||
        workspaceDepartment === "HUMAN_RESOURCES"));

  useEffect(() => {
    if (!maintenanceEntry) return;
    const permitted =
      session.role === "Administrator" ||
      isTechnologyDepartmentManager ||
      (maintenanceEntry === "WAREHOUSE" && isWarehouseOperator) ||
      (maintenanceEntry === "WORKSHOP" && isWorkshopOperator);
    if (!permitted) return;
    setConfigOpen(false);
    setSupportTab("tickets");
    setSupportStage("maintenance");
    setMaintenanceSelectedDepartment(null);
    setMaintenanceArea(maintenanceEntry);
    setMaintenanceView("dashboard");
    setMaintenanceQuery("");
    setMaintenanceMovementFilter("ALL");
  }, [
    isWarehouseOperator,
    isWorkshopOperator,
    isTechnologyDepartmentManager,
    maintenanceEntry,
    session.role,
  ]);

  const loadAgencyTransitions = useCallback(async () => {
    const transitionData = await request<{ projects: AgencyTransition[] }>(
      "/api/agency-transitions",
    ).catch(() => ({ projects: [] }));
    setAgencyTransitions(transitionData.projects || []);
  }, []);

  const loadMaintenanceMovements = useCallback(async () => {
    if(maintenanceLoadInFlightRef.current||Date.now()<maintenanceRetryAfterRef.current)return;
    maintenanceLoadInFlightRef.current=true;setMaintenanceLoading(true);
    try{
      const [maintenanceData,procurementData]=await Promise.all([
        request<{movements:MaintenanceMovement[];products:MaintenanceProduct[]}>("/api/maintenance/movements"),
        request<MaintenanceProcurement>("/api/maintenance/procurement").catch(()=>({suppliers:[],requisitions:[],purchaseOrders:[],returns:[]})),
      ]);
      setMaintenanceMovements((maintenanceData.movements||[]).map((movement) => ({
        ...movement,
        createdAt: deviceDate(movement.createdAt),
        occurredAt: deviceDate(movement.occurredAt),
      })));
      setMaintenanceProducts(maintenanceData.products||[]);
      setMaintenanceProcurement({
        suppliers:(procurementData.suppliers||[]).map((item) => ({ ...item, createdAt: deviceDate(item.createdAt) })),
        requisitions:(procurementData.requisitions||[]).map((item) => ({ ...item, createdAt: deviceDate(item.createdAt), updatedAt: deviceDate(item.updatedAt) })),
        purchaseOrders:(procurementData.purchaseOrders||[]).map((item) => ({ ...item, createdAt: deviceDate(item.createdAt), updatedAt: deviceDate(item.updatedAt), expectedAt: item.expectedAt ? deviceDate(item.expectedAt, "") : null })),
        returns:(procurementData.returns||[]).map((item) => ({ ...item, createdAt: deviceDate(item.createdAt) })),
      });
      setMaintenanceLoadError("");
      maintenanceRetryAfterRef.current=0;
      return maintenanceData.movements||[];
    }catch(loadError){
      maintenanceRetryAfterRef.current=Date.now()+60_000;
      setMaintenanceLoadError((loadError as Error).message||"Mantenimiento no está disponible temporalmente.");
    }finally{maintenanceLoadInFlightRef.current=false;setMaintenanceLoading(false);}
  }, []);

  useEffect(()=>{if(maintenanceEntry)void loadMaintenanceMovements();},[maintenanceEntry,loadMaintenanceMovements]);
  const load = useCallback(async (silent = false) => {
    if(loadInFlightRef.current)return;
    if(!navigator.onLine){
      if(!silent){setLoading(false);setError("No hay conexión. La actualización continuará automáticamente al recuperar internet.");}
      return;
    }
    loadInFlightRef.current=true;
    if (!silent) setLoading(true);
    if (!silent) setError("");
    try {
      const [
        ticketData,
        agencyData,
        findingData,
        catalogData,
        technicianData,
        statisticsData,
      ] =
        await Promise.all([
          request<{ tickets: Ticket[] }>("/api/tickets"),
          request<{ agencies: TicketAgency[] }>("/api/tickets/agencies"),
          isSupportTechnician
            ? request<{ findings: AutomaticFinding[] }>("/api/tickets/findings")
            : Promise.resolve({ findings: [] }),
          request<{ departments: SupportDepartment[] }>("/api/support/catalog"),
          request<{ technicians: Technician[] }>("/api/tickets/technicians"),
          request<TicketStatistics>("/api/tickets/statistics").catch(
            () => null,
          ),
        ]);
      const loadedTickets = (Array.isArray(ticketData.tickets) ? ticketData.tickets : [])
        .filter((ticket): ticket is Ticket => !!ticket && typeof ticket === "object")
        .map((ticket) => normalizeSupportTicket(ticket));
      setTickets(loadedTickets);
      setAgencies(agencyData.agencies || []);
      setFindings((findingData.findings || []).map((finding) => ({
        ...finding,
        submittedAt: deviceDate(finding.submittedAt),
        resolvedAt: finding.resolvedAt ? deviceDate(finding.resolvedAt, "") : null,
        evidence: (finding.evidence || []).map((photo) => ({
          ...photo,
          capturedAt: deviceDate(photo.capturedAt),
        })),
      })));
      setCatalog(catalogData.departments || []);
      setTechnicians(technicianData.technicians || []);
      setTicketStatistics(statisticsData);
      setManagement((current) =>
        Object.fromEntries(
          loadedTickets.map((ticket) => [
            ticket.id,
            current[ticket.id] || {
              status: ticket.status,
              priority: ticket.priority,
              resolution: ticket.resolution || "",
              assignedTechnicianId: ticket.assignedTechnicianId || "",
              assignedDepartment: ticket.assignedDepartment,
              assignedTeam: ticket.assignedTeam || "",
              sharedWithDepartments: ticket.sharedWithDepartments || [],
              routingComment: "",
              evidence: [],
            },
          ]),
        ),
      );
      setLastSync(new Date());
      loadHasDataRef.current=true;
      setError("");
      // Estos módulos pesados no deben bloquear la primera pintura ni la apertura
      // de las tarjetas. Se precargan en paralelo y tienen recarga específica.
      void loadAgencyTransitions();
      void loadMaintenanceMovements();
    } catch (loadError) {
      if ((loadError as Error & { status?: number }).status === 401)
        onSessionExpiredRef.current();
      if(!silent||!loadHasDataRef.current)setError((loadError as Error).message);
    } finally {
      loadInFlightRef.current=false;
      if (!silent) setLoading(false);
    }
  }, [isSupportTechnician, loadAgencyTransitions, loadMaintenanceMovements]);

  useEffect(() => {
    if (
      (session.role === "GroupAdministrator" && supervisorInboxRequestKey) ||
      (navigationTarget?.kind === "TICKET" &&
        navigationTarget.requestKey != null)
    )
      void load(true);
  }, [
    load,
    navigationTarget?.kind,
    navigationTarget?.requestKey,
    session.role,
    supervisorInboxRequestKey,
  ]);

  const openAgencyDirectoryEditor=(agency:TicketAgency)=>{
    setAgencyDirectoryEditId(agency.id);
    setAgencyDirectoryDraft({codigo:agency.codigo,terminal:agency.terminal,grupo:agency.grupo,direccion:agency.direccion||"",sector:agency.sector||"",municipio:agency.municipio||"",provincia:agency.provincia||"",latitude:agency.latitude==null?"":String(agency.latitude),longitude:agency.longitude==null?"":String(agency.longitude)});
  };
  const saveAgencyDirectory=async()=>{
    if(!agencyDirectoryEditId||!agencyDirectoryDraft)return;
    const latitude=agencyDirectoryDraft.latitude.trim();
    const longitude=agencyDirectoryDraft.longitude.trim();
    if(!agencyDirectoryDraft.codigo.trim()||!agencyDirectoryDraft.terminal.trim()||!agencyDirectoryDraft.grupo.trim()){setError("Completa el código, la terminal y el grupo de la agencia.");return;}
    if((latitude&&!longitude)||(!latitude&&longitude)){setError("Completa juntas la latitud y la longitud.");return;}
    setSaving(true);setError("");
    try{
      await request(`/api/technology/agencies/${agencyDirectoryEditId}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify({...agencyDirectoryDraft,latitude:latitude?Number(latitude):null,longitude:longitude?Number(longitude):null})});
      setAgencyDirectoryEditId(null);setAgencyDirectoryDraft(null);setNotice("Agencia actualizada y registrada en la auditoría del sistema.");await load(true);
    }catch(saveError){setError((saveError as Error).message);}finally{setSaving(false);}
  };

  const downloadAgencyDirectoryTemplate=async()=>{
    try{
      const ExcelJS=await import("exceljs");
      const workbook=new ExcelJS.Workbook();
      workbook.creator="Real Agencias · Grupo Tejeda";
      const sheet=workbook.addWorksheet("Agencias",{views:[{state:"frozen",ySplit:1}]});
      sheet.columns=[
        {header:"Código *",key:"codigo",width:18},{header:"Terminal *",key:"terminal",width:42},{header:"Grupo *",key:"grupo",width:28},
        {header:"Región",key:"region",width:20},{header:"Dirección",key:"direccion",width:42},{header:"Sector",key:"sector",width:22},
        {header:"Municipio",key:"municipio",width:22},{header:"Provincia",key:"provincia",width:22},{header:"Latitud",key:"latitude",width:16},{header:"Longitud",key:"longitude",width:16},
      ];
      const header=sheet.getRow(1);header.height=28;header.eachCell(cell=>{cell.font={bold:true,color:{argb:"FFFFFFFF"}};cell.fill={type:"pattern",pattern:"solid",fgColor:{argb:"FF0F6C9C"}};cell.alignment={vertical:"middle",horizontal:"center"};});
      for(let index=2;index<=101;index++){
        const row=sheet.getRow(index);row.height=22;
        for(let column=1;column<=10;column++){
          const cell=row.getCell(column);
          cell.fill={type:"pattern",pattern:"solid",fgColor:{argb:column<=3?"FFFFF4CC":"FFEAF6FC"}};
          cell.border={bottom:{style:"hair",color:{argb:"FFB8D7E8"}}};
        }
      }
      sheet.autoFilter={from:"A1",to:"J101"};
      const instructions=workbook.addWorksheet("Instrucciones");
      instructions.columns=[{width:28},{width:92}];
      [["REAL · IMPORTACIÓN DE AGENCIAS","Completa una agencia por fila en la hoja Agencias. Puedes editar libremente las celdas preparadas."],["Campos obligatorios","Solo Código, Terminal y Grupo (columnas amarillas con *)."],["Campos opcionales","Región, Dirección, Sector, Municipio, Provincia, Latitud y Longitud. Las coordenadas deben completarse juntas."],["Registro automático","Cada agencia se registra en el grupo escrito y aparecerá en Panel, Auditoría de campo y Tickets de soporte según los permisos del usuario."],["Duplicados","Las agencias con el mismo código, o la misma terminal y grupo, se omiten automáticamente."],["Límite","Hasta 1,000 agencias por archivo .xlsx."]].forEach((values,index)=>{const row=instructions.addRow(values);row.height=index===0?34:30;row.getCell(1).font={bold:true,color:{argb:index===0?"FFFFFFFF":"FF0F6C9C"}};if(index===0){row.eachCell(cell=>{cell.fill={type:"pattern",pattern:"solid",fgColor:{argb:"FF071B2E"}};cell.font={bold:true,color:{argb:"FFFFFFFF"}};});}row.eachCell(cell=>{cell.alignment={vertical:"middle",wrapText:true};});});
      const buffer=await workbook.xlsx.writeBuffer();
      downloadBlob(new Blob([buffer as BlobPart],{type:"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"}),"plantilla-nuevas-agencias-real.xlsx");
      setNotice("Plantilla Excel descargada. Completa la hoja Agencias sin cambiar los encabezados.");
    }catch(templateError){setError(`No se pudo generar la plantilla: ${(templateError as Error).message}`);}
  };

  const readAgencyDirectoryExcel=async(file:File)=>{
    setError("");setAgencyDirectoryImportResult(null);
    if(!file.name.toLowerCase().endsWith(".xlsx")){setError("Selecciona un archivo Excel con extensión .xlsx.");return;}
    if(file.size>5*1024*1024){setError("El archivo supera el límite de 5 MB.");return;}
    try{
      const ExcelJS=await import("exceljs");
      const workbook=new ExcelJS.Workbook();
      await workbook.xlsx.load(await file.arrayBuffer() as never);
      const sheet=workbook.worksheets[0];
      if(!sheet){setError("El archivo Excel no contiene hojas.");return;}
      const cellText=(value:unknown):string=>{
        if(value==null)return"";
        if(typeof value==="string"||typeof value==="number")return String(value).trim();
        if(typeof value==="object"){
          const complex=value as {text?:string;result?:unknown;richText?:Array<{text:string}>};
          if(complex.text!=null)return String(complex.text).trim();
          if(complex.result!=null)return String(complex.result).trim();
          if(complex.richText)return complex.richText.map(part=>part.text).join("").trim();
        }
        return String(value).trim();
      };
      const normalize=(value:string)=>value.normalize("NFD").replace(/[\u0300-\u036f]/g,"").replace(/[^a-z0-9]/gi,"").toLowerCase();
      const headers=new Map<string,number>();
      sheet.getRow(1).eachCell((cell,column)=>headers.set(normalize(cellText(cell.value)),column));
      const column=(...aliases:string[])=>aliases.map(alias=>headers.get(alias)).find(value=>value!=null);
      const codigoColumn=column("codigo","codigodeagencia","codigoagencia");
      const terminalColumn=column("terminal","nombreterminal","agencia");
      const grupoColumn=column("grupo","grupoagencia");
      const regionColumn=column("region","zona");
      const direccionColumn=column("direccion","direccionagencia");
      const sectorColumn=column("sector");
      const municipioColumn=column("municipio","ciudad");
      const provinciaColumn=column("provincia");
      const latitudeColumn=column("latitud","latitude","lat");
      const longitudeColumn=column("longitud","longitude","lng","lon");
      if(!codigoColumn||!terminalColumn||!grupoColumn){setError("El Excel debe contener los encabezados Código, Terminal y Grupo en la primera fila.");return;}
      const rows:AgencyDirectoryImportRow[]=[];const errors:string[]=[];
      for(let rowNumber=2;rowNumber<=sheet.actualRowCount;rowNumber++){
        const row=sheet.getRow(rowNumber);
        const codigo=cellText(row.getCell(codigoColumn).value),terminal=cellText(row.getCell(terminalColumn).value),grupo=cellText(row.getCell(grupoColumn).value);
        const region=regionColumn?cellText(row.getCell(regionColumn).value):"";
        const direccion=direccionColumn?cellText(row.getCell(direccionColumn).value):"";
        const sector=sectorColumn?cellText(row.getCell(sectorColumn).value):"";
        const municipio=municipioColumn?cellText(row.getCell(municipioColumn).value):"";
        const provincia=provinciaColumn?cellText(row.getCell(provinciaColumn).value):"";
        const latitudeText=latitudeColumn?cellText(row.getCell(latitudeColumn).value).replace(",","."):"";
        const longitudeText=longitudeColumn?cellText(row.getCell(longitudeColumn).value).replace(",","."):"";
        if(!codigo&&!terminal&&!grupo&&!region&&!direccion&&!sector&&!municipio&&!provincia&&!latitudeText&&!longitudeText)continue;
        if(!codigo||!terminal||!grupo){errors.push(`Fila ${rowNumber}: faltan Código, Terminal o Grupo.`);continue;}
        if((latitudeText&&!longitudeText)||(!latitudeText&&longitudeText)){errors.push(`Fila ${rowNumber}: completa ambas coordenadas.`);continue;}
        const latitude=latitudeText?Number(latitudeText):null,longitude=longitudeText?Number(longitudeText):null;
        if((latitudeText&&!Number.isFinite(latitude))||(longitudeText&&!Number.isFinite(longitude))||(latitude!=null&&(latitude<-90||latitude>90))||(longitude!=null&&(longitude<-180||longitude>180))||(latitude===0&&longitude===0)){errors.push(`Fila ${rowNumber}: coordenadas no válidas.`);continue;}
        rows.push({rowNumber,codigo,terminal,grupo,region:region||null,direccion:direccion||null,sector:sector||null,municipio:municipio||null,provincia:provincia||null,latitude,longitude});
      }
      if(rows.length>1000)errors.unshift("El archivo supera el límite de 1,000 agencias.");
      if(!rows.length&&!errors.length)errors.push("No encontramos agencias debajo de los encabezados.");
      setAgencyDirectoryImport({fileName:file.name,rows,errors});
    }catch(importError){setError(`No se pudo leer el archivo Excel: ${(importError as Error).message}`);}
    finally{if(agencyDirectoryFileRef.current)agencyDirectoryFileRef.current.value="";}
  };

  const importAgencyDirectoryExcel=async()=>{
    if(!agencyDirectoryImport||agencyDirectoryImport.errors.length||!agencyDirectoryImport.rows.length)return;
    setSaving(true);setError("");
    try{
      const result=await request<AgencyDirectoryImportResult>("/api/technology/agencies/import",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({fileName:agencyDirectoryImport.fileName,agencies:agencyDirectoryImport.rows})});
      setAgencyDirectoryImport(null);setAgencyDirectoryImportResult(result);setNotice(`${result.created} agencias nuevas agregadas. ${result.skipped} duplicadas fueron omitidas.`);await load(true);
    }catch(importError){setError((importError as Error).message);}finally{setSaving(false);}
  };

  const createAgencyTransition=async()=>{
    if(!transitionDraft.agencyId){setError("Selecciona la agencia que está en construcción o reestructuración.");return;}
    setSaving(true);setError("");
    try{await request("/api/agency-transitions",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(transitionDraft)});setTransitionFormOpen(false);setTransitionDraft({agencyId:"",projectType:"RESTRUCTURING",notes:""});setTransitionAgencyQuery("");setNotice("Proceso de agencia creado. Servicios Generales puede iniciar la remodelación.");await loadAgencyTransitions();}catch(saveError){setError((saveError as Error).message);}finally{setSaving(false);}
  };
  const updateAgencyTransition=async(project:AgencyTransition,stage:AgencyTransitionStage,status:"IN_PROGRESS"|"COMPLETED")=>{
    const note=(transitionNotes[stage.id]||"").trim();
    if(status==="IN_PROGRESS"&&!note){setError("Describe el avance antes de guardarlo.");return;}
    setSaving(true);setError("");
    try{await request(`/api/agency-transitions/${project.id}/stage`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify({status,notes:note||null})});setTransitionNotes(current=>({...current,[stage.id]:""}));if(status==="COMPLETED")setTransitionRecordDecisionStageId(null);setNotice(status==="COMPLETED"?"Etapa del departamento completada. El proceso pasó al área siguiente.":"Registro creado dentro de la etapa.");await loadAgencyTransitions();}catch(saveError){setError((saveError as Error).message);}finally{setSaving(false);}
  };
  const updateAgencyTransitionStatus=async(project:AgencyTransition,status:"ACTIVE"|"PENDING")=>{
    const reason=status==="PENDING"?window.prompt("Indica por qué el proceso quedará pendiente (por ejemplo: falta de material o tiempo):","")?.trim()||"":"";
    if(status==="PENDING"&&reason.length<5){setError("Escribe un motivo de al menos 5 caracteres para dejar la agencia pendiente.");return;}
    setSaving(true);setError("");
    try{await request(`/api/agency-transitions/${project.id}/status`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify({status,reason:reason||null})});setNotice(status==="PENDING"?"La agencia quedó pendiente y conserva todo su avance.":"El proceso de la agencia fue reanudado.");setTransitionStatusFilter(status);setSelectedTransitionId(null);setSelectedTransitionStageId(null);await loadAgencyTransitions();}catch(saveError){setError((saveError as Error).message);}finally{setSaving(false);}
  };
  const editAgencyTransitionStage=async(project:AgencyTransition,stage:AgencyTransitionStage)=>{setSaving(true);setError("");try{await request(`/api/agency-transitions/${project.id}/stages/${stage.id}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify({notes:transitionStageEditNotes||null})});setTransitionEditMode(false);setNotice("Descripción actualizada y registrada en el historial de avances.");await loadAgencyTransitions();}catch(saveError){setError((saveError as Error).message);}finally{setSaving(false);}};
  const updateAgencyTransitionProgress=async(project:AgencyTransition,stage:AgencyTransitionStage,entry:AgencyTransitionProgress,status?:"IN_PROGRESS"|"COMPLETED")=>{const note=transitionProgressEditId===entry.id?transitionProgressEditNote.trim():"";if(!status&&!note){setError("Escribe el contenido actualizado del registro.");return;}setSaving(true);setError("");try{await request(`/api/agency-transitions/${project.id}/stages/${stage.id}/progress/${entry.id}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify({note:note||null,status:status||null})});setTransitionProgressEditId(null);setTransitionProgressEditNote("");if(status==="COMPLETED"){setTransitionRecordDecisionStageId(stage.id);setNotice("Registro completado. Decide si crearás otro registro o cerrarás la etapa del departamento.");}else setNotice("Registro actualizado correctamente.");await loadAgencyTransitions();}catch(saveError){setError((saveError as Error).message);}finally{setSaving(false);}};
  const deleteAgencyTransitionProgress=async(project:AgencyTransition,stage:AgencyTransitionStage,entry:AgencyTransitionProgress)=>{if(!window.confirm("¿Eliminar este registro? Se ocultará del historial visible y la acción quedará auditada."))return;setSaving(true);setError("");try{await request(`/api/agency-transitions/${project.id}/stages/${stage.id}/progress/${entry.id}`,{method:"DELETE"});setTransitionProgressEditId(null);setNotice("Registro eliminado correctamente.");await loadAgencyTransitions();}catch(saveError){setError((saveError as Error).message);}finally{setSaving(false);}};
  const uploadAgencyTransitionEvidence=async(project:AgencyTransition,stage:AgencyTransitionStage,entry:AgencyTransitionProgress,fileList:FileList|null)=>{
    const files=Array.from(fileList||[]);
    if(!files.length)return;
    const current=entry.evidence?.length||0;
    const remaining=4-current;
    if(files.length>remaining){
      const message=`Este registro admite ${remaining} fotografía${remaining===1?"":"s"} adicional${remaining===1?"":"es"}; el máximo es 4.`;
      setError(message);setTransitionEvidenceFeedback({entryId:entry.id,kind:"error",message});return;
    }
    if(files.some(file=>file.size<=0)){
      const message="Una de las fotografías está vacía. Selecciónala nuevamente.";
      setError(message);setTransitionEvidenceFeedback({entryId:entry.id,kind:"error",message});return;
    }
    setSaving(true);setTransitionEvidenceUploadingId(entry.id);setTransitionEvidenceFeedback({entryId:entry.id,kind:"working",message:"Optimizando la fotografía y obteniendo el GPS al mismo tiempo..."});setError("");
    try{
      if(!("geolocation" in navigator))throw new Error("Este dispositivo no permite obtener el GPS requerido para la evidencia.");
      const gpsPromise=new Promise<GeolocationPosition>((resolve,reject)=>navigator.geolocation.getCurrentPosition(resolve,reject,{enableHighAccuracy:true,timeout:12000,maximumAge:60000}));
      const filesPromise=Promise.all(files.map(prepareAgencyEvidence));
      const [position,preparedFiles]=await Promise.all([gpsPromise,filesPromise]);
      const location={latitude:position.coords.latitude,longitude:position.coords.longitude,accuracyMeters:position.coords.accuracy,capturedAt:new Date(position.timestamp).toISOString()};
      setTransitionEvidenceLocations(current=>({...current,[entry.id]:location}));
      const form=new FormData();preparedFiles.forEach(file=>form.append("files",file,file.name));
      form.append("latitude",String(location.latitude));form.append("longitude",String(location.longitude));form.append("accuracyMeters",String(location.accuracyMeters));form.append("locationCapturedAt",location.capturedAt);
      setTransitionEvidenceFeedback({entryId:entry.id,kind:"working",message:`Subiendo ${preparedFiles.length} fotografía${preparedFiles.length===1?"":"s"}...`});
      await request(`/api/agency-transitions/${project.id}/stages/${stage.id}/progress/${entry.id}/evidence`,{method:"POST",body:form});
      const message=`${preparedFiles.length} evidencia${preparedFiles.length===1?"":"s"} guardada${preparedFiles.length===1?"":"s"} con GPS verificado.`;
      setNotice(`${message} Ya puedes completar el registro.`);setTransitionEvidenceFeedback({entryId:entry.id,kind:"success",message});await loadAgencyTransitions();
    }catch(saveError){
      const rawMessage=(saveError as Error).message;
      const message=rawMessage.toLocaleLowerCase("es").includes("ruta solicitada no existe")||rawMessage.includes("404")
        ? "Monster todavía ejecuta un servidor anterior. Instala V219 y reinicia por completo el servicio para activar la carga rápida con GPS."
        : rawMessage.toLocaleLowerCase("es").includes("location")||rawMessage.toLocaleLowerCase("es").includes("ubicación")
          ? "Activa la ubicación del teléfono y autoriza el GPS para tomar la evidencia."
          : rawMessage;
      setError(message);setTransitionEvidenceFeedback({entryId:entry.id,kind:"error",message});
    }finally{setSaving(false);setTransitionEvidenceUploadingId(null);}
  };
  const deleteAgencyTransitionEvidence=async(project:AgencyTransition,stage:AgencyTransitionStage,entry:AgencyTransitionProgress,evidence:AgencyTransitionEvidence)=>{if(!window.confirm("¿Eliminar esta fotografía de evidencia?"))return;setSaving(true);setError("");try{await request(`/api/agency-transitions/${project.id}/stages/${stage.id}/progress/${entry.id}/evidence/${evidence.id}`,{method:"DELETE"});setTransitionEvidenceViewer(current=>{if(!current||current.entry.id!==entry.id)return current;const remaining=(current.entry.evidence||[]).filter(item=>item.id!==evidence.id);return remaining.length?{...current,entry:{...current.entry,evidence:remaining}}:null;});setNotice("Evidencia eliminada.");await loadAgencyTransitions();}catch(saveError){setError((saveError as Error).message);}finally{setSaving(false);}};
  const deleteAgencyTransitionStageContent=async(project:AgencyTransition,stage:AgencyTransitionStage)=>{if(!window.confirm(`¿Eliminar la descripción y los registros de la etapa de ${departments[stage.department]||stage.department}? La etapa estructural permanecerá en el flujo.`))return;setSaving(true);setError("");try{await request(`/api/agency-transitions/${project.id}/stages/${stage.id}`,{method:"DELETE"});setTransitionEditMode(false);setTransitionProgressEditId(null);setTransitionRecordDecisionStageId(null);setNotice("Contenido y registros de la etapa eliminados correctamente.");await loadAgencyTransitions();}catch(saveError){setError((saveError as Error).message);}finally{setSaving(false);}};
  const assignAgencyTransitionRecord=async(project:AgencyTransition,stage:AgencyTransitionStage,entry:AgencyTransitionProgress)=>{const technician=technicians.find(item=>item.id===transitionAssignmentDraft.technicianUserId);const contractorName=transitionAssignmentDraft.contractorName.trim();if(transitionAssignmentDraft.assignmentType==="TECHNICIAN"&&!technician){setError("Selecciona un técnico del departamento.");return;}if(transitionAssignmentDraft.assignmentType==="CONTRACTOR"&&contractorName.length<2){setError("Escribe el nombre del contratista.");return;}setSaving(true);setError("");try{await request(`/api/agency-transitions/${project.id}/stages/${stage.id}/progress/${entry.id}/assignment`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify({assignmentType:transitionAssignmentDraft.assignmentType,technicianUserId:transitionAssignmentDraft.assignmentType==="TECHNICIAN"?technician?.id:null,contractorName:transitionAssignmentDraft.assignmentType==="CONTRACTOR"?contractorName:null})});setTransitionAssignmentRecordId(null);setNotice(`${transitionAssignmentDraft.assignmentType==="TECHNICIAN"?technician?.name:contractorName} fue asignado a este registro.`);await loadAgencyTransitions();}catch(saveError){setError((saveError as Error).message);}finally{setSaving(false);}};
  const deleteAgencyTransition=async(project:AgencyTransition)=>{if(!window.confirm(`¿Eliminar el proceso de ${project.codigo} · ${project.terminal}? El historial quedará auditado.`))return;setSaving(true);setError("");try{await request(`/api/agency-transitions/${project.id}`,{method:"DELETE"});setSelectedTransitionId(null);setSelectedTransitionStageId(null);setNotice("Proceso eliminado correctamente.");await loadAgencyTransitions();}catch(saveError){setError((saveError as Error).message);}finally{setSaving(false);}};
  const readMaintenanceInventoryExcel=async(file:File)=>{
    setError("");
    if(!file.name.toLocaleLowerCase("es").endsWith(".xlsx")){setError("Selecciona un archivo de Excel con extensión .xlsx.");return;}
    if(file.size>8*1024*1024){setError("El archivo Excel no puede superar 8 MB.");return;}
    try{
      const ExcelJS=await import("exceljs");const workbook=new ExcelJS.Workbook();await workbook.xlsx.load(await file.arrayBuffer() as never);const sheet=workbook.worksheets[0];
      if(!sheet){setError("El archivo Excel no contiene hojas.");return;}
      const cellText=(value:unknown)=>{if(value===null||value===undefined)return"";if(typeof value==="object"&&value&&"text" in value)return String((value as {text?:unknown}).text??"");if(typeof value==="object"&&value&&"result" in value)return String((value as {result?:unknown}).result??"");return String(value).trim();};
      const header=(value:unknown)=>cellText(value).normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es").replace(/[^a-z0-9]/g,"");
      const columns=new Map<string,number>();sheet.getRow(1).eachCell((cell,column)=>columns.set(header(cell.value),column));
      const col=(...names:string[])=>names.map(name=>columns.get(name)).find(Boolean)||0;
      const departmentCol=col("departamento","area"),equipmentCol=col("equipo","producto","articulo","nombreequipo"),serialCol=col("serial","codigo","codigoequipo"),quantityCol=col("cantidad","existencia"),locationCol=col("ubicacion","destino"),notesCol=col("notas","observaciones");
      if(!departmentCol||!equipmentCol||!serialCol){setError("El Excel debe tener los encabezados Departamento, Equipo y Serial en la primera fila.");return;}
      const departmentCodes:Record<string,string>={tecnologia:"TECHNOLOGY",technology:"TECHNOLOGY",serviciosgenerales:"GENERAL_SERVICES",generalservices:"GENERAL_SERVICES",recursoshumanos:"HUMAN_RESOURCES",rrhh:"HUMAN_RESOURCES",humanresources:"HUMAN_RESOURCES"};
      const rows:MaintenanceInventoryImportRow[]=[],errors:string[]=[],duplicates:string[]=[];const seen=new Set<string>();const existing=new Set(maintenanceMovements.map(item=>normalizeMaintenanceSerial(item.serialNumber||"")).filter(Boolean));
      for(let rowNumber=2;rowNumber<=sheet.rowCount;rowNumber++){
        const row=sheet.getRow(rowNumber),rawDepartment=cellText(row.getCell(departmentCol).value),equipmentType=cellText(row.getCell(equipmentCol).value),serialNumber=normalizeMaintenanceSerial(cellText(row.getCell(serialCol).value));
        if(!rawDepartment&&!equipmentType&&!serialNumber)continue;
        const departmentCode=departmentCodes[header(rawDepartment)]||rawDepartment.trim().toUpperCase();const quantityRaw=quantityCol?Number(cellText(row.getCell(quantityCol).value)||1):1;
        if(!["TECHNOLOGY","GENERAL_SERVICES","HUMAN_RESOURCES"].includes(departmentCode))errors.push(`Fila ${rowNumber}: departamento no reconocido.`);
        if(!equipmentType||equipmentType.length>100)errors.push(`Fila ${rowNumber}: indica un equipo válido de hasta 100 caracteres.`);
        if(!serialNumber)errors.push(`Fila ${rowNumber}: falta el serial o código.`);
        if(!Number.isInteger(quantityRaw)||quantityRaw<1||quantityRaw>1000)errors.push(`Fila ${rowNumber}: la cantidad debe ser un entero entre 1 y 1,000.`);
        if(serialNumber&&(seen.has(serialNumber)||existing.has(serialNumber))){duplicates.push(`Fila ${rowNumber}: ${serialNumber}`);continue;}
        if(!departmentCode||!equipmentType||!serialNumber||!Number.isInteger(quantityRaw)||quantityRaw<1||quantityRaw>1000||!["TECHNOLOGY","GENERAL_SERVICES","HUMAN_RESOURCES"].includes(departmentCode))continue;
        seen.add(serialNumber);rows.push({rowNumber,department:departmentCode,equipmentType:equipmentType.slice(0,100),serialNumber,quantity:quantityRaw,destinationName:locationCol?cellText(row.getCell(locationCol).value).slice(0,200):"Almacén central",notes:notesCol?cellText(row.getCell(notesCol).value).slice(0,2000):""});
      }
      if(rows.length>500)errors.push("El archivo supera el límite de 500 equipos por importación.");
      setMaintenanceInventoryImport({fileName:file.name,rows:rows.slice(0,500),errors,duplicates});setMaintenanceInventoryImportOpen(true);setMaintenanceInventoryImportProgress(0);
    }catch(importError){setError(`No se pudo leer el Excel: ${(importError as Error).message}`);}
    finally{if(maintenanceInventoryFileRef.current)maintenanceInventoryFileRef.current.value="";}
  };
  const importMaintenanceInventory=async()=>{
    if(!maintenanceInventoryImport||maintenanceInventoryImport.errors.length||!maintenanceInventoryImport.rows.length||saving)return;
    setSaving(true);setError("");setMaintenanceInventoryImportProgress(0);let created=0;
    try{
      for(const [index,row] of maintenanceInventoryImport.rows.entries()){
        const identity=createMaintenanceIdentity("WAREHOUSE");
        await request("/api/maintenance/movements",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({agencyId:null,ticketId:null,technicianUserId:null,technicianName:session.displayName,movementType:"ENTRY",equipmentType:row.equipmentType,componentType:null,failureCause:"Importación inicial desde Excel",serialNumber:row.serialNumber,quantity:row.quantity,notes:row.notes||`Importado desde ${maintenanceInventoryImport.fileName}`,department:row.department,operationalArea:"WAREHOUSE",documentNumber:identity.documentNumber,qrToken:identity.qrToken,deliveredByName:"Importación Excel",receivedByName:session.displayName,destinationName:row.destinationName||"Almacén central",signatureData:null,occurredAt:new Date().toISOString()})});
        created+=1;setMaintenanceInventoryImportProgress(index+1);
      }
      const omitted=maintenanceInventoryImport.duplicates.length;setMaintenanceInventoryImportOpen(false);setMaintenanceInventoryImport(null);setNotice(`${created} equipos importados al inventario.${omitted?` ${omitted} seriales duplicados fueron omitidos.`:""}`);await loadMaintenanceMovements();
    }catch(importError){setError(`Se importaron ${created} equipos antes del error: ${(importError as Error).message}`);await loadMaintenanceMovements();}finally{setSaving(false);}
  };
  const closeMaintenanceForm=()=>{setMaintenanceFormOpen(false);setMaintenanceFormError("");setMaintenanceScannerActive(false);setMaintenanceScanFeedback(null);setMaintenanceScannedMovement(null);maintenanceScannerBufferRef.current={value:"",lastAt:0};};
  const applyMaintenanceScan=(rawCode:string)=>{
    const scanned=workshopCode(rawCode);
    if(rawCode.includes("?equipment=")||maintenanceMovements.some(item=>item.movementType==="TRANSFER_TO_WORKSHOP"&&(item.qrToken===scanned||item.documentNumber===scanned))){setMaintenanceFormOpen(false);setTrackingCode(scanned);return;}
    const raw=rawCode.trim().replace(/[\r\n\t]/g,"").slice(0,200);
    const upper=raw.toLocaleUpperCase("es");
    if(upper.length<3){setMaintenanceScanFeedback({kind:"ready",message:"El código leído es demasiado corto. Intenta nuevamente."});return;}
    const qrDocument=upper.startsWith("REAL-MNT|")?upper.slice("REAL-MNT|".length):upper;
    const qrMovement=departmentMaintenanceMovements.find(item=>item.qrToken?.toLocaleUpperCase("es")===upper||item.documentNumber?.toLocaleUpperCase("es")===qrDocument);
    const code=normalizeMaintenanceSerial(qrMovement?.serialNumber||raw);
    const existing=qrMovement||departmentMaintenanceMovements.find(item=>normalizeMaintenanceSerial(item.serialNumber||"")===code);
    const catalogProduct=maintenanceProducts.find(item=>item.department===maintenanceDepartment&&normalizeMaintenanceSerial(item.scanCode)===code);
    const suggestedProduct=existing?.equipmentType||catalogProduct?.productName||maintenanceProductSuggestion(raw);
    const area=isWorkshopOperator?"WORKSHOP":isWarehouseOperator?"WAREHOUSE":existing?.operationalArea||maintenanceArea||maintenanceDraft.operationalArea;
    const nextMovement=existing?(area==="WAREHOUSE"?(existing.movementType==="REQUEST"?"NEW_DELIVERY":existing.movementType==="DAMAGED_RETURN"?"TRANSFER_TO_WORKSHOP":existing.movementType==="EXIT"||existing.movementType==="DISCHARGE"?"ENTRY":"EXIT"):(existing.movementType==="EXIT"?"ENTRY":existing.movementType==="TRANSFER_TO_WORKSHOP"?"ENTRY":"REPAIR")):"ENTRY";
    const identity=createMaintenanceIdentity(area);
    setMaintenanceArea(area);
    setMaintenanceScannedMovement(existing||null);
    setMaintenanceDraft(current=>({...current,
      operationalArea:area,
      documentNumber:identity.documentNumber,
      qrToken:identity.qrToken,
      agencyId:existing?.agencyId||current.agencyId,
      movementType:nextMovement,
      equipmentType:suggestedProduct||current.equipmentType,
      componentType:existing?.componentType||catalogProduct?.componentType||current.componentType,
      failureCause:existing?(nextMovement==="ENTRY"?"Reingreso de activo escaneado":nextMovement==="EXIT"?"Despacho de activo escaneado":"Inspección de activo escaneado"):"Registro inicial de activo",
      serialNumber:code,
      quantity:existing?.quantity||1,
      technicianUserId:existing?.technicianUserId||current.technicianUserId,
      technicianName:existing?.technicianName||current.technicianName,
      deliveredByName:existing?.receivedByName||existing?.technicianName||current.deliveredByName,
      receivedByName:session.displayName,
      destinationName:existing?.destinationName||current.destinationName,
      occurredAt:maintenanceLocalNow(),
    }));
    setMaintenanceScanFeedback(existing
      ?{kind:"existing",message:`QR/equipo reconocido: ${existing.equipmentType} · ${existing.documentNumber} · ${existing.codigo}. Se cargaron todos sus datos y el responsable.`}
      :catalogProduct?{kind:"existing",message:`Producto reconocido en el catálogo: ${catalogProduct.productName}. Completa la operación y el responsable.`}
      :{kind:"new",message:"Código nuevo detectado. Confirma o corrige el nombre sugerido; al guardar, el sistema aprenderá esta relación para los próximos escaneos."});
    window.setTimeout(()=>maintenanceSerialInputRef.current?.select(),0);
  };
  const applyMaintenanceScanRef=useRef(applyMaintenanceScan);
  applyMaintenanceScanRef.current=applyMaintenanceScan;
  const openMaintenanceForm=(scanner=false,area:MaintenanceArea=maintenanceArea||"WORKSHOP",movementType?:string)=>{
    setMaintenanceFormError("");
    setMaintenanceArea(area);
    setMaintenanceScannerActive(scanner);
    setMaintenanceScannedMovement(null);
    setMaintenanceScanFeedback(scanner?{kind:"ready",message:"Lector 2D listo. Escanea un código de barras o el QR de un formulario y confirma con Enter."}:null);
    setMaintenanceDraft(newMaintenanceDraft(area,movementType||(scanner?"ENTRY":area==="WORKSHOP"?"REPAIR":"ENTRY")));
    setMaintenanceFormOpen(true);
    if(scanner)window.setTimeout(()=>maintenanceSerialInputRef.current?.focus(),120);
  };
  useEffect(()=>{
    if(!maintenanceFormOpen||!maintenanceScannerActive)return;
    const captureScanner=(event:KeyboardEvent)=>{
      const target=event.target as HTMLElement|null;
      if(target===maintenanceSerialInputRef.current||target?.tagName==="INPUT"||target?.tagName==="TEXTAREA"||target?.tagName==="SELECT"||event.ctrlKey||event.altKey||event.metaKey)return;
      if(event.key==="Enter"){
        const buffered=maintenanceScannerBufferRef.current.value;
        maintenanceScannerBufferRef.current={value:"",lastAt:0};
        if(buffered){event.preventDefault();applyMaintenanceScanRef.current(buffered);}
        return;
      }
      if(event.key.length!==1)return;
      const now=performance.now();
      const current=maintenanceScannerBufferRef.current;
      maintenanceScannerBufferRef.current={value:(now-current.lastAt>120?"":current.value)+event.key,lastAt:now};
    };
    window.addEventListener("keydown",captureScanner);
    return()=>window.removeEventListener("keydown",captureScanner);
  },[maintenanceFormOpen,maintenanceScannerActive,departmentMaintenanceMovements,maintenanceProducts,maintenanceDepartment]);
  const downloadMaintenanceForm=(item:MaintenanceMovement)=>{setDocumentToEdit(item);};
  const createMaintenanceMovement=async()=>{
    const fail=(message:string)=>{setMaintenanceFormError(message);setError("");};
    setMaintenanceFormError("");
    if(!maintenanceDepartment){fail("Selecciona primero el departamento responsable del mantenimiento.");return;}
    if(!maintenanceDraft.occurredAt){fail("Indica la fecha y hora efectiva del movimiento.");return;}
    if(!maintenanceDraft.equipmentType.trim()||!maintenanceDraft.failureCause){fail("Completa el equipo y el motivo del movimiento.");return;}
    if(maintenanceDraft.operationalArea==="WORKSHOP"&&maintenanceDepartment==="HUMAN_RESOURCES"){fail("Recursos Humanos no forma parte de Taller. Usa Almacén para sus movimientos.");return;}
    if(maintenanceDraft.operationalArea==="WORKSHOP"&&maintenanceDepartment==="GENERAL_SERVICES"&&!/INVERSOR|INVERTER/i.test(maintenanceDraft.equipmentType)){fail("El Taller de Servicios Generales recibe únicamente inversores.");return;}
    if(maintenanceDraft.operationalArea==="WORKSHOP"&&!maintenanceDraft.agencyId&&maintenanceDraft.movementType!=="ENTRY"){fail("Selecciona la agencia de origen o destino del equipo de Taller.");return;}
    if(maintenanceDraft.movementType==="REQUEST"&&!maintenanceDraft.agencyId){fail("Selecciona la agencia que requiere el equipo.");return;}
    if(maintenanceDraft.operationalArea==="WAREHOUSE"&&!['ENTRY','REQUEST'].includes(maintenanceDraft.movementType)&&!maintenanceDraft.agencyId&&!maintenanceDraft.destinationName.trim()){fail("Indica la agencia, el departamento o el Taller de destino del movimiento.");return;}
    if(maintenanceDraft.movementType==="TRANSFER_TO_WORKSHOP"&&maintenanceDepartment==="HUMAN_RESOURCES"){fail("Recursos Humanos no puede transferir equipos a Taller.");return;}
    if(maintenanceDraft.movementType==="TRANSFER_TO_WORKSHOP"&&maintenanceDepartment==="GENERAL_SERVICES"&&!/INVERSOR|INVERTER/i.test(maintenanceDraft.equipmentType)){fail("Servicios Generales solo puede transferir inversores a su taller independiente.");return;}
    if(maintenanceDraft.operationalArea==="WAREHOUSE"&&!["ENTRY","REQUEST","NEW_DELIVERY","DAMAGED_RETURN","TRANSFER_TO_WORKSHOP","EXIT","DISCHARGE"].includes(maintenanceDraft.movementType)){fail("Almacén solo admite solicitudes, entradas, entregas, retiros, transferencias, salidas y descargos.");return;}
    if(maintenanceDraft.movementType!=="REQUEST"&&(!maintenanceDraft.deliveredByName.trim()||!maintenanceDraft.receivedByName.trim())){fail("Registra quién entrega y quién recibe para conservar la cadena de custodia.");return;}
    if(["REQUEST","NEW_DELIVERY","EXIT"].includes(maintenanceDraft.movementType)&&!maintenanceDraft.technicianUserId){fail(maintenanceDraft.movementType==="REQUEST"?"Selecciona el técnico que instalará el equipo solicitado.":"Selecciona el técnico o supervisor que recibe el equipo.");return;}
    const serial=normalizeMaintenanceSerial(maintenanceDraft.serialNumber);
    if(!serial&&maintenanceDraft.movementType!=="REQUEST"){fail("Escanea o escribe el serial/código del equipo antes de guardarlo.");maintenanceSerialInputRef.current?.focus();return;}
    const existing=serial?maintenanceAssetMap.get(serial):null;
    if(existing&&maintenanceDraft.movementType==="ENTRY"&&existing.movementType!=="EXIT"&&existing.movementType!=="DISCHARGE"){
      fail(`El código ${serial} ya está registrado en ${existing.codigo}. Selecciona Reparación, Reemplazo o Salida para agregar un movimiento.`);return;
    }
    setSaving(true);setError("");
    try{
      const ownTechnician=session.role==="Technology"&&session.supportTeam==="TECHNICIANS";
      await request("/api/maintenance/movements",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({...maintenanceDraft,serialNumber:serial||null,signatureData:null,department:maintenanceDepartment,ticketId:maintenanceDraft.ticketId||null,agencyId:maintenanceDraft.agencyId||null,technicianUserId:ownTechnician?session.id:maintenanceDraft.technicianUserId||null,technicianName:ownTechnician?session.displayName:maintenanceDraft.technicianName})});
      const savedDocument=maintenanceDraft.documentNumber;
      closeMaintenanceForm();
      setMaintenanceDraft(newMaintenanceDraft(maintenanceDraft.operationalArea));
      setMaintenanceView(maintenanceDraft.movementType==="REQUEST"||isTechnologyWarehouseRequester?"history":"inventory");
      setMaintenanceQuery(maintenanceDraft.movementType==="REQUEST"?savedDocument:serial);
      setNotice(maintenanceDraft.movementType==="REQUEST"?`Solicitud ${savedDocument} enviada a Almacén para la agencia y el técnico instalador seleccionados.`:`Movimiento ${savedDocument} registrado. Abre PDF + QR para completar las firmas de entrega y recepción.`);
      const refreshed=await loadMaintenanceMovements();
      const savedMovement=refreshed?.find(item=>item.documentNumber===savedDocument);
      if(savedMovement)setDocumentToEdit(savedMovement);
    }catch(saveError){fail((saveError as Error).message||"No fue posible guardar el formulario.");}finally{setSaving(false);}
  };
  const openMaintenanceProcurement=(mode:"supplier"|"requisition"|"order"|"receive"|"return",targetId="")=>{
    const requisition=maintenanceProcurement.requisitions.find(item=>item.id===targetId);
    const order=maintenanceProcurement.purchaseOrders.find(item=>item.id===targetId);
    setMaintenanceProcurementError("");setMaintenanceProcurementMode(mode);setMaintenanceProcurementTarget(targetId);
    setMaintenanceProcurementDraft({supplierId:order?.supplierId||"",supplierName:"",requisitionId:requisition?.id||order?.requisitionId||"",productName:requisition?.productName||order?.productName||"",quantity:mode==="receive"&&order?Math.max(1,order.quantityOrdered-order.quantityReceived):requisition?Math.max(1,requisition.quantityRequested-requisition.quantityFulfilled):1,unitCost:order?.unitCost||0,priority:requisition?.priority||"MEDIUM",expectedAt:"",serialNumber:"",receivedByName:session.displayName,reason:"",taxId:"",contactName:"",phone:"",email:"",notes:""});
  };
  const saveMaintenanceProcurement=async()=>{
    if(!maintenanceProcurementMode)return;const draft=maintenanceProcurementDraft;const fail=(message:string)=>setMaintenanceProcurementError(message);setMaintenanceProcurementError("");
    let url="",body:Record<string,unknown>={};
    if(maintenanceProcurementMode==="supplier"){
      if(!draft.supplierName.trim()){fail("Escribe el nombre comercial del proveedor.");return;}url="/api/maintenance/suppliers";body={name:draft.supplierName,taxId:draft.taxId||null,contactName:draft.contactName||null,phone:draft.phone||null,email:draft.email||null};
    }else if(maintenanceProcurementMode==="requisition"){
      if(!maintenanceDepartment||!draft.productName.trim()||draft.quantity<1){fail("Completa el artículo y la cantidad solicitada.");return;}url="/api/maintenance/requisitions";body={department:maintenanceDepartment,productName:draft.productName,quantity:draft.quantity,priority:draft.priority,notes:draft.notes||null};
    }else if(maintenanceProcurementMode==="order"){
      if(!maintenanceDepartment||!draft.supplierId||!draft.productName.trim()||draft.quantity<1){fail("Selecciona proveedor y completa artículo y cantidad.");return;}url="/api/maintenance/purchase-orders";body={supplierId:draft.supplierId,requisitionId:draft.requisitionId||null,department:maintenanceDepartment,productName:draft.productName,quantity:draft.quantity,unitCost:draft.unitCost,expectedAt:draft.expectedAt||null,notes:draft.notes||null};
    }else if(maintenanceProcurementMode==="receive"){
      if(!maintenanceProcurementTarget||!draft.serialNumber.trim()||draft.quantity<1){fail("Indica cantidad recibida y serial o lote físico.");return;}url=`/api/maintenance/purchase-orders/${maintenanceProcurementTarget}/receive`;body={quantity:draft.quantity,serialNumber:draft.serialNumber,receivedByName:draft.receivedByName||session.displayName,notes:draft.notes||null};
    }else{
      if(!maintenanceDepartment||!draft.productName.trim()||!draft.serialNumber.trim()||!draft.reason.trim()||draft.quantity<1){fail("Completa artículo, serial o lote, cantidad y motivo de devolución.");return;}url="/api/maintenance/returns";body={supplierId:draft.supplierId||null,purchaseOrderId:maintenanceProcurementTarget||null,department:maintenanceDepartment,productName:draft.productName,serialNumber:draft.serialNumber,quantity:draft.quantity,reason:draft.reason,notes:draft.notes||null};
    }
    setSaving(true);try{await request(url,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)});setMaintenanceProcurementMode(null);setNotice(maintenanceProcurementMode==="receive"?"Recepción física registrada y sumada al inventario.":maintenanceProcurementMode==="order"?"Orden de compra emitida y asociada correctamente.":maintenanceProcurementMode==="requisition"?"Requisición registrada para seguimiento.":maintenanceProcurementMode==="return"?"Devolución registrada y descontada del inventario.":"Proveedor guardado correctamente.");await loadMaintenanceMovements();}catch(saveError){fail((saveError as Error).message||"No fue posible guardar la operación.");}finally{setSaving(false);}
  };
  useEffect(() => {
    void load();
    const refresh = () => {
      if (document.visibilityState === "visible") void load(true);
    };
    window.addEventListener("support-live-refresh", refresh);
    return () => {
      window.removeEventListener("support-live-refresh", refresh);
    };
  }, [load, session.id]);

  useEffect(() => {
    if (supportStage === "transitions") void loadAgencyTransitions();
    if (supportStage === "maintenance") void loadMaintenanceMovements();
  }, [supportStage, loadAgencyTransitions, loadMaintenanceMovements]);
  useEffect(()=>{
    if(!isMaintenanceOperator)return;
    setSupportStage("maintenance");
    setMaintenanceArea(isWarehouseOperator?"WAREHOUSE":"WORKSHOP");
    setMaintenanceSelectedDepartment(null);
  },[isMaintenanceOperator,isWarehouseOperator]);
  useEffect(()=>{
    setMaintenanceFormOpen(false);
    setMaintenanceDraft(current=>({...current,technicianUserId:"",technicianName:"",equipmentType:"",componentType:"",failureCause:""}));
  },[maintenanceDepartment]);
  useEffect(()=>{setFindingCategoryOpen(null);},[workspaceDepartment,findingView]);

  const navigationRequestKey = navigationTarget?.requestKey;
  const navigationDepartment = navigationTarget?.assignedDepartment;
  const navigationKind = navigationTarget?.kind;
  const navigationTicketId = navigationTarget?.ticketId;
  const navigationTicketNumber = navigationTarget?.ticketNumber;
  const navigationTicketType = navigationTarget?.ticketType;
  const navigationTicketStatus = navigationTarget?.ticketStatus;
  useEffect(() => {
    if (!navigationKind || navigationRequestKey == null) return;
    const openingSupervisorTicket =
      session.role === "GroupAdministrator" && navigationKind === "TICKET";
    setConfigOpen(false);
    setSupportTab("tickets");
    setSupportStage("tickets");
    setWorkspaceDepartment(
      session.role === "Administrator" || session.role === "GroupAdministrator"
        ? navigationDepartment || null
        : session.supportDepartment,
    );
    setPriorityFilter("ALL");
    setTicketWorkspaceView(
      navigationTicketStatus && resolvedStatuses.has(navigationTicketStatus)
        ? "history"
        : navigationTicketStatus === "IN_PROGRESS"
          ? "process"
          : "active",
    );
    setTechnologyTeamFilter(strictTechnologyTechnician?"ALL":sessionRoutingTeam);
    setTeamViewChosen(true);
    setAssignmentScope(strictTechnologyTechnician||openingSupervisorTicket?"MINE":"ALL");
    setAssignedToMeOnly(strictTechnologyTechnician||openingSupervisorTicket);
    setPersonalAssignmentView(strictTechnologyTechnician||openingSupervisorTicket);
    setSupervisorHistoryView(false);
    if (navigationKind === "TICKET") {
      setReturnToTicketCreation(false);
      directTicketFocusRef.current = true;
      setDirectTicketFocus({
        id: navigationTicketId,
        ticketNumber: navigationTicketNumber,
      });
      if (navigationTicketType) setTicketTypeFilter(navigationTicketType);
      setStatusFilter("ALL");
      setPriorityFilter("ALL");
      setCategoryFilter("ALL");
      setTechnicianFilter("ALL");
      setQuery("");
    } else setQuery("");
  }, [
    navigationDepartment,
    navigationKind,
    navigationRequestKey,
    navigationTicketId,
    navigationTicketNumber,
    navigationTicketStatus,
    navigationTicketType,
    sessionRoutingTeam,
    session.supportTeam,
    session.supportDepartment,
    session.role,
    strictTechnologyTechnician,
  ]);

  const reportAgencyId = reportAgency?.id;
  useEffect(() => {
    if (reportAgencyId == null) return;
    setDraft((current) => ({ ...current, agencyId: reportAgencyId }));
    setFormOpen(false);
  }, [reportAgencyId]);
  useEffect(() => {
    if (directTicketFocusRef.current) return;
    if (supervisorInboxOpeningRef.current) {
      supervisorInboxOpeningRef.current = false;
      return;
    }
    const openingNotification =
      navigationKind === "TICKET" &&
      navigationRequestKey != null &&
      (navigationDepartment || null) === workspaceDepartment;
    if (openingNotification) return;
    setCategoryFilter("ALL");
    setFindingCategoryOpen(null);
    setTicketWorkspaceView(strictTechnologyTechnician?"active":"menu");
    setTechnologyTeamFilter(sessionRoutingTeam);
    setTeamViewChosen(strictTechnologyTechnician);
    setAssignmentScope(strictTechnologyTechnician?"MINE":"ALL");
    setAssignedToMeOnly(strictTechnologyTechnician);
  }, [
    navigationDepartment,
    navigationKind,
    navigationRequestKey,
    sessionRoutingTeam,
    session.supportTeam,
    workspaceDepartment,
    strictTechnologyTechnician,
  ]);
  useEffect(() => {
    if (session.supportTeam === "CALL_CENTER") {
      setTicketTypeFilter("INTERNAL");
      setTechnologyTeamFilter("CALL_CENTER");
      setTeamViewChosen(true);
    } else if (session.supportTeam === "TECHNICAL_FAILURE") {
      setTicketTypeFilter("SUPPORT");
      setTechnologyTeamFilter("TECHNICAL_FAILURE");
      setTeamViewChosen(true);
    } else if (session.supportTeam === "TECHNICIANS") {
      setTechnologyTeamFilter("TECHNICIANS");
      setTeamViewChosen(true);
    }
  }, [session.supportTeam]);

  const normalized = query
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
  const workspaceTickets = useMemo(
    () =>
      workspaceDepartment
        ? tickets.filter(
            (ticket) =>
              ticket.assignedDepartment === workspaceDepartment ||
              (ticket.sharedWithDepartments || []).includes(
                workspaceDepartment,
              ),
          )
        : tickets,
    [tickets, workspaceDepartment],
  );
  const workspaceFindings = useMemo(
    () =>
      workspaceDepartment
        ? findings.filter(
            (finding) => finding.department === workspaceDepartment,
          )
        : findings,
    [findings, workspaceDepartment],
  );
  const pendingFindingCount = workspaceFindings.filter(
    (finding) => !resolvedStatuses.has(finding.status),
  ).length;
  const resolvedFindingCount = workspaceFindings.filter(
    (finding) => resolvedStatuses.has(finding.status),
  ).length;
  const visible = useMemo(
    () =>
      workspaceTickets.filter(
        (ticket) => {
          const showingMyAssignedWork =
            personalAssignmentView ||
            assignedToMeOnly ||
            assignmentScope === "MINE";
          const assignedToCurrentUser =
            ticket.isAssignedToCurrentUser === true ||
            String(ticket.assignedTechnicianId || "").trim().toLowerCase() ===
              String(session.id).trim().toLowerCase();
          const supervisorTouchedTicket =
            assignedToCurrentUser ||
            String(ticket.createdByUsername || "").toLowerCase() === String(session.email).toLowerCase() ||
            String(ticket.resolvedByName || "").toLocaleLowerCase("es") === String(session.displayName).toLocaleLowerCase("es") ||
            (ticket.history || []).some((entry) => String(entry.actorName || "").toLocaleLowerCase("es") === String(session.displayName).toLocaleLowerCase("es"));
          // Las asignaciones directas históricas pueden conservar el equipo
          // anterior (por ejemplo, Avería Técnica). El técnico responsable no
          // debe perderlas por ese dato de ruta; su bandeja es personal.
          const lockedTeamMatches=!lockedTechnologyTeam||ticketBelongsToTechnologyTeam(ticket,lockedTechnologyTeam)||(strictTechnologyTechnician&&assignedToCurrentUser);
          const selectedTechnologyTeam = lockedTechnologyTeam || technologyTeamFilter;
          // Las asignaciones históricas pueden no traer assigned_team=TECHNICIANS
          // (o conservar Avería Técnica). Si el ticket pertenece directamente a
          // este técnico, debe permanecer visible aunque la ruta del equipo sea
          // antigua o incompleta.
          const belongsToSelectedTechnologyTeam =
            ticketBelongsToTechnologyTeam(ticket, selectedTechnologyTeam) ||
            (strictTechnologyTechnician && assignedToCurrentUser);
          // Avería Técnica recibe tanto soporte normal como los tickets
          // internos escalados por Call Center; ambos deben entrar en su
          // bandeja activa sin perder el origen del caso.
          const matchesTicketKind=session.supportTeam==="TECHNICAL_FAILURE"
            ? ticket.ticketType==="SUPPORT" || ticket.ticketType==="INTERNAL"
            : ticket.ticketType===ticketTypeFilter;
          return (
          (supervisorHistoryView || personalAssignmentView || matchesTicketKind) &&
          (!supervisorHistoryView || supervisorTouchedTicket) &&
          (!supervisorHistoryView || successfulStatuses.has(ticket.status)) &&
          (!personalAssignmentView ||
            ticketWorkspaceView !== "history" ||
            ["RESOLVED", "CLOSED"].includes(ticket.status)) &&
          (!assignedToMeOnly || supervisorHistoryView || assignedToCurrentUser) &&
          (supervisorHistoryView || assignmentScope === "ALL" ||
            (assignmentScope === "UNASSIGNED"
              ? !ticket.assignedTechnicianId
              : assignedToCurrentUser)) &&
          (supervisorHistoryView ||
            (ticketWorkspaceView === "active"
              ? showingMyAssignedWork
                ? !resolvedStatuses.has(ticket.status) &&
                  assignedToCurrentUser
                : !resolvedStatuses.has(ticket.status) &&
                  !ticket.assignedTechnicianId &&
                  ticket.status !== "IN_PROGRESS"
              : ticketWorkspaceView === "process"
                ? !resolvedStatuses.has(ticket.status) && (!!ticket.assignedTechnicianId || ticket.status === "IN_PROGRESS")
              : ticketWorkspaceView === "history"
                ? personalAssignmentView
                  ? successfulStatuses.has(ticket.status)
                  : resolvedStatuses.has(ticket.status)
                : true)) &&
          lockedTeamMatches &&
          belongsToSelectedTechnologyTeam &&
          (statusFilter === "ALL" ||
            (statusFilter === "ACTIVE"
              ? !["RESOLVED", "CLOSED"].includes(ticket.status)
              : ticket.status === statusFilter)) &&
          (priorityFilter === "ALL" || ticket.priority === priorityFilter) &&
          (categoryFilter === "ALL" || ticket.category === categoryFilter) &&
          (technicianFilter === "ALL" ||
            String(ticket.assignedTechnicianId || "").toLowerCase() ===
              String(technicianFilter).toLowerCase()) &&
          (!normalized ||
            `${ticket.ticketNumber} ${ticket.codigo} ${ticket.terminal} ${ticket.grupo} ${ticket.subject} ${ticket.createdByName} ${ticket.assignedTechnicianName || ""} ${ticket.resolvedByName || ""}`
              .normalize("NFD")
              .replace(/[\u0300-\u036f]/g, "")
              .toLowerCase()
              .includes(normalized))
          );
        },
      ),
    [
      workspaceTickets,
      normalized,
      statusFilter,
      priorityFilter,
      categoryFilter,
      technicianFilter,
      ticketTypeFilter,
      personalAssignmentView,
      supervisorHistoryView,
      ticketWorkspaceView,
      technologyTeamFilter,
      lockedTechnologyTeam,
      assignedToMeOnly,
      assignmentScope,
      strictTechnologyTechnician,
      session.id,
      session.email,
      session.displayName,
      session.supportTeam,
    ],
  );
  const ticketsPerPage = 12;
  const ticketPageCount = Math.max(1, Math.ceil(visible.length / ticketsPerPage));
  const pagedVisible = visible.slice(
    (ticketPage - 1) * ticketsPerPage,
    ticketPage * ticketsPerPage,
  );
  const ticketBoardRows = pagedVisible;
  useEffect(() => {
    setTicketPage(1);
    setExpandedTicketId(null);
    setTicketActionMode(null);
    setExpandedHistoryTicketId(null);
  }, [
    query,
    statusFilter,
    priorityFilter,
    categoryFilter,
    ticketTypeFilter,
    ticketWorkspaceView,
    technologyTeamFilter,
    assignedToMeOnly,
    workspaceDepartment,
  ]);
  useEffect(() => {
    if (!directTicketFocus) return;
    const target = tickets.find(
      (ticket) =>
        (!!directTicketFocus.id && ticket.id === directTicketFocus.id) ||
        (!!directTicketFocus.ticketNumber &&
          ticket.ticketNumber === directTicketFocus.ticketNumber),
    );
    if (!target) {
      return;
    }
    const expectedView = resolvedStatuses.has(target.status)
      ? "history"
      : target.status === "IN_PROGRESS" ||
          target.status === "PENDING" ||
          !!target.assignedTechnicianId
        ? "process"
        : "active";
    if (ticketWorkspaceView !== expectedView) {
      setTicketWorkspaceView(expectedView);
      return;
    }
    const targetIndex = visible.findIndex((ticket) => ticket.id === target.id);
    if (targetIndex < 0) return;
    setTicketPage(Math.floor(targetIndex / ticketsPerPage) + 1);
    setExpandedTicketId(target.id);
    setTicketActionMode(null);
    setExpandedHistoryTicketId(null);
    directTicketFocusRef.current = false;
    setDirectTicketFocus(null);
    window.setTimeout(
      () =>
        document
          .querySelector('[data-ticket-id="' + target.id + '"]')
          ?.scrollIntoView({ behavior: "smooth", block: "start" }),
      180,
    );
  }, [
    directTicketFocus,
    loading,
    ticketWorkspaceView,
    tickets,
    visible,
  ]);
  useEffect(() => {
    if (ticketPage > ticketPageCount) setTicketPage(ticketPageCount);
  }, [ticketPage, ticketPageCount]);
  const agencyNeedle = agencyQuery
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
  const visibleAgencies = agencies.filter(
    (agency) =>
      !agencyNeedle ||
      `${agency.codigo} ${agency.terminal} ${agency.grupo}`
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .toLowerCase()
        .includes(agencyNeedle),
  );
  const agencyDirectoryNeedle=agencyDirectoryQuery.normalize("NFD").replace(/[\u0300-\u036f]/g,"").trim().toLowerCase();
  const agencyDirectoryRows=agencies.filter(agency=>!agencyDirectoryNeedle||`${agency.codigo} ${agency.terminal} ${agency.grupo} ${agency.direccion||""} ${agency.municipio||""}`.normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLowerCase().includes(agencyDirectoryNeedle));
  const findingGroups = Array.from(
    new Set(workspaceFindings.map((finding) => finding.grupo)),
  ).sort((a, b) => a.localeCompare(b, "es"));
  const ticketCategoryChoices = Array.from(
    new Map(
      catalog
        .filter(
          (department) =>
            !(workspaceDepartment||(session.role==="Technology"&&session.supportTeam!=="CALL_CENTER"&&session.supportTeam!=="TECHNICAL_FAILURE")) ||
            department.code === (workspaceDepartment||session.supportDepartment),
        )
        .flatMap((department) =>
          department.categories.map((category) => [category.code, category.name]),
        ),
    ).entries(),
  ).sort((a, b) => a[1].localeCompare(b[1], "es"));
  const isFindingSupervisor=(finding:AutomaticFinding)=>technicians.some(item=>item.id===finding.assignedTechnicianId&&(item.isAgencySupervisor||item.role==="GroupAdministrator"));
  const findingAssigneeChoices=technicians.filter(item=>findingAssigneeKind==="SUPERVISORS"?(item.isAgencySupervisor||item.role==="GroupAdministrator"):findingAssigneeKind==="TECHNICIANS"?(item.isTechnician&&(!workspaceDepartment||item.department===workspaceDepartment)):(item.isTechnician&&(!workspaceDepartment||item.department===workspaceDepartment))||item.isAgencySupervisor||item.role==="GroupAdministrator");
  const activeAssignedFindings=workspaceFindings.filter(finding=>!!finding.assignedTechnicianId&&!resolvedStatuses.has(finding.status));
  const assignedFindingCounts={technicians:activeAssignedFindings.filter(finding=>!isFindingSupervisor(finding)).length,supervisors:activeAssignedFindings.filter(isFindingSupervisor).length};
  const visibleFindings = workspaceFindings.filter(
    (finding) =>
      (findingGroup === "ALL" || finding.grupo === findingGroup) &&
      (findingAssigneeKind==="ALL"||(findingAssigneeKind==="SUPERVISORS"?isFindingSupervisor(finding):!!finding.assignedTechnicianId&&!isFindingSupervisor(finding))) &&
      (findingAssigneeFilter==="ALL"||finding.assignedTechnicianId===findingAssigneeFilter) &&
       (findingView === "resolved"
         ? resolvedStatuses.has(finding.status)
         : findingView === "tracking"
           ? true
           : !resolvedStatuses.has(finding.status)),
  );
  const findingsByType = Array.from(visibleFindings.reduce((sections,finding)=>{
    const category=automaticFindingCategory(finding);
    const key=`${finding.department}:${category.key}`;
    const section=sections.get(key)||{key,title:category.title,department:finding.department,rows:[] as AutomaticFinding[]};
    section.rows.push(finding);sections.set(key,section);return sections;
  },new Map<string,{key:string;title:string;department:string;rows:AutomaticFinding[]}>()).values()).sort((left,right)=>left.department.localeCompare(right.department,"es")||left.title.localeCompare(right.title,"es"));

  const reportBaseTickets = useMemo(
    () =>
      workspaceTickets.filter((ticket) => {
        if (ticket.ticketType !== ticketTypeFilter) return false;
        if (lockedTechnologyTeam) {
          const assignedToCurrentUser =
            ticket.isAssignedToCurrentUser === true ||
            String(ticket.assignedTechnicianId || "").trim().toLowerCase() ===
              String(session.id).trim().toLowerCase();
          if (
            !ticketBelongsToTechnologyTeam(ticket, lockedTechnologyTeam) &&
            !(strictTechnologyTechnician && assignedToCurrentUser)
          ) return false;
        }
        if (
          teamViewChosen &&
          workspaceDepartment === "TECHNOLOGY"
        ) {
          const selectedTechnologyTeam = lockedTechnologyTeam || technologyTeamFilter;
          const assignedToCurrentUser =
            ticket.isAssignedToCurrentUser === true ||
            String(ticket.assignedTechnicianId || "").trim().toLowerCase() ===
              String(session.id).trim().toLowerCase();
          if (
            !ticketBelongsToTechnologyTeam(ticket, selectedTechnologyTeam) &&
            !(strictTechnologyTechnician && assignedToCurrentUser)
          ) return false;
        }
        return !strictTechnologyTechnician ||
          String(ticket.assignedTechnicianId || "").trim().toLowerCase() === String(session.id).trim().toLowerCase() ||
          ticket.resolvedByName === session.displayName;
      }),
    [
      session.displayName,
      session.id,
      strictTechnologyTechnician,
      ticketTypeFilter,
      lockedTechnologyTeam,
      teamViewChosen,
      technologyTeamFilter,
      workspaceDepartment,
      workspaceTickets,
    ],
  );
  const reportBaseFindings = useMemo(
    () =>
      strictTechnologyTechnician
        ? workspaceFindings.filter(
            (finding) =>
              String(finding.assignedTechnicianId || "").toLowerCase() ===
                String(session.id).toLowerCase() ||
              finding.resolvedByName === session.displayName,
          )
        : workspaceFindings,
    [
      session.displayName,
      session.id,
      strictTechnologyTechnician,
      workspaceFindings,
    ],
  );
  const resolvedTickets = useMemo(() => {
    const now=new Date();
    const cutoff=new Date(now);
    if(reportPeriod==="DAY")cutoff.setHours(0,0,0,0);
    else if(reportPeriod==="WEEK")cutoff.setDate(now.getDate()-6);
    else if(reportPeriod==="MONTH")cutoff.setDate(now.getDate()-29);
    return reportBaseTickets.filter((ticket)=>{
      if(!successfulStatuses.has(ticket.status))return false;
      const closed=new Date(ticket.closedAt||ticket.updatedAt);
      if(reportPeriod!=="ALL"&&closed<cutoff)return false;
      return reportOwner==="ALL"||ticket.resolvedByName===reportOwner||ticket.assignedTechnicianName===reportOwner;
    });
  },[reportBaseTickets,reportPeriod,reportOwner]);
  const reportScopedTickets = useMemo(() => {
    const now = new Date();
    const cutoff = new Date(now);
    if (reportPeriod === "DAY") cutoff.setHours(0, 0, 0, 0);
    else if (reportPeriod === "WEEK") cutoff.setDate(now.getDate() - 6);
    else if (reportPeriod === "MONTH") cutoff.setDate(now.getDate() - 29);
    return reportBaseTickets.filter((ticket) => {
      const activityDate = new Date(
        resolvedStatuses.has(ticket.status)
          ? ticket.closedAt || ticket.updatedAt
          : ticket.createdAt,
      );
      if (reportPeriod !== "ALL" && activityDate < cutoff) return false;
      return (
        reportOwner === "ALL" ||
        ticket.resolvedByName === reportOwner ||
        ticket.assignedTechnicianName === reportOwner
      );
    });
  }, [reportBaseTickets, reportPeriod, reportOwner]);
  const reportPersonForTicket = useCallback(
    (ticket: Ticket) =>
      technicians.find(
        (person) =>
          String(person.id).toLowerCase() ===
            String(ticket.assignedTechnicianId || "").toLowerCase() ||
          (!!ticket.resolvedByName && person.name === ticket.resolvedByName) ||
          (!!ticket.assignedTechnicianName &&
            person.name === ticket.assignedTechnicianName),
      ),
    [technicians],
  );
  const reportResponsibleType = useCallback(
    (ticket: Ticket) => {
      const person = reportPersonForTicket(ticket);
      if (person?.isAgencySupervisor || person?.role === "GroupAdministrator")
        return "SUPERVISORS" as const;
      const team = effectiveTechnologyTeam(ticket);
      if (team === "CALL_CENTER") return "CALL_CENTER" as const;
      if (team === "TECHNICAL_FAILURE") return "TECHNICAL_FAILURE" as const;
      if (team === "TECHNICIANS" || ticket.assignedTechnicianId)
        return "TECHNICIANS" as const;
      return "OTHER" as const;
    },
    [reportPersonForTicket],
  );
  const responsibleComparison = useMemo(() => {
    const definitions = [
      { key: "TECHNICIANS", label: "Técnicos", color: "#39c7ef" },
      { key: "SUPERVISORS", label: "Supervisores", color: "#a78bfa" },
      { key: "CALL_CENTER", label: "Call Center", color: "#f59e0b" },
      { key: "TECHNICAL_FAILURE", label: "Avería Técnica", color: "#34d399" },
      { key: "OTHER", label: "Otros departamentos", color: "#94a3b8" },
    ] as const;
    return definitions.map((definition) => {
      const rows = reportScopedTickets.filter(
        (ticket) => reportResponsibleType(ticket) === definition.key,
      );
      const resolved = rows.filter((ticket) => successfulStatuses.has(ticket.status));
      const totalMinutes = resolved.reduce(
        (total, ticket) => total + ticketMinutes(ticket),
        0,
      );
      return {
        ...definition,
        total: rows.length,
        resolved: resolved.length,
        active: rows.length - resolved.length,
        critical: rows.filter(
          (ticket) =>
            ticket.priority === "CRITICAL" &&
            !resolvedStatuses.has(ticket.status),
        ).length,
        evidence: resolved.filter((ticket) => (ticket.evidence?.length || 0) > 0)
          .length,
        averageMinutes: resolved.length
          ? Math.round(totalMinutes / resolved.length)
          : 0,
      };
    });
  }, [reportResponsibleType, reportScopedTickets]);
  const responsibleMaximum = Math.max(
    1,
    ...responsibleComparison.map((row) => row.total),
  );
  const statusBreakdown = Object.entries(statuses).map(([key, label]) => ({
    key,
    label,
    total: reportScopedTickets.filter((ticket) => ticket.status === key).length,
  }));
  const priorityBreakdown = Object.entries(priorities).map(([key, label]) => ({
    key,
    label,
    total: reportScopedTickets.filter((ticket) => ticket.priority === key).length,
  }));
  const personnelPerformance = useMemo(
    () =>
      technicians
        .filter((person) => person.isTechnician || person.isAgencySupervisor || person.role === "GroupAdministrator")
        .map((person) => {
          const rows = reportScopedTickets.filter(
            (ticket) =>
              String(ticket.assignedTechnicianId || "").toLowerCase() ===
                String(person.id).toLowerCase() ||
              ticket.resolvedByName === person.name,
          );
          const resolved = rows.filter((ticket) =>
            successfulStatuses.has(ticket.status),
          );
          return {
            id: person.id,
            name: person.name,
            role:
              person.isAgencySupervisor || person.role === "GroupAdministrator"
                ? "Supervisor"
                : "Técnico",
            department: departments[person.department] || person.department,
            groups: person.groups?.length || 0,
            active: rows.length - resolved.length,
            resolved: resolved.length,
            critical: rows.filter(
              (ticket) =>
                ticket.priority === "CRITICAL" &&
                !resolvedStatuses.has(ticket.status),
            ).length,
            averageMinutes: resolved.length
              ? Math.round(
                  resolved.reduce(
                    (total, ticket) => total + ticketMinutes(ticket),
                    0,
                  ) / resolved.length,
                )
              : 0,
            evidencePercent: resolved.length
              ? Math.round(
                  (resolved.filter(
                    (ticket) => (ticket.evidence?.length || 0) > 0,
                  ).length /
                    resolved.length) *
                    100,
                )
              : 0,
          };
        })
        .filter((person) => person.active > 0 || person.resolved > 0)
        .sort(
          (a, b) =>
            b.resolved - a.resolved ||
            a.averageMinutes - b.averageMinutes ||
            a.name.localeCompare(b.name, "es"),
        ),
    [reportScopedTickets, technicians],
  );
  const reportFindings = useMemo(() => {
    const now = new Date();
    const cutoff = new Date(now);
    if (reportPeriod === "DAY") cutoff.setHours(0, 0, 0, 0);
    else if (reportPeriod === "WEEK") cutoff.setDate(now.getDate() - 6);
    else if (reportPeriod === "MONTH") cutoff.setDate(now.getDate() - 29);
    return reportBaseFindings.filter((finding) => {
      const activity = new Date(finding.resolvedAt || finding.submittedAt);
      if (reportPeriod !== "ALL" && activity < cutoff) return false;
      return (
        reportOwner === "ALL" ||
        finding.resolvedByName === reportOwner ||
        finding.assignedTechnicianName === reportOwner
      );
    });
  }, [reportBaseFindings, reportOwner, reportPeriod]);
  const findingReportSummary = useMemo(() => {
    const resolved = reportFindings.filter((finding) =>
      resolvedStatuses.has(finding.status),
    );
    const assignedToSupervisors = reportFindings.filter((finding) =>
      technicians.some(
        (person) =>
          String(person.id).toLowerCase() ===
            String(finding.assignedTechnicianId || "").toLowerCase() &&
          (person.isAgencySupervisor || person.role === "GroupAdministrator"),
      ),
    ).length;
    return {
      total: reportFindings.length,
      pending: reportFindings.length - resolved.length,
      resolved: resolved.length,
      technicians: reportFindings.filter(
        (finding) => finding.assignedTechnicianId,
      ).length - assignedToSupervisors,
      supervisors: assignedToSupervisors,
      evidence: resolved.filter((finding) => finding.evidence.length > 0).length,
      averageMinutes: resolved.length
        ? Math.round(
            resolved.reduce(
              (total, finding) =>
                total +
                (finding.resolutionMinutes ??
                  Math.max(
                    0,
                    Math.round(
                      (new Date(finding.resolvedAt || finding.submittedAt).getTime() -
                        new Date(finding.submittedAt).getTime()) /
                        60000,
                    ),
                  )),
              0,
            ) / resolved.length,
          )
        : 0,
    };
  }, [reportFindings, technicians]);
  const findingTypeReport = useMemo(() => {
    const buckets = new Map<string, { label: string; total: number; resolved: number }>();
    reportFindings.forEach((finding) => {
      const bucket = buckets.get(finding.findingKey) || {
        label: finding.title,
        total: 0,
        resolved: 0,
      };
      bucket.total += 1;
      if (resolvedStatuses.has(finding.status)) bucket.resolved += 1;
      buckets.set(finding.findingKey, bucket);
    });
    return Array.from(buckets, ([key, bucket]) => ({ key, ...bucket })).sort(
      (a, b) => b.total - a.total || a.label.localeCompare(b.label, "es"),
    );
  }, [reportFindings]);
  const activeTeamCounts = useMemo(() => {
    const active = workspaceTickets.filter((ticket) =>
      ticket.ticketType === ticketTypeFilter &&
      !resolvedStatuses.has(ticket.status) &&
      (!lockedTechnologyTeam || ticketBelongsToTechnologyTeam(ticket,lockedTechnologyTeam)),
    );
    return {
      all: active.length,
      callCenter: active.filter((ticket) => effectiveTechnologyTeam(ticket) === "CALL_CENTER").length,
      technicalFailure: active.filter((ticket) => effectiveTechnologyTeam(ticket) === "TECHNICAL_FAILURE").length,
      technicians: active.filter((ticket) => effectiveTechnologyTeam(ticket) === "TECHNICIANS").length,
      unassigned: active.filter((ticket) => !ticket.assignedTechnicianId).length,
      mine: active.filter((ticket) => ticket.assignedTechnicianId === session.id).length,
    };
  }, [workspaceTickets, ticketTypeFilter, session.id, lockedTechnologyTeam]);
  const reportRows = useMemo(() => {
    const labels = new Map<string, string>();
    const buckets = new Map<
      string,
      { resolved: number; minutes: number; categories: Set<string> }
    >();
    resolvedTickets.forEach((ticket) => {
      let key = ticket.assignedDepartment;
      let label = departments[key] || key;
      if (reportDimension === "technician") {
        key =
          ticket.resolvedByName ||
          ticket.assignedTechnicianId ||
          "UNASSIGNED";
        label =
          ticket.resolvedByName ||
          ticket.assignedTechnicianName ||
          "Sin técnico registrado";
      } else if (reportDimension === "category") {
        key = ticket.category;
        label =
          ticketCategoryChoices.find(([code]) => code === ticket.category)?.[1] ||
          categories[ticket.category] ||
          ticket.category;
      } else if (reportDimension === "group") {
        key = ticket.grupo || "SIN_GRUPO";
        label = ticket.grupo || "Sin grupo registrado";
      }
      labels.set(key, label);
      const bucket = buckets.get(key) || {
        resolved: 0,
        minutes: 0,
        categories: new Set<string>(),
      };
      bucket.resolved += 1;
      bucket.minutes += ticketMinutes(ticket);
      bucket.categories.add(ticket.category);
      buckets.set(key, bucket);
    });
    return Array.from(buckets, ([key, bucket]) => ({
      key,
      label: labels.get(key) || key,
      resolved: bucket.resolved,
      averageMinutes: Math.round(bucket.minutes / Math.max(1, bucket.resolved)),
      categories: bucket.categories.size,
      detail: `${bucket.categories.size} categorías atendidas`,
    })).sort((a, b) => b.resolved - a.resolved || a.label.localeCompare(b.label, "es"));
  }, [
    reportDimension,
    resolvedTickets,
    ticketCategoryChoices,
  ]);
  const reportMaximum = Math.max(1, ...reportRows.map((row) => row.resolved));
  const reportHistory = useMemo(() => {
    const totals = new Map<string, number>();
    resolvedTickets.forEach((ticket) => {
      const rawDate = new Date(ticket.closedAt || ticket.updatedAt);
      let date=(ticket.closedAt || ticket.updatedAt).slice(0,10);
      if(reportPeriod==="WEEK"){
        const start=new Date(rawDate);start.setDate(rawDate.getDate()-((rawDate.getDay()+6)%7));date=start.toISOString().slice(0,10);
      }else if(reportPeriod==="MONTH")date=date.slice(0,7);
      totals.set(date, (totals.get(date) || 0) + 1);
    });
    return Array.from(totals, ([date, resolved]) => ({ date, resolved }))
      .sort((a, b) => a.date.localeCompare(b.date))
      .slice(-12);
  }, [resolvedTickets,reportPeriod]);
  const reportOwners=technicians
    .filter(person=>(person.isTechnician||person.isAgencySupervisor||person.role==="GroupAdministrator")&&(!workspaceDepartment||person.department===workspaceDepartment||person.isAgencySupervisor))
    .map(person=>person.name)
    .sort((a,b)=>a.localeCompare(b,"es"));
  const reportSummary={
    totalCases:reportScopedTickets.length,
    total:resolvedTickets.length,
    technicians:resolvedTickets.filter(ticket=>reportResponsibleType(ticket)==="TECHNICIANS").length,
    supervisors:resolvedTickets.filter(ticket=>reportResponsibleType(ticket)==="SUPERVISORS").length,
    callCenter:resolvedTickets.filter(ticket=>reportResponsibleType(ticket)==="CALL_CENTER").length,
    technicalFailure:resolvedTickets.filter(ticket=>reportResponsibleType(ticket)==="TECHNICAL_FAILURE").length,
    open:reportScopedTickets.filter(ticket=>!resolvedStatuses.has(ticket.status)&&!ticket.assignedTechnicianId&&ticket.status!=="IN_PROGRESS").length,
    inProgress:reportScopedTickets.filter(ticket=>!resolvedStatuses.has(ticket.status)&&(!!ticket.assignedTechnicianId||ticket.status==="IN_PROGRESS")).length,
    critical:reportScopedTickets.filter(ticket=>!resolvedStatuses.has(ticket.status)&&ticket.priority==="CRITICAL").length,
    unassigned:reportScopedTickets.filter(ticket=>!resolvedStatuses.has(ticket.status)&&!ticket.assignedTechnicianId).length,
    withEvidence:resolvedTickets.filter(ticket=>(ticket.evidence?.length||0)>0).length,
    averageMinutes:resolvedTickets.length?Math.round(resolvedTickets.reduce((total,ticket)=>total+ticketMinutes(ticket),0)/resolvedTickets.length):0,
    evidencePercent:resolvedTickets.length?Math.round((resolvedTickets.filter(ticket=>(ticket.evidence?.length||0)>0).length/resolvedTickets.length)*100):0,
    resolutionRate:reportScopedTickets.length?Math.round((resolvedTickets.length/reportScopedTickets.length)*100):0,
  };
  const reportRankings = useMemo(() => {
    const build = (dimension: "department" | "area") => {
      const buckets = new Map<string, { label: string; total: number; resolved: number; minutes: number; evidence: number }>();
      reportScopedTickets.forEach((ticket) => {
        const department = departments[ticket.assignedDepartment] || ticket.assignedDepartment || "Sin departamento";
        const teamLabel = ticket.assignedTeam === "CALL_CENTER" ? "Call Center" : ticket.assignedTeam === "TECHNICAL_FAILURE" ? "Avería Técnica" : ticket.assignedTeam === "TECHNICIANS" ? "Técnicos de campo" : ticket.category ? (ticketCategoryChoices.find(([code]) => code === ticket.category)?.[1] || categories[ticket.category] || ticket.category) : "Gestión general";
        const key = dimension === "department" ? ticket.assignedDepartment || "NONE" : `${ticket.assignedDepartment}:${ticket.assignedTeam || ticket.category || "GENERAL"}`;
        const bucket = buckets.get(key) || { label: dimension === "department" ? department : `${department} · ${teamLabel}`, total: 0, resolved: 0, minutes: 0, evidence: 0 };
        bucket.total += 1;
        if (successfulStatuses.has(ticket.status)) {
          bucket.resolved += 1;
          bucket.minutes += ticketMinutes(ticket);
          if ((ticket.evidence?.length || 0) > 0) bucket.evidence += 1;
        }
        buckets.set(key, bucket);
      });
      return Array.from(buckets, ([key, bucket]) => ({
        key,
        ...bucket,
        active: bucket.total - bucket.resolved,
        effectiveness: bucket.total ? Math.round((bucket.resolved / bucket.total) * 100) : 0,
        averageMinutes: bucket.resolved ? Math.round(bucket.minutes / bucket.resolved) : 0,
        evidencePercent: bucket.resolved ? Math.round((bucket.evidence / bucket.resolved) * 100) : 0,
      })).sort((a, b) => b.resolved - a.resolved || b.effectiveness - a.effectiveness || a.label.localeCompare(b.label, "es"));
    };
    return { departments: build("department"), areas: build("area") };
  }, [reportScopedTickets, ticketCategoryChoices]);
  const departmentalReport = useMemo(() => {
    const departmentCode = workspaceDepartment || session.supportDepartment || "GENERAL_SERVICES";
    const now = new Date();
    const cutoff = new Date(now);
    if (reportPeriod === "DAY") cutoff.setHours(0, 0, 0, 0);
    else if (reportPeriod === "WEEK") cutoff.setDate(now.getDate() - 6);
    else if (reportPeriod === "MONTH") cutoff.setDate(now.getDate() - 29);
    const rows = reportBaseTickets.filter(ticket => {
      if (ticket.assignedDepartment !== departmentCode) return false;
      const activity = new Date(ticket.status === "RESOLVED" || ticket.status === "CLOSED" || ticket.status === "CANCELLED" ? ticket.closedAt || ticket.updatedAt : ticket.createdAt);
      if (reportPeriod !== "ALL" && activity < cutoff) return false;
      return reportOwner === "ALL" || ticket.resolvedByName === reportOwner || ticket.assignedTechnicianName === reportOwner;
    });
    const commonSections = [
      { key: "resolved", label: "Incidencias resueltas", color: "green", rows: rows.filter(ticket => ticket.status === "RESOLVED" || ticket.status === "CLOSED") },
      { key: "unanswered", label: "Incidencias sin responder", color: "cyan", rows: rows.filter(ticket => ticket.status === "OPEN" && !ticket.assignedTechnicianId) },
      { key: "pending", label: "Incidencias pendientes", color: "yellow", rows: rows.filter(ticket => ticket.status === "IN_PROGRESS" || (ticket.status === "OPEN" && !!ticket.assignedTechnicianId)) },
      { key: "cancellation", label: "Tickets anulados", color: "red", rows: rows.filter(ticket => ticket.status === "CANCELLED") },
    ];
    const specialized = departmentCode === "TECHNOLOGY" ? [
      { key: "call-center", label: "Mesa de servicio / Call Center", color: "violet", rows: rows.filter(ticket => ticket.assignedTeam === "CALL_CENTER") },
      { key: "technical-failure", label: "Avería técnica", color: "orange", rows: rows.filter(ticket => ticket.assignedTeam === "TECHNICAL_FAILURE") },
      { key: "field", label: "Técnicos de campo", color: "sky", rows: rows.filter(ticket => ticket.assignedTeam === "TECHNICIANS" || (!!ticket.assignedTechnicianId && !ticket.assignedTeam)) },
      { key: "systems", label: "Sistemas, redes y conectividad", color: "rose", rows: rows.filter(ticket => ["SYSTEM_FAILURE", "PERROS_SYSTEM_FAILURE", "NETWORK_FAILURE", "INTERNET_FAILURE", "CONNECTIVITY_FAILURE"].includes(ticket.category)) },
    ] : departmentCode === "HUMAN_RESOURCES" ? [
      { key: "staff", label: "Falta de personal", color: "violet", rows: rows.filter(ticket => ticket.category === "STAFF_SHORTAGE") },
      { key: "leave", label: "Licencias y permisos", color: "sky", rows: rows.filter(ticket => ticket.category === "LEAVE_REQUEST") },
      { key: "payroll", label: "Reclamos de nómina", color: "orange", rows: rows.filter(ticket => ticket.category === "PAYROLL_CLAIM") },
      { key: "incentives", label: "Incentivos por ventas", color: "rose", rows: rows.filter(ticket => ticket.category === "SALES_INCENTIVE_CLAIM") },
    ] : [
      { key: "painting", label: "Equipo de pintura", color: "violet", rows: rows.filter(ticket => ticket.category === "PAINTING") },
      { key: "internal", label: "Mantenimiento de plantas eléctricas", color: "sky", rows: rows.filter(ticket => ["GENERATOR_REQUEST", "INVERTER_MAINTENANCE", "BATTERY_MAINTENANCE", "ELECTRICAL_FAILURE"].includes(ticket.category)) },
      { key: "maintenance", label: "Reestructuración física y remodelaciones", color: "orange", rows: rows.filter(ticket => ["PHYSICAL_RESTRUCTURE", "METALWORK", "SHUTTER_FAILURE"].includes(ticket.category)) },
      { key: "transport", label: "Transportación", color: "rose", rows: rows.filter(ticket => ticket.category === "TRANSPORTATION") },
    ];
    return { departmentCode, departmentName: departments[departmentCode] || departmentCode, total: rows.length, sections: [...commonSections.slice(0, 3), ...specialized, commonSections[3]] };
  }, [reportBaseTickets, reportOwner, reportPeriod, session.supportDepartment, workspaceDepartment]);
  const reportPeriodLabel = reportPeriod === "DAY" ? "Hoy" : reportPeriod === "WEEK" ? "Últimos 7 días" : reportPeriod === "MONTH" ? "Últimos 30 días" : "Todo el historial";
  const reportScopeLabel = session.role === "Administrator"
    ? workspaceDepartment
      ? `Vista administrativa · ${departments[workspaceDepartment] || workspaceDepartment}`
      : "Vista administrativa global · Todos los departamentos"
    : strictTechnologyTechnician
      ? `Estadísticas personales · ${session.displayName}`
      : `Operación de ${departments[session.supportDepartment || workspaceDepartment || ""] || "soporte"}`;
  const reportScopeDetail = strictTechnologyTechnician
    ? "Solo incluye tus casos asignados, soluciones, evidencias y tiempos de atención."
    : session.role === "Administrator"
      ? "Incluye soportes, departamentos, responsables, técnicos, estados, evidencias y tiempos."
      : "Incluye los casos del soporte y el trabajo de los técnicos de este departamento.";
  const reportHistoryMaximum = Math.max(
    1,
    ...reportHistory.map((point) => point.resolved),
  );
  const _exportStatisticsCsv = () => {
    const safeCell = (value: unknown) => {
      const raw = String(value ?? "");
      const protectedValue = /^[=+\-@]/.test(raw) ? `'${raw}` : raw;
      return `"${protectedValue.replace(/"/g, '""')}"`;
    };
    const rows: unknown[][] = [[
      "Registro", "Número/Tipo", "Código", "Agencia", "Grupo", "Departamento",
      "Categoría", "Prioridad", "Estado", "Responsable", "Tipo de responsable",
      "Creado/Detectado", "Cerrado/Resuelto", "Minutos de solución", "Evidencias",
    ]];
    reportScopedTickets.forEach((ticket) => rows.push([
      "Ticket", ticket.ticketNumber, ticket.codigo, ticket.terminal, ticket.grupo,
      departments[ticket.assignedDepartment] || ticket.assignedDepartment,
      ticketCategoryChoices.find(([code]) => code === ticket.category)?.[1] || categories[ticket.category] || ticket.category,
      priorities[ticket.priority] || ticket.priority, statuses[ticket.status] || ticket.status,
      ticket.resolvedByName || ticket.assignedTechnicianName || "Sin responsable",
      responsibleComparison.find(row => row.key === reportResponsibleType(ticket))?.label || reportResponsibleType(ticket),
      ticket.createdAt, ticket.closedAt || "", resolvedStatuses.has(ticket.status) ? ticketMinutes(ticket) : "",
      ticket.evidence?.length || 0,
    ]));
    reportFindings.forEach((finding) => {
      const supervisor = technicians.some(person => String(person.id).toLowerCase() === String(finding.assignedTechnicianId || "").toLowerCase() && (person.isAgencySupervisor || person.role === "GroupAdministrator"));
      rows.push([
        "Hallazgo automático", finding.title, finding.codigo, finding.terminal, finding.grupo,
        departments[finding.department] || finding.department, finding.title,
        priorities[finding.priority] || finding.priority, statuses[finding.status] || finding.status,
        finding.resolvedByName || finding.assignedTechnicianName || "Sin responsable",
        finding.assignedTechnicianId ? (supervisor ? "Supervisor" : "Técnico") : "Sin asignar",
        finding.submittedAt, finding.resolvedAt || "", finding.resolutionMinutes ?? "", finding.evidence.length,
      ]);
    });
    const csv = `\uFEFF${rows.map(row => row.map(safeCell).join(";")).join("\r\n")}`;
    const url = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = `reporte-estadistico-${reportPeriod.toLowerCase()}-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };
  type ReportSection = "all" | "departments" | "areas" | "personnel" | "resolved" | "findings" | "department";
  const reportFileName = (section: ReportSection, owner?: string) =>
    `real-reporte-${section}-${(owner || reportOwner || "general").normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[^a-zA-Z0-9]+/g, "-").replace(/^-|-$/g, "").toLowerCase()}-${new Date().toISOString().slice(0, 10)}`;
  const downloadBlob = (blob: Blob, fileName: string) => {
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 0);
  };
  const ticketsForOwner = (owner?: string) => owner
    ? resolvedTickets.filter(ticket => ticket.resolvedByName === owner || ticket.assignedTechnicianName === owner)
    : resolvedTickets;
  const exportReportExcel = async (section: ReportSection = "all", owner?: string) => {
    try {
      const ExcelJS = await import("exceljs");
      const workbook = new ExcelJS.Workbook();
      workbook.creator = "Real Agencias · Grupo Tejeda";
      workbook.created = new Date();
      workbook.subject = "Reporte ejecutivo de soporte operativo";
      workbook.company = "Real Agencias · Grupo Tejeda";
      workbook.calcProperties.fullCalcOnLoad = true;
      const navy = "FF071B2E", blue = "FF0F6C9C", cyan = "FF4DD7FF", white = "FFFFFFFF", green = "FF159B78", violet = "FF7255C9", amber = "FFF59E0B", red = "FFB63A58";
      type ReportCell = string | number | Date;
      const addSheet = (name: string, title: string, headers: string[], rows: ReportCell[][], color = blue) => {
        const sheet = workbook.addWorksheet(name, { views: [{ state: "frozen", ySplit: 4, showGridLines: false }], properties: { tabColor: { argb: color } } });
        sheet.mergeCells(1, 1, 1, Math.max(1, headers.length));
        const titleCell = sheet.getCell(1, 1); titleCell.value = `REAL · ${title}`; titleCell.font = { bold: true, size: 20, color: { argb: white } }; titleCell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: navy } }; titleCell.alignment = { vertical: "middle" }; sheet.getRow(1).height = 34;
        sheet.mergeCells(2, 1, 2, Math.max(1, headers.length));
        sheet.getCell(2, 1).value = `${reportScopeLabel} · Periodo: ${reportPeriodLabel} · Generado: ${new Date().toLocaleString("es-DO")} · Responsable: ${owner || reportOwner === "ALL" ? owner || "Todos" : reportOwner}`;
        sheet.getCell(2, 1).font = { italic: true, color: { argb: "FF557487" } }; sheet.getRow(2).height = 22;
        const header = sheet.getRow(4); header.values = headers; header.height = 25;
        header.eachCell(cell => { cell.font = { bold: true, color: { argb: white } }; cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: color } }; cell.alignment = { vertical: "middle", horizontal: "center", wrapText: true }; });
        rows.forEach((values, index) => { const row = sheet.addRow(values); row.height = 22; row.eachCell(cell => { cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: index % 2 ? "FFF4FAFD" : white } }; cell.alignment = { vertical: "middle", wrapText: true }; cell.border = { bottom: { style: "hair", color: { argb: "FFBBD9E8" } } }; }); });
        sheet.autoFilter = { from: { row: 4, column: 1 }, to: { row: Math.max(4, rows.length + 4), column: headers.length } };
        headers.forEach((headerName, index) => { const values = [headerName, ...rows.map(row => String(row[index] ?? ""))]; sheet.getColumn(index + 1).width = Math.min(45, Math.max(12, ...values.map(value => value.length + 2))); });
        headers.forEach((headerName, index) => {
          const column = sheet.getColumn(index + 1);
          if (/fecha|creado|actualizado|cierre|detectado|resuelto/i.test(headerName)) column.numFmt = "yyyy-mm-dd hh:mm";
          if (/efectividad|evidencia %|resolución %/i.test(headerName)) column.numFmt = "0%";
          if (/minutos|promedio \(min\)|duración \(min\)/i.test(headerName)) column.numFmt = "#,##0";
        });
        const lastRow = Math.max(5, rows.length + 4);
        const statusColumn = headers.findIndex(value => value === "Estado") + 1;
        if (statusColumn > 0 && rows.length) {
          const letter = sheet.getColumn(statusColumn).letter;
          sheet.addConditionalFormatting({ ref: `${letter}5:${letter}${lastRow}`, rules: [
            { type: "containsText", priority: 1, operator: "containsText", text: "Resuelto", style: { fill: { type: "pattern", pattern: "solid", bgColor: { argb: "FFDDF5EA" }, fgColor: { argb: "FFDDF5EA" } }, font: { color: { argb: "FF176B52" }, bold: true } } },
          ] });
        }
        const priorityColumn = headers.findIndex(value => value === "Prioridad") + 1;
        if (priorityColumn > 0 && rows.length) {
          const letter = sheet.getColumn(priorityColumn).letter;
          sheet.addConditionalFormatting({ ref: `${letter}5:${letter}${lastRow}`, rules: [{ type: "containsText", priority: 2, operator: "containsText", text: "Crítica", style: { fill: { type: "pattern", pattern: "solid", bgColor: { argb: "FFFBE3EA" }, fgColor: { argb: "FFFBE3EA" } }, font: { color: { argb: red }, bold: true } } }] });
        }
        headers.forEach((headerName, index) => {
          if (!/efectividad|evidencia %|resolución %/i.test(headerName) || !rows.length) return;
          const letter = sheet.getColumn(index + 1).letter;
          sheet.addConditionalFormatting({ ref: `${letter}5:${letter}${lastRow}`, rules: [{ type: "colorScale", priority: 3 + index, cfvo: [{ type: "min" }, { type: "percentile", value: 50 }, { type: "max" }], color: [{ argb: "FFFADBD8" }, { argb: "FFFFF0B5" }, { argb: "FFC8F1DF" }] }] });
        });
        sheet.pageSetup = { orientation: headers.length > 6 ? "landscape" : "portrait", fitToPage: true, fitToWidth: 1, fitToHeight: 0, paperSize: 9, printTitlesRow: "1:4", margins: { left: 0.3, right: 0.3, top: 0.45, bottom: 0.45, header: 0.2, footer: 0.2 } };
        sheet.headerFooter.oddFooter = "&LReal Agencias · Confidencial&C&P de &N&R&D";
        return sheet;
      };
      const resolved = ticketsForOwner(owner);
      if (section === "all") {
        const sheet = workbook.addWorksheet("Dashboard", { views: [{ state: "frozen", ySplit: 3, showGridLines: false }], properties: { tabColor: { argb: green } } });
        for (let column = 1; column <= 12; column += 1) sheet.getColumn(column).width = 13;
        sheet.mergeCells("A1:L1");
        sheet.getCell("A1").value = "REAL · DASHBOARD EJECUTIVO DE SOPORTE";
        sheet.getCell("A1").font = { bold: true, size: 22, color: { argb: white } };
        sheet.getCell("A1").fill = { type: "pattern", pattern: "solid", fgColor: { argb: navy } };
        sheet.getCell("A1").alignment = { vertical: "middle" };
        sheet.getRow(1).height = 38;
        sheet.mergeCells("A2:L2");
        sheet.getCell("A2").value = `${reportScopeLabel} · ${reportPeriodLabel} · Generado ${new Date().toLocaleString("es-DO")}`;
        sheet.getCell("A2").font = { italic: true, color: { argb: "FF557487" } };
        sheet.getCell("A2").alignment = { vertical: "middle" };
        sheet.getRow(2).height = 22;
        const inventoryEnd = Math.max(5, reportScopedTickets.length + 4);
        const dashboardCards = [
          { label: "CASOS DEL PERIODO", result: reportSummary.totalCases, formula: `COUNTA('Todos los casos'!A5:A${inventoryEnd})`, color: blue },
          { label: "CASOS RESUELTOS", result: reportSummary.total, formula: `COUNTIF('Todos los casos'!E5:E${inventoryEnd},"Resuelto")+COUNTIF('Todos los casos'!E5:E${inventoryEnd},"Cerrado")`, color: green },
          { label: "CARGA ACTIVA", result: reportSummary.open + reportSummary.inProgress, formula: `COUNTIF('Todos los casos'!E5:E${inventoryEnd},"Abierto")+COUNTIF('Todos los casos'!E5:E${inventoryEnd},"En proceso")`, color: amber },
          { label: "RESOLUCIÓN %", result: reportSummary.resolutionRate / 100, formula: `IFERROR((COUNTIF('Todos los casos'!E5:E${inventoryEnd},"Resuelto")+COUNTIF('Todos los casos'!E5:E${inventoryEnd},"Cerrado"))/COUNTA('Todos los casos'!A5:A${inventoryEnd}),0)`, color: cyan, percentage: true },
          { label: "PROMEDIO (MIN)", result: reportSummary.averageMinutes, formula: `IFERROR(AVERAGEIF('Todos los casos'!J5:J${inventoryEnd},">0"),0)`, color: violet },
          { label: "EVIDENCIA %", result: reportSummary.evidencePercent / 100, formula: `IFERROR(COUNTIF('Todos los casos'!K5:K${inventoryEnd},">0")/(COUNTIF('Todos los casos'!E5:E${inventoryEnd},"Resuelto")+COUNTIF('Todos los casos'!E5:E${inventoryEnd},"Cerrado")),0)`, color: "FF367F94", percentage: true },
        ];
        dashboardCards.forEach((card, index) => {
          const from = index * 2 + 1, to = from + 1;
          sheet.mergeCells(4, from, 4, to);
          sheet.mergeCells(5, from, 6, to);
          const labelCell = sheet.getCell(4, from), valueCell = sheet.getCell(5, from);
          labelCell.value = card.label;
          labelCell.font = { bold: true, size: 8, color: { argb: white } };
          labelCell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: card.color } };
          labelCell.alignment = { horizontal: "center", vertical: "middle" };
          valueCell.value = { formula: card.formula, result: card.result };
          valueCell.font = { bold: true, size: 22, color: { argb: navy } };
          valueCell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF1F8FB" } };
          valueCell.alignment = { horizontal: "center", vertical: "middle" };
          if (card.percentage) valueCell.numFmt = "0%";
          for (let row = 4; row <= 6; row += 1) for (let column = from; column <= to; column += 1) sheet.getCell(row, column).border = { top: { style: "thin", color: { argb: "FFB8D7E6" } }, bottom: { style: "thin", color: { argb: "FFB8D7E6" } }, left: { style: "thin", color: { argb: "FFB8D7E6" } }, right: { style: "thin", color: { argb: "FFB8D7E6" } } };
        });
        sheet.getRow(4).height = 23; sheet.getRow(5).height = 25; sheet.getRow(6).height = 25;
        const addDashboardTable = (startRow: number, startColumn: number, titleText: string, headers: string[], rows: ReportCell[][], color: string) => {
          const endColumn = startColumn + headers.length - 1;
          sheet.mergeCells(startRow, startColumn, startRow, endColumn);
          const titleCell = sheet.getCell(startRow, startColumn); titleCell.value = titleText; titleCell.font = { bold: true, size: 11, color: { argb: white } }; titleCell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: color } }; titleCell.alignment = { vertical: "middle" };
          const headerRow = sheet.getRow(startRow + 1);
          headers.forEach((value, index) => { const cell = headerRow.getCell(startColumn + index); cell.value = value; cell.font = { bold: true, color: { argb: navy } }; cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFDCECF4" } }; });
          rows.forEach((values, rowIndex) => values.forEach((value, columnIndex) => { const cell = sheet.getCell(startRow + 2 + rowIndex, startColumn + columnIndex); cell.value = value; cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: rowIndex % 2 ? "FFF4FAFD" : white } }; cell.border = { bottom: { style: "hair", color: { argb: "FFC5DCE7" } } }; }));
        };
        addDashboardTable(8, 1, "DISTRIBUCIÓN POR ESTADO", ["Estado", "Casos", "%"], statusBreakdown.map(row => [row.label, row.total, reportScopedTickets.length ? row.total / reportScopedTickets.length : 0]), blue);
        addDashboardTable(8, 5, "DISTRIBUCIÓN POR PRIORIDAD", ["Prioridad", "Casos", "%"], priorityBreakdown.map(row => [row.label, row.total, reportScopedTickets.length ? row.total / reportScopedTickets.length : 0]), red);
        addDashboardTable(8, 9, "TOP RESPONSABLES", ["Responsable", "Resueltos", "Evidencia %", "Promedio (min)"], personnelPerformance.slice(0, 8).map(row => [row.name, row.resolved, row.evidencePercent / 100, row.averageMinutes]), violet);
        sheet.getColumn(1).width = 21; sheet.getColumn(5).width = 21; sheet.getColumn(9).width = 29;
        sheet.getColumn(11).numFmt = "0%";
        sheet.getColumn(3).numFmt = "0%"; sheet.getColumn(7).numFmt = "0%";
        const historyStart = 19;
        addDashboardTable(historyStart, 1, "TENDENCIA DE RESOLUCIONES", ["Periodo", "Casos resueltos", "Participación"], reportHistory.map(point => [point.date, point.resolved, resolved.length ? point.resolved / resolved.length : 0]), green);
        sheet.getColumn(3).numFmt = "0%";
        sheet.mergeCells(historyStart, 5, historyStart, 12);
        sheet.getCell(historyStart, 5).value = "LECTURA EJECUTIVA";
        sheet.getCell(historyStart, 5).font = { bold: true, color: { argb: white } };
        sheet.getCell(historyStart, 5).fill = { type: "pattern", pattern: "solid", fgColor: { argb: navy } };
        sheet.mergeCells(historyStart + 1, 5, historyStart + 5, 12);
        sheet.getCell(historyStart + 1, 5).value = `${reportScopeDetail}\n\nCríticos activos: ${reportSummary.critical}. Sin asignar: ${reportSummary.unassigned}. Cobertura de evidencia: ${reportSummary.evidencePercent}%.`;
        sheet.getCell(historyStart + 1, 5).alignment = { vertical: "top", wrapText: true };
        sheet.getCell(historyStart + 1, 5).fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF1F8FB" } };
        sheet.pageSetup = { orientation: "landscape", fitToPage: true, fitToWidth: 1, fitToHeight: 1, paperSize: 9, margins: { left: 0.25, right: 0.25, top: 0.4, bottom: 0.4, header: 0.2, footer: 0.2 } };
        sheet.headerFooter.oddFooter = "&LReal Agencias · Dashboard confidencial&C&P de &N&R&D";
      }
      if (section === "all" || section === "departments") addSheet("Ranking departamentos", "RANKING POR DEPARTAMENTOS", ["Posición", "Departamento", "Total", "Resueltos", "Activos", "Efectividad", "Promedio (min)", "Evidencia %"], reportRankings.departments.map((row, index) => [index + 1, row.label, row.total, row.resolved, row.active, row.effectiveness / 100, row.averageMinutes, row.evidencePercent / 100]), blue);
      if (section === "all" || section === "areas") addSheet("Ranking áreas", "RANKING POR ÁREAS", ["Posición", "Área / equipo", "Total", "Resueltos", "Activos", "Efectividad", "Promedio (min)", "Evidencia %"], reportRankings.areas.map((row, index) => [index + 1, row.label, row.total, row.resolved, row.active, row.effectiveness / 100, row.averageMinutes, row.evidencePercent / 100]), violet);
      if (section === "all" || section === "personnel") addSheet("Ranking personal", owner ? `RESULTADOS DE ${owner.toUpperCase()}` : "RANKING DE TÉCNICOS Y SUPERVISORES", ["Posición", "Responsable", "Tipo", "Departamento", "Activos", "Resueltos", "Críticos", "Promedio (min)", "Evidencia %"], personnelPerformance.filter(person => !owner || person.name === owner).map((person, index) => [index + 1, person.name, person.role, person.department, person.active, person.resolved, person.critical, person.averageMinutes, person.evidencePercent / 100]), cyan);
      if (section === "all" || section === "resolved" || (section === "personnel" && !!owner)) addSheet("Casos resueltos", "CASOS RESUELTOS", ["Ticket", "Agencia", "Grupo", "Departamento", "Categoría", "Prioridad", "Responsable", "Fecha cierre", "Duración (min)", "Evidencias", "Solución"], resolved.map(ticket => [ticket.ticketNumber, `${ticket.codigo} · ${ticket.terminal}`, ticket.grupo, departments[ticket.assignedDepartment] || ticket.assignedDepartment, ticketCategoryChoices.find(([code]) => code === ticket.category)?.[1] || ticket.category, priorities[ticket.priority] || ticket.priority, ticket.resolvedByName || ticket.assignedTechnicianName || "Sin responsable", new Date(ticket.closedAt || ticket.updatedAt), ticketMinutes(ticket), ticket.evidence?.length || 0, ticket.resolution || "Sin detalle"]), green);
      if (section === "all" && departmentalReport.total > 0) addSheet(departmentalReport.departmentName.slice(0, 31), `INFORME AUTOMÁTICO DE ${departmentalReport.departmentName.toUpperCase()}`, ["Sección", "Ticket", "Agencia", "Incidencia", "Técnico / soporte", "Estado", "Prioridad", "Actualizado"], departmentalReport.sections.flatMap(group => group.rows.map(ticket => [group.label, ticket.ticketNumber, `${ticket.codigo} · ${ticket.terminal}`, ticket.subject, ticket.assignedTechnicianName || "Sin asignar", statuses[ticket.status] || ticket.status, priorities[ticket.priority] || ticket.priority, new Date(ticket.updatedAt)])), cyan);
      if (section === "department") departmentalReport.sections.forEach((group, index) => addSheet(group.label.slice(0, 31), group.label.toUpperCase(), ["Ticket", "Agencia", "Grupo", "Incidencia", "Categoría", "Técnico / soporte", "Estado", "Prioridad", "Creado", "Actualizado", "Solución / motivo"], group.rows.map(ticket => [ticket.ticketNumber, `${ticket.codigo} · ${ticket.terminal}`, ticket.grupo, ticket.subject, ticketCategoryChoices.find(([code]) => code === ticket.category)?.[1] || categories[ticket.category] || ticket.category, ticket.assignedTechnicianName || ticket.resolvedByName || "Sin asignar", statuses[ticket.status] || ticket.status, priorities[ticket.priority] || ticket.priority, new Date(ticket.createdAt), new Date(ticket.updatedAt), ticket.resolution || "—"]), [green, blue, "FF9B7D08", violet, "FF367F94", "FFA8612C", "FF96505A", "FFA8263D"][index] || blue));
      if (section === "all") addSheet("Todos los casos", "INVENTARIO COMPLETO DE CASOS", ["Ticket", "Agencia", "Grupo", "Departamento", "Estado", "Prioridad", "Responsable", "Creado", "Actualizado", "Duración (min)", "Evidencias", "Categoría", "Solución"], reportScopedTickets.map(ticket => [ticket.ticketNumber, `${ticket.codigo} · ${ticket.terminal}`, ticket.grupo, departments[ticket.assignedDepartment] || ticket.assignedDepartment, statuses[ticket.status] || ticket.status, priorities[ticket.priority] || ticket.priority, ticket.resolvedByName || ticket.assignedTechnicianName || "Sin asignar", new Date(ticket.createdAt), new Date(ticket.updatedAt), resolvedStatuses.has(ticket.status) ? ticketMinutes(ticket) : 0, ticket.evidence?.length || 0, ticketCategoryChoices.find(([code]) => code === ticket.category)?.[1] || categories[ticket.category] || ticket.category, ticket.resolution || ""]), blue);
      if (section === "all" || section === "findings") addSheet("Hallazgos", "HALLAZGOS AUTOMÁTICOS", ["Agencia", "Hallazgo", "Departamento", "Prioridad", "Estado", "Responsable", "Detectado", "Resuelto", "Evidencias"], reportFindings.map(row => [`${row.codigo} · ${row.terminal}`, row.title, departments[row.department] || row.department, priorities[row.priority] || row.priority, statuses[row.status] || row.status, row.resolvedByName || row.assignedTechnicianName || "Sin asignar", new Date(row.submittedAt), row.resolvedAt ? new Date(row.resolvedAt) : "—", row.evidence.length]), violet);
      if (section === "all") addSheet("Metodología", "ALCANCE Y DEFINICIONES", ["Elemento", "Definición"], [
        ["Alcance aplicado", reportScopeLabel],
        ["Periodo", reportPeriodLabel],
        ["Casos resueltos", "Tickets con estado Resuelto o Cerrado dentro del periodo seleccionado."],
        ["Carga activa", "Tickets abiertos o en proceso que todavía no han sido finalizados."],
        ["Tiempo promedio", "Promedio de minutos entre creación y resolución/cierre de los casos resueltos."],
        ["Cobertura de evidencia", "Porcentaje de casos resueltos que incluyen al menos un archivo de evidencia."],
        ["Permisos", reportScopeDetail],
      ], navy);
      const buffer = await workbook.xlsx.writeBuffer();
      downloadBlob(new Blob([buffer as BlobPart], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }), `${reportFileName(section, owner)}.xlsx`);
      setNotice("Libro Excel generado con dashboard, métricas, hojas filtrables y metodología.");
    } catch (exportError) { setError(`No se pudo generar el Excel: ${(exportError as Error).message}`); }
  };
  const exportReportPdf = async (section: ReportSection = "all", owner?: string) => {
    try {
      const { jsPDF } = await import("jspdf");
      const pdf = new jsPDF({ unit: "mm", format: "a4", orientation: "landscape" });
      pdf.setProperties({ title: "Reporte ejecutivo de soporte", subject: reportScopeLabel, author: "Real Agencias · Grupo Tejeda", creator: "Centro de Reportes y Estadísticas" });
      const pageWidth = 297, margin = 12;
      const title = owner ? `Resultados individuales · ${owner}` : section === "department" ? `Informe de ${departmentalReport.departmentName}` : section === "departments" ? "Ranking por departamentos" : section === "areas" ? "Ranking por áreas" : section === "personnel" ? "Ranking de técnicos y supervisores" : section === "resolved" ? "Casos resueltos" : section === "findings" ? "Hallazgos automáticos" : "Reporte ejecutivo de soporte";
      const footer = () => { pdf.setDrawColor(196, 219, 231); pdf.line(margin, 200, pageWidth - margin, 200); pdf.setFontSize(7); pdf.setTextColor(83, 108, 124); pdf.text("REAL · Grupo Tejeda · Información operativa confidencial", margin, 205); pdf.text(`Página ${pdf.getNumberOfPages()}`, pageWidth - margin, 205, { align: "right" }); };
      const banner = (subtitle = title) => { pdf.setFillColor(5, 34, 61); pdf.rect(0, 0, pageWidth, 30, "F"); pdf.setTextColor(75, 213, 255); pdf.setFont("helvetica", "bold"); pdf.setFontSize(8); pdf.text("REAL · CENTRO DE REPORTES Y ESTADÍSTICAS", margin, 9); pdf.setTextColor(255, 255, 255); pdf.setFontSize(19); pdf.text(subtitle, margin, 20, { maxWidth: 170 }); pdf.setFontSize(6.5); pdf.setTextColor(174, 217, 237); pdf.text(reportScopeLabel, pageWidth - margin, 11, { align: "right", maxWidth: 92 }); pdf.text(`${new Date().toLocaleString("es-DO")} · ${reportPeriodLabel}`, pageWidth - margin, 21, { align: "right" }); };
      const table = (headers: string[], rows: (string | number)[][], widths: number[]) => { let y = 39; const drawHeader = () => { pdf.setFillColor(15, 108, 156); pdf.rect(margin, y - 5, widths.reduce((a,b)=>a+b,0), 8, "F"); pdf.setTextColor(255,255,255); pdf.setFontSize(6); pdf.setFont("helvetica","bold"); let x=margin; headers.forEach((value,i)=>{pdf.text(value, x+1.5,y,{maxWidth:widths[i]-3});x+=widths[i];}); y+=6; }; drawHeader(); rows.forEach((row,index)=>{ const lineHeight=Math.max(7,...row.map((value,i)=>pdf.splitTextToSize(String(value??""),widths[i]-3).length*3.2+2)); if(y+lineHeight>196){footer();pdf.addPage();banner();y=39;drawHeader();} if(index%2===0){pdf.setFillColor(238,247,251);pdf.rect(margin,y-4,widths.reduce((a,b)=>a+b,0),lineHeight,"F");} pdf.setTextColor(28,52,66);pdf.setFont("helvetica","normal");pdf.setFontSize(6);let x=margin;row.forEach((value,i)=>{pdf.text(pdf.splitTextToSize(String(value??""),widths[i]-3),x+1.5,y,{maxWidth:widths[i]-3});x+=widths[i];});y+=lineHeight;}); };
      banner();
      const resolved = ticketsForOwner(owner);
      if (section === "department") { departmentalReport.sections.forEach((group,index)=>{if(index){footer();pdf.addPage();banner(group.label);} table(["Ticket","Agencia","Incidencia","Técnico / soporte","Estado","Actualizado","Solución / motivo"],group.rows.map(row=>[row.ticketNumber,`${row.codigo} · ${row.terminal}`,row.subject,row.assignedTechnicianName||row.resolvedByName||"Sin asignar",statuses[row.status]||row.status,new Date(row.updatedAt).toLocaleDateString("es-DO"),row.resolution||"—"]),[18,45,58,45,26,27,58]); }); }
      else if (section === "departments" || section === "areas") { const rows = section === "departments" ? reportRankings.departments : reportRankings.areas; table(["#", section === "departments" ? "Departamento" : "Área / equipo", "Total", "Resueltos", "Activos", "Efectividad", "Promedio", "Evidencia"], rows.map((row,index)=>[index+1,row.label,row.total,row.resolved,row.active,`${row.effectiveness}%`,compactDuration(row.averageMinutes),`${row.evidencePercent}%`]),[12,93,24,27,24,30,37,30]); }
      else if (section === "personnel" && !owner) table(["#","Responsable","Tipo","Departamento","Activos","Resueltos","Críticos","Promedio","Evidencia"],personnelPerformance.map((row,index)=>[index+1,row.name,row.role,row.department,row.active,row.resolved,row.critical,compactDuration(row.averageMinutes),`${row.evidencePercent}%`]),[10,55,28,45,22,25,22,40,30]);
      else if (section === "findings") table(["Agencia","Hallazgo","Departamento","Estado","Responsable","Detectado","Evidencias"],reportFindings.map(row=>[`${row.codigo} · ${row.terminal}`,row.title,departments[row.department]||row.department,statuses[row.status]||row.status,row.resolvedByName||row.assignedTechnicianName||"Sin asignar",new Date(row.submittedAt).toLocaleDateString("es-DO"),row.evidence.length]),[48,62,40,27,50,30,20]);
      else if (section === "resolved" || owner) table(["Ticket","Agencia","Grupo","Departamento","Responsable","Cierre","Duración","Solución"],resolved.map(row=>[row.ticketNumber,`${row.codigo} · ${row.terminal}`,row.grupo,departments[row.assignedDepartment]||row.assignedDepartment,row.resolvedByName||row.assignedTechnicianName||"Sin responsable",new Date(row.closedAt||row.updatedAt).toLocaleDateString("es-DO"),compactDuration(ticketMinutes(row)),row.resolution||"Sin detalle"]),[18,46,34,39,45,27,28,40]);
      else {
        pdf.setFillColor(240,248,252); pdf.roundedRect(margin,38,273,38,4,4,"F");
        const cards=[["Casos",reportScopedTickets.length],["Resueltos",resolved.length],["En proceso",reportSummary.inProgress],["Resolución",`${reportSummary.resolutionRate}%`],["Evidencia",`${reportSummary.evidencePercent}%`]];
        cards.forEach(([label,value],i)=>{const x=18+i*54;pdf.setTextColor(15,108,156);pdf.setFontSize(7);pdf.setFont("helvetica","bold");pdf.text(String(label).toUpperCase(),x,50);pdf.setTextColor(5,34,61);pdf.setFontSize(19);pdf.text(String(value),x,65);});
        pdf.setFont("helvetica","normal"); pdf.setFontSize(7); pdf.setTextColor(70,101,118); pdf.text(reportScopeDetail, margin, 83, { maxWidth: 270 });
        let y=96; pdf.setFontSize(12);pdf.setFont("helvetica","bold");pdf.setTextColor(5,34,61);pdf.text("Ranking de departamentos",margin,y);y+=8;const max=Math.max(1,...reportRankings.departments.map(row=>row.resolved));reportRankings.departments.slice(0,8).forEach((row,index)=>{pdf.setFontSize(7);pdf.setFont("helvetica","normal");pdf.setTextColor(31,63,81);pdf.text(`${index+1}. ${row.label}`,margin,y,{maxWidth:65});pdf.setFillColor(222,237,244);pdf.roundedRect(82,y-3,150,4,2,2,"F");pdf.setFillColor(21,155,120);pdf.roundedRect(82,y-3,Math.max(2,(row.resolved/max)*150),4,2,2,"F");pdf.setFont("helvetica","bold");pdf.text(`${row.resolved} resueltos · ${row.effectiveness}%`,240,y);y+=11;});
        footer(); pdf.addPage(); banner("Distribución operativa completa");
        table(["Dimensión","Clasificación","Casos","Participación"],[...statusBreakdown.map(row=>["Estado",row.label,row.total,`${reportScopedTickets.length?Math.round(row.total/reportScopedTickets.length*100):0}%`]),...priorityBreakdown.map(row=>["Prioridad",row.label,row.total,`${reportScopedTickets.length?Math.round(row.total/reportScopedTickets.length*100):0}%`])],[42,105,35,55]);
        footer(); pdf.addPage(); banner(strictTechnologyTechnician?"Rendimiento personal":"Rendimiento por responsable");
        table(["#","Responsable","Tipo","Departamento","Activos","Resueltos","Críticos","Promedio","Evidencia"],personnelPerformance.map((row,index)=>[index+1,row.name,row.role,row.department,row.active,row.resolved,row.critical,compactDuration(row.averageMinutes),`${row.evidencePercent}%`]),[10,55,28,45,22,25,22,40,30]);
        if (resolved.length) { footer(); pdf.addPage(); banner("Trazabilidad de casos resueltos"); table(["Ticket","Agencia","Grupo","Departamento","Responsable","Cierre","Duración","Solución"],resolved.slice(0,150).map(row=>[row.ticketNumber,`${row.codigo} · ${row.terminal}`,row.grupo,departments[row.assignedDepartment]||row.assignedDepartment,row.resolvedByName||row.assignedTechnicianName||"Sin responsable",new Date(row.closedAt||row.updatedAt).toLocaleDateString("es-DO"),compactDuration(ticketMinutes(row)),row.resolution||"Sin detalle"]),[18,46,34,39,45,27,28,40]); }
      }
      footer(); pdf.save(`${reportFileName(section, owner)}.pdf`); setNotice("Reporte PDF profesional generado correctamente.");
    } catch (exportError) { setError(`No se pudo generar el PDF: ${(exportError as Error).message}`); }
  };
  const currentUserId = String(session.id);
  const canRouteTicket = session.permissions.canAssignTickets;
  const isCallCenter =
    session.role === "Technology" && session.supportTeam === "CALL_CENTER";
  const isTechnologyDispatcher =
    session.role === "Administrator" ||
    (session.role === "Technology" &&
      (session.supportTeam === "CALL_CENTER" ||
        session.supportTeam === "TECHNICAL_FAILURE"));
  // Los perfiles antiguos de técnicos pueden no tener supportTeam todavía.
  // Todo usuario Technology que no sea Call Center/Avería se trata como técnico.
  const isTechnologyTechnician = strictTechnologyTechnician;
  const isSelectedPersonnelView =
    teamViewChosen && technicianFilter !== "ALL";
  const canDispatchTicket =
    !isSelectedPersonnelView &&
    canRouteTicket &&
    !isCallCenter &&
    (session.supportDepartment !== "TECHNOLOGY" || isTechnologyDispatcher);
  // Call Center puede pedir apoyo de otros departamentos, pero no puede
  // cambiar el departamento propietario ni asignar técnicos.
  const canShareTicket = !isSelectedPersonnelView && (canDispatchTicket || isCallCenter);
  const isDepartmentSupportAdministrator =
    !!session.supportDepartment &&
    !session.supportTeam &&
    session.permissions.canAssignTickets &&
    session.role !== "GroupAdministrator";
  const canEscalateCallCenterTicket = (ticket: Ticket) =>
    isCallCenter &&
    ticket.ticketType === "INTERNAL" &&
    ticket.assignedDepartment === "TECHNOLOGY" &&
    effectiveTechnologyTeam(ticket) === "CALL_CENTER" &&
    !resolvedStatuses.has(ticket.status);
  const canViewTicketEvidence =
    !isSelectedPersonnelView &&
    (session.role === "Administrator" ||
      session.supportTeam === "CALL_CENTER" ||
      session.supportTeam === "TECHNICAL_FAILURE" ||
      isDepartmentSupportAdministrator);
  const requiresTechnicianGpsEvidence = (ticket: Ticket) =>
    (isTechnologyTechnician &&
      ticket.assignedTechnicianId === currentUserId) ||
    (session.role === "GroupAdministrator" &&
      ticket.assignedTechnicianId === currentUserId) ||
    (isSelectedPersonnelView &&
      personnelKind === "TECHNICIANS" &&
      String(ticket.assignedTechnicianId || "").toLowerCase() ===
        String(technicianFilter).toLowerCase());
  const isAssignedInternalSupervisor = (ticket: Ticket) =>
    ticket.assignedTechnicianId === currentUserId &&
    session.role === "GroupAdministrator";
  const canManageTicket = (ticket: Ticket) =>
    !resolvedStatuses.has(ticket.status) &&
    ((isTechnologyTechnician
      ? ticket.assignedTechnicianId === currentUserId
      : session.permissions.canResolveTickets) ||
      isAssignedInternalSupervisor(ticket));
  const canAdministerTicket = (ticket: Ticket) =>
    !resolvedStatuses.has(ticket.status) &&
    !isCallCenter &&
      (session.role === "Administrator" ||
      (session.permissions.canAssignTickets &&
        !!session.supportDepartment &&
        ticket.assignedDepartment === session.supportDepartment &&
        (session.supportDepartment !== "TECHNOLOGY" || isTechnologyDispatcher || isTechnologyDepartmentManager)));
  const techniciansForTicket = (
    ticket: Ticket,
    assignedTeam: TechnologyTeam | "" = ticket.assignedTeam || "",
  ) =>
    technicians.filter((technician) => {
      const team = assignedTeam || ticket.assignedTeam || "";
      const isTechnologySupport = ticket.assignedDepartment === "TECHNOLOGY" &&
        (technician.supportTeam === "CALL_CENTER" || technician.supportTeam === "TECHNICAL_FAILURE" || technician.supportTeam === "TECHNICIANS");
      const technicianMatchesAssignedTeam = technician.supportTeam===assignedTeam || technician.supportTeam===team;
      const areaSupport=isTechnologySupport&&!!team&&technicianMatchesAssignedTeam;
      if (technician.department === ticket.assignedDepartment && (technician.isTechnician||areaSupport||(ticket.assignedDepartment === "TECHNOLOGY" && !team && isTechnologySupport))) {
        if (ticket.assignedDepartment !== "TECHNOLOGY") return true;
        // Los técnicos de campo también son responsables válidos cuando el
        // ticket parte de Call Center o Avería Técnica. Al seleccionarlos, el
        // equipo se actualiza a TECHNICIANS para conservar el enrutamiento.
        if(assignedTeam==="CALL_CENTER")return technician.supportTeam==="CALL_CENTER"||technician.isTechnician;
        if(assignedTeam==="TECHNICAL_FAILURE")return technician.supportTeam==="TECHNICAL_FAILURE"||technician.isTechnician;
        if(assignedTeam==="TECHNICIANS")return technician.supportTeam==="TECHNICIANS"||(!technician.supportTeam&&technician.isTechnician);
        return isTechnologySupport || technician.isTechnician;
      }
      const supervisor =
        technician.isAgencySupervisor ||
        technician.role === "GroupAdministrator";
      return supervisor;
    });
  const technologyTechnicians=technicians.filter(item=>item.department==="TECHNOLOGY"&&item.isTechnician);
  const agencySupervisors=technicians.filter(item=>item.isAgencySupervisor||item.role==="GroupAdministrator");
  const isSupervisorAssignee=(item:Technician)=>!!(item.isAgencySupervisor||item.role==="GroupAdministrator");
  const draftAssignees=technicians.filter(item=>{
    if(isSupervisorAssignee(item))return true;
    if(item.department!==draft.assignedDepartment)return false;
    if(draft.assignedDepartment!=="TECHNOLOGY")return true;
    if(draft.assignedTeam==="CALL_CENTER")return item.supportTeam==="CALL_CENTER"||item.isTechnician;
    if(draft.assignedTeam==="TECHNICAL_FAILURE")return item.supportTeam==="TECHNICAL_FAILURE"||item.isTechnician;
    if(draft.assignedTeam==="TECHNICIANS")return item.isTechnician&&(!item.supportTeam||item.supportTeam==="TECHNICIANS");
    return item.isTechnician||item.supportTeam==="CALL_CENTER"||item.supportTeam==="TECHNICAL_FAILURE"||item.supportTeam==="TECHNICIANS";
  });
  const assigneeTeam=(id:string,fallback:TechnologyTeam|"")=>{const person=technicians.find(item=>item.id===id);if(person?.supportTeam==="CALL_CENTER"||person?.supportTeam==="TECHNICAL_FAILURE"||person?.supportTeam==="TECHNICIANS")return person.supportTeam;if(person?.isTechnician)return "TECHNICIANS";return fallback||"TECHNICIANS";};
  const assigneeSuffix=(item:Technician)=>item.supportTeam==="CALL_CENTER"?"Soporte Call Center":item.supportTeam==="TECHNICAL_FAILURE"?"Soporte Avería Técnica":"Técnico";
  const responsibleNeedle = responsibleQuery.trim().normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("es");
  const matchesResponsibleQuery = (item: Technician) => !responsibleNeedle || `${item.name} ${item.id} ${item.supportTeam || ""}`.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("es").includes(responsibleNeedle);
  const visibleDraftAssignees = draftAssignees.filter(matchesResponsibleQuery);
  const visiblePersonnel=(personnelKind==="SUPERVISORS"?agencySupervisors:personnelKind==="TECHNICIANS"?technologyTechnicians:[]).filter(item=>{
    const search=personnelQuery.trim().toLocaleLowerCase("es");
    return !search||`${item.name} ${(item.groups||[]).join(" ")}`.toLocaleLowerCase("es").includes(search);
  });
  const duplicateAgencyId = reportAgency?.id || draft.agencyId;
  const duplicateAgency = reportAgency || agencies.find((item) => item.id === duplicateAgencyId);
  const selectedCaseKeys = new Set(
    selectedCases.map((item) => `${item.department}:${item.category}`),
  );
  const possibleDuplicateTickets = duplicateAgencyId
    ? tickets.filter(
        (ticket) =>
          ticket.codigo === duplicateAgency?.codigo &&
          ticket.ticketType === draft.ticketType &&
          !resolvedStatuses.has(ticket.status) &&
          ticket.status !== "CANCELLED" &&
          selectedCaseKeys.has(`${ticket.assignedDepartment}:${ticket.category}`),
      )
    : [];
  const duplicateSignature = `${duplicateAgencyId}|${draft.ticketType}|${[...selectedCaseKeys].sort().join(",")}|${possibleDuplicateTickets.map((ticket) => ticket.id).sort().join(",")}`;
  const restoreTicketCreationAfterDuplicate = () => {
    directTicketFocusRef.current = false;
    setDirectTicketFocus(null);
    setExpandedTicketId(null);
    setExpandedHistoryTicketId(null);
    setTicketActionMode(null);
    setWorkspaceDepartment(draft.assignedDepartment || null);
    setTicketTypeFilter(draft.ticketType);
    setSupportTab("tickets");
    setSupportStage("home");
    setTicketWorkspaceView("menu");
    setPersonalAssignmentView(false);
    setSupervisorHistoryView(false);
    setAssignedToMeOnly(false);
    setAssignmentScope("ALL");
    setQuery("");
    setReturnToTicketCreation(false);
    setResponsibleQuery("");
    setFormOpen(true);
  };
  const openPossibleDuplicate = (ticket: Ticket) => {
    setReturnToTicketCreation(true);
    directTicketFocusRef.current = true;
    setDirectTicketFocus({ id: ticket.id, ticketNumber: ticket.ticketNumber });
    setFormOpen(false);
    setSupportTab("tickets");
    setSupportStage("tickets");
    setWorkspaceDepartment(
      session.role === "Administrator" || session.role === "GroupAdministrator"
        ? ticket.assignedDepartment
        : session.supportDepartment,
    );
    setTicketTypeFilter(ticket.ticketType);
    setTicketWorkspaceView(
      resolvedStatuses.has(ticket.status)
        ? "history"
        : ticket.status === "IN_PROGRESS" ||
            ticket.status === "PENDING" ||
            !!ticket.assignedTechnicianId
          ? "process"
          : "active",
    );
    setStatusFilter("ALL");
    setPriorityFilter("ALL");
    setCategoryFilter("ALL");
    setTechnicianFilter("ALL");
    setTechnologyTeamFilter("ALL");
    setAssignedToMeOnly(false);
    setPersonalAssignmentView(false);
    setSupervisorHistoryView(false);
    setAssignmentScope("ALL");
    setTeamViewChosen(true);
    setQuery("");
    setExpandedTicketId(null);
    setTicketActionMode(null);
  };
  const createTicket = async (event: React.FormEvent) => {
    event.preventDefault();
    if (
      session.supportTeam === "CALL_CENTER" &&
      draft.ticketType !== "INTERNAL"
    ) {
      setError("Call Center solo puede crear tickets internos.");
      return;
    }
    if (session.role === "GroupAdministrator") {
      if (supervisorEscalationReason.trim().length < 15) {
        setError("Explica qué revisaste y por qué el caso no puede resolverse en la agencia (mínimo 15 caracteres).");
        return;
      }
      if (supervisorCreationEvidence.length < 1) {
        setError("Adjunta al menos una fotografía que demuestre por qué necesitas escalar el caso.");
        return;
      }
    }
    if (
      possibleDuplicateTickets.length > 0 &&
      duplicateCreationConfirmed !== duplicateSignature
    ) {
      setError("Ya existe un ticket activo de esta avería para la agencia. Revisa el ticket existente o confirma que deseas crear uno nuevo.");
      return;
    }
    setSaving(true);
    setError("");
    setNotice("");
    try {
      const cases = selectedCases.length
        ? selectedCases
        : [{ department: draft.assignedDepartment, category: draft.category }];
      const created = await Promise.all(
        cases.map(async (supportCase) => {
          const category = catalog
            .find((item) => item.code === supportCase.department)
            ?.categories.find((item) => item.code === supportCase.category);
          const caseKey = `${supportCase.department}:${supportCase.category}`;
          const fields = caseDetails[caseKey] || { option: "", detail: "" };
          const caseDescription = [
            draft.description,
            fields.option ? `Tipo: ${fields.option}` : "",
            fields.detail
              ? `${category?.detailLabel || "Detalle"}: ${fields.detail}`
              : "",
          ]
            .filter(Boolean)
            .join("\n\n");
          const payload = {
            ...draft,
            assignedDepartment: supportCase.department,
            // Call Center atiende sus propios tickets internos. No se muestra
            // un selector de responsables: el backend los asigna al usuario
            // actualmente autenticado y luego permite escalarlos a Avería.
            assignedTechnicianId: isCallCenter ? currentUserId : draft.assignedTechnicianId,
            assignedTeam:
              supportCase.department === "TECHNOLOGY"
                ? isCallCenter
                  ? "CALL_CENTER"
                  : (draft.assignedTeam || sessionRoutingTeam !== "ALL")
                    ? draft.assignedTeam || sessionRoutingTeam
                    : technologyTeamFilter !== "ALL"
                      ? technologyTeamFilter
                      : "TECHNICAL_FAILURE"
                : null,
            category: supportCase.category,
            priority: ticketPriorityForCategory(
              supportCase.department,
              supportCase.category,
              category?.defaultPriority || draft.priority,
            ),
            subject: category?.name || draft.subject,
            description: caseDescription,
            allowDuplicate: duplicateCreationConfirmed === duplicateSignature,
          };
          if (session.role === "GroupAdministrator") {
            const form = new FormData();
            Object.entries(payload).forEach(([key, value]) => {
              if (value !== null && value !== undefined) form.append(key, String(value));
            });
            form.append("supervisorEscalationReason", supervisorEscalationReason.trim());
            supervisorCreationEvidence.forEach((file) => form.append("evidence", file, file.name));
            return request<Ticket>("/api/tickets", { method: "POST", body: form });
          }
          return request<Ticket>("/api/tickets", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload),
          });
        }),
      );
      setTickets((current) => [
        ...created.map((ticket) => normalizeSupportTicket(ticket)),
        ...current,
      ]);
      setFormOpen(false);
      setDraft({
        agencyId: reportAgency?.id || "",
        category: "",
        assignedDepartment: "",
        priority: "MEDIUM",
        subject: "",
        description: "",
        ticketType: ticketTypeFilter,
        assignedTechnicianId: "",
        assignedTeam: "",
        detail: "",
        option: "",
      });
      setSelectedCases([]);
      setCaseDetails({});
      setSupervisorEscalationReason("");
      setSupervisorCreationEvidence([]);
      setDuplicateCreationConfirmed("");
      setNotice(
        created.length === 1
          ? `Ticket #${created[0].ticketNumber} creado correctamente.`
          : `${created.length} casos creados para la agencia seleccionada.`,
      );
    } catch (saveError) {
      if((saveError as Error & {status?:number}).status===409){
        setDuplicateCreationConfirmed("");
        await load(true);
      }
      setError((saveError as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const updateTicket = async (ticket: Ticket) => {
    const values = management[ticket.id];
    if (!values) return;
    if (
      ["RESOLVED", "CLOSED"].includes(values.status) &&
      !values.resolution.trim()
    ) {
      setError(
        "Escribe la solución aplicada antes de resolver o cerrar el ticket.",
      );
      return;
    }
    setSaving(true);
    setError("");
    try {
      await request(`/api/tickets/${ticket.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });
      setNotice(`Ticket #${ticket.ticketNumber} actualizado.`);
      if (ticketActionMode === "resolve") {
        setExpandedTicketId(null);
        setExpandedHistoryTicketId(null);
        setTicketActionMode(null);
      }
      await load();
    } catch (saveError) {
      if ((saveError as Error & { status?: number }).status === 401)
        onSessionExpired();
      setError((saveError as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const cancelTicket = async (ticket: Ticket) => {
    const values = management[ticket.id];
    const reason = values?.resolution.trim() || "";
    if (reason.length < 5) {
      setError("Escribe el motivo de la anulación (mínimo 5 caracteres).");
      return;
    }
    if (!window.confirm(`¿Anular el ticket #${ticket.ticketNumber}? El motivo quedará registrado en el historial.`)) return;
    setSaving(true);
    setError("");
    try {
      await request(`/api/tickets/${ticket.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          status: "CANCELLED",
          priority: values?.priority || ticket.priority,
          resolution: reason,
          assignedTechnicianId: ticket.assignedTechnicianId || null,
        }),
      });
      setNotice(`Ticket #${ticket.ticketNumber} anulado correctamente.`);
      setExpandedTicketId(null);
      setTicketActionMode(null);
      await load(true);
    } catch (saveError) {
      if ((saveError as Error & { status?: number }).status === 401) onSessionExpired();
      setError((saveError as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const routeTicket = async (ticket: Ticket, escalate = false) => {
    const values = management[ticket.id];
    if (!values) return;
    const assignedDepartment = escalate
      ? "TECHNOLOGY"
      : values.assignedDepartment;
    const assignedTeam = escalate
      ? "TECHNICAL_FAILURE"
      : values.assignedTeam || null;
    if (!assignedDepartment) {
      setError("Selecciona el departamento que recibirá el ticket.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      await request(`/api/tickets/${ticket.id}/route`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          assignedDepartment,
          assignedTeam,
          assignedTechnicianId: escalate
            ? null
            : values.assignedTechnicianId || null,
          comment:
            values.routingComment.trim() ||
            (escalate
              ? "Escalado por Call Center para atención técnica."
              : "Ticket transferido desde la bandeja de soporte."),
        }),
      });
      setNotice(
        escalate
          ? `Ticket #${ticket.ticketNumber} escalado a Avería Técnica.`
          : `Ticket #${ticket.ticketNumber} transferido correctamente.`,
      );
      await load(true);
    } catch (saveError) {
      if ((saveError as Error & { status?: number }).status === 401)
        onSessionExpired();
      setError((saveError as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const shareTicket = async (ticket: Ticket) => {
    const values = management[ticket.id];
    if (!values?.sharedWithDepartments.length) {
      setError("Selecciona al menos un departamento para compartir el ticket.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      await request(`/api/tickets/${ticket.id}/share`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          departments: values.sharedWithDepartments,
          comment: values.routingComment.trim() || null,
        }),
      });
      setNotice(`Ticket #${ticket.ticketNumber} compartido entre departamentos.`);
      await load(true);
    } catch (saveError) {
      setError((saveError as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const handleTicketEvidenceFiles = async (ticket: Ticket, edit: TicketManagementDraft, selectedFiles: File[]) => {
    if (!selectedFiles.length) return;
    const files = [...edit.evidence, ...selectedFiles];
    if (files.length > 6) {
      setError("Puedes adjuntar un máximo de 6 evidencias.");
      return;
    }
    if (files.some((file) => !["image/jpeg", "image/jpg", "image/png", "image/webp"].includes(file.type))) {
      setError("Las evidencias deben ser imágenes JPG, PNG o WebP.");
      return;
    }
    if (files.some((file) => file.size > 8 * 1024 * 1024)) {
      setError("Cada evidencia debe pesar 8 MB o menos.");
      return;
    }
    if (requiresTechnicianGpsEvidence(ticket)) {
      try {
        const location = await captureTicketGps();
        if (session.role === "GroupAdministrator") {
          if (ticket.latitude == null || ticket.longitude == null) throw new Error("Esta agencia no tiene una ubicación verificada. Completa primero la Auditoría de campo de la agencia.");
          const distanceMeters = ticketHaversine(location.latitude, location.longitude, ticket.latitude, ticket.longitude);
          if (distanceMeters > TICKET_AGENCY_RADIUS_METERS) throw new Error(`No estás dentro de la agencia ${ticket.terminal}. El GPS indica una distancia de ${Math.round(distanceMeters)} m; debes estar a ${TICKET_AGENCY_RADIUS_METERS} m o menos.`);
          location.distanceMeters = distanceMeters;
        }
        setTicketEvidenceLocations((current) => ({ ...current, [ticket.id]: location }));
      } catch (captureError) {
        setError((captureError as Error).message);
        return;
      }
    }
    setManagement((current) => ({
      ...current,
      [ticket.id]: { ...edit, evidence: files },
    }));
    setError("");
  };

  const resolveTicketWithEvidence = async (ticket: Ticket) => {
    const values = management[ticket.id];
    if (!values?.resolution.trim()) {
      setError("Describe la solución aplicada antes de cerrar el caso.");
      return;
    }
    if (!values.evidence.length) {
      setError("Adjunta al menos una evidencia para resolver o cerrar el ticket.");
      return;
    }
    if (values.evidence.length > 6) {
      setError("Puedes adjuntar un máximo de 6 evidencias.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      const form = new FormData();
      form.append("status", values.status === "CLOSED" ? "CLOSED" : "RESOLVED");
      form.append("resolution", values.resolution.trim());
      const requiresGps = requiresTechnicianGpsEvidence(ticket);
      if (requiresGps) {
        const location=ticketEvidenceLocations[ticket.id];
        if(!location)throw new Error("Toma las fotografías desde este formulario y permite el GPS antes de resolver.");
        if(Date.now()-Date.parse(location.capturedAt)>10*60_000)throw new Error("La ubicación GPS venció. Vuelve a tomar la evidencia desde la agencia.");
        const distanceMeters=location.distanceMeters;
        if(session.role==="GroupAdministrator"&&distanceMeters==null)throw new Error("La agencia no tiene una ubicación verificada. Completa primero su Auditoría de campo.");
        if(session.role==="GroupAdministrator"&&distanceMeters!=null&&distanceMeters>TICKET_AGENCY_RADIUS_METERS)throw new Error(`Debes estar dentro de la agencia. La lectura actual está a ${Math.round(distanceMeters)} m.`);
        form.append("latitude",String(location.latitude));
        form.append("longitude",String(location.longitude));
        form.append("accuracyMeters",String(location.accuracyMeters));
        form.append("locationCapturedAt",location.capturedAt);
      }
      values.evidence.forEach((file) => form.append("evidence", file));
      await request(`/api/tickets/${ticket.id}/resolve`, {
        method: "POST",
        body: form,
      });
      setNotice(`Ticket #${ticket.ticketNumber} resuelto con evidencia.`);
      setExpandedTicketId(null);
      setExpandedHistoryTicketId(null);
      setTicketActionMode(null);
      await load(true);
    } catch (saveError) {
      if ((saveError as Error & { status?: number }).status === 401)
        onSessionExpired();
      setError((saveError as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const deleteTicket = async (ticket: Ticket) => {
    if (
      !window.confirm(
        `¿Eliminar definitivamente el ticket #${ticket.ticketNumber}? Esta acción quedará registrada en auditoría.`,
      )
    )
      return;
    setSaving(true);
    setError("");
    try {
      await request(`/api/tickets/${ticket.id}`, { method: "DELETE" });
      setNotice(`Ticket #${ticket.ticketNumber} eliminado.`);
      await load(true);
    } catch (saveError) {
      setError((saveError as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const workForFinding = (id: string): FindingWorkDraft =>
    findingWork[id] || {
      diagnosis: "",
      resolution: "",
      verification: "",
      recommendations: "",
      photos: [],
    };
  const updateFindingWork = (id: string, patch: Partial<FindingWorkDraft>) =>
    setFindingWork((current) => ({
      ...current,
      [id]: { ...workForFinding(id), ...patch },
    }));

  const resolveFinding = async (finding: AutomaticFinding) => {
    const work = workForFinding(finding.id);
    if (
      work.diagnosis.trim().length < 10 ||
      work.resolution.trim().length < 10 ||
      work.verification.trim().length < 10
    ) {
      setError(
        "Completa el diagnóstico, la solución aplicada y la verificación final.",
      );
      return;
    }
    if (work.photos.length < 1 || work.photos.length > 6) {
      setError("Adjunta entre 1 y 6 fotografías de evidencia.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      const form = new FormData();
      form.append("diagnosis", work.diagnosis.trim());
      form.append("resolution", work.resolution.trim());
      form.append("verification", work.verification.trim());
      form.append("recommendations", work.recommendations.trim());
      const assignee=technicians.find(item=>item.id===finding.assignedTechnicianId);
      const requiresGps=!!assignee&&!(assignee.isAgencySupervisor||assignee.role==="GroupAdministrator");
      if(requiresGps){const location=ticketEvidenceLocations[finding.id];if(!location){setSaving(false);setError("Las evidencias del técnico deben tomarse desde este formulario con el GPS activado.");return;}form.append("latitude",String(location.latitude));form.append("longitude",String(location.longitude));form.append("accuracyMeters",String(location.accuracyMeters));form.append("locationCapturedAt",location.capturedAt);}
      work.photos.forEach((photo) => form.append("evidence", photo));
      await request(`/api/tickets/findings/${finding.id}/resolve`, {
        method: "PUT",
        body: form,
      });
      setNotice(
        `${finding.title} fue resuelto con ${work.photos.length} evidencias fotográficas.`,
      );
      setFindingWork((current) => {
        const next = { ...current };
        delete next[finding.id];
        return next;
      });
      await load(true);
    } catch (saveError) {
      if ((saveError as Error & { status?: number }).status === 401)
        onSessionExpired();
      setError((saveError as Error).message);
    } finally {
      setSaving(false);
    }
  };
  const assignFinding=async(finding:AutomaticFinding,assignedTechnicianId:string)=>{
    try{
      await request(`/api/tickets/findings/${finding.id}/assign`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify({assignedTechnicianId})});
      const assignee=technicians.find(item=>item.id===assignedTechnicianId);
      setFindings(current=>current.map(item=>item.id===finding.id?{...item,assignedTechnicianId,assignedTechnicianName:assignee?.name||item.assignedTechnicianName}:item));
      setFindingAssigneeKind("ALL");
      setFindingAssigneeFilter("ALL");
      setSelectedFindingId(null);
      setFindingView("pending");
      setNotice(assignedTechnicianId?`Hallazgo asignado a ${assignee?.name||"responsable"}.`:"Se retiró el responsable del hallazgo.");
      await load(true);
      // La recarga no debe restaurar la vista filtrada del responsable.
      setFindingAssigneeKind("ALL");
      setFindingAssigneeFilter("ALL");
      setSelectedFindingId(null);
      setFindingView("pending");
    }catch(saveError){if((saveError as Error & {status?:number}).status===401)onSessionExpired();setError((saveError as Error).message);}
  };

  const exportResolvedFindings = async () => {
    const rows = findings.filter(
      (finding) =>
        finding.status === "RESOLVED" &&
        (findingGroup === "ALL" || finding.grupo === findingGroup) &&
        (reportOwner === "ALL" || finding.resolvedByName === reportOwner || finding.assignedTechnicianName === reportOwner),
    );
    if (!rows.length) {
      setError("No hay soluciones para exportar con el grupo seleccionado.");
      return;
    }
    const { jsPDF } = await import("jspdf");
    const documentPdf = new jsPDF({ unit: "mm", format: "a4" });
    let realLogo: string | null = null;
    try {
      const logoResponse = await fetch("/loto-real-logo-transparent.png");
      const logoBlob = await logoResponse.blob();
      realLogo = await new Promise<string>((resolve) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.readAsDataURL(logoBlob);
      });
    } catch {
      realLogo = null;
    }
    const durationLabel = (minutes: number | null | undefined) => {
      const total = Math.max(0, minutes || 0);
      const days = Math.floor(total / 1440);
      const hours = Math.floor((total % 1440) / 60);
      const remaining = total % 60;
      return [
        days ? `${days} día${days === 1 ? "" : "s"}` : "",
        hours ? `${hours} hora${hours === 1 ? "" : "s"}` : "",
        `${remaining} minuto${remaining === 1 ? "" : "s"}`,
      ]
        .filter(Boolean)
        .join(", ");
    };
    const textBlock = (label: string, value: string, y: number) => {
      documentPdf.setFont("helvetica", "bold");
      documentPdf.setTextColor(15, 72, 108);
      documentPdf.setFontSize(9);
      documentPdf.text(label.toUpperCase(), 16, y);
      documentPdf.setFont("helvetica", "normal");
      documentPdf.setTextColor(42, 55, 68);
      documentPdf.setFontSize(10);
      const lines = documentPdf.splitTextToSize(value || "No registrado", 178);
      documentPdf.text(lines, 16, y + 5);
      return y + 7 + lines.length * 4;
    };
    for (let index = 0; index < rows.length; index += 1) {
      const row = rows[index];
      if (index) documentPdf.addPage();
      documentPdf.setFillColor(5, 34, 61);
      documentPdf.rect(0, 0, 210, 34, "F");
      if (realLogo) documentPdf.addImage(realLogo, "PNG", 157, 5, 33, 19);
      documentPdf.setTextColor(86, 211, 255);
      documentPdf.setFont("helvetica", "bold");
      documentPdf.setFontSize(9);
      documentPdf.text("GRUPO TEJEDA", 16, 10);
      documentPdf.setTextColor(155, 226, 255);
      documentPdf.setFontSize(7);
      documentPdf.text("IDENTIDAD CORPORATIVA · SOPORTE OPERATIVO", 16, 15);
      documentPdf.setTextColor(255, 255, 255);
      documentPdf.setFontSize(18);
      documentPdf.text("Auditoría técnica de solución", 16, 24);
      documentPdf.setFontSize(8);
      documentPdf.text(`EXPEDIENTE ${row.id.toUpperCase()}`, 16, 30);
      let y = 43;
      y = textBlock(
        "Identificación",
        `${row.codigo} · ${row.terminal} · ${row.grupo} · ${departments[row.department]}`,
        y,
      );
      y = textBlock(
        "Hallazgo y prioridad",
        `${row.title} (${priorities[row.priority]}) — ${row.detail}`,
        y,
      );
      y = textBlock("Diagnóstico técnico", row.diagnosis || "", y);
      y = textBlock("Trabajo y solución aplicada", row.resolution || "", y);
      y = textBlock("Verificación final", row.verification || "", y);
      y = textBlock(
        "Recomendaciones",
        row.recommendations || "Sin recomendaciones adicionales",
        y,
      );
      y = textBlock(
        "Trazabilidad",
        `Responsable: ${row.resolvedByName || "No registrado"} · Apertura: ${new Date(row.submittedAt).toLocaleString("es-DO")} · Cierre: ${row.resolvedAt ? new Date(row.resolvedAt).toLocaleString("es-DO") : "No registrado"} · Duración total del caso: ${durationLabel(row.resolutionMinutes)}`,
        y,
      );
      y = textBlock(
        "Evidencia fotográfica",
        `${row.evidence?.length || 0} fotografías vinculadas al expediente`,
        y,
      );
      for (const photo of (row.evidence || []).slice(0, 4)) {
        try {
          const response = await fetch(
            `/api/tickets/findings/evidence/${photo.id}`,
          );
          if (!response.ok) continue;
          const blob = await response.blob();
          const dataUrl = await new Promise<string>((resolve) => {
            const reader = new FileReader();
            reader.onload = () => resolve(String(reader.result));
            reader.readAsDataURL(blob);
          });
          if (y > 245) {
            documentPdf.addPage();
            y = 20;
          }
          documentPdf.addImage(
            dataUrl,
            photo.contentType.includes("png")
              ? "PNG"
              : photo.contentType.includes("webp")
                ? "WEBP"
                : "JPEG",
            16,
            y,
            58,
            38,
            undefined,
            "FAST",
          );
          documentPdf.setFontSize(7);
          documentPdf.setTextColor(90, 108, 122);
          documentPdf.text(photo.fileName, 78, y + 8);
          documentPdf.text(
            new Date(photo.capturedAt).toLocaleString("es-DO"),
            78,
            y + 14,
          );
          y += 44;
        } catch {
          continue;
        }
      }
      documentPdf.setFontSize(7);
      documentPdf.setTextColor(100, 115, 128);
      documentPdf.text(
        `Documento generado el ${new Date().toLocaleString("es-DO")} · Registro auditable`,
        16,
        289,
      );
    }
    documentPdf.save(
      `auditoria-tecnica-${findingGroup === "ALL" ? "todos-los-grupos" : findingGroup.replace(/[^a-z0-9]+/gi, "-")}-${new Date().toISOString().slice(0, 10)}.pdf`,
    );
  };

  const saveDepartment = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    try {
      await request(`/api/admin/support/departments/${departmentEdit.code}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(departmentEdit),
      });
      setDepartmentEdit({
        code: "",
        name: "",
        description: "",
        accentColor: "#38BDF8",
        displayOrder: 100,
        isActive: true,
      });
      await load(true);
      setNotice("Departamento guardado.");
    } catch (saveError) {
      setError((saveError as Error).message);
    } finally {
      setSaving(false);
    }
  };
  const saveCategory = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    try {
      const body = {
        ...categoryEdit,
        code: (categoryEdit.code || "")
          .toUpperCase()
          .replace(/[^A-Z0-9]+/g, "_"),
        options: categoryEdit.options || [],
      };
      await request(
        categoryEdit.id
          ? `/api/admin/support/categories/${categoryEdit.id}`
          : "/api/admin/support/categories",
        {
          method: categoryEdit.id ? "PUT" : "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(body),
        },
      );
      setCategoryEdit({
        defaultPriority: "MEDIUM",
        requiresDetail: true,
        isActive: true,
        displayOrder: 100,
      });
      await load(true);
      setNotice("Tipo de soporte guardado.");
    } catch (saveError) {
      setError((saveError as Error).message);
    } finally {
      setSaving(false);
    }
  };
  const deleteCategory = async (category: SupportCategory) => {
    if (!confirm(`¿Eliminar ${category.name}?`)) return;
    try {
      await request(`/api/admin/support/categories/${category.id}`, {
        method: "DELETE",
      });
      await load(true);
      setNotice("Tipo de soporte eliminado.");
    } catch (saveError) {
      setError((saveError as Error).message);
    }
  };

  const countedTickets=workspaceTickets.filter(ticket=>{
    const assignedToCurrentUser=ticket.isAssignedToCurrentUser===true||String(ticket.assignedTechnicianId||"").trim().toLowerCase()===String(session.id).trim().toLowerCase();
    const selectedTechnologyTeam=lockedTechnologyTeam||technologyTeamFilter;
    const matchesSelectedTeam=ticketBelongsToTechnologyTeam(ticket,selectedTechnologyTeam)||(strictTechnologyTechnician&&assignedToCurrentUser);
    return ticket.ticketType===ticketTypeFilter&&(!lockedTechnologyTeam||ticketBelongsToTechnologyTeam(ticket,lockedTechnologyTeam)||(strictTechnologyTechnician&&assignedToCurrentUser))&&(!teamViewChosen||workspaceDepartment!=="TECHNOLOGY"||matchesSelectedTeam);
  });
  const supportTicketCount=workspaceTickets.filter(ticket=>ticket.ticketType==="SUPPORT").length;
  const internalTicketCount=workspaceTickets.filter(ticket=>ticket.ticketType==="INTERNAL").length;
  const counts = {
    active: countedTickets.filter((ticket) => !resolvedStatuses.has(ticket.status) && !ticket.assignedTechnicianId && ticket.status !== "IN_PROGRESS").length,
    process: countedTickets.filter((ticket) => !resolvedStatuses.has(ticket.status) && (!!ticket.assignedTechnicianId || ticket.status === "IN_PROGRESS")).length,
    critical: countedTickets.filter(
      (ticket) =>
        ticket.priority === "CRITICAL" &&
        !resolvedStatuses.has(ticket.status),
    ).length,
    resolved: countedTickets.filter((ticket) => resolvedStatuses.has(ticket.status)).length,
    total: countedTickets.length,
  };
  const openTicketWorkspaceCard = (view: "active" | "process" | "history" | "reports") => {
    // Las tarjetas son puntos de entrada independientes. Limpiar el estado de
    // una bandeja anterior evita que una búsqueda, un responsable o la vista
    // personal oculte los casos que el contador de la tarjeta acaba de indicar.
    setSupervisorHistoryView(false);
    setPersonalAssignmentView(false);
    setAssignedToMeOnly(false);
    setAssignmentScope(view === "active" ? "UNASSIGNED" : "ALL");
    setTicketWorkspaceView(view);
    setStatusFilter(view === "active" ? "ACTIVE" : "ALL");
    setPriorityFilter("ALL");
    setCategoryFilter("ALL");
    setTechnicianFilter("ALL");
    setQuery("");
    setSupportTab("tickets");
    setSupportStage("tickets");
    setTeamViewChosen(true);
  };
  const requiresDepartmentEntry = session.permissions.canViewSupportDashboard;
  const isDepartmentLanding =
    !isDedicatedMaintenancePortal && !isTechnologyTechnician &&
    ((session.role === "GroupAdministrator" && !workspaceDepartment) ||
      (session.role === "Administrator" && requiresDepartmentEntry && !workspaceDepartment && !configOpen));
  const departmentChoices = session.permissions.canViewAllSupportDepartments
    ? catalog
    : session.supportDepartment
      ? catalog.filter((item) => item.code === session.supportDepartment)
      : catalog;
  const activeDepartment = catalog.find(
    (item) => item.code === workspaceDepartment,
  );
  const showDepartmentCards =
    !isDedicatedMaintenancePortal && !isTechnologyTechnician && ((canCreateTickets && session.role === "GroupAdministrator" && (!workspaceDepartment || !!reportAgency)) ||
    (session.role === "Administrator" && requiresDepartmentEntry && !workspaceDepartment && !configOpen));
  const isCurrentSupervisorTicket=(ticket:Ticket)=>ticket.isAssignedToCurrentUser===true||String(ticket.assignedTechnicianId||"").trim().toLowerCase()===String(session.id).trim().toLowerCase();
  const isCurrentSupervisorActivity=(ticket:Ticket)=>
    isCurrentSupervisorTicket(ticket)||
    String(ticket.createdByUsername||"").toLowerCase()===String(session.email).toLowerCase()||
    String(ticket.resolvedByName||"").toLocaleLowerCase("es")===String(session.displayName).toLocaleLowerCase("es")||
    (ticket.history||[]).some(entry=>String(entry.actorName||"").toLocaleLowerCase("es")===String(session.displayName).toLocaleLowerCase("es"));
  const supervisorAssignedTickets=workspaceTickets.filter(ticket=>ticket.ticketType==="SUPPORT"&&isCurrentSupervisorTicket(ticket));
  const supervisorAssignedActive=supervisorAssignedTickets.filter(ticket=>!resolvedStatuses.has(ticket.status));
  const supervisorAssignedResolved=supervisorAssignedTickets.filter(ticket=>["RESOLVED","CLOSED"].includes(ticket.status));
  const supervisorActivityTickets=workspaceTickets.filter(ticket=>successfulStatuses.has(ticket.status)&&isCurrentSupervisorActivity(ticket));
  const filteredAgencyTransitions=agencyTransitions.filter(project=>project.status===transitionStatusFilter&&(!transitionListQuery.trim()||`${project.codigo} ${project.terminal} ${project.grupo}`.toLowerCase().includes(transitionListQuery.trim().toLowerCase())));
  const selectedTransition=filteredAgencyTransitions.find(project=>project.id===selectedTransitionId)||null;
  const selectedTransitionStage=selectedTransition?.stages.find(stage=>stage.id===selectedTransitionStageId)||selectedTransition?.stages.find(stage=>stage.department===selectedTransition.currentStage)||selectedTransition?.stages[selectedTransition.stages.length-1]||null;
  const selectedTransitionCurrentStage=selectedTransition?.stages.find(stage=>stage.department===selectedTransition.currentStage)||null;
  const selectedTransitionUserStage=session.supportDepartment?selectedTransition?.stages.find(stage=>stage.department===session.supportDepartment)||null:null;
  const canManageSelectedTransitionStage=!!selectedTransitionStage&&(session.role==="Administrator"||session.supportDepartment===selectedTransitionStage.department);
  const canChangeSelectedTransitionStatus=!!selectedTransition&&(session.role==="Administrator"||session.supportDepartment===selectedTransition.currentStage);
  const selectedTransitionPendingRecords=selectedTransitionStage?.progress.filter(entry=>transitionProgressType(entry)==="ADVANCE"&&entry.progressStatus==="IN_PROGRESS").length||0;
  const selectedTransitionTechnicians=selectedTransitionStage?technicians.filter(item=>item.isTechnician&&item.department===selectedTransitionStage.department):[];
  const selectedTransitionHistoryEntries=(selectedTransitionStage?.progress||[]).filter(entry=>transitionProgressType(entry)!=="ASSIGNMENT");
  const allTransitionHistoryExpanded=selectedTransitionHistoryEntries.length>0&&selectedTransitionHistoryEntries.every(entry=>expandedTransitionRecords[entry.id]);
  return (
    <div
      className={`ticket-center${supportStage==="maintenance"?" ops-modern":""}${isTechnologyTechnician?" technician-strict-workspace":""}${isDepartmentLanding ? " ticket-center-landing" : ""}${configOpen ? " support-configuration-view" : ""}${reportAgency ? " incident-report-view" : ""}${reportAgency && formOpen ? " incident-report-form-view" : ""}`}
    >
      {showDepartmentCards && (
        <div className="incident-landing-title">
          <span>
            <LifeBuoy /> SOPORTE OPERATIVO
          </span>
          <h1>Centro de incidencias</h1>
          {reportAgency && onCloseReport && (
            <button className="report-back-button" onClick={onCloseReport}>
              <ArrowLeft /> Volver a la agencia
            </button>
          )}
          {session.permissions.canConfigureSupport && (
            <button
              className="landing-config-button"
              onClick={() => setConfigOpen(true)}
            >
              <ShieldCheck /> Configurar soporte
            </button>
          )}
        </div>
      )}
      {showDepartmentCards && (
        <section className="support-department-portal" id="crear-ticket">
          <header>
            <div>
              <span>
                {session.role === "GroupAdministrator"
                  ? "CREAR NUEVO TICKET"
                  : "ENTRAR AL DEPARTAMENTO"}
              </span>
              <h2>
                {session.role === "GroupAdministrator"
                  ? "¿Qué departamento necesitas?"
                  : "Selecciona el departamento"}
              </h2>
              <p>
                {session.role === "GroupAdministrator"
                  ? "Selecciona un área para consultar sus servicios especializados."
                  : "Abre la bandeja empresarial, los tickets y el CRM del área correspondiente."}
              </p>
            </div>
            <em>01 · DEPARTAMENTO</em>
          </header>
          <div className="department-card-grid department-primary-grid">
            {departmentChoices.map((department) => {
              const DepartmentIcon = department.code === "TECHNOLOGY"
                ? Wrench
                : department.code === "HUMAN_RESOURCES"
                  ? UsersRound
                  : Building2;
              const serviceCount = department.categories.filter(
                (item) => item.isActive,
              ).length;
              const departmentTickets = tickets.filter(
                (item) => item.assignedDepartment === department.code,
              ).length;
              const supervisorDepartmentTickets = tickets.filter(
                (item) =>
                  item.assignedDepartment === department.code &&
                  item.ticketType === "SUPPORT" &&
                  isCurrentSupervisorTicket(item),
              );
              const supervisorDepartmentPending = supervisorDepartmentTickets.filter(
                (item) => !resolvedStatuses.has(item.status),
              ).length;
              const supervisorDepartmentResolved = supervisorDepartmentTickets.filter(
                (item) => ["RESOLVED", "CLOSED"].includes(item.status),
              ).length;
              const departmentPendingFindings = findings.filter(
                (item) =>
                  item.department === department.code &&
                  !["RESOLVED", "CLOSED"].includes(item.status),
              ).length;
              const departmentSolutions = findings.filter(
                (item) =>
                  item.department === department.code &&
                  item.status === "RESOLVED",
              ).length;
              return (
                <button
                  type="button"
                  key={department.code}
                  style={
                    {
                      "--department-accent": department.accentColor,
                    } as React.CSSProperties
                  }
                  onClick={() => {
                    if (session.role === "GroupAdministrator") {
                      setDraft({
                        ...draft,
                        assignedDepartment: department.code,
                        assignedTeam: department.code==="TECHNOLOGY"&&sessionRoutingTeam!=="ALL"?sessionRoutingTeam:"",
                        category: "",
                        priority: "MEDIUM",
                        detail: "",
                        option: "",
                      });
                      setSelectedCases([]);
                      setCaseDetails({});
                      if(reportAgency){setResponsibleQuery("");setFormOpen(true);}
                      else{
                        setFormOpen(false);
                        setWorkspaceDepartment(department.code);
                        setSupportTab("tickets");
                        setSupportStage("home");
                        setTicketTypeFilter("SUPPORT");
                        setSupervisorHistoryView(false);
                        setQuery("");
                      }
                    } else {
                      setWorkspaceDepartment(department.code);
                      setSupportTab("tickets");
                      setSupportStage("home");
                      setFindingView("menu");
                      setQuery("");
                    }
                  }}
                >
                  <span className="department-card-icon">
                    <DepartmentIcon />
                  </span>
                  <span className="department-card-copy">
                    <em>DEPARTAMENTO</em>
                    <strong>{department.name}</strong>
                    <small>{department.description}</small>
                  </span>
                  <span className="department-live-metrics">
                    {session.role === "GroupAdministrator" ? (
                      <>
                        <span>
                          <strong>{supervisorDepartmentTickets.length}</strong>
                          <small>Total</small>
                        </span>
                        <span>
                          <strong>{supervisorDepartmentPending}</strong>
                          <small>Pendientes</small>
                        </span>
                        <span>
                          <strong>{supervisorDepartmentResolved}</strong>
                          <small>Resueltos</small>
                        </span>
                      </>
                    ) : (
                      <>
                        <span>
                          <strong>{departmentTickets}</strong>
                          <small>Tickets</small>
                        </span>
                        <span>
                          <strong>{departmentPendingFindings}</strong>
                          <small>Hallazgos</small>
                        </span>
                        <span>
                          <strong>{departmentSolutions}</strong>
                          <small>Soluciones</small>
                        </span>
                      </>
                    )}
                  </span>
                  <span className="department-card-footer">
                    <span className="department-service-count">
                      <i />
                      {serviceCount}{" "}
                      {serviceCount === 1
                        ? "servicio disponible"
                        : "servicios disponibles"}
                    </span>
                    <span className="department-card-action">
                      {session.role === "GroupAdministrator"
                        ? "Abrir panel"
                        : "Entrar al área"}{" "}
                      <ArrowRight />
                    </span>
                  </span>
                </button>
              );
            })}
          </div>
        </section>
      )}
      {!isDedicatedMaintenancePortal&&<header className="ticket-hero">
        <div>
          <span>
            <LifeBuoy /> SOPORTE OPERATIVO ·{" "}
            {activeDepartment?.name || "CENTRO GENERAL"}
          </span>
          <h1>{activeDepartment?.name || "Centro de incidencias"}</h1>
          <p>
            {activeDepartment
              ? `Bandeja de tickets, hallazgos y conversaciones de ${activeDepartment.name}.`
              : session.supportDepartment
                ? `Bandeja asignada a ${departments[session.supportDepartment] || session.supportDepartment}.`
                : "Reporta, consulta y da seguimiento a situaciones asociadas a cada agencia."}
          </p>
          <small className="ticket-live-status">
            <i /> Actualización automática cada 15 segundos
            {lastSync ? ` · ${lastSync.toLocaleTimeString("es-DO")}` : ""}
          </small>
        </div>
        <div className="ticket-hero-actions">
          {(session.permissions.canViewAllSupportDepartments || session.role === "GroupAdministrator") && workspaceDepartment && !isTechnologyTechnician && (
            <button
              onClick={() => {
                setWorkspaceDepartment(null);
                setSupportStage("home");
                setFindingView("menu");
                setSupervisorHistoryView(false);
                setPersonalAssignmentView(false);
                setAssignedToMeOnly(false);
              }}
            >
              <ArrowLeft /> Departamentos
            </button>
          )}
          <button onClick={() => void load()}>
            <RefreshCw /> Actualizar ahora
          </button>
          {session.permissions.canConfigureSupport && (
            <button onClick={() => setConfigOpen(true)}>
              <ShieldCheck /> Configurar soporte
            </button>
          )}
        </div>
      </header>}
      {error && <div className="alert">{error}</div>}
      {notice && (
        <div className="ticket-notice">
          <CheckCircle2 /> {notice}
        </div>
      )}
      {supportStage === "home" && !isMaintenanceOperator && (
        <nav className={`support-section-tabs${isTechnologyTechnician?" technician-personal-home":""}${session.role==="GroupAdministrator"&&workspaceDepartment?" supervisor-department-home":""}`}>
          {session.role==="GroupAdministrator"&&workspaceDepartment&&<>
            <button className="supervisor-assigned-entry" onClick={()=>{setSupervisorHistoryView(false);setPersonalAssignmentView(true);setAssignedToMeOnly(true);setAssignmentScope("MINE");setTicketTypeFilter("SUPPORT");setTechnologyTeamFilter("ALL");setStatusFilter("ACTIVE");setPriorityFilter("ALL");setCategoryFilter("ALL");setTechnicianFilter("ALL");setQuery("");setSupportTab("tickets");setSupportStage("tickets");setTicketWorkspaceView("active");setTeamViewChosen(true);}}>
              <span className="support-section-icon"><UserRoundCheck/></span><span className="support-section-copy"><em>{activeDepartment?.name||departments[workspaceDepartment]} · ASIGNADOS POR SOPORTE</em><strong>Mis tickets asignados</strong><small>Casos abiertos, en proceso o pendientes asignados directamente a tu cuenta.</small></span><span className="support-section-count">{supervisorAssignedActive.length} activos · {supervisorAssignedTickets.length} total <ArrowRight/></span>
            </button>
            <button className="supervisor-resolved-entry" onClick={()=>{setSupervisorHistoryView(false);setPersonalAssignmentView(true);setAssignedToMeOnly(true);setAssignmentScope("MINE");setTicketTypeFilter("SUPPORT");setTechnologyTeamFilter("ALL");setStatusFilter("ALL");setPriorityFilter("ALL");setCategoryFilter("ALL");setTechnicianFilter("ALL");setQuery("");setSupportTab("tickets");setSupportStage("tickets");setTicketWorkspaceView("history");setTeamViewChosen(true);}}>
              <span className="support-section-icon"><ShieldCheck/></span><span className="support-section-copy"><em>RESULTADOS PERSONALES</em><strong>Mis tickets resueltos</strong><small>Soluciones, evidencias y cierres realizados en este departamento.</small></span><span className="support-section-count">{supervisorAssignedResolved.length} resueltos · {supervisorAssignedTickets.length} total <ArrowRight/></span>
            </button>
            <button className="supervisor-report-entry" onClick={()=>{setSupervisorHistoryView(false);setPersonalAssignmentView(true);setAssignedToMeOnly(true);setAssignmentScope("MINE");setTicketTypeFilter("SUPPORT");setSupportTab("tickets");setSupportStage("tickets");setTicketWorkspaceView("reports");setTeamViewChosen(true);setReportOwner(session.displayName);setActiveReportModule(null);}}>
              <span className="support-section-icon"><BarChart3/></span><span className="support-section-copy"><em>RENDIMIENTO · {activeDepartment?.name||departments[workspaceDepartment]}</em><strong>Reportes y estadísticas</strong><small>Volumen atendido, tiempos, resultados y archivos exportables.</small></span><span className="support-section-count">{supervisorAssignedTickets.length} tickets <ArrowRight/></span>
            </button>
            <button className="supervisor-history-entry" onClick={()=>{setSupervisorHistoryView(true);setPersonalAssignmentView(false);setAssignedToMeOnly(false);setAssignmentScope("ALL");setTicketTypeFilter("SUPPORT");setTechnologyTeamFilter("ALL");setStatusFilter("ALL");setPriorityFilter("ALL");setCategoryFilter("ALL");setTechnicianFilter("ALL");setQuery("");setSupportTab("tickets");setSupportStage("tickets");setTicketWorkspaceView("process");setTeamViewChosen(true);}}>
              <span className="support-section-icon"><History/></span><span className="support-section-copy"><em>TRAZABILIDAD PERSONAL FINALIZADA</em><strong>Historial de mi gestión</strong><small>Casos resueltos o cerrados en los que participaste.</small></span><span className="support-section-count">{supervisorActivityTickets.length} finalizados <ArrowRight/></span>
            </button>
            <button className="supervisor-create-entry" onClick={()=>{setSupervisorHistoryView(false);setResponsibleQuery("");setDraft({...draft,assignedDepartment:workspaceDepartment,assignedTeam:technologyTeamFilter!=="ALL"?technologyTeamFilter:sessionRoutingTeam!=="ALL"?sessionRoutingTeam:"",category:"",priority:"MEDIUM",detail:"",option:""});setSelectedCases([]);setCaseDetails({});setDuplicateCreationConfirmed("");setFormOpen(true);}}>
              <span className="support-section-icon"><Plus/></span><span className="support-section-copy"><em>NUEVA INCIDENCIA</em><strong>Crear ticket</strong><small>Reporta una incidencia nueva en {activeDepartment?.name||departments[workspaceDepartment]}.</small></span><span className="support-section-count">{supervisorAssignedActive.length} pendientes actuales <ArrowRight/></span>
            </button>
          </>}
          {isTechnologyTechnician&&<button
            className="technician-assignment-entry"
            onClick={()=>{
              setPersonalAssignmentView(true);
              setAssignedToMeOnly(true);
              setAssignmentScope("MINE");
              setTechnologyTeamFilter("ALL");
              setStatusFilter("ACTIVE");
              setPriorityFilter("ALL");
              setCategoryFilter("ALL");
              setTechnicianFilter("ALL");
              setQuery("");
              setSupportTab("tickets");
              setSupportStage("tickets");
              setTicketWorkspaceView("active");
              setTeamViewChosen(true);
            }}
          >
            <span className="support-section-icon"><UserRoundCheck /></span>
            <span className="support-section-copy"><em>ESPACIO DEL TÉCNICO · {departments[session.supportDepartment || ""]}</em><strong>Mis tickets asignados</strong><small>Casos que el soporte de tu departamento asignó directamente a tu cuenta.</small></span>
            <span className="support-section-count">{workspaceTickets.filter(ticket=>ticket.ticketType==="SUPPORT"&&ticket.assignedTechnicianId===session.id&&!resolvedStatuses.has(ticket.status)).length} pendientes <ArrowRight /></span>
          </button>}
          {isTechnologyTechnician&&<button className="technician-resolved-entry" onClick={()=>{setPersonalAssignmentView(true);setAssignedToMeOnly(true);setAssignmentScope("MINE");setTechnologyTeamFilter("ALL");setStatusFilter("ALL");setPriorityFilter("ALL");setCategoryFilter("ALL");setTechnicianFilter("ALL");setQuery("");setSupportTab("tickets");setSupportStage("tickets");setTicketWorkspaceView("history");setTeamViewChosen(true);}}><span className="support-section-icon"><ShieldCheck/></span><span className="support-section-copy"><em>HISTORIAL PERSONAL</em><strong>Mis casos resueltos</strong><small>Tickets que completaste con su solución y evidencias.</small></span><span className="support-section-count">{workspaceTickets.filter(ticket=>ticket.ticketType==="SUPPORT"&&ticket.assignedTechnicianId===session.id&&resolvedStatuses.has(ticket.status)).length} resueltos <ArrowRight/></span></button>}
          {isTechnologyTechnician&&<button className="technician-statistics-entry" onClick={()=>{setPersonalAssignmentView(true);setAssignedToMeOnly(true);setSupportTab("tickets");setSupportStage("tickets");setTicketWorkspaceView("reports");setTeamViewChosen(true);setReportOwner(session.displayName);}}><span className="support-section-icon"><BarChart3/></span><span className="support-section-copy"><em>RENDIMIENTO PERSONAL</em><strong>Mis estadísticas</strong><small>Casos resueltos, categorías atendidas y tiempo promedio.</small></span><span className="support-section-count">Ver indicadores <ArrowRight/></span></button>}
          {isTechnologyTechnician&&session.permissions.canUseSupportChat&&<button className="technician-chat-entry" onClick={()=>document.getElementById("support-chat-section")?.scrollIntoView({behavior:"smooth",block:"start"})}><span className="support-section-icon"><MessageCircle/></span><span className="support-section-copy"><em>MENSAJES DE SOPORTE</em><strong>Chat de soporte</strong><small>Consulta conversaciones, responde mensajes y recibe avisos del equipo.</small></span><span className="support-section-count">Abrir mensajes <ArrowRight/></span></button>}
          {session.role==="Technology"&&session.supportTeam==="CALL_CENTER"&&<button className="technology-area-entry call-center-only" onClick={()=>{setPersonalAssignmentView(false);setAssignedToMeOnly(false);setAssignmentScope("ALL");setTicketTypeFilter("INTERNAL");setTechnologyTeamFilter("CALL_CENTER");setStatusFilter("ACTIVE");setPriorityFilter("ALL");setCategoryFilter("ALL");setTechnicianFilter("ALL");setQuery("");setSupportTab("tickets");setSupportStage("tickets");setTicketWorkspaceView("active");setTeamViewChosen(true);}}><span className="support-section-icon"><LifeBuoy/></span><span className="support-section-copy"><em>RECEPCIÓN Y CIERRE · TECNOLOGÍA</em><strong>Call Center</strong><small>Solo verás tickets internos creados y atendidos por tu equipo.</small></span><span className="support-section-count">{activeTeamCounts.callCenter} casos <ArrowRight/></span></button>}
          {!isTechnologyTechnician && showFullSupportHome && session.role !== "GroupAdministrator" && session.supportTeam !== "CALL_CENTER" && (
            <button
              onClick={() => {
                setSupervisorHistoryView(false);
                setPersonalAssignmentView(false);
                setAssignedToMeOnly(false);
                setAssignmentScope("ALL");
                setTicketTypeFilter("SUPPORT");
                setStatusFilter("ACTIVE");
                setPriorityFilter("ALL");
                setCategoryFilter("ALL");
                setTechnicianFilter("ALL");
                setQuery("");
                setSupportTab("tickets");
                setSupportStage("tickets");
                setTicketWorkspaceView("active");
                setTeamViewChosen(false);
                setTechnologyTeamFilter("ALL");
              }}
            >
              <span className="support-section-icon">
                <LifeBuoy />
              </span>
              <span className="support-section-copy">
                <em>CENTRO OPERATIVO</em>
                <strong>Tickets</strong>
                <small>
                  Consulta, asigna y da seguimiento únicamente a las incidencias de {activeDepartment?.name || departments[session.supportDepartment || ""]}.
                </small>
              </span>
              <span className="support-section-count">
                {supportTicketCount} registros <ArrowRight />
              </span>
            </button>
          )}
          {!isTechnologyTechnician&&showFullSupportHome&&session.role!=="GroupAdministrator"&&<button
            onClick={() => {
              setSupervisorHistoryView(false);
              setPersonalAssignmentView(false);
              setAssignedToMeOnly(false);
              setAssignmentScope("ALL");
              setTicketTypeFilter("INTERNAL");
              setSupportTab("tickets");
              setSupportStage("tickets");
              setTicketWorkspaceView("active");
              setTeamViewChosen(false);
              setTechnologyTeamFilter("ALL");
              setStatusFilter("ACTIVE");
              setPriorityFilter("ALL");
              setCategoryFilter("ALL");
              setTechnicianFilter("ALL");
              setQuery("");
            }}
          >
            <span className="support-section-icon"><Building2 /></span>
            <span className="support-section-copy">
              <em>GESTIÓN ENTRE DEPARTAMENTOS</em>
              <strong>Tickets internos</strong>
              <small>Crea, asigna y consulta solicitudes internas del personal.</small>
            </span>
            <span className="support-section-count">
              {internalTicketCount} registros <ArrowRight />
            </span>
          </button>}
          {showFullSupportHome&&session.permissions.canManageAgencies&&<button className="agency-directory-entry" onClick={()=>{setSupportStage("agencyDirectory");setAgencyDirectoryEditId(null);setAgencyDirectoryDraft(null);setAgencyDirectoryQuery("");}}>
            <span className="support-section-icon"><Building2 /></span><span className="support-section-copy"><em>DIRECTORIO MAESTRO</em><strong>Actualizar agencias</strong><small>Corrige código, terminal, grupo, dirección y ubicación con historial de auditoría.</small></span><span className="support-section-count">{agencies.length} agencias <ArrowRight /></span>
          </button>}
          {!isTechnologyTechnician&&showFullSupportHome&&(session.role==="Administrator"||session.supportDepartment) && <button
            onClick={()=>setSupportStage("transitions")}
          >
            <span className="support-section-icon"><Building2 /></span>
            <span className="support-section-copy">
              <em>PROYECTO COMPARTIDO</em>
              <strong>Agencias en construcción</strong>
              <small>Consulta la remodelación, instalación tecnológica y contratación del personal.</small>
            </span>
            <span className="support-section-count">
              {agencyTransitions.filter(item=>item.status==="ACTIVE").length} en proceso · {agencyTransitions.filter(item=>item.status==="PENDING").length} pendientes <ArrowRight />
            </span>
          </button>}
          {showFullSupportHome&&canViewMaintenance&&<button className="maintenance-section-entry" onClick={()=>{setMaintenanceSelectedDepartment(null);setMaintenanceArea(isTechnologyWarehouseRequester?"WAREHOUSE":isWarehouseOperator?"WAREHOUSE":isWorkshopOperator?"WORKSHOP":null);setMaintenanceView(isTechnologyWarehouseRequester?"history":"dashboard");setWarehousePanel(null);setSupportStage("maintenance");}}>
            <span className="support-section-icon"><Wrench /></span><span className="support-section-copy"><em>{isTechnologyWarehouseRequester?"ALMACÉN · SOLICITUDES DE TECNOLOGÍA":`REQUERIMIENTOS A TALLER Y ALMACÉN · ${departments[maintenanceDepartment]||maintenanceDepartment}`}</em>{isTechnologyWarehouseRequester?<strong>Solicitudes a Almacén</strong>:<strong>Mantenimiento y equipos</strong>}<small>{isTechnologyWarehouseRequester?"Envía solicitudes de equipos y consulta tus formularios e historial.":"Solicitudes, entregas a técnicos o supervisores, recepción de dañados, reparación, devolución y cadena de custodia."}</small></span><span className="support-section-count">{isTechnologyWarehouseRequester?warehouseMovements.length:departmentMaintenanceMovements.length} {isTechnologyWarehouseRequester?"formularios":"movimientos"} <ArrowRight /></span>
          </button>}
          {!isTechnologyTechnician&&showFullSupportHome&&session.permissions.canViewSupportFindings && (
            <button
              onClick={() => {
                setSupportTab("findings");
                setSupportStage("findings");
                setFindingView("menu");
              }}
            >
              <span className="support-section-icon">
                <AlertTriangle />
              </span>
              <span className="support-section-copy">
                <em>CONTROL PREVENTIVO</em>
                <strong>Hallazgos automáticos</strong>
                <small>
                  Revisa pendientes, soluciones y tiempos de resolución.
                </small>
              </span>
              <span className="support-section-count">
                {pendingFindingCount} pendientes · {resolvedFindingCount}{" "}
                soluciones <ArrowRight />
              </span>
            </button>
          )}
        </nav>
      )}
      {supportStage !== "home" && !isDedicatedMaintenancePortal && (
        <nav className="support-view-back">
          <button
            onClick={() => {
              if(returnToTicketCreation){restoreTicketCreationAfterDuplicate();return;}
              if (supportStage === "findings" && findingView !== "menu") {
                setFindingView("menu");
                return;
              }
              if(supervisorHistoryView){setSupervisorHistoryView(false);setSupportStage("home");setTicketWorkspaceView("menu");setTeamViewChosen(false);return;}
              if(personalAssignmentView){setPersonalAssignmentView(false);setAssignedToMeOnly(false);setAssignmentScope("ALL");setSupportStage("home");setTicketWorkspaceView("menu");setTeamViewChosen(false);return;}
              if(supportStage==="agencyDirectory"&&agencyDirectoryEditId){setAgencyDirectoryEditId(null);setAgencyDirectoryDraft(null);return;}
              if(technicianPickerOpen){setTechnicianPickerOpen(false);setPersonnelKind(null);setPersonnelQuery("");setTechnologyTeamFilter("ALL");setTechnicianFilter("ALL");return;}
              if (supportStage === "tickets" && teamViewChosen && ticketWorkspaceView !== "menu") {
                setTicketWorkspaceView("menu");
                return;
              }
              if (supportStage === "tickets" && teamViewChosen && ticketWorkspaceView === "menu") {
                setTeamViewChosen(false);
                setTicketWorkspaceView("active");
                return;
              }
              setSupportStage("home");
              setSupportTab("tickets");
              setFindingView("menu");
            }}
          >
            <ArrowLeft />
            {supportStage === "findings" && findingView !== "menu"
              ? "Volver a Hallazgos automáticos"
              : "Volver al centro de soporte"}
          </button>
          <span>
            {supervisorHistoryView ? "Historial de mi gestión" : personalAssignmentView ? "Mis tickets asignados" : supportStage === "agencyDirectory" ? "Actualizar agencias" : supportStage === "maintenance" ? (isTechnologyWarehouseRequester ? "Solicitudes a Almacén" : "Mantenimiento y equipos") : supportStage === "transitions" ? "Agencias en construcción y reestructuración" : supportStage === "tickets"
              ? ticketTypeFilter === "INTERNAL" ? "Tickets internos" : "Tickets de soporte"
              : findingView === "menu"
                ? "Hallazgos automáticos"
                : findingView === "pending"
                  ? "Hallazgos · Pendientes"
                  : findingView === "resolved"
                    ? "Hallazgos · Soluciones"
                    : "Hallazgos · Seguimiento"}
          </span>
        </nav>
      )}
      {supportStage==="agencyDirectory"&&session.permissions.canManageAgencies&&<section className={`agency-directory-board${agencyDirectoryEditId?" has-editor":""}`}>
        <header><div><span>TECNOLOGÍA · DIRECTORIO MAESTRO</span><h2>Actualización de agencias</h2><p>Actualiza agencias existentes o incorpora nuevas desde una hoja de Excel con validación y auditoría.</p></div><div className="agency-directory-header-actions"><b>{agencyDirectoryRows.length} resultados</b><button type="button" onClick={()=>void downloadAgencyDirectoryTemplate()}><Download/>Plantilla Excel</button><button type="button" className="primary" onClick={()=>agencyDirectoryFileRef.current?.click()}><Upload/>Subir agencias</button><input ref={agencyDirectoryFileRef} type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" hidden onChange={event=>{const file=event.target.files?.[0];if(file)void readAgencyDirectoryExcel(file);}}/></div></header>
        {agencyDirectoryImportResult&&<aside className="agency-import-result"><CheckCircle2/><span><strong>Importación completada</strong><small>{agencyDirectoryImportResult.created} agencias agregadas · {agencyDirectoryImportResult.skipped} duplicadas omitidas · {agencyDirectoryImportResult.total} filas procesadas</small></span><button type="button" onClick={()=>setAgencyDirectoryImportResult(null)} aria-label="Cerrar resumen"><X/></button></aside>}
        {agencyDirectoryImport&&<section className="agency-import-preview">
          <header><span><Upload/><i><strong>Revisión antes de importar</strong><small>{agencyDirectoryImport.fileName} · {agencyDirectoryImport.rows.length} agencias válidas</small></i></span><button type="button" onClick={()=>setAgencyDirectoryImport(null)} aria-label="Cancelar importación"><X/></button></header>
          {agencyDirectoryImport.errors.length>0?<div className="agency-import-errors"><AlertTriangle/><span><strong>Corrige el archivo antes de continuar</strong>{agencyDirectoryImport.errors.slice(0,8).map(message=><small key={message}>{message}</small>)}{agencyDirectoryImport.errors.length>8&&<small>+{agencyDirectoryImport.errors.length-8} errores adicionales</small>}</span></div>:<><div className="agency-import-table"><table><thead><tr><th>Fila</th><th>Código</th><th>Terminal</th><th>Grupo</th><th>Ubicación opcional</th></tr></thead><tbody>{agencyDirectoryImport.rows.slice(0,6).map(row=><tr key={`${row.rowNumber}-${row.codigo}`}><td>{row.rowNumber}</td><td>{row.codigo}</td><td>{row.terminal}</td><td>{row.grupo}</td><td>{[row.municipio,row.provincia,row.region].filter(Boolean).join(" · ")||"—"}</td></tr>)}</tbody></table></div><footer><small>Se omitirán automáticamente las agencias que ya existan.</small><button type="button" disabled={saving} onClick={()=>void importAgencyDirectoryExcel()}><ShieldCheck/>{saving?"Importando...":`Importar ${agencyDirectoryImport.rows.length} agencias`}</button></footer></>}
        </section>}
        <label className="agency-directory-search"><Search/><input value={agencyDirectoryQuery} onChange={event=>setAgencyDirectoryQuery(event.target.value)} placeholder="Buscar por código, terminal, grupo, dirección o municipio"/></label>
        <div className="agency-directory-workspace">
          <div className="agency-directory-list">
            {agencyDirectoryRows.slice(0,250).map(agency=><button type="button" className={agencyDirectoryEditId===agency.id?"active":""} key={agency.id} onClick={()=>openAgencyDirectoryEditor(agency)}><span><Building2/></span><i><strong>{agency.codigo} · {agency.terminal}</strong><small>{agency.grupo}{agency.direccion?` · ${agency.direccion}`:" · Dirección pendiente"}</small></i><Pencil/></button>)}
            {!agencyDirectoryRows.length&&<div className="ticket-empty"><Search/><strong>No encontramos esa agencia</strong><span>Revisa el código, terminal o grupo utilizado.</span></div>}
          </div>
          {agencyDirectoryDraft&&agencyDirectoryEditId?<form className="agency-directory-editor" onSubmit={event=>{event.preventDefault();void saveAgencyDirectory();}}>
            <header><div><span>EXPEDIENTE SELECCIONADO</span><h3>{agencyDirectoryDraft.codigo} · {agencyDirectoryDraft.terminal}</h3><p>Modifica únicamente los datos confirmados. La edición quedará auditada.</p></div><button type="button" onClick={()=>{setAgencyDirectoryEditId(null);setAgencyDirectoryDraft(null);}} aria-label="Cerrar editor"><X/></button></header>
            <div><label>Código<input required maxLength={100} value={agencyDirectoryDraft.codigo} onChange={event=>setAgencyDirectoryDraft({...agencyDirectoryDraft,codigo:event.target.value})}/></label><label>Terminal<input required maxLength={300} value={agencyDirectoryDraft.terminal} onChange={event=>setAgencyDirectoryDraft({...agencyDirectoryDraft,terminal:event.target.value})}/></label><label>Grupo<input required maxLength={150} value={agencyDirectoryDraft.grupo} onChange={event=>setAgencyDirectoryDraft({...agencyDirectoryDraft,grupo:event.target.value})}/></label><label>Dirección<input maxLength={300} value={agencyDirectoryDraft.direccion} onChange={event=>setAgencyDirectoryDraft({...agencyDirectoryDraft,direccion:event.target.value})}/></label><label>Sector<input maxLength={120} value={agencyDirectoryDraft.sector} onChange={event=>setAgencyDirectoryDraft({...agencyDirectoryDraft,sector:event.target.value})}/></label><label>Municipio<input maxLength={120} value={agencyDirectoryDraft.municipio} onChange={event=>setAgencyDirectoryDraft({...agencyDirectoryDraft,municipio:event.target.value})}/></label><label>Provincia<input maxLength={120} value={agencyDirectoryDraft.provincia} onChange={event=>setAgencyDirectoryDraft({...agencyDirectoryDraft,provincia:event.target.value})}/></label><label>Latitud<input type="number" step="0.000001" min="-90" max="90" value={agencyDirectoryDraft.latitude} onChange={event=>setAgencyDirectoryDraft({...agencyDirectoryDraft,latitude:event.target.value})}/></label><label>Longitud<input type="number" step="0.000001" min="-180" max="180" value={agencyDirectoryDraft.longitude} onChange={event=>setAgencyDirectoryDraft({...agencyDirectoryDraft,longitude:event.target.value})}/></label></div>
            <footer><button type="button" onClick={()=>{setAgencyDirectoryEditId(null);setAgencyDirectoryDraft(null);}}>Cancelar</button><button className="primary" disabled={saving}><ShieldCheck/>{saving?"Guardando...":"Guardar actualización"}</button></footer>
          </form>:<div className="agency-directory-placeholder"><Building2/><strong>Selecciona una agencia</strong><small>Aquí podrás actualizar sus datos sin salir del departamento de Tecnología.</small></div>}
        </div>
      </section>}
      {supportStage==="transitions" && (
        <section className="agency-transition-board">
          <header>
          <div><span>FLUJO INTERDEPARTAMENTAL · V193</span><h2>Agencias en construcción y reestructuración</h2><p>Cada registro exige evidencias fotográficas antes de completarse y conserva su técnico o contratista responsable.</p></div>
            <button type="button" onClick={()=>setTransitionFormOpen(true)}><Plus /> Registrar agencia</button>
          </header>
          <div className="transition-legend">
            <span><b>1</b> Servicios Generales <small>Remodelación y adecuación de la agencia</small></span>
            <ArrowRight />
            <span><b>2</b> Tecnología <small>Instalación y ejecución del trabajo técnico</small></span>
            <ArrowRight />
            <span><b>3</b> Recursos Humanos <small>Contratación del personal</small></span>
          </div>
          <div className="transition-agency-filterbar">
            <div className="transition-status-segments" aria-label="Estado de las agencias">
              <button type="button" className={transitionStatusFilter==="ACTIVE"?"active":""} onClick={()=>{setTransitionStatusFilter("ACTIVE");setSelectedTransitionId(null);setSelectedTransitionStageId(null);}}><Clock3/><span>Agencias en proceso de construcción</span><b>{agencyTransitions.filter(project=>project.status==="ACTIVE").length}</b></button>
              <button type="button" className={transitionStatusFilter==="PENDING"?"active":""} onClick={()=>{setTransitionStatusFilter("PENDING");setSelectedTransitionId(null);setSelectedTransitionStageId(null);}}><AlertTriangle/><span>Pendientes por material o tiempo</span><b>{agencyTransitions.filter(project=>project.status==="PENDING").length}</b></button>
              <button type="button" className={transitionStatusFilter==="COMPLETED"?"active":""} onClick={()=>{setTransitionStatusFilter("COMPLETED");setSelectedTransitionId(null);setSelectedTransitionStageId(null);}}><CheckCircle2/><span>Completadas</span><b>{agencyTransitions.filter(project=>project.status==="COMPLETED").length}</b></button>
            </div>
            <label className="transition-agency-search"><Search/><input value={transitionListQuery} onChange={event=>{setTransitionListQuery(event.target.value);setSelectedTransitionId(null);setSelectedTransitionStageId(null);}} placeholder="Buscar agencia"/></label>
            <label className="transition-agency-selector"><Building2/><select value={selectedTransitionId||""} onChange={event=>{const project=filteredAgencyTransitions.find(item=>item.id===event.target.value);const target=project?.stages.find(stage=>stage.department===project.currentStage)||project?.stages[project.stages.length-1];setSelectedTransitionId(project?.id||null);setSelectedTransitionStageId(target?.id||null);setTransitionEditMode(false);setTransitionStageEditNotes(target?.notes||"");setTransitionAssignmentRecordId(null);}}><option value="">Seleccionar una agencia</option>{filteredAgencyTransitions.map(project=><option value={project.id} key={`selector-${project.id}`}>{project.codigo} · {project.terminal} · Grupo {project.grupo}</option>)}</select></label>
          </div>
          <div className={`agency-transition-workspace${selectedTransition&&selectedTransitionStage?" has-selection":""}`}>
            <div className="agency-transition-list">
              {filteredAgencyTransitions.filter(project=>!selectedTransitionId||project.id===selectedTransitionId).map(project=>{
                const completed=project.stages.filter(stage=>stage.status==="COMPLETED").length;
                const currentProjectStage=project.stages.find(stage=>stage.department===project.currentStage)||project.stages[project.stages.length-1];
                const currentStageNote=currentProjectStage?.notes?.trim();
                return <article className={`agency-transition-card status-${project.status.toLowerCase()}${selectedTransitionId===project.id?" selected":""}`} key={project.id}>
                  <header role="button" tabIndex={0} onClick={()=>{const target=project.stages.find(stage=>stage.department===project.currentStage)||project.stages[project.stages.length-1];setSelectedTransitionId(project.id);setSelectedTransitionStageId(target?.id||null);setTransitionEditMode(false);setTransitionStageEditNotes(target?.notes||"");setTransitionAssignmentRecordId(null);}} onKeyDown={event=>{if(event.key!=="Enter"&&event.key!==" ")return;event.preventDefault();const target=project.stages.find(stage=>stage.department===project.currentStage)||project.stages[project.stages.length-1];setSelectedTransitionId(project.id);setSelectedTransitionStageId(target?.id||null);setTransitionEditMode(false);setTransitionStageEditNotes(target?.notes||"");setTransitionAssignmentRecordId(null);}}><div><em>{project.projectType==="CONSTRUCTION"?"CONSTRUCCIÓN":"REESTRUCTURACIÓN"}</em><h3>{project.codigo} · {project.terminal}</h3><p>Grupo {project.grupo}</p></div><strong>{project.status==="COMPLETED"?"Completado":`${completed}/3`}</strong></header>
                  {project.notes&&<p className="transition-project-note">{project.notes}</p>}
                  <div className={`transition-current-marker${project.status==="COMPLETED"?" completed":project.status==="PENDING"?" pending":""}`}><span>{project.status==="COMPLETED"?<CheckCircle2/>:project.status==="PENDING"?<AlertTriangle/>:<Clock3/>}<i><small>{project.status==="COMPLETED"?"ESTADO DEL PROCESO":project.status==="PENDING"?"PROCESO EN ESPERA":"ETAPA ACTUAL"}</small><strong>{project.status==="COMPLETED"?"Todas las etapas completadas":transitionDepartmentLabels[project.currentStage]||departments[project.currentStage]||project.currentStage}</strong>{currentStageNote&&<span className="transition-current-note">{currentStageNote}</span>}</i></span><em>{project.status==="COMPLETED"?"Finalizado":project.status==="PENDING"?"Pendiente":"En ejecución"}</em></div>
                  <div className="transition-progress" style={{"--transition-progress":`${completed/3*100}%`} as React.CSSProperties}><i /></div>
                  <div className="transition-department-cards" aria-label={`Etapas de ${project.codigo}`}>
                    {project.stages.map(stage=><button type="button" className={`transition-department-card ${stage.status.toLowerCase()}${selectedTransitionStageId===stage.id?" selected":""}`} key={stage.id} onClick={event=>{event.stopPropagation();setSelectedTransitionId(project.id);setSelectedTransitionStageId(stage.id);setTransitionEditMode(false);setTransitionStageEditNotes(stage.notes||"");setTransitionAssignmentRecordId(null);}}>
                      <span>{stage.status==="COMPLETED"?<CheckCircle2 />:<Clock3 />}</span>
                      <i><small>ETAPA {stage.stageOrder}</small><strong>{transitionDepartmentLabels[stage.department]||departments[stage.department]||stage.department}</strong><em>{stage.status==="COMPLETED"?"Completada":stage.status==="IN_PROGRESS"?"En proceso":"Pendiente"}</em>{project.status==="ACTIVE"&&stage.department===project.currentStage&&<b>ACTUAL</b>}</i>
                      <ArrowRight />
                    </button>)}
                    {!project.stages.length&&<span className="transition-stage-recovery"><RefreshCw/><strong>Recuperando etapas del proceso</strong><small>Actualiza la vista en unos segundos.</small></span>}
                  </div>
                </article>;
              })}
              {!filteredAgencyTransitions.length&&<div className="ticket-empty"><Building2/><strong>No hay agencias en esta clasificación</strong><span>Cambia el filtro o registra la primera construcción o reestructuración.</span></div>}
            </div>
            {selectedTransition&&selectedTransitionStage&&<section className={`transition-stage-detail ${selectedTransitionStage.status.toLowerCase()}`}>
              <header>
                <button className="transition-detail-back" type="button" onClick={()=>{setSelectedTransitionId(null);setSelectedTransitionStageId(null);setTransitionEditMode(false);setTransitionAssignmentRecordId(null);}}><ArrowLeft/> Volver a agencias</button>
                 <div><span>ETAPA {selectedTransitionStage.stageOrder} · {selectedTransition.projectType==="CONSTRUCTION"?"CONSTRUCCIÓN":"REESTRUCTURACIÓN"}</span><h3>{transitionDepartmentLabels[selectedTransitionStage.department]||departments[selectedTransitionStage.department]||selectedTransitionStage.department}</h3><p>{transitionDepartmentResponsibilities[selectedTransitionStage.department]||"Gestión del departamento"} · {selectedTransition.codigo} · {selectedTransition.terminal} · Grupo {selectedTransition.grupo}</p></div>
                <strong>{selectedTransition.status==="PENDING"?"Proceso pendiente":selectedTransitionStage.status==="COMPLETED"?"Etapa completada":selectedTransitionStage.status==="IN_PROGRESS"?"Etapa en proceso":"Etapa pendiente"}</strong>
              </header>
              <div className="transition-section-shortcuts" aria-label="Sección actual del proceso">
                {selectedTransitionCurrentStage&&<button type="button" className={`transition-section-shortcut current${selectedTransitionCurrentStage.id===selectedTransitionStage.id?" active":""}`} onClick={()=>{setSelectedTransitionStageId(selectedTransitionCurrentStage.id);setTransitionEditMode(false);setTransitionStageEditNotes(selectedTransitionCurrentStage.notes||"");setTransitionProgressEditId(null);setTransitionAssignmentRecordId(null);}}>
                  <span className="transition-section-shortcut-icon"><Clock3/></span>
                   <span><small>{selectedTransitionUserStage?.id===selectedTransitionCurrentStage.id?"MI SECCIÓN · ETAPA ACTUAL":"ETAPA ACTUAL DEL PROCESO"}</small><strong>{transitionDepartmentLabels[selectedTransitionCurrentStage.department]||departments[selectedTransitionCurrentStage.department]||selectedTransitionCurrentStage.department}</strong><em>{selectedTransition.status==="COMPLETED"?"Proceso completado":"En ejecución"}</em></span>
                  {selectedTransitionCurrentStage.id===selectedTransitionStage.id?<CheckCircle2/>:<ArrowRight/>}
                </button>}
                {selectedTransitionUserStage&&selectedTransitionUserStage.id!==selectedTransitionCurrentStage?.id&&<button type="button" className={`transition-section-shortcut mine${selectedTransitionUserStage.id===selectedTransitionStage.id?" active":""}`} onClick={()=>{setSelectedTransitionStageId(selectedTransitionUserStage.id);setTransitionEditMode(false);setTransitionStageEditNotes(selectedTransitionUserStage.notes||"");setTransitionProgressEditId(null);setTransitionAssignmentRecordId(null);}}>
                  <span className="transition-section-shortcut-icon"><ShieldCheck/></span>
                   <span><small>MI SECCIÓN</small><strong>{transitionDepartmentLabels[selectedTransitionUserStage.department]||departments[selectedTransitionUserStage.department]||selectedTransitionUserStage.department}</strong><em>{selectedTransitionUserStage.status==="COMPLETED"?"Completada":selectedTransitionUserStage.status==="IN_PROGRESS"?"En proceso":"Pendiente"}</em></span>
                  {selectedTransitionUserStage.id===selectedTransitionStage.id?<CheckCircle2/>:<ArrowRight/>}
                </button>}
              </div>
              <nav className="transition-detail-stage-tabs" aria-label="Etapas del proceso seleccionado">
                {selectedTransition.stages.map(stage=><button type="button" className={`${stage.status.toLowerCase()}${stage.id===selectedTransitionStage.id?" active":""}`} key={`detail-${stage.id}`} onClick={()=>{setSelectedTransitionStageId(stage.id);setTransitionEditMode(false);setTransitionStageEditNotes(stage.notes||"");setTransitionProgressEditId(null);setTransitionAssignmentRecordId(null);}}><span>{stage.status==="COMPLETED"?<CheckCircle2/>:<Clock3/>}</span><i><small>ETAPA {stage.stageOrder}</small><strong>{departments[stage.department]||stage.department}</strong><em>{stage.status==="COMPLETED"?"Completada":stage.status==="IN_PROGRESS"?"En proceso":"Pendiente"}</em></i></button>)}
              </nav>
              {selectedTransition.status==="ACTIVE"&&selectedTransitionStage.department!==selectedTransition.currentStage&&<button type="button" className="transition-current-callout" onClick={()=>{const currentStage=selectedTransition.stages.find(stage=>stage.department===selectedTransition.currentStage);if(currentStage){setSelectedTransitionStageId(currentStage.id);setTransitionEditMode(false);setTransitionStageEditNotes(currentStage.notes||"");setTransitionAssignmentRecordId(null);}}}><Clock3/><span><small>ESTÁS CONSULTANDO UNA ETAPA ANTERIOR</small><strong>Etapa actual: {departments[selectedTransition.currentStage]||selectedTransition.currentStage}</strong></span><ArrowRight/></button>}
              {selectedTransition.status==="ACTIVE"&&selectedTransitionStage.department===selectedTransition.currentStage&&<div className="transition-current-callout active"><Clock3/><span><small>ETAPA ACTUAL DEL PROCESO</small><strong>{departments[selectedTransition.currentStage]||selectedTransition.currentStage} · En ejecución</strong></span></div>}
              {selectedTransition.status==="PENDING"&&<div className="transition-current-callout pending"><AlertTriangle/><span><small>PROCESO EN ESPERA</small><strong>El avance se conserva hasta que el departamento actual lo reanude.</strong></span></div>}
              <div className="transition-detail-actions">
                {canChangeSelectedTransitionStatus&&selectedTransition.status==="ACTIVE"&&<button type="button" className="pending" disabled={saving} onClick={()=>void updateAgencyTransitionStatus(selectedTransition,"PENDING")}><AlertTriangle/> Poner proceso pendiente</button>}
                {canChangeSelectedTransitionStatus&&selectedTransition.status==="PENDING"&&<button type="button" className="resume" disabled={saving} onClick={()=>void updateAgencyTransitionStatus(selectedTransition,"ACTIVE")}><Clock3/> Reanudar proceso</button>}
                {canManageSelectedTransitionStage&&<button type="button" onClick={()=>{setTransitionEditMode(value=>!value);setTransitionStageEditNotes(selectedTransitionStage.notes||"");}}><Pencil/> Editar etapa</button>}
                {canManageSelectedTransitionStage&&<button type="button" className="danger" disabled={saving} onClick={()=>void deleteAgencyTransitionStageContent(selectedTransition,selectedTransitionStage)}><Trash2/> Eliminar datos de etapa</button>}
                {session.role==="Administrator"&&<button type="button" className="danger process-delete" disabled={saving} onClick={()=>void deleteAgencyTransition(selectedTransition)}><Trash2/> Eliminar proceso</button>}
                {!canManageSelectedTransitionStage&&<span className="transition-read-only"><ShieldCheck/> Solo lectura · Esta etapa pertenece a {departments[selectedTransitionStage.department]||selectedTransitionStage.department}</span>}
              </div>
              {transitionEditMode&&canManageSelectedTransitionStage&&<div className="transition-stage-editor"><label>Descripción actual de la etapa<textarea maxLength={2000} value={transitionStageEditNotes} onChange={event=>setTransitionStageEditNotes(event.target.value)}/></label><div><button type="button" onClick={()=>setTransitionEditMode(false)}>Cancelar</button><button type="button" className="primary" disabled={saving} onClick={()=>void editAgencyTransitionStage(selectedTransition,selectedTransitionStage)}>Guardar edición</button></div></div>}
              <div className="transition-history-panel transition-history-accordion">
                <header><div><History/><span><strong>Historial de avances</strong><small>Toca un registro para consultar sus evidencias y acciones.</small></span></div><div className="transition-history-header-actions"><b>{selectedTransitionHistoryEntries.length} registros</b>{selectedTransitionHistoryEntries.length>1&&<button type="button" onClick={()=>setExpandedTransitionRecords(current=>{const next={...current};selectedTransitionHistoryEntries.forEach(entry=>{next[entry.id]=!allTransitionHistoryExpanded;});return next;})}><ChevronDown className={allTransitionHistoryExpanded?"expanded":""}/>{allTransitionHistoryExpanded?"Ocultar todos":"Expandir todos"}</button>}</div></header>
                <div className="transition-history-list">
                  {selectedTransitionHistoryEntries.map((entry,index)=>{const storedEntryType=transitionProgressType(entry);const entryType=(entry.evidence?.length||0)>0?"ADVANCE":storedEntryType;const isExpanded=expandedTransitionRecords[entry.id]??(entry.progressStatus==="COMPLETED"&&(entry.evidence?.length||0)>0);const assignment=transitionRecordAssignment(selectedTransitionStage,entry.id);const entryTitle=entryType==="STAGE_COMPLETION"?"Cierre de etapa":entryType==="STAGE_EDIT"?`Edición de etapa · Registro ${index+1}`:`Registro de trabajo ${index+1}`;return <article className={`transition-history-record ${entry.progressStatus.toLowerCase()} type-${entryType.toLowerCase()}${isExpanded?" is-expanded":" is-collapsed"}`} key={entry.id}>
                    <button type="button" className="transition-history-record-toggle" aria-expanded={isExpanded} aria-controls={`transition-record-${entry.id}`} onClick={()=>setExpandedTransitionRecords(current=>({...current,[entry.id]:!current[entry.id]}))}>
                      <span className="transition-history-record-icon">{entry.progressStatus==="COMPLETED"?<CheckCircle2/>:entryType==="STAGE_EDIT"?<Pencil/>:<Clock3/>}</span>
                      <span className="transition-history-record-summary"><small>{entryTitle}</small><strong>{entry.note}</strong><em>{assignment?`${assignment.assignedName} · ${assignment.assignmentType==="TECHNICIAN"?"Técnico":"Contratista"}`:`${entry.createdByName} · ${entry.progressStatus==="COMPLETED"?"Completado":"Pendiente"}`}</em></span>
                      <time>{new Date(entry.updatedAt||entry.createdAt).toLocaleString("es-DO")}</time>
                      <span className="transition-history-record-open"><b>{isExpanded?"Ocultar":"Ver detalles"}</b><ChevronDown/></span>
                    </button>
                    {isExpanded&&<div className="transition-history-record-details" id={`transition-record-${entry.id}`}>
                      {transitionProgressEditId===entry.id?<div className="transition-progress-editor"><textarea maxLength={2000} value={transitionProgressEditNote} onChange={event=>setTransitionProgressEditNote(event.target.value)}/><div><button type="button" onClick={()=>{setTransitionProgressEditId(null);setTransitionProgressEditNote("");}}>Cancelar</button><button type="button" disabled={saving} onClick={()=>void updateAgencyTransitionProgress(selectedTransition,selectedTransitionStage,entry)}>Guardar cambios</button></div></div>:<p>{entry.note}</p>}
                      <small>{entry.createdByName}{entryType==="STAGE_COMPLETION"?" · Completó la etapa del departamento":entryType==="STAGE_EDIT"?" · Editó la descripción de la etapa":entry.progressStatus==="COMPLETED"?" · Registro completado":" · Registro pendiente"}</small>
                      {entryType==="ADVANCE"&&<div className={`transition-record-assignee${assignment?" assigned":""}`}><UserRoundCheck/><span><small>RESPONSABLE DE ESTE REGISTRO</small><strong>{assignment?.assignedName||"Sin responsable asignado"}</strong>{assignment&&<em>{assignment.assignmentType==="TECHNICIAN"?"Técnico del departamento":"Contratista externo"}</em>}</span></div>}
                      {entryType==="ADVANCE"&&<section className={`transition-record-evidence transition-camera-gps${entry.evidence?.length?" has-evidence":""}`}>
                        <header><span><Camera/><i><strong>Fotografías con cámara y GPS</strong><small>{entry.evidence?.length||0} de 4 fotografías · ubicación obligatoria</small></i></span>{entry.progressStatus!=="COMPLETED"&&canManageSelectedTransitionStage&&<label className={`transition-evidence-upload transition-camera-button${(entry.evidence?.length||0)>=4?" disabled":""}`}><Camera/>{transitionEvidenceUploadingId===entry.id?"Verificando GPS...":"Tomar foto con GPS"}<input type="file" accept="image/*,.heic,.heif" capture="environment" disabled={saving||(entry.evidence?.length||0)>=4} onChange={event=>{void uploadAgencyTransitionEvidence(selectedTransition,selectedTransitionStage,entry,event.target.files);event.currentTarget.value="";}}/></label>}</header>
                        {transitionEvidenceLocations[entry.id]&&<div className="transition-camera-location"><MapPin/><span><strong>GPS listo</strong><small>{transitionEvidenceLocations[entry.id].latitude.toFixed(6)}, {transitionEvidenceLocations[entry.id].longitude.toFixed(6)} · precisión ±{Math.round(transitionEvidenceLocations[entry.id].accuracyMeters)} m</small></span></div>}
                        {transitionEvidenceFeedback?.entryId===entry.id&&<div className={`transition-evidence-feedback ${transitionEvidenceFeedback.kind}`}>{transitionEvidenceFeedback.kind==="success"?<CheckCircle2/>:transitionEvidenceFeedback.kind==="error"?<AlertTriangle/>:<RefreshCw/>}<span>{transitionEvidenceFeedback.message}</span></div>}
                        {entry.evidence?.length?<div className="transition-evidence-ready"><CheckCircle2/><span><strong>{entry.evidence.length} {entry.evidence.length===1?"evidencia guardada":"evidencias guardadas"}</strong><small>Consulta las fotografías, quién las subió, su fecha y ubicación GPS.</small></span><button type="button" onClick={()=>setTransitionEvidenceViewer({project:selectedTransition,stage:selectedTransitionStage,entry,recordNumber:index+1})}><Eye/> Ver evidencias <b>{entry.evidence.length}</b></button></div>:<div className="transition-evidence-required"><AlertTriangle/><span><strong>Evidencia pendiente</strong><small>Toma al menos una fotografía con la cámara y el GPS activado antes de completar este registro.</small></span></div>}
                      </section>}
                      {canManageSelectedTransitionStage&&entryType==="ADVANCE"&&transitionProgressEditId!==entry.id&&<div className="transition-progress-actions"><button type="button" onClick={()=>{setTransitionAssignmentRecordId(value=>value===entry.id?null:entry.id);setTransitionAssignmentDraft({assignmentType:assignment?.assignmentType||"TECHNICIAN",technicianUserId:assignment?.assignedUserId||"",contractorName:assignment?.assignmentType==="CONTRACTOR"?assignment.assignedName:""});}}><UserRoundCheck/> {assignment?"Cambiar responsable":"Asignar responsable"}</button><button type="button" onClick={()=>{setTransitionProgressEditId(entry.id);setTransitionProgressEditNote(entry.note);setTransitionAssignmentRecordId(null);}}><Pencil/> Editar registro</button>{entry.progressStatus!=="COMPLETED"&&<button type="button" className="complete" disabled={saving||!(entry.evidence?.length)} title={entry.evidence?.length?"Completar registro":"Debes subir al menos una evidencia"} onClick={()=>void updateAgencyTransitionProgress(selectedTransition,selectedTransitionStage,entry,"COMPLETED")}><CheckCircle2/> Completar registro</button>}<button type="button" className="danger" disabled={saving} onClick={()=>void deleteAgencyTransitionProgress(selectedTransition,selectedTransitionStage,entry)}><Trash2/> Eliminar</button></div>}
                      {transitionAssignmentRecordId===entry.id&&canManageSelectedTransitionStage&&<div className="transition-assignment-editor transition-record-assignment-editor"><header><UserRoundCheck/><span><strong>Responsable del registro de trabajo {index+1}</strong><small>Solo aparecen técnicos activos de {departments[selectedTransitionStage.department]||selectedTransitionStage.department}.</small></span></header><div className="transition-assignment-type"><button type="button" className={transitionAssignmentDraft.assignmentType==="TECHNICIAN"?"active":""} onClick={()=>setTransitionAssignmentDraft(current=>({...current,assignmentType:"TECHNICIAN"}))}>Técnico del departamento</button><button type="button" className={transitionAssignmentDraft.assignmentType==="CONTRACTOR"?"active":""} onClick={()=>setTransitionAssignmentDraft(current=>({...current,assignmentType:"CONTRACTOR"}))}>Contratista</button></div>{transitionAssignmentDraft.assignmentType==="TECHNICIAN"?<label>Técnico<select value={transitionAssignmentDraft.technicianUserId} onChange={event=>setTransitionAssignmentDraft(current=>({...current,technicianUserId:event.target.value}))}><option value="">Seleccionar técnico</option>{selectedTransitionTechnicians.map(item=><option value={item.id} key={item.id}>{item.name}</option>)}</select>{!selectedTransitionTechnicians.length&&<small>No hay técnicos activos configurados para este departamento.</small>}</label>:<label>Nombre del contratista<input maxLength={160} value={transitionAssignmentDraft.contractorName} onChange={event=>setTransitionAssignmentDraft(current=>({...current,contractorName:event.target.value}))} placeholder="Nombre completo o empresa contratista"/></label>}<footer><button type="button" onClick={()=>setTransitionAssignmentRecordId(null)}>Cancelar</button><button type="button" className="primary" disabled={saving||(transitionAssignmentDraft.assignmentType==="TECHNICIAN"?!transitionAssignmentDraft.technicianUserId:transitionAssignmentDraft.contractorName.trim().length<2)} onClick={()=>void assignAgencyTransitionRecord(selectedTransition,selectedTransitionStage,entry)}><UserRoundCheck/> Guardar responsable</button></footer></div>}
                    </div>}
                  </article>;})}
                  {!selectedTransitionHistoryEntries.length&&<div className="transition-history-empty"><History/><strong>Aún no hay avances registrados</strong><span>El primer avance aparecerá aquí y no reemplazará los siguientes.</span></div>}
                </div>
              </div>
              {transitionRecordDecisionStageId===selectedTransitionStage.id&&canManageSelectedTransitionStage&&<div className="transition-record-decision"><CheckCircle2/><span><strong>Registro completado correctamente</strong><small>¿Deseas crear otro registro o completar toda la etapa de {departments[selectedTransitionStage.department]||selectedTransitionStage.department}?</small></span><div><button type="button" onClick={()=>{setTransitionRecordDecisionStageId(null);window.requestAnimationFrame(()=>transitionNewRecordRef.current?.focus());}}><Plus/> Crear otro registro</button><button type="button" className="complete" disabled={saving||selectedTransitionPendingRecords>0} onClick={()=>void updateAgencyTransition(selectedTransition,selectedTransitionStage,"COMPLETED")}><CheckCircle2/> Completar etapa de {departments[selectedTransitionStage.department]||selectedTransitionStage.department}</button></div></div>}
              {selectedTransition.status==="ACTIVE"&&selectedTransitionStage.department===selectedTransition.currentStage&&canManageSelectedTransitionStage?<div className="transition-update-panel"><label>Nuevo registro de {departments[selectedTransitionStage.department]||selectedTransitionStage.department}<textarea ref={transitionNewRecordRef} maxLength={2000} placeholder={selectedTransitionPendingRecords?"Completa el registro actual y adjunta sus evidencias para crear otro":"Describe exclusivamente el nuevo trabajo realizado"} value={transitionNotes[selectedTransitionStage.id]||""} disabled={transitionEditMode||selectedTransitionPendingRecords>0} onChange={event=>setTransitionNotes(current=>({...current,[selectedTransitionStage.id]:event.target.value}))}/></label><div><button type="button" disabled={saving||transitionEditMode||selectedTransitionPendingRecords>0} onClick={()=>void updateAgencyTransition(selectedTransition,selectedTransitionStage,"IN_PROGRESS")}><Plus/> Crear registro</button><button type="button" className="complete stage-complete" disabled={saving||transitionEditMode||selectedTransitionPendingRecords>0} title={selectedTransitionPendingRecords?"Completa o elimina los registros pendientes":"Completar toda la etapa del departamento"} onClick={()=>void updateAgencyTransition(selectedTransition,selectedTransitionStage,"COMPLETED")}><CheckCircle2/> Completar etapa de {departments[selectedTransitionStage.department]||selectedTransitionStage.department}</button></div>{selectedTransitionPendingRecords>0&&<small className="transition-pending-warning">Completa el registro actual con al menos una fotografía de evidencia antes de crear otro registro o cerrar la etapa.</small>}</div>:selectedTransition.status==="ACTIVE"&&selectedTransitionStage.department===selectedTransition.currentStage?<span className="transition-waiting">Esta etapa corresponde a {departments[selectedTransition.currentStage]||selectedTransition.currentStage}. Tu acceso es solamente de lectura.</span>:null}
              {selectedTransitionStage.status==="COMPLETED"&&<div className="transition-completed-banner"><CheckCircle2/><span><strong>Etapa completada</strong><small>{selectedTransitionStage.completedByName?`Finalizada por ${selectedTransitionStage.completedByName}.`:"La siguiente área ya puede continuar."}</small></span></div>}
            </section>}
          </div>
        </section>
      )}
      {transitionEvidenceViewer&&<div className="transition-evidence-viewer-backdrop" role="presentation" onMouseDown={event=>{if(event.target===event.currentTarget)setTransitionEvidenceViewer(null);}}>
        <section className="transition-evidence-viewer" role="dialog" aria-modal="true" aria-labelledby="transition-evidence-viewer-title">
          <header><div><span>EVIDENCIAS DEL HISTORIAL</span><h2 id="transition-evidence-viewer-title">Registro de trabajo {transitionEvidenceViewer.recordNumber}</h2><p>{departments[transitionEvidenceViewer.stage.department]||transitionEvidenceViewer.stage.department} · {transitionEvidenceViewer.project.codigo} · {transitionEvidenceViewer.project.terminal}</p></div><button type="button" onClick={()=>setTransitionEvidenceViewer(null)} aria-label="Cerrar visor de evidencias"><X/></button></header>
          <div className="transition-evidence-viewer-grid">
            {(transitionEvidenceViewer.entry.evidence||[]).map((evidence,photoIndex)=><article key={evidence.id}>
              <a className="transition-evidence-viewer-photo" href={evidence.url} target="_blank" rel="noreferrer" aria-label={`Abrir evidencia ${photoIndex+1} en tamaño completo`}><img src={evidence.url} alt={`Evidencia ${photoIndex+1} del registro ${transitionEvidenceViewer.recordNumber}`} loading={photoIndex===0?"eager":"lazy"} decoding="async" fetchPriority={photoIndex===0?"high":"low"}/><span><Eye/> Clic para ampliar</span></a>
              <div className="transition-evidence-viewer-info"><div><Camera/><span><strong>Fotografía {photoIndex+1}</strong><small>Subida por {evidence.uploadedByName||transitionEvidenceViewer.entry.createdByName}</small></span></div><time dateTime={evidence.capturedAt}><Clock3/> {new Date(evidence.capturedAt).toLocaleString("es-DO")}</time>{evidence.latitude!=null&&evidence.longitude!=null?<a href={`https://www.google.com/maps?q=${evidence.latitude},${evidence.longitude}`} target="_blank" rel="noreferrer"><MapPin/> Ver ubicación GPS{evidence.accuracyMeters!=null?` · precisión ±${Math.round(evidence.accuracyMeters)} m`:""}</a>:<span className="transition-evidence-no-location"><AlertTriangle/> Ubicación no disponible</span>}{transitionEvidenceViewer.entry.progressStatus!=="COMPLETED"&&canManageSelectedTransitionStage&&<button type="button" className="transition-evidence-viewer-delete" disabled={saving} onClick={()=>void deleteAgencyTransitionEvidence(transitionEvidenceViewer.project,transitionEvidenceViewer.stage,transitionEvidenceViewer.entry,evidence)}><Trash2/> Eliminar fotografía</button>}</div>
            </article>)}
          </div>
        </section>
      </div>}
      {supportStage==="maintenance"&&!maintenanceArea&&!isMaintenanceOperator&&<section className="maintenance-area-hub">
        <header><span>OPERACIÓN DE MANTENIMIENTO · {departments[maintenanceDepartment]||maintenanceDepartment}</span><h2>Selecciona el área de trabajo</h2><p>Taller y Almacén comparten la trazabilidad del activo, pero cada uno conserva sus operaciones y controles propios.</p></header>
        {maintenanceLoadError&&<div className="maintenance-service-warning"><AlertTriangle/><div><strong>No fue posible sincronizar Mantenimiento</strong><span>{maintenanceLoadError}</span></div><button onClick={()=>{maintenanceRetryAfterRef.current=0;void loadMaintenanceMovements();}}><RefreshCw/> Reintentar</button></div>}
        <div className="maintenance-area-cards">
          <article className="workshop"><span className="area-icon"><Hammer/></span><div><em>REPARACIÓN Y DIAGNÓSTICO</em><h3>Taller</h3><p>Recibe y entrega equipos, diagnostica fallas, documenta reparaciones, reemplazos y cambios de componentes.</p><ul><li>Entradas y salidas</li><li>Reparación y reemplazo</li><li>Responsable y firma de entrega</li></ul><strong>{workshopMovements.length} movimientos registrados</strong></div><footer><button onClick={()=>{setMaintenanceArea("WORKSHOP");setMaintenanceView("dashboard");}}><Wrench/> Abrir Taller</button><button className="scan" onClick={()=>openMaintenanceForm(true,"WORKSHOP")}><QrCode/> Escanear</button></footer></article>
          <article className="warehouse"><span className="area-icon"><Warehouse/></span><div><em>INVENTARIO Y DESPACHO</em><h3>Almacén</h3><p>Recibe equipos nuevos, controla existencias, despacha a agencias o departamentos y registra descargos definitivos.</p><ul><li>Entrada de equipos nuevos</li><li>Salidas y entregas</li><li>Descargos auditados</li></ul><strong>{warehouseMovements.length} movimientos registrados</strong></div><footer><button onClick={()=>{setMaintenanceArea("WAREHOUSE");setMaintenanceView("dashboard");}}><Boxes/> Abrir Almacén</button><button className="scan" onClick={()=>openMaintenanceForm(true,"WAREHOUSE")}><QrCode/> Escanear</button></footer></article>
        </div>
      </section>}
      {trackingCode!==null&&<EquipmentTracking initialCode={trackingCode} onClose={()=>setTrackingCode(null)}/>}
      {supportStage==="maintenance"&&maintenanceArea==="WAREHOUSE"&&!warehousePanel&&!isTechnologyWarehouseRequester&&<button className="wt-lookup-button" onClick={()=>setTrackingCode("")}><QrCode/>Estado y trazabilidad por QR</button>}
      {documentToEdit&&<MaintenanceDocumentEditor item={documentToEdit} onClose={()=>setDocumentToEdit(null)}/>}
      {supportStage==="maintenance"&&maintenanceArea==="WAREHOUSE"&&!warehousePanel&&!isDedicatedMaintenancePortal&&!isTechnologyWarehouseRequester&&<div className="mw-section-cards">
        <button onClick={()=>setWarehousePanel("requests")}><Send/><strong>{isWarehouseOperator||session.role==="Administrator"?"Comunicaciones de departamentos":"Solicitudes a Almacén"}</strong><small>{isWarehouseOperator||session.role==="Administrator"?"Mensajes y documentos enviados por cada departamento; todo queda organizado en su bandeja.":"Documentos enviados desde este departamento a Almacén General."}</small></button>
        <button onClick={()=>setWarehousePanel("requirements")}><ClipboardList/><strong>Requerimientos</strong><small>Equipos solicitados a Almacén, organizados solo para {departments[maintenanceDepartment]||maintenanceDepartment}.</small></button>
        <button onClick={()=>{setMaintenanceView("inventory");setMaintenanceQuery("");document.getElementById("maintenance-inventory-destination")?.scrollIntoView({behavior:"smooth",block:"start"});}}><Boxes/><strong>Inventario</strong><small>{isDedicatedMaintenancePortal&&!maintenanceSelectedDepartment?"Selecciona abajo un departamento para consultar sus activos.":"Existencias, ubicación de equipos y formularios PDF."}</small></button>
        <button onClick={()=>setWarehousePanel("templates")}><FileText/><strong>Mis plantillas</strong><small>Editor en vivo, firmas, guardar, imprimir y enviar documentos.</small></button>
        <button onClick={()=>setWarehousePanel("files")}><FileText/><strong>Archivos</strong><small>Carpetas, archivos compartidos, papelera y biblioteca de PDF.</small></button>
      </div>}
      {supportStage==="maintenance"&&maintenanceArea&&warehousePanel&&<MaintenanceCommunications view={warehousePanel} onNavigate={setWarehousePanel} session={session} department={maintenanceDepartment} onBack={()=>setWarehousePanel(null)} onChanged={()=>{maintenanceRetryAfterRef.current=0;void loadMaintenanceMovements();}}/>}
      {supportStage==="maintenance"&&maintenanceArea==="WORKSHOP"&&!warehousePanel&&!isDedicatedMaintenancePortal&&<button className="mw-template-launch" onClick={()=>setWarehousePanel("templates")}><FileText/> Plantillas y archivos institucionales <ArrowRight/></button>}
      {supportStage==="maintenance"&&!warehousePanel&&isDedicatedMaintenancePortal&&maintenanceArea&&!maintenanceSelectedDepartment&&<MaintenanceOverview area={maintenanceArea} movements={maintenanceMovements} loading={maintenanceLoading} error={maintenanceLoadError} pendingOrders={maintenanceOpenOrders.length} departmentScope={session.role==="Administrator"||isWarehouseOperator?undefined:session.supportDepartment?[session.supportDepartment]:undefined} onRefresh={()=>{maintenanceRetryAfterRef.current=0;void loadMaintenanceMovements();}} onDepartment={(department,view)=>{setMaintenanceSelectedDepartment(department);setMaintenanceView(view);setMaintenanceQuery("");setMaintenanceMovementFilter("ALL");}} onPanel={setWarehousePanel} onDocument={setDocumentToEdit}/>}
      {supportStage==="maintenance"&&!warehousePanel&&maintenanceArea==="WORKSHOP"&&(!isDedicatedMaintenancePortal||!!maintenanceSelectedDepartment)&&<WorkshopBoard department={maintenanceDepartment} onBack={()=>{setMaintenanceSelectedDepartment(null);if(!isDedicatedMaintenancePortal)setMaintenanceArea(null);}} onChanged={()=>{maintenanceRetryAfterRef.current=0;void loadMaintenanceMovements();}} onDocument={id=>{const item=maintenanceMovements.find(m=>m.id===id);if(item)setDocumentToEdit(item);else setTrackingCode(id);}}/>}
      {supportStage==="maintenance"&&!warehousePanel&&maintenanceArea&&["WAREHOUSE"].includes(maintenanceArea)&&(!isDedicatedMaintenancePortal||!!maintenanceSelectedDepartment)&&<section id="maintenance-inventory-destination" className={`maintenance-board maintenance-operations ${maintenanceArea.toLowerCase()}`}>
        <button className="maintenance-area-back" onClick={()=>{if(isTechnologyWarehouseRequester){setMaintenanceArea(null);setSupportStage("home");setMaintenanceView("dashboard");}else if(isDedicatedMaintenancePortal)setMaintenanceSelectedDepartment(null);else setMaintenanceArea(null);setMaintenanceQuery("");setMaintenanceMovementFilter("ALL");}}><ArrowLeft/> {isDedicatedMaintenancePortal?`Departamentos de ${maintenanceArea==="WORKSHOP"?"Taller":"Almacén"}`:isTechnologyWarehouseRequester?"Centro de soporte":"Taller y Almacén"}</button>
        {maintenanceLoadError&&<div className="maintenance-service-warning"><AlertTriangle/><div><strong>No fue posible sincronizar Mantenimiento</strong><span>{maintenanceLoadError}</span></div><button onClick={()=>{maintenanceRetryAfterRef.current=0;void loadMaintenanceMovements();}}><RefreshCw/> Reintentar</button></div>}
        <header>
          <div><span>{maintenanceArea==="WORKSHOP"?"TALLER TÉCNICO · REPARACIÓN Y REEMPLAZO":"ALMACÉN · INVENTARIO, DESPACHO Y DESCARGO"}</span><h2>{departments[maintenanceDepartment]|| (maintenanceArea==="WORKSHOP"?"Taller técnico":"Almacén central")}</h2><p>{maintenanceArea==="WORKSHOP"?"Entradas, salidas, diagnósticos, reparaciones y reemplazos con trazabilidad completa.":"Equipos nuevos, existencias, entregas a departamentos y descargos sin operaciones de reparación."}</p></div>
          <div className="maintenance-header-actions">
            {maintenanceArea==="WAREHOUSE"&&isWarehouseDepartmentRequester&&<button onClick={()=>setWarehouseRequestOpen(true)}><Send/> Solicitar a Almacén</button>}
            <button className="scanner" onClick={()=>openMaintenanceForm(true,maintenanceArea)}><QrCode/> Escanear QR o código</button><button onClick={()=>{if(isWarehouseDepartmentRequester)setWarehouseRequestOpen(true);else openMaintenanceForm(false,maintenanceArea,isTechnologyWarehouseRequester?"REQUEST":undefined);}}><Plus/> Nuevo formulario</button>
          </div>
        </header>
        <div className="maintenance-quick-actions">
          {(maintenanceArea==="WORKSHOP"?[["ENTRY","Recibir en Taller"],["REPAIR","Registrar reparación"],["REPLACEMENT","Registrar reemplazo"],["EXIT","Entregar reparado"]]:[["REQUEST","Solicitud de equipo"],["NEW_DELIVERY","Entregar equipo nuevo"],["DAMAGED_RETURN","Recibir equipo dañado"],["TRANSFER_TO_WORKSHOP","Entregar a Taller"],["ENTRY","Entrada de equipos"],["EXIT","Entregar / registrar salida"],["DISCHARGE","Registrar descargo"]]).map(([movement,label])=><button key={movement} onClick={()=>{if(movement==="REQUEST"&&isWarehouseDepartmentRequester)setWarehouseRequestOpen(true);else openMaintenanceForm(false,maintenanceArea,movement);}}><span>{movement==="ENTRY"?<Download/>:movement==="DISCHARGE"?<Trash2/>:movement==="TRANSFER_TO_WORKSHOP"?<Hammer/>:<ArrowRight/>}</span><strong>{label}</strong><small>{movement==="REQUEST"?"Varios equipos, componentes o devoluciones":"Formulario QR, hora y responsable"}</small></button>)}
        </div>
        <div className="maintenance-kpis">
          <article><span><Barcode/></span><div><small>ACTIVOS IDENTIFICADOS</small><strong>{maintenanceAssets.length}</strong><em>Seriales únicos en {maintenanceArea==="WORKSHOP"?"Taller":"Almacén"}</em></div></article>
          <article><span><Boxes/></span><div><small>DISPONIBLES / RECIBIDOS</small><strong>{activeMaintenanceAssets}</strong><em>Último movimiento activo</em></div></article>
          <article><span>{maintenanceArea==="WORKSHOP"?<Wrench/>:<Trash2/>}</span><div><small>{maintenanceArea==="WORKSHOP"?"INTERVENCIONES":"DESCARGOS"}</small><strong>{maintenanceArea==="WORKSHOP"?maintenanceRepairs:maintenanceAreaMovements.filter(item=>item.movementType==="DISCHARGE").length}</strong><em>{maintenanceArea==="WORKSHOP"?"Reparaciones y componentes":"Bajas definitivas auditadas"}</em></div></article>
          <article><span><FileText/></span><div><small>FORMULARIOS</small><strong>{maintenanceAreaMovements.length+(isWarehouseDepartmentRequester?warehouseRequestHistoryCount:0)}</strong><em>PDF y QR verificables</em></div></article>
        </div>
        <div className="maintenance-command-bar">
          <nav aria-label="Vista de mantenimiento">{!isTechnologyWarehouseRequester&&<><button className={maintenanceView==="dashboard"?"active":""} onClick={()=>setMaintenanceView("dashboard")}><BarChart3/> Resumen</button><button className={maintenanceView==="inventory"?"active":""} onClick={()=>setMaintenanceView("inventory")}><PackageSearch/> Inventario <b>{maintenanceAssets.length}</b></button>{maintenanceArea==="WAREHOUSE"&&<button className={maintenanceView==="procurement"?"active":""} onClick={()=>setMaintenanceView("procurement")}><ShoppingCart/> Compras y requisiciones <b>{maintenancePendingRequisitions.length}</b></button>}</>}<button className={maintenanceView==="history"?"active":""} onClick={()=>setMaintenanceView("history")}><History/> Formularios e historial <b>{maintenanceAreaMovements.length+(isWarehouseDepartmentRequester?warehouseRequestHistoryCount:0)}</b></button></nav>
          {maintenanceArea==="WAREHOUSE"&&maintenanceView==="inventory"&&(isWarehouseOperator||session.role==="Administrator")&&<><input ref={maintenanceInventoryFileRef} hidden type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={event=>{const file=event.target.files?.[0];if(file)void readMaintenanceInventoryExcel(file);}}/><button type="button" className="maintenance-excel-import" onClick={()=>maintenanceInventoryFileRef.current?.click()}><Upload/> Importar Excel</button></>}
          <label><Search/><input value={maintenanceQuery} onChange={event=>setMaintenanceQuery(event.target.value)} placeholder="Buscar serial, formulario, producto, agencia o responsable..."/>{maintenanceQuery&&<button type="button" onClick={()=>setMaintenanceQuery("")} aria-label="Limpiar búsqueda"><X/></button>}</label>
        </div>
        {maintenanceView==="dashboard"?<section className="maintenance-operational-dashboard">
          <header><div><span>{maintenanceArea==="WAREHOUSE"?"CONTROL DE ABASTECIMIENTO E INVENTARIO":"CONTROL DE ÓRDENES DE TALLER"}</span><h3>{maintenanceArea==="WAREHOUSE"?"Resumen operativo de Almacén":"Resumen operativo de Taller"}</h3><p>{departments[maintenanceDepartment]||maintenanceDepartment} · información actualizada con cada movimiento guardado.</p></div><button type="button" onClick={()=>void loadMaintenanceMovements()}><RefreshCw/> Actualizar</button></header>
          <section className="maintenance-document-library" aria-labelledby="maintenance-document-library-title"><header><div><span>ARCHIVOS Y COMUNICACIONES</span><h3 id="maintenance-document-library-title">Normas, formularios y plantillas</h3><p>Consulta los archivos y la biblioteca institucional de REAL / Grupo Tejeda.</p></div><button onClick={()=>setWarehousePanel("files")}><FileText/> Abrir archivos y biblioteca</button></header></section>
          <div className="maintenance-dashboard-kpis">
            <article className="orders"><span><ShoppingCart/></span><small>{maintenanceArea==="WAREHOUSE"?"ÓRDENES ABIERTAS":"ÓRDENES RECIBIDAS"}</small><strong>{maintenanceArea==="WAREHOUSE"?maintenanceOpenOrders.length:maintenanceAreaMovements.filter(item=>item.movementType==="TRANSFER_TO_WORKSHOP").length}</strong><em>{maintenanceArea==="WAREHOUSE"?"Pendientes de recepción total":"Envíos documentados desde Almacén"}</em></article>
            <article className="critical"><span><AlertTriangle/></span><small>{maintenanceArea==="WAREHOUSE"?"STOCK CRÍTICO":"PENDIENTES DE TALLER"}</small><strong>{maintenanceArea==="WAREHOUSE"?maintenanceCriticalStock.length:maintenanceWorkshopPending}</strong><em>{maintenanceArea==="WAREHOUSE"?"Artículos con 3 unidades o menos":"Equipos aún sin reparación posterior"}</em></article>
            <article><span><ClipboardList/></span><small>{maintenanceArea==="WAREHOUSE"?"REQUISICIONES":"REPARACIONES"}</small><strong>{maintenanceArea==="WAREHOUSE"?maintenancePendingRequisitions.length:maintenanceRepairs}</strong><em>{maintenanceArea==="WAREHOUSE"?"Pendientes, ordenadas o parciales":"Intervenciones registradas"}</em></article>
            <article><span><PackageCheck/></span><small>RECEPCIONES</small><strong>{maintenanceArea==="WAREHOUSE"?maintenancePhysicalReceipts.length:maintenanceAreaMovements.filter(item=>item.movementType==="ENTRY").length}</strong><em>{maintenanceArea==="WAREHOUSE"?"Entradas físicas contra OC":"Equipos ingresados al Taller"}</em></article>
          </div>
          {maintenanceArea==="WAREHOUSE"&&<div className="maintenance-dashboard-actions"><button type="button" onClick={()=>openMaintenanceProcurement("requisition")}><ClipboardList/><span><strong>Nueva requisición</strong><small>Solicitar un artículo faltante</small></span></button><button type="button" onClick={()=>openMaintenanceProcurement("order")}><ShoppingCart/><span><strong>Nueva orden de compra</strong><small>Asociar requisición y proveedor</small></span></button><button type="button" onClick={()=>{setMaintenanceView("procurement");}}><Truck/><span><strong>Recepciones y compras</strong><small>Órdenes, proveedores y devoluciones</small></span></button></div>}
          <div className="maintenance-dashboard-grid">
            {maintenanceArea==="WAREHOUSE"?<section><header><span><BarChart3/></span><div><strong>Artículos de mayor consumo</strong><small>Salidas, entregas y transferencias acumuladas</small></div></header><div className="maintenance-consumption-list">{(maintenanceHighConsumption.length?maintenanceHighConsumption:[{name:"Sin consumo registrado",stock:0,consumption:0}]).map((item,index)=><article key={item.name}><b>{index+1}</b><span><strong>{item.name}</strong><small>Existencia calculada: {item.stock}</small></span><em>{item.consumption} consumidas</em></article>)}</div></section>:<section><header><span><Wrench/></span><div><strong>Actividad de Taller</strong><small>Intervenciones registradas por tipo</small></div></header><div className="maintenance-consumption-list">{["ENTRY","REPAIR","REPLACEMENT","COMPONENT_REPLACEMENT","EXIT"].map((type,index)=><article key={type}><b>{index+1}</b><span><strong>{maintenanceMovementLabels[type]}</strong><small>Movimientos del departamento</small></span><em>{maintenanceAreaMovements.filter(item=>item.movementType===type).length}</em></article>)}</div></section>}
            <section><header><span>{maintenanceArea==="WAREHOUSE"?<ShoppingCart/>:<Hammer/>}</span><div><strong>{maintenanceArea==="WAREHOUSE"?"Órdenes de compra recientes":"Órdenes recientes de Taller"}</strong><small>Últimas operaciones registradas</small></div></header><div className="maintenance-recent-list">{(maintenanceArea==="WAREHOUSE"?maintenanceDepartmentOrders.slice(0,6):maintenanceAreaMovements.filter(item=>["TRANSFER_TO_WORKSHOP","REPAIR","EXIT"].includes(item.movementType)).slice(0,6)).map(item=>"orderNumber" in item?<article key={item.id}><span><strong>{item.orderNumber}</strong><small>{item.supplierName} · {item.productName}</small></span><em className={item.status.toLowerCase()}>{item.quantityReceived}/{item.quantityOrdered} · {item.status}</em></article>:<article key={item.id}><span><strong>{item.documentNumber}</strong><small>{item.equipmentType} · {item.serialNumber}</small></span><em>{maintenanceMovementLabels[item.movementType]}</em></article>)}{!(maintenanceArea==="WAREHOUSE"?maintenanceDepartmentOrders:maintenanceAreaMovements).length&&<p>Sin órdenes registradas todavía.</p>}</div></section>
            <section><header><span><AlertTriangle/></span><div><strong>{maintenanceArea==="WAREHOUSE"?"Stock crítico":"Equipos pendientes de intervención"}</strong><small>Atención prioritaria</small></div></header><div className="maintenance-critical-list">{maintenanceArea==="WAREHOUSE"?maintenanceCriticalStock.slice(0,8).map(item=><article key={item.name}><span><strong>{item.name}</strong><small>Punto crítico: 3 unidades</small></span><b>{item.stock}</b></article>):[...maintenanceAssetMap.values()].filter(item=>item.movementType==="TRANSFER_TO_WORKSHOP").slice(0,8).map(item=><article key={item.id}><span><strong>{item.equipmentType}</strong><small>{item.serialNumber} · {item.documentNumber}</small></span><b>Pendiente</b></article>)}</div></section>
            <section><header><span><PackageCheck/></span><div><strong>{maintenanceArea==="WAREHOUSE"?"Recepciones recientes":"Reparaciones recientes"}</strong><small>Cadena de custodia y responsable</small></div></header><div className="maintenance-recent-list">{(maintenanceArea==="WAREHOUSE"?maintenancePhysicalReceipts:maintenanceAreaMovements.filter(item=>["REPAIR","REPLACEMENT","COMPONENT_REPLACEMENT"].includes(item.movementType))).slice(0,6).map(item=><article key={item.id}><span><strong>{item.equipmentType}</strong><small>{item.documentNumber} · {item.receivedByName||item.technicianName}</small></span><em>{new Date(item.occurredAt).toLocaleDateString("es-DO")}</em></article>)}</div></section>
          </div>
        </section>:maintenanceView==="inventory"?<div className="maintenance-inventory-wrap">
          <table className="maintenance-inventory-table"><thead><tr><th>Activo</th><th>Código / serial</th><th>Ubicación actual</th><th>Estado</th><th>Responsable</th><th>Último movimiento</th><th>Comprobante</th></tr></thead><tbody>{visibleMaintenanceAssets.map(({serial,movement,status})=><tr key={serial}><td><strong>{movement.equipmentType}</strong><small>{movement.componentType||departments[movement.department]||movement.department}</small></td><td><code>{serial}</code><small>{movement.documentNumber}</small></td><td><strong>{movement.codigo}</strong><small>{movement.destinationName||`${movement.terminal} · ${movement.grupo}`}</small></td><td><span className={`maintenance-asset-status ${status.toLowerCase()}`}>{status==="ACTIVE"?"Disponible / recibido":status==="DISCHARGED"?"Descargado":"Salida registrada"}</span></td><td>{movement.technicianName}<small>{movement.receivedByName&&`Recibió: ${movement.receivedByName}`}</small>{movement.receivedByLogin&&<small>Login: {movement.receivedByLogin.replace(/@grupotejeda\.local$/i,"")}</small>}</td><td><strong>{maintenanceMovementLabels[movement.movementType]||movement.movementType}</strong><small>{new Date(movement.occurredAt||movement.createdAt).toLocaleString("es-DO")}</small></td><td><button className="maintenance-pdf-button" onClick={()=>void downloadMaintenanceForm(movement)}><Download/> PDF institucional + QR</button></td></tr>)}</tbody></table>
          {!visibleMaintenanceAssets.length&&<div className="maintenance-empty"><ScanBarcode/><strong>{maintenanceQuery?"No encontramos ese equipo":"Todavía no hay equipos identificados"}</strong><span>{maintenanceQuery?"Verifica el código, formulario o responsable.":"Escanea un código de barras o QR para registrar el primer activo."}</span></div>}
        </div>:maintenanceView==="procurement"?<section className="maintenance-procurement-board">
          <header><div><span>ABASTECIMIENTO · COMPRAS · RECEPCIÓN FÍSICA</span><h3>Requisiciones y órdenes de compra</h3><p>Convierte faltantes en órdenes, recibe mercancía contra la OC y conserva proveedores y devoluciones auditables.</p></div><div><button type="button" onClick={()=>openMaintenanceProcurement("supplier")}><Plus/> Proveedor</button><button type="button" onClick={()=>openMaintenanceProcurement("requisition")}><ClipboardList/> Requisición</button><button type="button" className="primary" onClick={()=>openMaintenanceProcurement("order")}><ShoppingCart/> Orden de compra</button></div></header>
          <div className="maintenance-procurement-summary"><article><strong>{maintenancePendingRequisitions.length}</strong><span>Requisiciones pendientes</span></article><article><strong>{maintenanceOpenOrders.length}</strong><span>Órdenes por recibir</span></article><article><strong>{maintenanceProcurement.suppliers.length}</strong><span>Proveedores activos</span></article><article><strong>{maintenanceProcurement.returns.filter(item=>item.department===maintenanceDepartment).length}</strong><span>Devoluciones</span></article></div>
          <section className="maintenance-procurement-section"><header><div><strong>Requisiciones pendientes</strong><small>Faltantes que pueden asociarse a una orden de compra</small></div></header><div className="maintenance-procurement-cards">{maintenancePendingRequisitions.map(item=><article key={item.id}><span className={`priority ${item.priority.toLowerCase()}`}>{item.priority}</span><h4>{item.productName}</h4><p>{item.requisitionNumber} · solicitó {item.requestedByName}</p><dl><div><dt>Solicitado</dt><dd>{item.quantityRequested}</dd></div><div><dt>Cubierto</dt><dd>{item.quantityFulfilled}</dd></div><div><dt>Estado</dt><dd>{item.status}</dd></div></dl><button type="button" disabled={!["APPROVED","ORDERED","PARTIAL"].includes(item.status)} onClick={()=>openMaintenanceProcurement("order",item.id)}><ShoppingCart/> Crear orden asociada</button></article>)}{!maintenancePendingRequisitions.length&&<div className="maintenance-empty"><CheckCircle2/><strong>Sin requisiciones pendientes</strong><span>Los faltantes de este departamento están cubiertos.</span></div>}</div></section>
          <section className="maintenance-procurement-section"><header><div><strong>Órdenes de compra recientes</strong><small>Recepción física comparada con la cantidad ordenada</small></div></header><div className="maintenance-order-table"><table><thead><tr><th>Orden</th><th>Proveedor</th><th>Artículo</th><th>Ordenado</th><th>Recibido</th><th>Estado</th><th>Acción</th></tr></thead><tbody>{maintenanceDepartmentOrders.map(item=><tr key={item.id}><td><strong>{item.orderNumber}</strong><small>{new Date(item.createdAt).toLocaleDateString("es-DO")}</small></td><td>{item.supplierName}</td><td>{item.productName}</td><td>{item.quantityOrdered}</td><td>{item.quantityReceived}</td><td><span className={`order-status ${item.status.toLowerCase()}`}>{item.status}</span></td><td>{!["RECEIVED","CANCELLED"].includes(item.status)?<button type="button" onClick={()=>openMaintenanceProcurement("receive",item.id)}><Truck/> Recibir mercancía</button>:<CheckCircle2/>}</td></tr>)}</tbody></table></div></section>
          <div className="maintenance-procurement-columns"><section className="maintenance-procurement-section"><header><div><strong>Proveedores</strong><small>Directorio comercial activo</small></div></header><div className="maintenance-supplier-list">{maintenanceProcurement.suppliers.map(item=><article key={item.id}><span><Building2/></span><div><strong>{item.name}</strong><small>{item.taxId||"Sin RNC"} · {item.contactName||"Sin contacto"}</small><em>{item.phone||item.email||"Contacto pendiente"}</em></div></article>)}</div></section><section className="maintenance-procurement-section"><header><div><strong>Devoluciones</strong><small>Mercancía devuelta a proveedores</small></div><button type="button" onClick={()=>openMaintenanceProcurement("return")}><RotateCcw/> Registrar</button></header><div className="maintenance-return-list">{maintenanceProcurement.returns.filter(item=>item.department===maintenanceDepartment).slice(0,8).map(item=><article key={item.id}><span><strong>{item.productName}</strong><small>{item.returnNumber} · {item.supplierName||"Sin proveedor"}</small></span><em>{item.quantity} · {item.reason}</em></article>)}</div></section></div>
        </section>:<>
          {maintenanceArea==="WAREHOUSE"&&isWarehouseDepartmentRequester&&["ALL","REQUEST"].includes(maintenanceMovementFilter)&&<SentWarehouseRequestHistory department={maintenanceDepartment} query={maintenanceQuery}/>}
          <div className="maintenance-history-filters">{[["ALL","Todos"],...Object.entries(maintenanceMovementLabels).filter(([value])=>maintenanceArea==="WAREHOUSE"?["ENTRY","REQUEST","NEW_DELIVERY","DAMAGED_RETURN","TRANSFER_TO_WORKSHOP","EXIT","DISCHARGE"].includes(value):["ENTRY","TRANSFER_TO_WORKSHOP","REPAIR","REPLACEMENT","COMPONENT_REPLACEMENT","EXIT"].includes(value))].map(([value,label])=><button key={value} className={maintenanceMovementFilter===value?"active":""} onClick={()=>setMaintenanceMovementFilter(value)}>{label}<b>{(value==="ALL"?maintenanceAreaMovements.length:maintenanceAreaMovements.filter(item=>item.movementType===value).length)+(isWarehouseDepartmentRequester&&(value==="ALL"||value==="REQUEST")?warehouseRequestHistoryCount:0)}</b></button>)}</div>
          <div className="maintenance-list">{visibleMaintenanceMovements.map(item=><article key={item.id}><header><span>{item.operationalArea==="WORKSHOP"?<Hammer/>:<Warehouse/>}<i>{maintenanceMovementLabels[item.movementType]||item.movementType}</i></span><time>{new Date(item.occurredAt||item.createdAt).toLocaleString("es-DO")}</time></header><div className="maintenance-document-number"><QrCode/><b>{item.documentNumber}</b></div><h3>{item.equipmentType}{item.componentType?` · ${item.componentType}`:""}</h3><p>{item.codigo} · {item.terminal} · {item.grupo}{item.destinationName?` · ${item.destinationName}`:""}</p>{item.serialNumber&&<code><Barcode/>{item.serialNumber}</code>}<dl><div><dt>Motivo</dt><dd>{item.failureCause}</dd></div><div><dt>Responsable</dt><dd>{item.technicianName}</dd></div><div><dt>Entregó</dt><dd>{item.deliveredByName||"No registrado"}</dd></div><div><dt>Recibió</dt><dd>{item.receivedByName||"No registrado"}{item.receivedByLogin&&<small>Login: {item.receivedByLogin.replace(/@grupotejeda\.local$/i,"")}</small>}</dd></div><div><dt>Cantidad</dt><dd>{item.quantity}</dd></div><div><dt>Firmas</dt><dd>Consultar entrega y recepción en PDF</dd></div></dl>{item.notes&&<small>{item.notes}</small>}<footer><span>Registrado por {item.createdByName}</span><button onClick={()=>void downloadMaintenanceForm(item)}><Download/> Descargar comprobante PDF</button></footer></article>)}{!visibleMaintenanceMovements.length&&<div className="maintenance-empty"><Search/><strong>No hay formularios con este criterio</strong><span>Prueba con otro serial, documento, agencia o movimiento.</span></div>}</div>
        </>}
      </section>}
      {maintenanceInventoryImportOpen&&maintenanceInventoryImport&&<div className="ticket-modal-backdrop"><section className="ticket-modal maintenance-inventory-import-modal" role="dialog" aria-modal="true" aria-labelledby="inventory-import-title"><header><div><span>ALMACÉN · IMPORTACIÓN CONTROLADA</span><h2 id="inventory-import-title">Importar inventario desde Excel</h2><p>{maintenanceInventoryImport.fileName} · revisa la vista previa antes de registrar los equipos.</p></div><button type="button" disabled={saving} onClick={()=>{setMaintenanceInventoryImportOpen(false);setMaintenanceInventoryImport(null);}} aria-label="Cerrar"><X/></button></header><div className="maintenance-import-guide"><FileText/><div><strong>Formato del archivo</strong><p>La primera fila debe incluir: Departamento, Equipo y Serial. También puedes agregar Cantidad, Ubicación y Notas.</p></div></div><div className="maintenance-import-summary"><article><strong>{maintenanceInventoryImport.rows.length}</strong><span>Listos para importar</span></article><article><strong>{maintenanceInventoryImport.duplicates.length}</strong><span>Duplicados omitidos</span></article><article className={maintenanceInventoryImport.errors.length?"error":"ok"}><strong>{maintenanceInventoryImport.errors.length}</strong><span>Errores por corregir</span></article></div>{maintenanceInventoryImport.errors.length>0&&<div className="maintenance-import-errors" role="alert"><strong>Corrige el Excel antes de continuar</strong>{maintenanceInventoryImport.errors.slice(0,12).map(message=><span key={message}>{message}</span>)}</div>}{maintenanceInventoryImport.duplicates.length>0&&<details className="maintenance-import-duplicates"><summary>Ver seriales duplicados omitidos</summary>{maintenanceInventoryImport.duplicates.slice(0,30).map(message=><span key={message}>{message}</span>)}</details>}<div className="maintenance-import-preview"><table><thead><tr><th>Fila</th><th>Departamento</th><th>Equipo</th><th>Serial</th><th>Cantidad</th><th>Ubicación</th></tr></thead><tbody>{maintenanceInventoryImport.rows.slice(0,50).map(row=><tr key={`${row.rowNumber}-${row.serialNumber}`}><td>{row.rowNumber}</td><td>{departments[row.department]||row.department}</td><td>{row.equipmentType}</td><td><code>{row.serialNumber}</code></td><td>{row.quantity}</td><td>{row.destinationName||"Almacén central"}</td></tr>)}</tbody></table>{maintenanceInventoryImport.rows.length>50&&<p>Se muestran 50 de {maintenanceInventoryImport.rows.length} filas válidas.</p>}</div>{saving&&<div className="maintenance-import-progress"><span style={{width:`${Math.round(maintenanceInventoryImportProgress/Math.max(1,maintenanceInventoryImport.rows.length)*100)}%`}}/><small>Importando {maintenanceInventoryImportProgress} de {maintenanceInventoryImport.rows.length} equipos…</small></div>}<footer><button type="button" disabled={saving} onClick={()=>{setMaintenanceInventoryImportOpen(false);setMaintenanceInventoryImport(null);}}>Cancelar</button><button type="button" className="primary" disabled={saving||!!maintenanceInventoryImport.errors.length||!maintenanceInventoryImport.rows.length} onClick={()=>void importMaintenanceInventory()}>{saving?<LoaderCircle className="spin"/>:<Upload/>}{saving?" Importando…":` Importar ${maintenanceInventoryImport.rows.length} equipos`}</button></footer></section></div>}
      {warehouseRequestOpen&&<WarehouseRequestComposer session={session} department={maintenanceDepartment} agencies={agencies} recipients={technicians} equipmentOptions={[...new Set([...maintenanceDepartmentCatalog.equipment,...maintenanceProducts.filter(item=>item.department===maintenanceDepartment).map(item=>item.productName)])]} componentOptions={[...new Set([...maintenanceDepartmentCatalog.components,...maintenanceProducts.filter(item=>item.department===maintenanceDepartment&&item.componentType).map(item=>item.componentType!).filter(Boolean)])]} onClose={()=>setWarehouseRequestOpen(false)} onSaved={number=>{setWarehouseRequestOpen(false);setWarehouseRequestHistoryCount(count=>count+1);setMaintenanceView("history");setMaintenanceMovementFilter("REQUEST");setMaintenanceQuery(number);setNotice(`Solicitud ${number} guardada en Formularios e historial y enviada a Pendientes de Almacén.`);}}/>}
      {maintenanceFormOpen&&<div className="ticket-modal-backdrop"><section className={`ticket-modal maintenance-modal maintenance-document-modal${maintenanceScannerActive?" scanner-mode":""}`}><header><div><span>{maintenanceScannerActive?"LECTOR 2D USB ACTIVO":"FORMULARIO OPERATIVO"} · {maintenanceDraft.operationalArea==="WORKSHOP"?"TALLER":"ALMACÉN"}</span><h2>{maintenanceScannerActive?"Escanear producto o formulario QR":maintenanceMovementLabels[maintenanceDraft.movementType]||"Registrar movimiento"}</h2><p>{maintenanceDraft.movementType==="REQUEST"?`Documento ${maintenanceDraft.documentNumber} · Solicita el equipo para una agencia y asigna el técnico que lo instalará.`:`Documento ${maintenanceDraft.documentNumber} · Dos firmas independientes: quien entrega y quien recibe.`}</p></div><button type="button" onClick={closeMaintenanceForm} aria-label="Cerrar"><X/></button></header><form noValidate onSubmit={event=>{event.preventDefault();void createMaintenanceMovement();}}>
        {maintenanceFormError&&<div className="maintenance-form-error" role="alert"><AlertTriangle/><span><strong>No se pudo guardar todavía</strong><small>{maintenanceFormError}</small></span></div>}
        {maintenanceScannerActive&&<section className={`maintenance-scanner-console ${maintenanceScanFeedback?.kind||"ready"}`}><div className="maintenance-scanner-beam"><QrCode/></div><label><span>Código QR, código de barras o serial</span><input ref={maintenanceSerialInputRef} data-maintenance-scanner="true" autoFocus autoComplete="off" maxLength={200} value={maintenanceDraft.serialNumber} onFocus={event=>event.currentTarget.select()} onChange={event=>setMaintenanceDraft(current=>({...current,serialNumber:event.target.value}))} onKeyDown={event=>{if(event.key!=="Enter")return;event.preventDefault();applyMaintenanceScan(event.currentTarget.value);}} placeholder="Haz clic aquí y escanea; el lector debe enviar Enter"/></label><small>{maintenanceScanFeedback?.message}</small></section>}
        {maintenanceScannedMovement&&<section className="maintenance-scanned-record"><header><ShieldCheck/><div><strong>Expediente localizado</strong><span>{maintenanceScannedMovement.documentNumber} · {maintenanceMovementLabels[maintenanceScannedMovement.movementType]}</span></div></header><dl><div><dt>Producto</dt><dd>{maintenanceScannedMovement.equipmentType}</dd></div><div><dt>Serial</dt><dd>{maintenanceScannedMovement.serialNumber}</dd></div><div><dt>Ubicación</dt><dd>{maintenanceScannedMovement.codigo} · {maintenanceScannedMovement.terminal}</dd></div><div><dt>Responsable</dt><dd>{maintenanceScannedMovement.technicianName}</dd></div><div><dt>Fecha y hora</dt><dd>{new Date(maintenanceScannedMovement.occurredAt).toLocaleString("es-DO")}</dd></div><div><dt>Entregó / recibió</dt><dd>{maintenanceScannedMovement.deliveredByName||"—"} / {maintenanceScannedMovement.receivedByName||"—"}</dd></div></dl></section>}
        {maintenanceScannerActive&&<section className="maintenance-autofill-note"><ScanBarcode/><span><strong>Datos cargados por el escáner</strong><small>Producto, serial, fecha y hora se completan automáticamente. Puedes corregir cualquiera de estos campos antes de guardar.</small></span></section>}
        <div className="maintenance-form-section"><h3><FileText/> Identificación del formulario</h3><div className="ticket-form-grid"><label>Número de formulario<input readOnly value={maintenanceDraft.documentNumber}/></label><label>Fecha y hora efectiva<input required type="datetime-local" value={maintenanceDraft.occurredAt} onChange={event=>setMaintenanceDraft({...maintenanceDraft,occurredAt:event.target.value})}/></label><label>Área operativa<input readOnly value={maintenanceDraft.operationalArea==="WORKSHOP"?"Taller":"Almacén"}/></label><label>Movimiento<select value={maintenanceDraft.movementType} onChange={event=>setMaintenanceDraft({...maintenanceDraft,movementType:event.target.value,failureCause:""})}>{maintenanceAvailableMovements.map(value=><option value={value} key={value}>{maintenanceMovementLabels[value]}</option>)}</select></label></div></div>
        <div className="maintenance-form-section"><h3><Boxes/> Producto, origen y destino</h3><div className="ticket-form-grid"><label>{maintenanceDraft.movementType==="REQUEST"?"Agencia que requiere el equipo (obligatoria)":`Agencia ${maintenanceDraft.operationalArea==="WAREHOUSE"&&maintenanceDraft.movementType==="ENTRY"?"(opcional)":""}`}<select required={maintenanceDraft.movementType==="REQUEST"} value={maintenanceDraft.agencyId} onChange={event=>setMaintenanceDraft({...maintenanceDraft,agencyId:event.target.value})}><option value="">{maintenanceDraft.movementType==="REQUEST"?"Seleccionar agencia solicitante":maintenanceDraft.operationalArea==="WAREHOUSE"?"Almacén central / sin agencia":"Seleccionar agencia"}</option>{agencies.map(agency=><option value={agency.id} key={agency.id}>{agency.codigo} · {agency.terminal} · {agency.grupo}</option>)}</select></label><label>Departamento o destino<input maxLength={200} value={maintenanceDraft.destinationName} onChange={event=>setMaintenanceDraft({...maintenanceDraft,destinationName:event.target.value})} placeholder={maintenanceDraft.movementType==="REQUEST"?"Destino final en la agencia seleccionada":"Ej. Tecnología, agencia 1038 o almacén central"}/></label><label>Nombre del equipo o producto<input required list="maintenance-product-catalog" maxLength={100} value={maintenanceDraft.equipmentType} onChange={event=>setMaintenanceDraft({...maintenanceDraft,equipmentType:event.target.value,componentType:""})} placeholder={maintenanceDraft.movementType==="REQUEST"?"Equipo que necesita la agencia":"Se completa al reconocer el código; también puedes escribirlo"}/><datalist id="maintenance-product-catalog">{[...new Set([...maintenanceDepartmentCatalog.equipment,...maintenanceProducts.filter(item=>item.department===maintenanceDepartment).map(item=>item.productName)])].map(value=><option value={value} key={value}/>)}</datalist><small className="maintenance-product-learning">{maintenanceDraft.movementType==="REQUEST"?"Almacén asignará el equipo físico y su serial al preparar la entrega.":`El escáner completa el nombre; puedes corregirlo. El sistema guardará la relación con el código ${maintenanceDraft.serialNumber||"escaneado"}.`}</small></label>{maintenanceDraft.operationalArea==="WORKSHOP"&&<label>Componente<select value={maintenanceDraft.componentType} onChange={event=>setMaintenanceDraft({...maintenanceDraft,componentType:event.target.value})}><option value="">Equipo completo / no aplica</option>{maintenanceDepartmentCatalog.components.map(value=><option key={value}>{value}</option>)}</select></label>}<label>{maintenanceDraft.movementType==="REQUEST"?"Serial o código (opcional; lo completa Almacén)":"Serial o código"}<input required={maintenanceDraft.movementType!=="REQUEST"} maxLength={120} value={maintenanceDraft.serialNumber} onChange={event=>setMaintenanceDraft({...maintenanceDraft,serialNumber:event.target.value})} placeholder={maintenanceDraft.movementType==="REQUEST"?"Pendiente de asignación por Almacén":"El escáner lo completa; también puedes corregirlo"}/></label><label>Cantidad<input type="number" min="1" max="1000" value={maintenanceDraft.quantity} onChange={event=>setMaintenanceDraft({...maintenanceDraft,quantity:Number(event.target.value)})}/></label><label>Motivo<select required value={maintenanceDraft.failureCause} onChange={event=>setMaintenanceDraft({...maintenanceDraft,failureCause:event.target.value})}><option value="">Seleccionar motivo</option>{maintenanceDraft.failureCause&&!maintenanceOperationalCauses.includes(maintenanceDraft.failureCause)&&<option value={maintenanceDraft.failureCause}>{maintenanceDraft.failureCause}</option>}{maintenanceOperationalCauses.map(value=><option key={value}>{value}</option>)}</select></label></div></div>
        <div className="maintenance-form-section"><h3><UserRoundCheck/> Custodia y responsables</h3><div className="ticket-form-grid"><label>{maintenanceDraft.movementType==="REQUEST"?"Técnico que instalará el equipo (obligatorio)":`Responsable asignado ${(["NEW_DELIVERY","EXIT"].includes(maintenanceDraft.movementType))?"(obligatorio)":""}`}<select required={["REQUEST","NEW_DELIVERY","EXIT"].includes(maintenanceDraft.movementType)} value={maintenanceDraft.technicianUserId} onChange={event=>{const tech=technicians.find(item=>item.id===event.target.value);setMaintenanceDraft({...maintenanceDraft,technicianUserId:event.target.value,technicianName:tech?.name||session.displayName});}}><option value="">{maintenanceDraft.movementType==="REQUEST"?"Seleccionar técnico instalador":"Seleccionar técnico o supervisor"}</option><optgroup label={`Técnicos de ${departments[maintenanceDepartment]||maintenanceDepartment}`}>{technicians.filter(item=>item.isTechnician&&item.department===maintenanceDepartment).map(item=><option value={item.id} key={item.id}>{item.name}</option>)}</optgroup><optgroup label="Supervisores">{technicians.filter(isSupervisorAssignee).map(item=><option value={item.id} key={item.id}>{item.name} · Supervisor</option>)}</optgroup></select></label>{maintenanceDraft.movementType!=="REQUEST"&&<><label>Nombre de quien entrega<input required maxLength={160} value={maintenanceDraft.deliveredByName} onChange={event=>setMaintenanceDraft({...maintenanceDraft,deliveredByName:event.target.value})} placeholder="Persona que entrega el equipo"/></label><label>Nombre de quien recibe<input required maxLength={160} value={maintenanceDraft.receivedByName} onChange={event=>setMaintenanceDraft({...maintenanceDraft,receivedByName:event.target.value})} placeholder="Persona que recibe el equipo"/></label></>}</div></div>
        <div className="maintenance-form-section maintenance-signature-section">{maintenanceDraft.movementType==="REQUEST"?<><h3><Send/> Ruta de la solicitud</h3><p>Al guardar, Almacén recibirá la solicitud con la agencia, el equipo requerido, la cantidad y el técnico instalador. La firma de entrega se completará cuando Almacén despache el equipo.</p><div className="mw-signature-summary"><article><strong>Origen</strong><span>Tecnología</span></article><article><strong>Destino</strong><span>Almacén tecnológico</span></article></div></>:<><h3><PenTool/> Firmas de entrega y recepción</h3><p>Primero guarda el movimiento. Se abrirá el formulario PDF con dos botones de firma independientes. Cada persona firma con su cuenta y puede usar su firma una vez o guardarla.</p><div className="mw-signature-summary"><article><strong>Firma de quien entrega</strong><span>{maintenanceDraft.deliveredByName||"Indica quién entrega"}</span></article><article><strong>Firma de quien recibe</strong><span>{maintenanceDraft.receivedByName||"Indica quién recibe"}</span></article></div></>}</div>
        <label>Notas y condición del equipo<textarea maxLength={2000} value={maintenanceDraft.notes} onChange={event=>setMaintenanceDraft({...maintenanceDraft,notes:event.target.value})} placeholder="Condición al recibir, trabajo realizado, accesorios, destino o detalles de la entrega"/></label>
        <footer><button type="button" onClick={closeMaintenanceForm}>Cancelar</button><span><QrCode/> El PDF incluirá un QR verificable</span><button type="button" className="primary" disabled={saving} onClick={()=>void createMaintenanceMovement()}>{saving?<LoaderCircle className="spin"/>:<FileText/>} {saving?"Guardando...":"Guardar formulario"}</button></footer>
      </form></section></div>}
      {maintenanceProcurementMode&&<div className="ticket-modal-backdrop"><section className="ticket-modal maintenance-procurement-modal" role="dialog" aria-modal="true"><header><div><span>ALMACÉN · FLUJO DE ABASTECIMIENTO</span><h2>{maintenanceProcurementMode==="supplier"?"Registrar proveedor":maintenanceProcurementMode==="requisition"?"Nueva requisición":maintenanceProcurementMode==="order"?"Emitir orden de compra":maintenanceProcurementMode==="receive"?"Recepción física contra orden":"Registrar devolución"}</h2><p>{departments[maintenanceDepartment]||maintenanceDepartment} · La operación quedará asociada al usuario, fecha y documento.</p></div><button type="button" onClick={()=>setMaintenanceProcurementMode(null)} aria-label="Cerrar"><X/></button></header><form noValidate onSubmit={event=>{event.preventDefault();void saveMaintenanceProcurement();}}>
        {maintenanceProcurementError&&<div className="maintenance-form-error" role="alert"><AlertTriangle/><span><strong>No se pudo guardar todavía</strong><small>{maintenanceProcurementError}</small></span></div>}
        {maintenanceProcurementMode==="supplier"&&<div className="ticket-form-grid"><label>Nombre comercial<input autoFocus maxLength={160} value={maintenanceProcurementDraft.supplierName} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,supplierName:event.target.value})}/></label><label>RNC o identificación<input maxLength={40} value={maintenanceProcurementDraft.taxId} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,taxId:event.target.value})}/></label><label>Persona de contacto<input maxLength={120} value={maintenanceProcurementDraft.contactName} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,contactName:event.target.value})}/></label><label>Teléfono<input maxLength={50} value={maintenanceProcurementDraft.phone} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,phone:event.target.value})}/></label><label>Correo<input type="email" maxLength={160} value={maintenanceProcurementDraft.email} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,email:event.target.value})}/></label></div>}
        {maintenanceProcurementMode==="requisition"&&<div className="ticket-form-grid"><label>Departamento<input readOnly value={departments[maintenanceDepartment]||maintenanceDepartment}/></label><label>Artículo requerido<input autoFocus list="maintenance-product-catalog" maxLength={160} value={maintenanceProcurementDraft.productName} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,productName:event.target.value})}/></label><label>Cantidad<input type="number" min="1" max="100000" value={maintenanceProcurementDraft.quantity} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,quantity:Number(event.target.value)})}/></label><label>Prioridad<select value={maintenanceProcurementDraft.priority} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,priority:event.target.value})}><option value="LOW">Baja</option><option value="MEDIUM">Media</option><option value="HIGH">Alta</option><option value="CRITICAL">Crítica</option></select></label></div>}
        {maintenanceProcurementMode==="order"&&<div className="ticket-form-grid"><label>Proveedor<select autoFocus value={maintenanceProcurementDraft.supplierId} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,supplierId:event.target.value})}><option value="">Seleccionar proveedor</option>{maintenanceProcurement.suppliers.map(item=><option value={item.id} key={item.id}>{item.name}</option>)}</select></label><label>Requisición asociada<select value={maintenanceProcurementDraft.requisitionId} onChange={event=>{const req=maintenanceProcurement.requisitions.find(item=>item.id===event.target.value);setMaintenanceProcurementDraft({...maintenanceProcurementDraft,requisitionId:event.target.value,productName:req?.productName||maintenanceProcurementDraft.productName,quantity:req?Math.max(1,req.quantityRequested-req.quantityFulfilled):maintenanceProcurementDraft.quantity});}}><option value="">Orden sin requisición</option>{maintenancePendingRequisitions.filter(item=>["APPROVED","ORDERED","PARTIAL"].includes(item.status)).map(item=><option value={item.id} key={item.id}>{item.requisitionNumber} · {item.productName} · faltan {item.quantityRequested-item.quantityFulfilled}</option>)}</select></label><label>Artículo<input maxLength={160} value={maintenanceProcurementDraft.productName} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,productName:event.target.value})}/></label><label>Cantidad ordenada<input type="number" min="1" max="100000" value={maintenanceProcurementDraft.quantity} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,quantity:Number(event.target.value)})}/></label><label>Costo unitario RD$<input type="number" min="0" step="0.01" value={maintenanceProcurementDraft.unitCost} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,unitCost:Number(event.target.value)})}/></label><label>Entrega esperada<input type="date" value={maintenanceProcurementDraft.expectedAt} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,expectedAt:event.target.value})}/></label></div>}
        {maintenanceProcurementMode==="receive"&&(()=>{const order=maintenanceProcurement.purchaseOrders.find(item=>item.id===maintenanceProcurementTarget);return <><div className="maintenance-receipt-order"><ShoppingCart/><span><strong>{order?.orderNumber}</strong><small>{order?.supplierName} · {order?.productName}</small></span><b>Pendiente: {(order?.quantityOrdered||0)-(order?.quantityReceived||0)}</b></div><div className="ticket-form-grid"><label>Cantidad física recibida<input autoFocus type="number" min="1" max={Math.max(1,(order?.quantityOrdered||0)-(order?.quantityReceived||0))} value={maintenanceProcurementDraft.quantity} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,quantity:Number(event.target.value)})}/></label><label>Serial, lote o código físico<input maxLength={120} value={maintenanceProcurementDraft.serialNumber} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,serialNumber:event.target.value})} placeholder="Escanea o escribe el lote recibido"/></label><label>Recibido por<input maxLength={160} value={maintenanceProcurementDraft.receivedByName} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,receivedByName:event.target.value})}/></label></div></>})()}
        {maintenanceProcurementMode==="return"&&<div className="ticket-form-grid"><label>Orden relacionada<select value={maintenanceProcurementTarget} onChange={event=>{const order=maintenanceProcurement.purchaseOrders.find(item=>item.id===event.target.value);setMaintenanceProcurementTarget(event.target.value);setMaintenanceProcurementDraft({...maintenanceProcurementDraft,supplierId:order?.supplierId||"",productName:order?.productName||""});}}><option value="">Sin orden asociada</option>{maintenanceDepartmentOrders.map(item=><option value={item.id} key={item.id}>{item.orderNumber} · {item.productName}</option>)}</select></label><label>Proveedor<select value={maintenanceProcurementDraft.supplierId} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,supplierId:event.target.value})}><option value="">Sin proveedor</option>{maintenanceProcurement.suppliers.map(item=><option value={item.id} key={item.id}>{item.name}</option>)}</select></label><label>Artículo<input maxLength={160} value={maintenanceProcurementDraft.productName} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,productName:event.target.value})}/></label><label>Serial o lote<input maxLength={120} value={maintenanceProcurementDraft.serialNumber} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,serialNumber:event.target.value})}/></label><label>Cantidad<input type="number" min="1" max="100000" value={maintenanceProcurementDraft.quantity} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,quantity:Number(event.target.value)})}/></label><label>Motivo de devolución<input maxLength={500} value={maintenanceProcurementDraft.reason} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,reason:event.target.value})}/></label></div>}
        {maintenanceProcurementMode!=="supplier"&&<label>Notas y observaciones<textarea maxLength={2000} value={maintenanceProcurementDraft.notes} onChange={event=>setMaintenanceProcurementDraft({...maintenanceProcurementDraft,notes:event.target.value})} placeholder="Condiciones, especificaciones, diferencias o comentarios de auditoría"/></label>}
        <footer><button type="button" onClick={()=>setMaintenanceProcurementMode(null)}>Cancelar</button><button type="button" className="primary" disabled={saving} onClick={()=>void saveMaintenanceProcurement()}>{saving?<LoaderCircle className="spin"/>:<ShieldCheck/>}{saving?"Guardando...":"Guardar operación"}</button></footer>
      </form></section></div>}
      {transitionFormOpen&&<div className="ticket-modal-backdrop"><section className="ticket-modal transition-create-modal"><header><div><span>NUEVO PROCESO</span><h2>Registrar agencia</h2><p>El proceso iniciará en Servicios Generales.</p></div><button onClick={()=>setTransitionFormOpen(false)} aria-label="Cerrar"><X/></button></header><form onSubmit={event=>{event.preventDefault();void createAgencyTransition();}}><label>Buscar agencia<input value={transitionAgencyQuery} onChange={event=>setTransitionAgencyQuery(event.target.value)} placeholder="Código, terminal o grupo" /></label><label>Agencia<select required value={transitionDraft.agencyId} onChange={event=>setTransitionDraft({...transitionDraft,agencyId:event.target.value})}><option value="">Seleccionar agencia</option>{agencies.filter(agency=>!transitionAgencyQuery||`${agency.codigo} ${agency.terminal} ${agency.grupo}`.toLowerCase().includes(transitionAgencyQuery.toLowerCase())).slice(0,100).map(agency=><option value={agency.id} key={agency.id}>{agency.codigo} · {agency.terminal} · {agency.grupo}</option>)}</select></label><label>Tipo de proceso<select value={transitionDraft.projectType} onChange={event=>setTransitionDraft({...transitionDraft,projectType:event.target.value})}><option value="RESTRUCTURING">Reestructuración</option><option value="CONSTRUCTION">Construcción nueva</option></select></label><label>Descripción inicial<textarea value={transitionDraft.notes} maxLength={2000} onChange={event=>setTransitionDraft({...transitionDraft,notes:event.target.value})} placeholder="Describe el alcance de la obra o adecuación" /></label><footer><button type="button" onClick={()=>setTransitionFormOpen(false)}>Cancelar</button><button className="primary" disabled={saving}><Plus/> Crear proceso</button></footer></form></section></div>}
      {supportStage === "tickets" && supportTab === "tickets" && ticketWorkspaceView === "menu" && teamViewChosen && (
        <nav
          className="ticket-navigation-card-grid"
          aria-label="Bandejas y reportes de tickets"
        >
          <button
            onClick={() => openTicketWorkspaceCard("active")}
          >
            <span className="ticket-navigation-icon"><LifeBuoy /></span>
            <span><em>OPERACIÓN DIARIA</em><strong>Bandeja activa</strong><small>Consulta y atiende todos los casos pendientes.</small></span>
            <b>{counts.active} casos <ArrowRight /></b>
          </button>
          <button
            onClick={() => openTicketWorkspaceCard("process")}
          >
            <span className="ticket-navigation-icon"><Clock3 /></span>
            <span><em>CASOS ASIGNADOS</em><strong>En proceso</strong><small>Responsables trabajando hasta completar la solución.</small></span>
            <b>{counts.process} casos <ArrowRight /></b>
          </button>
          <button
            onClick={() => openTicketWorkspaceCard("history")}
          >
            <span className="ticket-navigation-icon"><History /></span>
            <span><em>CASOS FINALIZADOS</em><strong>Historial</strong><small>Revisa soluciones, evidencias y cierres anteriores.</small></span>
            <b>{countedTickets.filter((ticket) => resolvedStatuses.has(ticket.status)).length} casos <ArrowRight /></b>
          </button>
          <button
            onClick={() => openTicketWorkspaceCard("reports")}
          >
            <span className="ticket-navigation-icon"><BarChart3 /></span>
            <span><em>INDICADORES</em><strong>Reportes y estadísticas</strong><small>Compara productividad, categorías y departamentos.</small></span>
            <b>Ver reportes <ArrowRight /></b>
          </button>
        </nav>
      )}
      {supportStage === "tickets" &&
        supportTab === "tickets" &&
        workspaceDepartment === "TECHNOLOGY" &&
        ticketWorkspaceView === "active" &&
        !teamViewChosen && !technicianPickerOpen && (
          <nav
            className="ticket-navigation-card-grid team-card-grid"
            aria-label="Equipos de Tecnología"
          >
            <div className="ticket-team-selection-heading"><span>PRIMER PASO</span><strong>Selecciona el área de Tecnología</strong><small>Después podrás abrir la bandeja activa, los casos en proceso, el historial o los reportes de esa área.</small></div>
            {session.role === "Administrator" && <button
              className={technologyTeamFilter === "ALL" ? "active" : ""}
              onClick={() => {
                setAssignedToMeOnly(false);
                setTechnologyTeamFilter("ALL");
                setTeamViewChosen(true);
                setTicketWorkspaceView("menu");
              }}
            >
              <span className="ticket-navigation-icon"><UsersRound /></span><span><em>VISTA GENERAL</em><strong>Todo Tecnología</strong><small>Todos los casos activos del departamento.</small></span><b>{activeTeamCounts.all} casos <ArrowRight /></b>
            </button>}
            {allowedTechnologyTeams.includes("CALL_CENTER") && !isTechnologyTechnician && <button
              className={(lockedTechnologyTeam||technologyTeamFilter) === "CALL_CENTER" ? "active" : ""}
              onClick={() => {
                setAssignedToMeOnly(false);
                setTechnologyTeamFilter("CALL_CENTER");
                setTeamViewChosen(true);
                setTicketWorkspaceView("menu");
              }}
            >
              <span className="ticket-navigation-icon"><LifeBuoy /></span><span><em>RECEPCIÓN Y CIERRE</em><strong>Call Center</strong><small>Tickets internos creados y atendidos por el equipo.</small></span><b>{activeTeamCounts.callCenter} casos <ArrowRight /></b>
            </button>}
            {allowedTechnologyTeams.includes("TECHNICAL_FAILURE") && !isTechnologyTechnician && <button
              className={
                (lockedTechnologyTeam||technologyTeamFilter) === "TECHNICAL_FAILURE" ? "active" : ""
              }
              onClick={() => {
                setAssignedToMeOnly(false);
                setTechnologyTeamFilter("TECHNICAL_FAILURE");
                setTeamViewChosen(true);
                setTicketWorkspaceView("menu");
              }}
            >
              <span className="ticket-navigation-icon"><Wrench /></span><span><em>SOPORTE ESPECIALIZADO</em><strong>Avería Técnica</strong><small>Casos escalados para diagnóstico y solución técnica.</small></span><b>{activeTeamCounts.technicalFailure} casos <ArrowRight /></b>
            </button>}
            {allowedTechnologyTeams.includes("TECHNICIANS") && !isTechnologyTechnician && <button className={(lockedTechnologyTeam||technologyTeamFilter) === "TECHNICIANS" ? "active" : ""} onClick={()=>{setAssignedToMeOnly(false);setTechnologyTeamFilter("TECHNICIANS");setTechnicianPickerOpen(false);setPersonnelKind(null);setPersonnelQuery("");setTechnicianFilter("ALL");setTeamViewChosen(true);setTicketWorkspaceView("menu");}}>
              <span className="ticket-navigation-icon"><UserRoundCheck /></span><span><em>EJECUCIÓN EN CAMPO</em><strong>Técnicos</strong><small>Casos asignados para instalación, reparación y cierre con evidencias.</small></span><b>{activeTeamCounts.technicians} casos <ArrowRight /></b>
            </button>}
          </nav>
        )}
      {supportStage==="tickets"&&workspaceDepartment==="TECHNOLOGY"&&ticketWorkspaceView==="active"&&!teamViewChosen&&technicianPickerOpen&&<section className="technician-picker">
        <header><div><span>SELECCIONA EL RESPONSABLE</span><h2>Tickets por responsable</h2><p>Separa el trabajo del equipo técnico y los casos atendidos directamente por supervisores de grupo.</p></div><button onClick={()=>{setTechnicianPickerOpen(false);setPersonnelKind(null);setPersonnelQuery("");setTechnologyTeamFilter("ALL");}}><ArrowLeft/> Equipos</button></header>
        <div className="personnel-kind-cards">
          <button className={personnelKind==="TECHNICIANS"?"active":""} onClick={()=>{setPersonnelKind("TECHNICIANS");setPersonnelQuery("");}}><span><Wrench/></span><em>EQUIPO DE CAMPO</em><strong>Tickets Técnicos</strong><small>Instalaciones, reparaciones y visitas con evidencia.</small><b>{technologyTechnicians.length} responsables · {workspaceTickets.filter(ticket=>technologyTechnicians.some(item=>item.id===ticket.assignedTechnicianId)&&!resolvedStatuses.has(ticket.status)).length} casos</b><ArrowRight/></button>
          <button className={personnelKind==="SUPERVISORS"?"active":""} onClick={()=>{setPersonnelKind("SUPERVISORS");setPersonnelQuery("");}}><span><UsersRound/></span><em>RESPONSABLES DE AGENCIA</em><strong>Tickets Supervisores</strong><small>Casos internos resueltos por administradores de grupo.</small><b>{agencySupervisors.length} supervisores · {workspaceTickets.filter(ticket=>agencySupervisors.some(item=>item.id===ticket.assignedTechnicianId)&&!resolvedStatuses.has(ticket.status)).length} casos</b><ArrowRight/></button>
        </div>
        {personnelKind&&<div className="personnel-toolbar"><label htmlFor="personnel-search"><Search/><input id="personnel-search" name="personnelSearch" value={personnelQuery} onChange={event=>setPersonnelQuery(event.target.value)} placeholder={personnelKind==="TECHNICIANS"?"Buscar técnico...":"Buscar supervisor o grupo..."}/></label><div role="group" aria-label="Presentación"><button className={personnelLayout==="GRID"?"active":""} onClick={()=>setPersonnelLayout("GRID")} aria-label="Ver en cuadrícula"><LayoutGrid/></button><button className={personnelLayout==="LIST"?"active":""} onClick={()=>setPersonnelLayout("LIST")} aria-label="Ver como lista"><List/></button></div></div>}
        {personnelKind&&<div className={`personnel-results ${personnelLayout.toLowerCase()}`}>{visiblePersonnel.map(person=>{const total=workspaceTickets.filter(ticket=>String(ticket.assignedTechnicianId||"").toLowerCase()===String(person.id).toLowerCase()&&!resolvedStatuses.has(ticket.status)).length;const supervisor=person.isAgencySupervisor||person.role==="GroupAdministrator";const groupSummary=(person.groups||[]).slice(0,2).join(" · ");return <button key={person.id} onClick={()=>{setPersonalAssignmentView(false);setAssignedToMeOnly(false);setAssignmentScope("ALL");setTechnicianFilter(person.id);setTechnologyTeamFilter("TECHNICIANS");setTicketPage(1);setTeamViewChosen(true);}}><span><UserRoundCheck/></span><strong>{person.name}</strong><small>{supervisor?"Supervisor de grupo":"Técnico de Tecnología"} · {total} {total===1?"caso activo":"casos activos"}{supervisor&&groupSummary?` · ${groupSummary}${(person.groups?.length||0)>2?` +${(person.groups?.length||0)-2}`:""}`:""}</small><ArrowRight/></button>})}{!visiblePersonnel.length&&<div className="personnel-empty"><Search/><strong>No encontramos responsables</strong><small>Prueba con otro nombre o grupo.</small></div>}</div>}
        {!personnelKind&&<div className="personnel-selection-hint"><UsersRound/><strong>Selecciona una de las dos tarjetas</strong><small>La lista se abrirá separada según el tipo de responsable.</small></div>}
      </section>}
      {supportStage === "tickets" && supportTab === "tickets" && workspaceDepartment !== "TECHNOLOGY" && ticketWorkspaceView === "active" && !teamViewChosen && (
        <nav className="ticket-navigation-card-grid team-card-grid" aria-label="Vistas del departamento">
          <div className="ticket-team-selection-heading"><span>PRIMER PASO</span><strong>Selecciona la bandeja de {departments[workspaceDepartment || ""] || "tu departamento"}</strong><small>Después verás las opciones operativas correspondientes.</small></div>
          <button onClick={() => { setAssignmentScope("ALL"); setTeamViewChosen(true); setTicketWorkspaceView("menu"); }}><span className="ticket-navigation-icon"><UsersRound /></span><span><em>VISTA GENERAL</em><strong>Todo {departments[workspaceDepartment || ""] || "el departamento"}</strong><small>Todos los casos activos del área.</small></span><b>{activeTeamCounts.all} casos <ArrowRight /></b></button>
          <button onClick={() => { setAssignmentScope("UNASSIGNED"); setTeamViewChosen(true); setTicketWorkspaceView("menu"); }}><span className="ticket-navigation-icon"><LifeBuoy /></span><span><em>POR DISTRIBUIR</em><strong>Sin técnico asignado</strong><small>Casos que todavía necesitan responsable.</small></span><b>{activeTeamCounts.unassigned} casos <ArrowRight /></b></button>
          <button onClick={() => { setAssignmentScope("MINE"); setTeamViewChosen(true); setTicketWorkspaceView("menu"); }}><span className="ticket-navigation-icon"><UserRoundCheck /></span><span><em>MI TRABAJO</em><strong>Mis casos asignados</strong><small>Tickets enviados directamente a tu cuenta.</small></span><b>{activeTeamCounts.mine} casos <ArrowRight /></b></button>
        </nav>
      )}
      {supportStage === "tickets" &&
        supportTab === "tickets" &&
        ticketWorkspaceView === "reports" && (
          <section className={`automatic-findings ${activeReportModule ? "report-module-open" : ""}`} aria-labelledby="ticket-reports-title">
            <header>
              <div>
                <span>CENTRO DE REPORTES Y ESTADÍSTICAS</span>
                <h2 id="ticket-reports-title">Rendimiento operativo completo</h2>
                <p>
                  Compara técnicos, supervisores, equipos, departamentos,
                  estados, prioridades, evidencias y tiempos de solución.
                </p>
              </div>
              <div className="ticket-report-controls">
                <div className="ticket-report-filters">
                <label><span>Periodo</span><select id="ticket-report-period" name="ticketReportPeriod" value={reportPeriod} onChange={event=>setReportPeriod(event.target.value as typeof reportPeriod)}><option value="DAY">Hoy</option><option value="WEEK">Últimos 7 días</option><option value="MONTH">Últimos 30 días</option><option value="ALL">Todo el historial</option></select></label>
                {!isTechnologyTechnician&&<label><span>Responsable</span><select id="ticket-report-owner" name="ticketReportOwner" value={reportOwner} onChange={event=>setReportOwner(event.target.value)}><option value="ALL">Todos los técnicos y supervisores</option>{reportOwners.map(owner=><option key={owner} value={owner}>{owner}</option>)}</select></label>}
                <label>
                  <span>Agrupar estadísticas por</span>
                  <select
                  id="ticket-report-dimension"
                  name="ticketReportDimension"
                  value={reportDimension}
                  onChange={(event) =>
                    setReportDimension(
                      event.target.value as typeof reportDimension,
                    )
                  }
                >
                  <option value="technician">Responsable</option>
                  <option value="category">Categoría</option>
                  <option value="department">Departamento</option>
                  <option value="group">Grupo de agencias</option>
                  </select>
                </label>
                </div>
                <div className="ticket-report-actions">
                <button type="button" className="report-action-button data" onClick={()=>void exportReportExcel("all")}><Download/><span><strong>Libro Excel completo</strong><small>Dashboard, métricas y hojas filtrables</small></span></button>
                <button type="button" className="report-action-button print" onClick={()=>void exportReportPdf("all")}><Send/><span><strong>PDF ejecutivo completo</strong><small>Indicadores, rankings y trazabilidad</small></span></button>
                <button type="button" className="report-action-button audit" onClick={exportResolvedFindings}><ShieldCheck/><span><strong>Auditoría de soluciones</strong><small>Expediente y evidencias</small></span></button>
                </div>
              </div>
            </header>
            <section className="report-kpi-grid" aria-label="Indicadores principales del periodo">
              <article><span><LifeBuoy/></span><div><small>CASOS DEL PERIODO</small><strong>{reportSummary.totalCases}</strong><em>{reportSummary.unassigned} sin asignar</em></div></article>
              <article className="success"><span><CheckCircle2/></span><div><small>CASOS RESUELTOS</small><strong>{reportSummary.total}</strong><em>{reportSummary.resolutionRate}% de resolución</em></div></article>
              <article className="in-progress"><span><Clock3/></span><div><small>EN PROCESO</small><strong>{reportSummary.inProgress}</strong><em>{reportSummary.open} pendientes</em></div></article>
              <article className="critical"><span><AlertTriangle/></span><div><small>CRÍTICOS ACTIVOS</small><strong>{reportSummary.critical}</strong><em>requieren prioridad</em></div></article>
              <article className="time"><span><Clock3/></span><div><small>TIEMPO PROMEDIO</small><strong>{reportSummary.averageMinutes?compactDuration(reportSummary.averageMinutes):"—"}</strong><em>hasta la resolución</em></div></article>
              <article className="evidence"><span><Camera/></span><div><small>CON EVIDENCIA</small><strong>{reportSummary.evidencePercent}%</strong><em>{reportSummary.withEvidence} expedientes</em></div></article>
            </section>
            <section className="report-access-scope" aria-label="Alcance de las estadísticas">
              <span><ShieldCheck/></span>
              <div><small>ALCANCE DE ESTA VISTA · {reportPeriodLabel.toUpperCase()}</small><strong>{reportScopeLabel}</strong><p>{reportScopeDetail}</p></div>
            </section>
            <nav className="report-module-selector" aria-label="Secciones de reportes y estadísticas">
              {([
                ["overview", "RESUMEN EJECUTIVO", "Indicadores generales", `${reportSummary.total} resueltos`, BarChart3],
                ["productivity", "PRODUCTIVIDAD", "Gráficas e historial", `${reportRows.length} agrupaciones`, Clock3],
                ["department", "INFORME DEL ÁREA", departmentalReport.departmentName, `${departmentalReport.total} incidencias`, LifeBuoy],
                ["rankings", "CLASIFICACIONES", "Rankings", `${reportRankings.departments.length + reportRankings.areas.length} posiciones`, ShieldCheck],
                ["personnel", "RENDIMIENTO", "Técnicos y supervisores", `${personnelPerformance.length} responsables`, UsersRound],
                ["resolved", "TRAZABILIDAD", "Casos resueltos", `${resolvedTickets.length} casos`, CheckCircle2],
                ["findings", "AUDITORÍA", "Hallazgos automáticos", `${findingReportSummary.total} hallazgos`, AlertTriangle],
              ] as const).map(([key, eyebrow, title, detail, Icon]) => <button type="button" key={key} className={activeReportModule===key?"active":""} aria-expanded={activeReportModule===key} onClick={()=>setActiveReportModule(current=>current===key?null:key)}>
                <span><Icon/></span><em>{eyebrow}</em><strong>{title}</strong><small>{detail}</small><b>{activeReportModule===key?"Cerrar":"Abrir"} <ArrowRight/></b>
              </button>)}
            </nav>
            {activeReportModule&&<button type="button" className="mobile-section-back report-mobile-back" onClick={()=>setActiveReportModule(null)}><ArrowLeft/> Volver atrás</button>}
            {!activeReportModule&&<div className="report-module-empty"><LayoutGrid/><strong>Selecciona una tarjeta para consultar el reporte</strong><span>Solo se abrirá la información que necesitas, sin sobrecargar la pantalla.</span></div>}
            {activeReportModule==="overview"&&<>
            <div className="report-dashboard-grid">
              <section className="report-panel report-responsible-comparison" aria-labelledby="responsible-comparison-title">
                <header><div><span>COMPARATIVA GENERAL</span><h3 id="responsible-comparison-title">Técnicos, supervisores y equipos</h3><p>Volumen atendido, carga activa, críticos y tiempo promedio por tipo de responsable.</p></div></header>
                <div className="responsible-comparison-chart" role="img" aria-label="Comparación de casos por tipo de responsable">
                  {responsibleComparison.map(row=><article key={row.key}>
                    <div className="comparison-heading"><span className="comparison-color" style={{background:row.color}}/><strong>{row.label}</strong><em>{row.total} casos</em></div>
                    <div className="comparison-track"><i style={{width:`${Math.max(row.total?4:0,(row.total/responsibleMaximum)*100)}%`,background:row.color}}/></div>
                    <dl><div><dt>Resueltos</dt><dd>{row.resolved}</dd></div><div><dt>Activos</dt><dd>{row.active}</dd></div><div><dt>Críticos</dt><dd>{row.critical}</dd></div><div><dt>Promedio</dt><dd>{row.averageMinutes?compactDuration(row.averageMinutes):"—"}</dd></div><div><dt>Con evidencia</dt><dd>{row.evidence}</dd></div></dl>
                  </article>)}
                </div>
              </section>
              <section className="report-panel report-distribution-panel" aria-labelledby="status-distribution-title">
                <header><div><span>DISTRIBUCIÓN</span><h3 id="status-distribution-title">Estados de los casos</h3><p>Situación de todos los tickets dentro del periodo.</p></div></header>
                <div className="distribution-list">{statusBreakdown.map(row=><div key={row.key}><span><i className={`status-dot status-${row.key.toLowerCase()}`}/><strong>{row.label}</strong></span><b>{row.total}</b><div><i style={{width:`${reportScopedTickets.length?(row.total/reportScopedTickets.length)*100:0}%`}}/></div><small>{reportScopedTickets.length?Math.round((row.total/reportScopedTickets.length)*100):0}%</small></div>)}</div>
              </section>
              <section className="report-panel report-distribution-panel" aria-labelledby="priority-distribution-title">
                <header><div><span>NIVEL DE ATENCIÓN</span><h3 id="priority-distribution-title">Prioridades registradas</h3><p>Composición de la carga por nivel de prioridad.</p></div></header>
                <div className="distribution-list priority-distribution">{priorityBreakdown.map(row=><div key={row.key}><span><i className={`status-dot priority-${row.key.toLowerCase()}`}/><strong>{row.label}</strong></span><b>{row.total}</b><div><i style={{width:`${reportScopedTickets.length?(row.total/reportScopedTickets.length)*100:0}%`}}/></div><small>{reportScopedTickets.length?Math.round((row.total/reportScopedTickets.length)*100):0}%</small></div>)}</div>
              </section>
            </div>
            </>}
            {activeReportModule==="productivity"&&
            <div
              className="report-panel report-ranking-panel"
              role="img"
              aria-label={`Gráfica de barras de casos resueltos por ${reportDimension}`}
              style={{ display: "grid", gap: 12, padding: 18 }}
            >
              <header className="report-inline-heading"><div><span>PRODUCTIVIDAD</span><h3>Casos resueltos por {reportDimension==="technician"?"responsable":reportDimension==="category"?"categoría":reportDimension==="group"?"grupo de agencias":"departamento"}</h3><p>Cantidad resuelta y duración promedio para la agrupación seleccionada.</p></div></header>
              {reportRows.map((row) => (
                <article
                  key={row.key}
                  style={{ display: "grid", gap: 7 }}
                  aria-label={`${row.label}: ${row.resolved} casos resueltos, tiempo promedio ${compactDuration(row.averageMinutes)}`}
                >
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      gap: 12,
                    }}
                  >
                    <strong>{row.label}</strong>
                    <span>
                      {row.resolved} resueltos · {compactDuration(row.averageMinutes)}
                    </span>
                  </div>
                  <div
                    style={{
                      height: 18,
                      overflow: "hidden",
                      borderRadius: 999,
                      background: "#071a2f",
                    }}
                  >
                    <div
                      style={{
                        width: `${Math.max(3, (row.resolved / reportMaximum) * 100)}%`,
                        height: "100%",
                        borderRadius: 999,
                        background:
                          "linear-gradient(90deg, #168ec5, #5de1ff)",
                      }}
                    />
                  </div>
                  <small>{row.detail}</small>
                </article>
              ))}
              {!reportRows.length && (
                <div className="ticket-empty">
                  <BarChart3 />
                  <strong>Aún no hay casos resueltos</strong>
                  <span>Las estadísticas aparecerán al cerrar los primeros tickets.</span>
                </div>
              )}
              {!!reportHistory.length && (
                <section
                  aria-label="Historial de casos resueltos por fecha"
                  style={{
                    display: "grid",
                    gap: 10,
                    marginTop: 14,
                    paddingTop: 16,
                    borderTop: "1px solid #2c617f",
                  }}
                >
                  <h3 style={{ margin: 0 }}>Historial de resoluciones</h3>
                  {reportHistory.map((point) => (
                    <div
                      key={point.date}
                      style={{
                        display: "grid",
                        gridTemplateColumns: "minmax(90px, auto) 1fr auto",
                        alignItems: "center",
                        gap: 10,
                      }}
                      aria-label={`${point.date}: ${point.resolved} casos resueltos`}
                    >
                      <time dateTime={point.date}>
                        {point.date.length===7?new Date(`${point.date}-01T12:00:00`).toLocaleDateString("es-DO",{month:"short",year:"numeric"}):new Date(`${point.date}T12:00:00`).toLocaleDateString("es-DO",{day:"2-digit",month:"short"})}
                      </time>
                      <div
                        style={{
                          height: 12,
                          overflow: "hidden",
                          borderRadius: 999,
                          background: "#071a2f",
                        }}
                      >
                        <div
                          style={{
                            width: `${Math.max(3, (point.resolved / reportHistoryMaximum) * 100)}%`,
                            height: "100%",
                            borderRadius: 999,
                            background:
                              "linear-gradient(90deg, #27a778, #6ee7b7)",
                          }}
                        />
                      </div>
                      <strong>{point.resolved}</strong>
                    </div>
                  ))}
                </section>
              )}
            </div>
            }
            {activeReportModule==="department"&&workspaceDepartment && ["GENERAL_SERVICES","TECHNOLOGY","HUMAN_RESOURCES"].includes(workspaceDepartment) && <section className="report-panel general-services-auto-report" aria-labelledby="departmental-report-title">
              <header><div><span>INFORME AUTOMÁTICO · {departmentalReport.departmentName.toUpperCase()}</span><h3 id="departmental-report-title">Control operativo por tipo de incidencia</h3><p>Cada departamento consulta exclusivamente sus propios tickets y responsables, respetando el periodo seleccionado.</p></div><div className="table-export-actions"><strong>{departmentalReport.total} incidencias</strong><button onClick={()=>void exportReportExcel("department")}><Download/> Excel del informe</button><button onClick={()=>void exportReportPdf("department")}><Download/> PDF del informe</button></div></header>
              <div className="service-report-sections">{departmentalReport.sections.map(section=><article className={`service-report-block service-${section.color}`} key={section.key}><header><span>{section.label}</span><strong>{section.rows.length}</strong></header><div className="service-report-table"><div><b>Agencia</b><b>Incidencia</b><b>Responsable</b><b>Estado</b></div>{section.rows.slice(0,8).map(ticket=><div key={`${section.key}-${ticket.id}`}><span>{ticket.codigo} · {ticket.terminal}</span><span>{ticket.subject}</span><span>{ticket.assignedTechnicianName||"Sin asignar"}</span><span>{statuses[ticket.status]||ticket.status}</span></div>)}{!section.rows.length&&<p>Sin registros en este periodo.</p>}</div></article>)}</div>
            </section>}
            {activeReportModule==="rankings"&&
            <div className="report-ranking-grid">
              {([ ["departments", "RANKING CORPORATIVO", "Ranking por departamentos", "Compara productividad, carga y efectividad de cada departamento.", reportRankings.departments], ["areas", "RANKING OPERATIVO", "Ranking por áreas y equipos", "Separa Call Center, Avería Técnica, campo y categorías de trabajo.", reportRankings.areas] ] as const).map(([kind, eyebrow, title, description, rows])=><section className="report-panel report-leaderboard" key={kind}>
                <header><div><span>{eyebrow}</span><h3>{title}</h3><p>{description}</p></div><div className="table-export-actions"><button onClick={()=>void exportReportExcel(kind)} title="Descargar Excel"><Download/> Excel</button><button onClick={()=>void exportReportPdf(kind)} title="Descargar PDF"><Download/> PDF</button></div></header>
                <div className="leaderboard-list">{rows.slice(0,10).map((row,index)=><article key={row.key}><b className={index<3?`place place-${index+1}`:"place"}>{index+1}</b><span><strong>{row.label}</strong><small>{row.active} activos · {row.evidencePercent}% con evidencia</small></span><div><i style={{width:`${Math.max(row.resolved?5:0,(row.resolved/Math.max(1,rows[0]?.resolved||1))*100)}%`}}/></div><em>{row.resolved}<small>resueltos</small></em><strong>{row.effectiveness}%</strong></article>)}</div>
              </section>)}
            </div>
            }
            {activeReportModule==="personnel"&&
            <section className="report-panel personnel-performance-panel" aria-labelledby="personnel-performance-title">
              <header><div><span>DETALLE POR PERSONA</span><h3 id="personnel-performance-title">Comparación de técnicos y supervisores</h3><p>Todos los responsables con actividad durante el periodo seleccionado. Cada expediente se puede descargar de forma individual.</p></div><div className="table-export-actions"><strong>{personnelPerformance.length} responsables</strong><button onClick={()=>void exportReportExcel("personnel")}><Download/> Excel</button><button onClick={()=>void exportReportPdf("personnel")}><Download/> PDF</button></div></header>
              <div className="personnel-performance-table">
                <div className="personnel-performance-row personnel-performance-head"><span>Responsable</span><span>Tipo</span><span>Departamento</span><span>Activos</span><span>Resueltos</span><span>Críticos</span><span>Promedio</span><span>Evidencia</span><span>Reporte individual</span></div>
                {personnelPerformance.map(person=><article className="personnel-performance-row" key={person.id}><span><UserRoundCheck/><strong>{person.name}</strong>{person.groups>0&&<small>{person.groups} grupos</small>}</span><span><em className={person.role==="Supervisor"?"supervisor":"technician"}>{person.role}</em></span><span>{person.department}</span><span>{person.active}</span><span><b>{person.resolved}</b></span><span>{person.critical}</span><span>{person.averageMinutes?compactDuration(person.averageMinutes):"—"}</span><span>{person.evidencePercent}%</span><span className="row-export-actions"><button onClick={()=>void exportReportExcel("personnel",person.name)} aria-label={`Descargar Excel de ${person.name}`}><Download/> XLSX</button><button onClick={()=>void exportReportPdf("personnel",person.name)} aria-label={`Descargar PDF de ${person.name}`}><Download/> PDF</button></span></article>)}
                {!personnelPerformance.length&&<div className="ticket-empty"><UsersRound/><strong>No hay actividad por responsable</strong><span>Cambia el periodo o espera a que existan casos asignados.</span></div>}
              </div>
            </section>
            }
            {activeReportModule==="resolved"&&
            <section className="report-panel resolved-cases-panel" aria-labelledby="resolved-cases-title">
              <header><div><span>TRAZABILIDAD DE RESULTADOS</span><h3 id="resolved-cases-title">Casos resueltos</h3><p>Detalle verificable de cada solución, responsable, duración y evidencia entregada.</p></div><div className="table-export-actions"><strong>{resolvedTickets.length} casos</strong><button onClick={()=>void exportReportExcel("resolved")}><Download/> Excel</button><button onClick={()=>void exportReportPdf("resolved")}><Download/> PDF</button></div></header>
              <div className="resolved-report-table"><div className="resolved-report-row resolved-report-head"><span>Ticket / Agencia</span><span>Departamento</span><span>Responsable</span><span>Cierre</span><span>Duración</span><span>Evidencia</span><span>Solución</span></div>{resolvedTickets.slice(0,100).map(ticket=><article className="resolved-report-row" key={ticket.id}><span><b>#{ticket.ticketNumber}</b><strong>{ticket.codigo} · {ticket.terminal}</strong><small>{ticket.grupo}</small></span><span>{departments[ticket.assignedDepartment]||ticket.assignedDepartment}</span><span>{ticket.resolvedByName||ticket.assignedTechnicianName||"Sin responsable"}</span><span>{new Date(ticket.closedAt||ticket.updatedAt).toLocaleDateString("es-DO")}</span><span>{compactDuration(ticketMinutes(ticket))}</span><span><em className={(ticket.evidence?.length||0)>0?"complete":"missing"}>{ticket.evidence?.length||0} archivos</em></span><span title={ticket.resolution||"Sin detalle"}>{ticket.resolution||"Sin detalle registrado"}</span></article>)}</div>
            </section>
            }
            {activeReportModule==="findings"&&
            <section className="report-panel finding-statistics-panel" aria-labelledby="finding-statistics-title">
              <header><div><span>HALLAZGOS AUTOMÁTICOS</span><h3 id="finding-statistics-title">Auditorías detectadas y atendidas</h3><p>Datos de asignación, resolución, evidencia y duración de los hallazgos del mismo periodo.</p></div><div className="table-export-actions"><button onClick={()=>void exportReportExcel("findings")}><Download/> Excel</button><button onClick={()=>void exportReportPdf("findings")}><Download/> PDF</button></div></header>
              <div className="finding-statistics-cards">
                <article><AlertTriangle/><span><strong>{findingReportSummary.total}</strong><small>Total detectados</small></span></article>
                <article><Clock3/><span><strong>{findingReportSummary.pending}</strong><small>Pendientes</small></span></article>
                <article><CheckCircle2/><span><strong>{findingReportSummary.resolved}</strong><small>Resueltos</small></span></article>
                <article><Wrench/><span><strong>{findingReportSummary.technicians}</strong><small>Asignados a técnicos</small></span></article>
                <article><UsersRound/><span><strong>{findingReportSummary.supervisors}</strong><small>Asignados a supervisores</small></span></article>
                <article><Camera/><span><strong>{findingReportSummary.evidence}</strong><small>Con evidencia</small></span></article>
                <article><Clock3/><span><strong>{findingReportSummary.averageMinutes?compactDuration(findingReportSummary.averageMinutes):"—"}</strong><small>Tiempo promedio</small></span></article>
              </div>
              {!!findingTypeReport.length&&<div className="finding-type-report"><h4>Hallazgos por categoría</h4>{findingTypeReport.map(row=><article key={row.key}><span><strong>{row.label}</strong><small>{row.resolved} resueltos · {row.total-row.resolved} pendientes</small></span><div><i style={{width:`${findingReportSummary.total?(row.total/findingReportSummary.total)*100:0}%`}}/></div><b>{row.total}</b></article>)}</div>}
            </section>
            }
          </section>
        )}
      {isSupportTechnician &&
        supportStage === "findings" &&
        supportTab === "findings" &&
        findingView === "menu" && (
          <nav className="finding-view-tabs">
            <div>
              <button onClick={() => {setFindingAssigneeKind("ALL");setFindingAssigneeFilter("ALL");setFindingView("pending");}}>
                <AlertTriangle />
                <span>
                  <em>POR ATENDER</em>
                  <b>Pendientes</b>
                  <small>Condiciones detectadas que requieren atención.</small>
                </span>
                <strong>{pendingFindingCount}</strong>
              </button>
              <button onClick={() => {setFindingAssigneeKind("ALL");setFindingAssigneeFilter("ALL");setFindingView("resolved");}}>
                <ShieldCheck />
                <span>
                  <em>HISTORIAL VERIFICADO</em>
                  <b>Soluciones</b>
                  <small>Hallazgos resueltos y solución registrada.</small>
                </span>
                <strong>{resolvedFindingCount}</strong>
              </button>
              <button onClick={()=>{setFindingAssigneeKind("TECHNICIANS");setFindingAssigneeFilter("ALL");setFindingView("pending");}}><Wrench/><span><em>TRABAJO DE CAMPO</em><b>Asignados a técnicos</b><small>Casos por responsable con evidencia GPS.</small></span><strong>{assignedFindingCounts.technicians}</strong></button>
              <button onClick={()=>{setFindingAssigneeKind("SUPERVISORS");setFindingAssigneeFilter("ALL");setFindingView("pending");}}><UsersRound/><span><em>GESTIÓN DE AGENCIA</em><b>Asignados a supervisores</b><small>Casos atendidos por administradores de grupo.</small></span><strong>{assignedFindingCounts.supervisors}</strong></button>
            </div>
          </nav>
        )}
      <div
        className="ticket-workspace"
        hidden={
          supportStage !== "tickets" ||
          supportTab !== "tickets" ||
          ticketWorkspaceView === "reports" ||
          ticketWorkspaceView === "menu" ||
          (ticketWorkspaceView === "active" && !teamViewChosen)
        }
      >
        <section className="ticket-board">
          <header>
            <label>
              <Search />
              <input
                id="ticket-search"
                name="ticketSearch"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder={isTechnologyTechnician?"Buscar en mis casos por ticket, agencia o grupo...":"Buscar ticket, agencia, grupo o responsable..."}
                autoComplete="off"
              />
            </label>
            <div className="ticket-filter-stack" aria-label="Filtros de tickets">
              <div className={`ticket-status-filter status-layout-${ticketWorkspaceView}`} hidden={personalAssignmentView&&ticketWorkspaceView!=="active"}>
                <span className="ticket-filter-label">{personalAssignmentView?"Estado de mis tickets":"Estado del ticket"}</span>
                {(personalAssignmentView&&ticketWorkspaceView==="active"
                  ? [["IN_PROGRESS", "Tickets en proceso"], ["PENDING", "Tickets pendientes"]]
                  : ticketWorkspaceView === "active"
                  ? [
                      ["ACTIVE", "Pendientes de asignar"],
                      ["OPEN", "Abiertos"],
                    ]
                  : ticketWorkspaceView === "process"
                    ? [["IN_PROGRESS", "Tickets en proceso"], ["PENDING", "Tickets pendientes"]]
                  : [
                      ["ALL", "Todo el historial"],
                      ["RESOLVED", "Resueltos"],
                      ["CLOSED", "Cerrados"],
                    ]
                ).map(([value, label]) => (
                  <button
                    type="button"
                    key={value}
                    className={statusFilter === value||(personalAssignmentView&&ticketWorkspaceView==="active"&&statusFilter==="ACTIVE") ? "active" : ""}
                    onClick={() => setStatusFilter(personalAssignmentView&&ticketWorkspaceView==="active"&&statusFilter===value?"ACTIVE":value)}
                  >
                    {label}
                  </button>
                ))}
              </div>
              <label className="ticket-priority-filter">
                <span>Prioridad</span>
                <select
                  id="ticket-priority-filter"
                  name="ticketPriorityFilter"
                  value={priorityFilter}
                  onChange={(event) => setPriorityFilter(event.target.value)}
                >
                  <option value="ALL">Todas las prioridades</option>
                  <option value="CRITICAL">Crítica</option>
                  <option value="HIGH">Alta</option>
                  <option value="MEDIUM">Media</option>
                  <option value="LOW">Baja</option>
                </select>
              </label>
              <label className="ticket-priority-filter">
                <span>Categoría</span>
                <select
                  id="ticket-category-filter"
                  name="ticketCategoryFilter"
                  value={categoryFilter}
                  onChange={(event) => setCategoryFilter(event.target.value)}
                >
                  <option value="ALL">Todas las categorías</option>
                  {ticketCategoryChoices.map(([code, name]) => (
                    <option key={code} value={code}>
                      {name}
                    </option>
                  ))}
                </select>
              </label>
              {ticketWorkspaceView === "history" && !isTechnologyTechnician && <label className="ticket-priority-filter">
                <span>Técnico responsable</span>
                <select id="ticket-technician-filter" name="ticketTechnicianFilter" value={technicianFilter} onChange={(event) => setTechnicianFilter(event.target.value)}>
                  <option value="ALL">Todos los técnicos</option>
                  {technicians.filter((item) => item.isAgencySupervisor || (item.isTechnician && (!workspaceDepartment || item.department === workspaceDepartment))).map((item) => <option key={item.id} value={item.id}>{item.name}{item.isAgencySupervisor ? " · Supervisor" : " · Técnico"}</option>)}
                </select>
              </label>}
              {ticketTypeFilter === "INTERNAL" && canCreateTickets && (
                <button className="primary" onClick={()=>{setResponsibleQuery("");setDraft({...draft,ticketType:"INTERNAL",assignedDepartment:workspaceDepartment||session.supportDepartment||draft.assignedDepartment,assignedTeam:technologyTeamFilter!=="ALL"?technologyTeamFilter:sessionRoutingTeam!=="ALL"?sessionRoutingTeam:"TECHNICAL_FAILURE",category:"",agencyId:""});setSelectedCases([]);setCaseDetails({});setFormOpen(true);}}>
                  <Plus /> Crear ticket interno
                </button>
              )}
            </div>
          </header>
          {!loading && (
            <div className="ticket-board-results" aria-live="polite">
              <span>
                <strong>{visible.length}</strong>{" "}
                {visible.length === 1 ? "ticket visible" : "tickets visibles"}
              </span>
              <small>Abre solamente el caso que necesitas gestionar.</small>
              {(query.trim() || priorityFilter !== "ALL" || categoryFilter !== "ALL" || technicianFilter !== "ALL") && (
                <button
                  type="button"
                  className="ticket-clear-filters"
                  onClick={() => {
                    setQuery("");
                    setPriorityFilter("ALL");
                    setCategoryFilter("ALL");
                    setTechnicianFilter("ALL");
                  }}
                >
                  <X /> Limpiar filtros
                </button>
              )}
            </div>
          )}
          {loading && !tickets.length ? (
            <div className="ticket-loading">
              <LoaderCircle className="spin" /> Cargando incidencias...
            </div>
          ) : (
            <div className="ticket-list">
              {ticketBoardRows.map((ticket) => {
                const isExpanded = expandedTicketId === ticket.id;
                const edit = management[ticket.id] || {
                  status: ticket.status,
                  priority: ticket.priority,
                  resolution: ticket.resolution || "",
                  assignedTechnicianId: ticket.assignedTechnicianId || "",
                  assignedDepartment: ticket.assignedDepartment,
                  assignedTeam: ticket.assignedTeam || "",
                  sharedWithDepartments:
                    ticket.sharedWithDepartments || [],
                  routingComment: "",
                  evidence: [],
                };
                const ticketAssignees = techniciansForTicket(
                  { ...ticket, assignedDepartment: edit.assignedDepartment },
                  edit.assignedTeam,
                ).filter(matchesResponsibleQuery);
                return (
                  <article
                    className={`ticket-card priority-${ticket.priority.toLowerCase()}${isExpanded ? " expanded" : ""}`}
                    data-action-mode={isExpanded && ticketActionMode ? ticketActionMode : undefined}
                    data-ticket-id={ticket.id}
                    key={ticket.id}
                  >
                    {isExpanded && <div className="ticket-action-modal-heading">
                      <span><small>TICKET #{ticket.ticketNumber}</small><strong>{ticketActionMode === "transfer" ? "Transferir ticket" : ticketActionMode === "assign" ? (ticket.assignedTechnicianId?"Cambiar responsable":"Asignar responsable") : ticketActionMode === "resolve" ? "Resolver con evidencia" : ticketActionMode === "cancel" ? "Anular ticket" : ticketActionMode === "evidence" ? "Evidencias e historial" : "Ticket completo"}</strong></span>
                      <button type="button" aria-label={ticketActionMode?"Volver al ticket completo":"Cerrar ticket"} onClick={() => { if(ticketActionMode){setTicketActionMode(null);setResponsibleQuery("");setExpandedHistoryTicketId(null);return;}if(returnToTicketCreation){restoreTicketCreationAfterDuplicate();return;}setExpandedTicketId(null);setExpandedHistoryTicketId(null); }}>{ticketActionMode?<ArrowLeft/>:<X/>}</button>
                    </div>}
                    <div className="ticket-card-head">
                      <div>
                        <em>#{ticket.ticketNumber}</em>
                        <span
                          className={`ticket-status status-${ticket.status.toLowerCase()}`}
                        >
                          {statuses[ticket.status]}
                        </span>
                        <span
                          className={`ticket-priority priority-${ticket.priority.toLowerCase()}`}
                        >
                          {priorities[ticket.priority]}
                        </span>
                      </div>
                      <time>
                        <Clock3 />
                        {new Date(ticket.createdAt).toLocaleString("es-DO")}
                      </time>
                    </div>
                    <div
                      className="ticket-case-trace ticket-route-summary"
                      aria-label="Enrutamiento actual del ticket"
                    >
                      {effectiveTechnologyTeam(ticket) ===
                      "TECHNICAL_FAILURE" ? (
                        <Wrench />
                      ) : (
                        <UsersRound />
                      )}
                      <span>
                        <strong>
                          {departments[ticket.assignedDepartment] ||
                            ticket.assignedDepartment}
                          {effectiveTechnologyTeam(ticket)
                            ? ` · ${technologyTeams[effectiveTechnologyTeam(ticket)!]}`
                            : ""}
                        </strong>
                        <small>
                          {ticket.assignedTechnicianName
                            ? `Asignado a ${ticket.assignedTechnicianName}`
                            : "Pendiente de asignación"}
                          {!!ticket.sharedWithDepartments?.length &&
                            ` · Compartido con ${ticket.sharedWithDepartments
                              .map((code) => departments[code] || code)
                              .join(", ")}`}
                        </small>
                      </span>
                    </div>
                    <div className="ticket-agency ticket-location ticket-card-detail">
                      <MapPin />
                      <span>
                        <strong>{ticket.direccion || "Dirección pendiente de levantamiento"}</strong>
                        <small>{[ticket.sector,ticket.municipio,ticket.provincia].filter(Boolean).join(" · ") || "Sin ubicación detallada"}</small>
                        {ticket.latitude != null && ticket.longitude != null && <a href={`https://www.openstreetmap.org/?mlat=${ticket.latitude}&mlon=${ticket.longitude}#map=19/${ticket.latitude}/${ticket.longitude}`} target="_blank" rel="noreferrer">Ver ubicación de las fotos</a>}
                      </span>
                    </div>
                    <h3>{ticket.subject}</h3>
                    {!isExpanded && <button
                      type="button"
                      className="ticket-card-toggle"
                      aria-expanded="false"
                      aria-label={`Ver ticket #${ticket.ticketNumber} completo`}
                      onClick={() => {
                        setExpandedTicketId(ticket.id);
                        setTicketActionMode(null);
                      }}
                    >
                      <span>Ver ticket completo</span>
                      <ArrowRight />
                    </button>}
                    <div className="ticket-agency" aria-label={`Agencia ${ticket.terminal}, código ${ticket.codigo}, grupo ${ticket.grupo}`}>
                      <Building2 />
                      <span>
                        <strong>{ticket.terminal}</strong>
                        <small>
                          {ticket.codigo} · {ticket.grupo}
                        </small>
                      </span>
                    </div>
                    <p className="ticket-description">{ticket.description}</p>
                    <footer>
                      <span className="ticket-card-reporter">
                        <small>CREADO POR</small>
                        <strong>{ticket.createdByName}</strong>
                        <em>
                        {ticketCategoryChoices.find(
                          ([code]) => code === ticket.category,
                        )?.[1] || categories[ticket.category] || ticket.category}
                        </em>
                      </span>
                      <span className="ticket-card-actions">
                        {canViewTicketEvidence && <button type="button" onClick={() => { setExpandedTicketId(ticket.id); setTicketActionMode("evidence"); }}><History /> Historial y evidencias</button>}
                        {canDispatchTicket && !resolvedStatuses.has(ticket.status) && <button type="button" onClick={() => { setExpandedTicketId(ticket.id); setTicketActionMode("transfer"); }}><Share2 /> Transferir</button>}
                        {isCallCenter && canShareTicket && !resolvedStatuses.has(ticket.status) && <button type="button" onClick={() => { setExpandedTicketId(ticket.id); setTicketActionMode("transfer"); }}><Share2 /> Compartir y escalar</button>}
                        {canAdministerTicket(ticket) && <button type="button" onClick={() => { setResponsibleQuery(""); setExpandedTicketId(ticket.id); setTicketActionMode("assign"); }}><UserRoundCheck /> {ticket.assignedTechnicianId?"Cambiar responsable":"Asignar responsable"}</button>}
                        {canManageTicket(ticket) && !resolvedStatuses.has(ticket.status) && <button type="button" className="resolve" onClick={() => { setExpandedTicketId(ticket.id); setTicketActionMode("resolve"); }}><CheckCircle2 /> Estado / resolver</button>}
                        {canAdministerTicket(ticket) && <button type="button" className="cancel" onClick={() => { setExpandedTicketId(ticket.id); setTicketActionMode("cancel"); setManagement(current=>({...current,[ticket.id]:{...edit,resolution:""}})); }}><AlertTriangle /> Anular</button>}
                      </span>
                    </footer>
                    <div className="ticket-case-trace ticket-duration-detail ticket-card-detail">
                      <Clock3 />
                      <span>
                        <strong>Tiempo total del caso</strong>
                        <small>
                          Desde la apertura hasta{" "}
                          {ticket.status === "CLOSED"
                            ? "el cierre"
                            : ticket.status === "RESOLVED"
                              ? "la resolución"
                              : "este momento"}
                          :{" "}
                          {elapsedCaseTime(
                            ticket.createdAt,
                            ticket.status === "CLOSED"
                              ? ticket.closedAt
                              : ticket.status === "RESOLVED"
                                ? ticket.updatedAt
                                : null,
                          )}
                        </small>
                      </span>
                    </div>
                    {isExpanded && ticket.resolution && (
                        <div className="ticket-resolution">
                          <ShieldCheck />
                          <span>
                            <strong>Respuesta de soporte</strong>
                            {ticket.resolution}
                          </span>
                        </div>
                      )}
                    {isExpanded && (canManageTicket(ticket) || (ticketActionMode === "transfer" && canEscalateCallCenterTicket(ticket))) && ticketActionMode && ticketActionMode !== "evidence" && ticketActionMode !== "cancel" && (
                      <div className="ticket-management">
                        {ticketActionMode === "resolve" && isAssignedInternalSupervisor(ticket) && <div className="ticket-supervisor-gps-resolution"><MapPin/><span><strong>Resolución presencial desde la agencia</strong><small>Para cerrar tu ticket debes estar en {ticket.terminal}, permitir el GPS y tomar la evidencia con una precisión máxima de ±{TICKET_GPS_MAX_ACCURACY_METERS} m. El sistema validará un radio de {TICKET_AGENCY_RADIUS_METERS} m.</small></span></div>}
                        {ticketActionMode === "resolve" && !ticket.assignedTechnicianId && <div className="ticket-direct-resolution"><ShieldCheck/><span><strong>Resolución directa del departamento</strong><small>No necesitas asignar un técnico. Describe la solución y adjunta la evidencia normal del caso.</small></span></div>}
                        {((ticketActionMode === "transfer" && canDispatchTicket) || (ticketActionMode === "assign" && canAdministerTicket(ticket))) && (
                          <>
                            {ticketActionMode === "transfer" ? <label className="ticket-action-transfer">
                          Transferir a otro departamento
                          <small>
                            Actual: {departments[ticket.assignedDepartment] || ticket.assignedDepartment}
                          </small>
                          <select
                            value={edit.assignedDepartment}
                            onChange={(event) =>
                              setManagement((current) => ({
                                ...current,
                                [ticket.id]: {
                                  ...edit,
                                  assignedDepartment: event.target.value,
                                  assignedTeam:
                                    event.target.value === "TECHNOLOGY"
                                      ? edit.assignedTeam || "TECHNICAL_FAILURE"
                                      : "",
                                  assignedTechnicianId: "",
                                },
                              }))
                            }
                          >
                            {catalog.map((department) => (
                              <option
                                value={department.code}
                                key={department.code}
                              >
                                {department.name}
                              </option>
                            ))}
                          </select>
                            </label> : <div className="ticket-assignment-department"><UsersRound/><span><small>DEPARTAMENTO PROPIETARIO</small><strong>{departments[ticket.assignedDepartment]||ticket.assignedDepartment}</strong><em>Solo este departamento puede cambiar el responsable.</em></span></div>}
                            {ticketActionMode === "transfer" && edit.assignedDepartment === "TECHNOLOGY" && (
                              <label className="ticket-action-transfer">
                            Equipo de Tecnología
                            <select className="ticket-action-assign"
                              value={edit.assignedTeam}
                              onChange={(event) =>
                                setManagement((current) => ({
                                  ...current,
                                  [ticket.id]: {
                                    ...edit,
                                    assignedTeam: event.target
                                      .value as TechnologyTeam,
                                    assignedTechnicianId: "",
                                  },
                                }))
                              }
                            >
                              <option value="CALL_CENTER">Call Center</option>
                              <option value="TECHNICAL_FAILURE">
                                Avería Técnica
                              </option>
                              <option value="TECHNICIANS">Técnicos</option>
                            </select>
                              </label>
                            )}
                            <label className="ticket-responsible-selector"><span>{ticket.assignedTechnicianId?"Cambiar responsable del caso":"Responsable del caso"}</span><small>Selecciona otro técnico o deja el caso sin responsable para atención directa del soporte.</small><input
                          type="search"
                          value={responsibleQuery}
                          onChange={(event) => setResponsibleQuery(event.target.value)}
                          placeholder="Buscar por nombre..."
                          aria-label="Buscar responsable por nombre"
                        /><select
                          aria-label="Responsable asignado"
                          value={edit.assignedTechnicianId}
                          onChange={(event)=>setManagement((current)=>({...current,[ticket.id]:{...edit,assignedTechnicianId:event.target.value,assignedTeam:event.target.value&&edit.assignedDepartment==="TECHNOLOGY"?assigneeTeam(event.target.value,edit.assignedTeam):edit.assignedTeam}}))}
                        >
                          <option value="">Sin técnico asignado</option>
                          <optgroup label="Técnicos del departamento">
                          {ticketAssignees.filter(item=>!isSupervisorAssignee(item)).map((item) => (
                            <option value={item.id} key={item.id}>
                              {item.name} · {assigneeSuffix(item)}
                            </option>
                          ))}
                          </optgroup>
                          <optgroup label="Todos los supervisores">
                          {ticketAssignees.filter(isSupervisorAssignee).map((item)=><option value={item.id} key={item.id}>{item.name} · Supervisor</option>)}
                          </optgroup>
                            </select></label>
                          </>
                        )}
                        <select className="ticket-action-resolve"
                          aria-label="Estado de resolución"
                          value={
                            canDispatchTicket ||
                            ["IN_PROGRESS", "PENDING", "RESOLVED", "CLOSED"].includes(edit.status)
                              ? edit.status
                              : "RESOLVED"
                          }
                          onChange={(event) =>
                            setManagement((current) => ({
                              ...current,
                              [ticket.id]: {
                                ...edit,
                                status: event.target.value,
                              },
                            }))
                          }
                        >
                          {Object.entries(statuses)
                            .filter(([value]) =>
                              canDispatchTicket
                                ? true
                                : ["IN_PROGRESS", "PENDING", "RESOLVED", "CLOSED"].includes(value),
                            )
                            .map(([value, label]) => (
                            <option value={value} key={value}>
                              {label}
                            </option>
                            ))}
                        </select>
                        {canDispatchTicket && (
                          <select className="ticket-action-transfer"
                          aria-label="Prioridad del ticket"
                          value={edit.priority}
                          onChange={(event) =>
                            setManagement((current) => ({
                              ...current,
                              [ticket.id]: {
                                ...edit,
                                priority: event.target.value,
                              },
                            }))
                          }
                        >
                          {Object.entries(priorities).map(([value, label]) => (
                            <option value={value} key={value}>
                              {label}
                            </option>
                          ))}
                          </select>
                        )}
                        <textarea className="ticket-action-resolve"
                          value={edit.resolution}
                          onChange={(event) =>
                            setManagement((current) => ({
                              ...current,
                              [ticket.id]: {
                                ...edit,
                                resolution: event.target.value,
                              },
                            }))
                          }
                          placeholder={edit.status==="PENDING"?"Motivo de la espera: falta de material, tiempo u otra causa...":edit.status==="IN_PROGRESS"?"Nota opcional para reanudar el trabajo...":"Respuesta, diagnóstico o solución aplicada..."}
                          maxLength={2000}
                        />
                        {!resolvedStatuses.has(ticket.status) && (
                          <>
                            {canShareTicket && !isCallCenter && (
                              <>
                                <label className="ticket-action-transfer">
                              Nota de transferencia o seguimiento
                              <input
                                value={edit.routingComment}
                                onChange={(event) =>
                                  setManagement((current) => ({
                                    ...current,
                                    [ticket.id]: {
                                      ...edit,
                                      routingComment: event.target.value,
                                    },
                                  }))
                                }
                                maxLength={500}
                                placeholder="Contexto para el equipo que recibe el caso..."
                              />
                                </label>
                                <fieldset className="ticket-action-transfer"
                              style={{
                                display: "grid",
                                gap: 8,
                                gridColumn: "1 / -1",
                              }}
                            >
                              <legend>Compartir visibilidad con</legend>
                              <div
                                style={{
                                  display: "flex",
                                  flexWrap: "wrap",
                                  gap: 8,
                                }}
                              >
                                {catalog
                                  .filter(
                                    (department) =>
                                      department.code !==
                                      ticket.assignedDepartment,
                                  )
                                  .map((department) => {
                                    const checked =
                                      edit.sharedWithDepartments.includes(
                                        department.code,
                                      );
                                    return (
                                      <label key={department.code}>
                                        <input
                                          type="checkbox"
                                          checked={checked}
                                          onChange={() =>
                                            setManagement((current) => ({
                                              ...current,
                                              [ticket.id]: {
                                                ...edit,
                                                sharedWithDepartments: checked
                                                  ? edit.sharedWithDepartments.filter(
                                                      (code) =>
                                                        code !==
                                                        department.code,
                                                    )
                                                  : [
                                                      ...edit.sharedWithDepartments,
                                                      department.code,
                                                    ],
                                              },
                                            }))
                                          }
                                        />
                                        {department.name}
                                      </label>
                                    );
                                  })}
                              </div>
                                </fieldset>
                              </>
                            )}
                            {["RESOLVED","CLOSED"].includes(edit.status)&&<label
                              className="finding-photo-upload ticket-action-resolve ticket-evidence-dropzone"
                              style={{ gridColumn: "1 / -1" }}
                              onDragOver={(event) => {
                                event.preventDefault();
                                event.currentTarget.classList.add("is-dragging");
                              }}
                              onDragLeave={(event) => event.currentTarget.classList.remove("is-dragging")}
                              onDrop={(event) => {
                                event.preventDefault();
                                event.currentTarget.classList.remove("is-dragging");
                                void handleTicketEvidenceFiles(ticket, edit, Array.from(event.dataTransfer.files || []));
                              }}
                            >
                              <span>
                                <Upload /> Evidencia para resolver o cerrar
                                <small>
                                  1 imagen obligatoria · puedes agregar 2 adicionales o más · máximo 6 imágenes de 8 MB
                                </small>
                                <small className="ticket-evidence-drop-hint">Arrastra imágenes aquí o selecciónalas desde tus archivos.</small>
                                <small>
                                  {session.role==="GroupAdministrator"&&requiresTechnicianGpsEvidence(ticket)
                                    ? `Visita presencial obligatoria: el GPS debe coincidir con ${ticket.terminal} dentro de ${TICKET_AGENCY_RADIUS_METERS} m.`
                                    : requiresTechnicianGpsEvidence(ticket)
                                    ? "Visita técnica: la cámara capturará GPS, coordenadas y precisión al tomar las fotos."
                                    : "Asistencia remota: puedes adjuntar fotos normales sin GPS."}
                                </small>
                              </span>
                              <input
                                id={`ticket-resolution-evidence-${ticket.id}`}
                                name={`ticketResolutionEvidence-${ticket.id}`}
                                type="file"
                                multiple
                                accept="image/jpeg,image/png,image/webp"
                                capture={requiresTechnicianGpsEvidence(ticket) ? "environment" : undefined}
                                onChange={(event) => {
                                  const input = event.currentTarget;
                                  void handleTicketEvidenceFiles(ticket, edit, Array.from(input.files || []));
                                  input.value = "";
                                }}
                              />
                            </label>}
                            {["RESOLVED","CLOSED"].includes(edit.status)&&!!edit.evidence.length && (
                              <div className="ticket-captured-evidence ticket-action-resolve">
                                <div>{edit.evidence.map((file,index)=><figure key={`${file.name}-${index}`}><img src={URL.createObjectURL(file)} alt={`Evidencia ${index+1}`}/><figcaption>{file.name}</figcaption></figure>)}</div>
                                <small>{edit.evidence.length} evidencia{edit.evidence.length===1?"":"s"} lista{edit.evidence.length===1?"":"s"} para guardar.</small>
                                {ticketEvidenceLocations[ticket.id]&&<small className="ticket-captured-location"><MapPin/> GPS verificado · {ticketEvidenceLocations[ticket.id].latitude.toFixed(6)}, {ticketEvidenceLocations[ticket.id].longitude.toFixed(6)} · precisión ±{Math.round(ticketEvidenceLocations[ticket.id].accuracyMeters)} m{ticketEvidenceLocations[ticket.id].distanceMeters!=null?` · a ${Math.round(ticketEvidenceLocations[ticket.id].distanceMeters!)} m de la agencia`:""}</small>}
                              </div>
                            )}
                            <div className="ticket-action-command-bar"
                              style={{
                                display: "flex",
                                flexWrap: "wrap",
                                gap: 8,
                                gridColumn: "1 / -1",
                              }}
                            >
                              {ticketActionMode === "transfer" && canEscalateCallCenterTicket(ticket) && (
                                <button
                                  type="button"
                                  disabled={saving}
                                  onClick={() => void routeTicket(ticket, true)}
                                >
                                  <Share2 /> Compartir y escalar a Avería Técnica
                                </button>
                              )}
                              {ticketActionMode === "transfer" && canDispatchTicket &&
                                ticket.assignedDepartment === "TECHNOLOGY" &&
                                effectiveTechnologyTeam(ticket) ===
                                  "CALL_CENTER" && (
                                  <button
                                    type="button"
                                    disabled={saving}
                                    onClick={() =>
                                      void routeTicket(ticket, true)
                                    }
                                  >
                                    <Wrench /> Escalar a Avería Técnica
                                  </button>
                                )}
                              {((ticketActionMode === "transfer" && canDispatchTicket) || (ticketActionMode === "assign" && canAdministerTicket(ticket))) && (
                                <>
                                  <button
                                    type="button"
                                    disabled={saving}
                                    onClick={() => void routeTicket(ticket)}
                                  >
                                    <ArrowRight /> {ticketActionMode === "assign" ? (ticket.assignedTechnicianId?"Guardar nuevo responsable":"Guardar responsable") : "Transferir / reasignar"}
                                  </button>
                                  {ticketActionMode === "transfer" && canShareTicket && <button
                                    type="button"
                                    disabled={saving}
                                    onClick={() => void shareTicket(ticket)}
                                  >
                                    <Share2 /> Compartir ticket
                                  </button>}
                                </>
                              )}
                              {ticketActionMode === "resolve" && ["PENDING","IN_PROGRESS"].includes(edit.status) && <button
                                type="button"
                                disabled={saving||(edit.status==="PENDING"&&edit.resolution.trim().length<5)}
                                onClick={() => void updateTicket(ticket)}
                              >
                                <Clock3 /> {edit.status==="PENDING"?"Guardar como pendiente":"Reanudar en proceso"}
                              </button>}
                              {ticketActionMode === "resolve" && ["RESOLVED","CLOSED"].includes(edit.status) && <button
                                type="button"
                                disabled={saving}
                                onClick={() =>
                                  void resolveTicketWithEvidence(ticket)
                                }
                              >
                                <CheckCircle2 /> Resolver con evidencia
                              </button>}
                            </div>
                          </>
                        )}
                        {ticketActionMode === "transfer" && canDispatchTicket &&
                          session.permissions.canResolveTickets &&
                          !resolvedStatuses.has(edit.status) && (
                            <button
                              disabled={saving}
                              onClick={() => void updateTicket(ticket)}
                            >
                              <Send /> Guardar seguimiento
                            </button>
                          )}
                      </div>
                    )}
                    {isExpanded && ticketActionMode === "cancel" && canAdministerTicket(ticket) && (
                      <div className="ticket-cancel-panel">
                        <AlertTriangle/>
                        <span><strong>Anular ticket #{ticket.ticketNumber}</strong><small>La anulación es definitiva, conservará el historial y notificará a los involucrados.</small></span>
                        <label>Motivo de la anulación<textarea value={edit.resolution} onChange={event=>setManagement(current=>({...current,[ticket.id]:{...edit,resolution:event.target.value}}))} maxLength={2000} placeholder="Ejemplo: ticket duplicado, solicitud incorrecta o incidencia ya solucionada..."/></label>
                        <button type="button" disabled={saving||edit.resolution.trim().length<5} onClick={()=>void cancelTicket(ticket)}><AlertTriangle/>{saving?"Anulando...":"Confirmar anulación"}</button>
                      </div>
                    )}
                    {isExpanded && ticketActionMode === "evidence" && (ticket.evidence?.length || ticket.history?.length) && (
                      <div className="ticket-case-trace ticket-history-summary">
                        <History />
                        <span>
                          <strong>Historial y evidencias del caso</strong>
                          <small>
                            {ticket.history?.length || 0} movimientos ·{" "}
                            {ticket.evidence?.length || 0} evidencias
                          </small>
                        </span>
                        <button
                          type="button"
                          onClick={() =>
                            setExpandedHistoryTicketId((current) =>
                              current === ticket.id ? null : ticket.id,
                            )
                          }
                        >
                          {expandedHistoryTicketId === ticket.id
                            ? "Ocultar"
                            : "Ver historial"}
                        </button>
                      </div>
                    )}
                    {isExpanded && ticketActionMode === "evidence" && !ticket.evidence?.length && !ticket.history?.length && (
                      <div className="ticket-evidence-empty"><History /><strong>Este ticket todavía no tiene evidencias ni movimientos adicionales.</strong></div>
                    )}
                    {expandedHistoryTicketId === ticket.id && (
                      <div style={{ display: "grid", gap: 10 }}>
                        {(ticket.history || []).map((event) => (
                          <article
                            key={event.id}
                            style={{
                              padding: 10,
                              border: "1px solid #2c617f",
                              borderRadius: 10,
                            }}
                          >
                            <strong>
                              {historyActionLabels[event.action] ||
                                event.action}
                            </strong>
                            <small>
                              {event.actorName} ·{" "}
                              {new Date(event.createdAt).toLocaleString("es-DO")}
                            </small>
                            {(event.toDepartment || event.toTeam) && (
                              <p>
                                Destino: {departments[event.toDepartment || ""] ||
                                  event.toDepartment}
                                {event.toTeam
                                  ? ` · ${technologyTeams[event.toTeam]}`
                                  : ""}
                              </p>
                            )}
                            {event.comment && <p>{event.comment}</p>}
                          </article>
                        ))}
                        {!!ticket.evidence?.length && (
                          <div className="finding-evidence-gallery saved">
                            {ticket.evidence.map((file) => (
                              <div key={file.id} className="ticket-evidence-item">
                                <a href={file.url || `/api/tickets/evidence/${file.id}`} target="_blank" rel="noreferrer">
                                  {file.contentType.startsWith("image/") ? (
                                    <img src={file.url || `/api/tickets/evidence/${file.id}`} alt={`Evidencia ${file.fileName}`} loading="lazy" decoding="async" />
                                  ) : <span>{file.fileName}</span>}
                                </a>
                                {file.latitude != null && file.longitude != null ? (
                                  <a className="ticket-evidence-location" href={`https://www.google.com/maps?q=${file.latitude},${file.longitude}`} target="_blank" rel="noreferrer">
                                    GPS verificado{file.accuracyMeters != null ? ` · ±${Math.round(file.accuracyMeters)} m` : ""}
                                  </a>
                                ) : <small>Evidencia de asistencia remota</small>}
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}
                  </article>
                );
              })}
              {!visible.length && (
                <div className="ticket-empty">
                  <LifeBuoy />
                  <strong>No hay tickets con este filtro</strong>
                  <span>Las nuevas incidencias aparecerán aquí.</span>
                </div>
              )}
            </div>
          )}
          {!loading && visible.length > ticketsPerPage && (
            <nav className="ticket-pagination" aria-label="Páginas de tickets">
              <button
                type="button"
                disabled={ticketPage === 1}
                onClick={() => setTicketPage((page) => Math.max(1, page - 1))}
              >
                <ArrowLeft /> Anterior
              </button>
              <span>
                Página <strong>{ticketPage}</strong> de {ticketPageCount}
                <small>
                  Tickets {(ticketPage - 1) * ticketsPerPage + 1}–{Math.min(ticketPage * ticketsPerPage, visible.length)} de {visible.length}
                </small>
              </span>
              <button
                type="button"
                disabled={ticketPage === ticketPageCount}
                onClick={() => setTicketPage((page) => Math.min(ticketPageCount, page + 1))}
              >
                Siguiente <ArrowRight />
              </button>
            </nav>
          )}
        </section>
      </div>
      {supportStage === "findings" &&
        supportTab === "findings" &&
        findingView !== "menu" &&
        (!isSupportTechnician || findingView !== "tracking") && (
          <section className="automatic-findings">
            <header>
              <div>
                <span>
                  {findingView === "resolved"
                    ? "HISTORIAL DE SOLUCIONES"
                    : "EXPEDIENTES DE SOPORTE"}
                </span>
                <h2>
                  {findingView === "resolved"
                    ? "Soluciones verificadas"
                    : findingAssigneeKind === "TECHNICIANS"
                      ? "Asignados a técnicos"
                      : findingAssigneeKind === "SUPERVISORS"
                        ? "Asignados a supervisores"
                        : "Expedientes pendientes"}
                </h2>
                <p>
                  {findingView === "resolved"
                    ? "Consulta las soluciones registradas y el historial de atención."
                    : findingAssigneeKind === "ALL"
                      ? "Revisa todos los hallazgos pendientes organizados por tipo de expediente."
                      : "Consulta los hallazgos activos asignados y filtra por responsable."}
                </p>
              </div>
              <div className="automatic-finding-actions">
                {findingCategoryOpen&&<div className="finding-layout-toggle" role="group" aria-label="Ordenar hallazgos"><button type="button" className={findingLayout==="GRID"?"active":""} onClick={()=>setFindingLayout("GRID")}><LayoutGrid/> Tarjetas</button><button type="button" className={findingLayout==="LIST"?"active":""} onClick={()=>setFindingLayout("LIST")}><List/> Lista</button></div>}
                <label>
                  <Building2 /> Filtrar grupo
                  <select
                    value={findingGroup}
                    onChange={(event) => setFindingGroup(event.target.value)}
                  >
                    <option value="ALL">Todos los grupos</option>
                    {findingGroups.map((group) => (
                      <option key={group} value={group}>
                        {group}
                      </option>
                    ))}
                  </select>
                </label>
                {findingAssigneeKind!=="ALL"&&<label><UserRoundCheck/> Filtrar responsable<select id="finding-assignee-filter" name="findingAssigneeFilter" value={findingAssigneeFilter} onChange={event=>setFindingAssigneeFilter(event.target.value)}><option value="ALL">Todos los {findingAssigneeKind==="TECHNICIANS"?"técnicos":"supervisores"}</option>{findingAssigneeChoices.map(item=><option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
                {findingView === "resolved" && (
                  <button
                    className="export-solutions"
                    onClick={exportResolvedFindings}
                  >
                    <Send /> Descargar auditoría de soluciones
                  </button>
                )}
              </div>
            </header>
            {!findingCategoryOpen&&<nav className="finding-category-cards" aria-label="Categorías de hallazgos">{findingsByType.map(section=><button type="button" key={section.key} onClick={()=>setFindingCategoryOpen(section.key)}><span><AlertTriangle/></span><div><em>{departments[section.department]||section.department} · CATEGORÍA</em><strong>{section.title}</strong><small>{section.rows.length} {section.rows.length===1?"hallazgo":"hallazgos"}</small></div><ArrowRight/></button>)}</nav>}
            {!findingCategoryOpen&&!findingsByType.length&&<div className="ticket-empty finding-category-empty"><CheckCircle2/><strong>No hay hallazgos para los filtros seleccionados</strong><span>Las nuevas condiciones pendientes aparecerán automáticamente.</span></div>}
            {findingCategoryOpen&&<button type="button" className="finding-category-back" onClick={()=>setFindingCategoryOpen(null)}><ArrowLeft/> Volver a categorías</button>}
            {findingCategoryOpen&&
            <div className={`finding-table-stack finding-layout-${findingLayout.toLowerCase()}`}>
              {findingsByType.filter(section=>section.key===findingCategoryOpen).map((section) => (
                <article className="finding-table-card" key={section.key}>
                  <header>
                    <div>
                      <AlertTriangle />
                      <span>
                        <strong>{section.title}</strong>
                        <small>
                          {section.rows.length} agencias encontradas
                        </small>
                      </span>
                    </div>
                    <em>{section.rows.length}</em>
                  </header>
                  <div className="finding-table">
                    <div className="finding-row finding-head">
                      <span>Código</span>
                      <span>Agencia</span>
                      <span>Grupo</span>
                      <span>Detalle</span>
                      <span>Prioridad</span>
                      <span>Departamento</span>
                      <span>Fecha auditoría</span>
                      <span>Acciones</span>
                    </div>
                    {section.rows.map((row) => (
                      <div className="finding-row finding-ticket-card" key={row.id} role="button" tabIndex={0} aria-label={`Abrir expediente completo de ${row.title} en ${row.terminal}`} onClick={()=>{setFindingActionMode(resolvedStatuses.has(row.status)?"evidence":"details");setSelectedFindingId(row.id);setFindingView("tracking");}} onKeyDown={event=>{if(event.key!=="Enter"&&event.key!==" ")return;event.preventDefault();setFindingActionMode(resolvedStatuses.has(row.status)?"evidence":"details");setSelectedFindingId(row.id);setFindingView("tracking");}}>
                        <span data-label="Código">
                          <strong>{row.codigo}</strong>
                        </span>
                        <span data-label="Agencia">{row.terminal}</span>
                        <span data-label="Grupo">{row.grupo}</span>
                        <span data-label="Detalle">{row.detail}</span>
                        <span data-label="Prioridad">
                          <em
                            className={`ticket-priority priority-${row.priority.toLowerCase()}`}
                          >
                            {priorities[row.priority]}
                          </em>
                        </span>
                        <span data-label="Departamento">{departments[row.department]}</span>
                        <span data-label="Fecha de auditoría">
                          {new Date(row.submittedAt).toLocaleString("es-DO")}
                        </span>
                        <span className="finding-card-actions" data-label="Acciones">
                          <button type="button" className="finding-open-ticket" onClick={event=>{event.stopPropagation();setFindingActionMode(resolvedStatuses.has(row.status)?"evidence":"details");setSelectedFindingId(row.id);setFindingView("tracking");}}><span>Ver ticket completo</span><ArrowRight/></button>
                        </span>
                      </div>
                    ))}
                  </div>
                </article>
              ))}
              {!findingsByType.length && (
                <div className="ticket-empty">
                  <CheckCircle2 />
                  <strong>No hay hallazgos para los filtros seleccionados</strong>
                  <span>
                    Las auditorías con condiciones pendientes aparecerán
                    automáticamente.
                  </span>
                </div>
              )}
            </div>}
          </section>
        )}
      {formOpen && (
        <div className="ticket-modal-backdrop">
          <section className="ticket-modal support-catalog-modal">
            <header>
              <div>
                <span>{draft.ticketType === "INTERNAL" ? "NUEVA SOLICITUD INTERNA" : "NUEVA INCIDENCIA"}</span>
                <h2>{draft.ticketType === "INTERNAL" ? "Crear ticket interno" : "Crear ticket de soporte"}</h2>
                <p>
                  Selecciona el servicio especializado del departamento elegido.
                </p>
              </div>
              <button onClick={() => { setFormOpen(false); setSupervisorEscalationReason(""); setSupervisorCreationEvidence([]); }}>
                <X />
              </button>
            </header>
            <form onSubmit={createTicket}>
              <div className="selected-support-department">
                <button type="button" onClick={() => { setFormOpen(false); setSupervisorEscalationReason(""); setSupervisorCreationEvidence([]); }}>
                  ← Cambiar departamento
                </button>
                <span>
                  <small>DEPARTAMENTO SELECCIONADO</small>
                  <strong>
                    {
                      catalog.find(
                        (item) => item.code === draft.assignedDepartment,
                      )?.name
                    }
                  </strong>
                  <em className="ticket-auto">Asignado automáticamente</em>
                </span>
              </div>
              {!draft.category && (
                <>
                  <div className="modal-step-heading">
                    <span>02 · TIPO DE SOPORTE</span>
                    <h3>Selecciona el ticket que necesitas</h3>
                  </div>
                  <div className="support-category-grid">
                    {catalog
                      .find((item) => item.code === draft.assignedDepartment)
                      ?.categories.filter((item) => item.isActive)
                      .map((category) => {
                        const categoryPriority = ticketPriorityForCategory(
                          draft.assignedDepartment,
                          category.code,
                          category.defaultPriority,
                        );
                        const selected = selectedCases.some(
                          (item) =>
                            item.department === draft.assignedDepartment &&
                            item.category === category.code,
                        );
                        return (
                          <button
                            type="button"
                            key={category.id}
                            className={selected ? "active" : ""}
                            aria-pressed={selected}
                            onClick={() =>
                              setSelectedCases((current) =>
                                selected
                                  ? current.filter(
                                      (item) =>
                                        !(
                                          item.department ===
                                            draft.assignedDepartment &&
                                          item.category === category.code
                                        ),
                                    )
                                  : [
                                      ...current,
                                      {
                                        department: draft.assignedDepartment,
                                        category: category.code,
                                      },
                                    ],
                              )
                            }
                          >
                            <input
                              type="checkbox"
                              checked={selected}
                              readOnly
                              tabIndex={-1}
                              aria-hidden="true"
                            />
                            <span>
                              <strong>{category.name}</strong>
                              <small>{category.description}</small>
                            </span>
                            <em
                              className={`ticket-priority priority-${categoryPriority.toLowerCase()}`}
                            >
                              {priorities[categoryPriority]}
                            </em>
                          </button>
                        );
                      })}
                  </div>
                  <button
                    type="button"
                    className="support-category-continue"
                    disabled={!selectedCases.length}
                    onClick={() => {
                      const first = selectedCases[0];
                      const category = catalog
                        .find((item) => item.code === first.department)
                        ?.categories.find((item) => item.code === first.category);
                      if (!category) return;
                      setDraft({
                        ...draft,
                        category: category.code,
                        priority: ticketPriorityForCategory(
                          first.department,
                          first.category,
                          category.defaultPriority,
                        ),
                        subject: category.name,
                        detail: "",
                        option: "",
                      });
                    }}
                  >
                    Continuar con {selectedCases.length || 0} caso
                    {selectedCases.length === 1 ? "" : "s"}
                    <ArrowRight />
                  </button>
                </>
              )}
              {draft.category && (
                <div className="ticket-detail-step">
                  <div className="modal-step-heading">
                    <span>03 · DATOS DEL TICKET</span>
                    <h3>Completa la solicitud</h3>
                  </div>
                  {reportAgency ? (
                    <div className="report-agency-context">
                      <Building2 />
                      <span>
                        <small>AGENCIA SELECCIONADA</small>
                        <strong>{reportAgency.terminal}</strong>
                        <em>
                          {reportAgency.codigo} · {reportAgency.grupo}
                        </em>
                      </span>
                    </div>
                  ) : (
                    <>
                      <label className="ticket-agency-search">
                        Buscar agencia
                        <Search />
                        <input
                          value={agencyQuery}
                          onChange={(event) =>
                            setAgencyQuery(event.target.value)
                          }
                          placeholder="Código, terminal o grupo..."
                        />
                      </label>
                      <label>
                        Agencia
                        <select
                          required
                          value={draft.agencyId}
                          onChange={(event) =>
                            setDraft({ ...draft, agencyId: event.target.value })
                          }
                        >
                          <option value="">Seleccionar agencia...</option>
                          {visibleAgencies.map((agency) => (
                            <option key={agency.id} value={agency.id}>
                              {agency.codigo} · {agency.terminal} ·{" "}
                              {agency.grupo}
                            </option>
                          ))}
                        </select>
                      </label>
                    </>
                  )}
                  {session.role !== "GroupAdministrator" && !isCallCenter && <label>
                    Responsable asignado (opcional)
                    <input
                      type="search"
                      value={responsibleQuery}
                      onChange={(event) => setResponsibleQuery(event.target.value)}
                      placeholder="Buscar responsable por nombre..."
                      aria-label="Buscar responsable por nombre"
                    />
                    <select value={draft.assignedTechnicianId} onChange={(event)=>setDraft({...draft,assignedTechnicianId:event.target.value,assignedTeam:event.target.value&&draft.assignedDepartment==="TECHNOLOGY"?assigneeTeam(event.target.value,draft.assignedTeam):draft.assignedTeam})}>
                      <option value="">Asignar más adelante</option>
                      <optgroup label="Responsables del área">{visibleDraftAssignees.filter(item=>!isSupervisorAssignee(item)).map(item=><option key={item.id} value={item.id}>{item.name} · {assigneeSuffix(item)}</option>)}</optgroup>
                      <optgroup label="Todos los supervisores">{visibleDraftAssignees.filter(isSupervisorAssignee).map(item=><option key={item.id} value={item.id}>{item.name} · Supervisor</option>)}</optgroup>
                    </select>
                  </label>}
                  {isCallCenter && (
                    <div className="ticket-auto-responsible" role="status">
                      <UserRoundCheck />
                      <span>
                        <strong>Responsable automático</strong>
                        <small>Este ticket quedará asignado a tu usuario. Si necesitas apoyo, usa Compartir para escalarlo a Avería Técnica.</small>
                      </span>
                    </div>
                  )}
                  {possibleDuplicateTickets.length > 0 && (
                    <aside className="ticket-duplicate-warning" role="alert">
                      <AlertTriangle />
                      <div>
                        <strong>Ya hay un ticket de esta avería para esta agencia</strong>
                        <p>
                          {possibleDuplicateTickets.length === 1
                            ? `El ticket #${possibleDuplicateTickets[0].ticketNumber} continúa activo.`
                            : `Encontramos ${possibleDuplicateTickets.length} tickets activos con el mismo tipo de incidencia.`}
                          {" "}Puedes revisarlo antes de generar otro.
                        </p>
                        <div>
                          <button type="button" onClick={() => openPossibleDuplicate(possibleDuplicateTickets[0])}>
                            <LifeBuoy /> Ver ticket duplicado
                          </button>
                          <button
                            type="button"
                            className="continue"
                            onClick={() => {
                              setDuplicateCreationConfirmed(duplicateSignature);
                              setError("");
                            }}
                          >
                            <ArrowRight /> Continuar con el nuevo ticket
                          </button>
                        </div>
                        {duplicateCreationConfirmed === duplicateSignature && (
                          <small>Creación del nuevo ticket confirmada. Ya puedes enviarlo.</small>
                        )}
                      </div>
                    </aside>
                  )}
                  <div className="selected-case-detail-list">
                    {selectedCases.map((supportCase, index) => {
                      const category = catalog
                        .find((item) => item.code === supportCase.department)
                        ?.categories.find(
                          (item) => item.code === supportCase.category,
                      );
                      if (!category) return null;
                      const categoryPriority = ticketPriorityForCategory(
                        supportCase.department,
                        category.code,
                        category.defaultPriority,
                      );
                      const key = `${supportCase.department}:${supportCase.category}`;
                      const fields = caseDetails[key] || {
                        option: "",
                        detail: "",
                      };
                      return (
                        <section key={key} className="selected-case-detail">
                          <header>
                            <span>{String(index + 1).padStart(2, "0")}</span>
                            <div>
                              <strong>{category.name}</strong>
                              <small>
                                {priorities[categoryPriority]} · Caso
                                independiente
                              </small>
                            </div>
                          </header>
                          {category.options?.length ? (
                            <label>
                              Tipo específico de {category.name}
                              <select
                                required
                                value={fields.option}
                                onChange={(event) =>
                                  setCaseDetails((current) => ({
                                    ...current,
                                    [key]: {
                                      ...fields,
                                      option: event.target.value,
                                    },
                                  }))
                                }
                              >
                                <option value="">Seleccionar...</option>
                                {category.options.map((option) => (
                                  <option key={option}>{option}</option>
                                ))}
                              </select>
                            </label>
                          ) : null}
                          <label>
                            {category.detailLabel ||
                              `Detalle de ${category.name}`}
                            <textarea
                              required={category.requiresDetail}
                              maxLength={1000}
                              rows={3}
                              value={fields.detail}
                              onChange={(event) =>
                                setCaseDetails((current) => ({
                                  ...current,
                                  [key]: {
                                    ...fields,
                                    detail: event.target.value,
                                  },
                                }))
                              }
                              placeholder={`Describe únicamente el caso: ${category.name}`}
                            />
                          </label>
                        </section>
                      );
                    })}
                  </div>
                  <label>
                    Descripción general
                    <textarea
                      required
                      maxLength={2000}
                      rows={5}
                      value={draft.description}
                      onChange={(event) =>
                        setDraft({ ...draft, description: event.target.value })
                      }
                      placeholder="Explica qué ocurrió, desde cuándo y cómo afecta la operación..."
                    />
                    <small>{draft.description.length}/2000</small>
                  </label>
                  {session.role === "GroupAdministrator" && (
                    <section className="supervisor-ticket-escalation-evidence">
                      <header>
                        <span><ShieldCheck /></span>
                        <div>
                          <strong>Evidencia obligatoria del supervisor</strong>
                          <small>Antes de enviarlo al departamento, demuestra qué revisaste y por qué no puede resolverse en la agencia.</small>
                        </div>
                      </header>
                      <label>
                        ¿Por qué necesitas escalar este caso?
                        <textarea
                          required
                          minLength={15}
                          maxLength={1000}
                          rows={4}
                          value={supervisorEscalationReason}
                          onChange={(event) => setSupervisorEscalationReason(event.target.value)}
                          placeholder="Indica las verificaciones realizadas, el resultado y qué impide resolverlo localmente..."
                        />
                        <small>{supervisorEscalationReason.length}/1000 · mínimo 15 caracteres</small>
                      </label>
                      <div className="supervisor-evidence-upload">
                        <input
                          id="supervisor-ticket-evidence"
                          type="file"
                          accept="image/jpeg,image/png,image/webp,.heic,.heif"
                          capture="environment"
                          multiple
                          onChange={async (event) => {
                            const selected = Array.from(event.target.files || []);
                            event.target.value = "";
                            if (!selected.length) return;
                            if (supervisorCreationEvidence.length + selected.length > 4) {
                              setError("Puedes adjuntar hasta 4 fotografías de evidencia.");
                              return;
                            }
                            setError("");
                            try {
                              const prepared = await Promise.all(selected.map(prepareAgencyEvidence));
                              setSupervisorCreationEvidence((current) => [...current, ...prepared]);
                            } catch (evidenceError) {
                              setError((evidenceError as Error).message);
                            }
                          }}
                        />
                        <label htmlFor="supervisor-ticket-evidence">
                          <Camera />
                          <span><strong>Tomar o adjuntar fotografías</strong><small>JPG, PNG o WebP · máximo 4 archivos</small></span>
                        </label>
                      </div>
                      {supervisorCreationEvidence.length > 0 && (
                        <div className="supervisor-evidence-files">
                          {supervisorCreationEvidence.map((file, index) => (
                            <article key={`${file.name}-${file.lastModified}-${index}`}>
                              <Camera />
                              <span><strong>{file.name}</strong><small>{Math.max(1, Math.round(file.size / 1024))} KB</small></span>
                              <button type="button" aria-label={`Quitar ${file.name}`} onClick={() => setSupervisorCreationEvidence((current) => current.filter((_, fileIndex) => fileIndex !== index))}><X /></button>
                            </article>
                          ))}
                        </div>
                      )}
                    </section>
                  )}
                  <footer>
                    <button
                      type="button"
                      onClick={() => {
                        setDraft({
                          ...draft,
                          category: "",
                          detail: "",
                          option: "",
                        });
                        setSelectedCases([]);
                        setCaseDetails({});
                      }}
                    >
                      Volver a tipos
                    </button>
                    <button
                      className="primary"
                      disabled={
                        saving ||
                        !draft.assignedDepartment ||
                        !draft.category ||
                        selectedCases.length === 0 ||
                        (session.role === "GroupAdministrator" &&
                          (supervisorEscalationReason.trim().length < 15 || supervisorCreationEvidence.length === 0))
                      }
                    >
                      <Send />
                      {saving ? "Enviando..." : "Crear ticket"}
                    </button>
                  </footer>
                </div>
              )}
            </form>
          </section>
        </div>
      )}
      {isSupportTechnician &&
        supportStage === "findings" &&
        supportTab === "findings" &&
        findingView === "tracking" && (
          <div className="ticket-modal-backdrop finding-action-backdrop">
          <section className="finding-resolution-panel finding-action-modal">
            <header>
              <div>
                <ShieldCheck />
                <span>
                  <strong>{findingActionMode==="assign"?"Asignar o cambiar responsable":findingActionMode==="evidence"?"Expediente completo del hallazgo":findingActionMode==="details"?"Ticket completo del hallazgo":"Resolver con evidencia"}</strong>
                  <small>
                    {findingActionMode==="evidence"?"Consulta diagnóstico, solución, trazabilidad y evidencias como una auditoría completa.":findingActionMode==="details"?"Revisa todos los datos antes de asignar o resolver el hallazgo.":"El sistema calcula el tiempo desde la detección hasta la solución."}
                  </small>
                </span>
                <button type="button" className="finding-modal-close" onClick={()=>{setFindingAssigneeKind("ALL");setFindingAssigneeFilter("ALL");setSelectedFindingId(null);setFindingView("pending");}} aria-label="Cerrar"><X/></button>
              </div>
            </header>
            <div>
              {visibleFindings.filter(finding=>!selectedFindingId||finding.id===selectedFindingId).map((finding) => (
                <article
                  key={`resolve-${finding.id}`}
                  className={finding.status === "RESOLVED" ? "resolved" : ""}
                >
                  <div>
                    <strong>{finding.title}</strong>
                    <span>
                      {finding.codigo} · {finding.terminal}
                    </span>
                    <small>{finding.grupo}</small>
                    <small><MapPin /> {[finding.direccion,finding.sector,finding.municipio,finding.provincia].filter(Boolean).join(" · ") || "Dirección pendiente"}</small>
                    {finding.latitude != null && finding.longitude != null && <a href={`https://www.openstreetmap.org/?mlat=${finding.latitude}&mlon=${finding.longitude}#map=19/${finding.latitude}/${finding.longitude}`} target="_blank" rel="noreferrer">Ver ubicación de las fotos</a>}
                    {findingActionMode==="assign"&&<label className="finding-assignment-control">
                      Técnico o supervisor asignado
                        <select value={finding.assignedTechnicianId||""} onChange={(event)=>{const assignedId=event.target.value;setFindingAssigneeKind("ALL");setFindingAssigneeFilter("ALL");void assignFinding(finding,assignedId);}}>
                          <option value="">Sin asignar</option>
                          {technicians.filter((item)=>(item.isTechnician&&item.department===finding.department)||item.isAgencySupervisor||item.role==="GroupAdministrator").map((item)=><option key={item.id} value={item.id}>{item.name}{item.isAgencySupervisor||item.role==="GroupAdministrator"?" · Supervisor":" · Técnico"}</option>)}
                      </select>
                    </label>}
                  </div>
                  {findingActionMode === "details" && (
                    <div className="finding-ticket-complete">
                      <div className="finding-ticket-badges"><span className={`ticket-status status-${finding.status.toLowerCase()}`}>{statuses[finding.status]||finding.status}</span><em className={`ticket-priority priority-${finding.priority.toLowerCase()}`}>{priorities[finding.priority]||finding.priority}</em></div>
                      <section><AlertTriangle/><span><small>HALLAZGO DETECTADO</small><strong>{finding.detail}</strong></span></section>
                      <section><Building2/><span><small>AGENCIA Y GRUPO</small><strong>{finding.codigo} · {finding.terminal}</strong><em>{finding.grupo}</em></span></section>
                      <section><UsersRound/><span><small>DEPARTAMENTO RESPONSABLE</small><strong>{departments[finding.department]||finding.department}</strong><em>{finding.assignedTechnicianName?`Asignado a ${finding.assignedTechnicianName}`:"Pendiente de asignación"}</em></span></section>
                      <section><Clock3/><span><small>FECHA DE DETECCIÓN</small><strong>{new Date(finding.submittedAt).toLocaleString("es-DO")}</strong></span></section>
                      <div className="finding-ticket-complete-actions">
                        <button type="button" onClick={()=>setFindingActionMode("assign")}><UserRoundCheck/>{finding.assignedTechnicianId?"Cambiar responsable":"Asignar responsable"}</button>
                        <button type="button" className="resolve" onClick={()=>setFindingActionMode("resolve")}><ShieldCheck/>Resolver hallazgo</button>
                      </div>
                    </div>
                  )}
                  {findingActionMode === "evidence" && (
                    <div className="finding-resolved-result finding-audit-complete">
                      <CheckCircle2 />
                      <span>
                        <strong>Resuelto</strong>
                        <small>
                          <b>Diagnóstico:</b> {finding.diagnosis}
                        </small>
                        <small>
                          <b>Solución:</b> {finding.resolution}
                        </small>
                        <small>
                          <b>Verificación:</b> {finding.verification}
                        </small>
                        <em>
                          Tiempo de solución:{" "}
                          {finding.resolutionMinutes == null
                            ? "calculando"
                            : finding.resolutionMinutes < 60
                              ? `${finding.resolutionMinutes} minutos`
                              : `${Math.floor(finding.resolutionMinutes / 60)} h ${finding.resolutionMinutes % 60} min`}
                        </em>
                        <div className="finding-evidence-gallery saved">
                          {(finding.evidence || []).map((photo) => (
                            <div key={photo.id} className="ticket-evidence-item"><a
                              href={`/api/tickets/findings/evidence/${photo.id}`}
                              target="_blank"
                              rel="noreferrer"
                              title={photo.fileName}
                            >
                              <img
                                src={`/api/tickets/findings/evidence/${photo.id}`}
                                alt={`Evidencia ${photo.fileName}`}
                                loading="lazy"
                                decoding="async"
                              />
                            </a>{photo.latitude!=null&&photo.longitude!=null&&<a className="ticket-evidence-location" href={`https://www.google.com/maps?q=${photo.latitude},${photo.longitude}`} target="_blank" rel="noreferrer">GPS verificado{photo.accuracyMeters!=null?` · ±${Math.round(photo.accuracyMeters)} m`:""}</a>}</div>
                          ))}
                        </div>
                      </span>
                    </div>
                  )}
                  {findingActionMode === "resolve" && (
                    <div className="finding-resolve-form">
                      <label>
                        Diagnóstico técnico
                        <textarea
                          maxLength={2000}
                          value={workForFinding(finding.id).diagnosis}
                          onChange={(event) =>
                            updateFindingWork(finding.id, {
                              diagnosis: event.target.value,
                            })
                          }
                          placeholder="Indica la causa encontrada y las pruebas realizadas..."
                        />
                      </label>
                      <label>
                        Trabajo y solución aplicada
                        <textarea
                          maxLength={3000}
                          value={workForFinding(finding.id).resolution}
                          onChange={(event) =>
                            updateFindingWork(finding.id, {
                              resolution: event.target.value,
                            })
                          }
                          placeholder="Describe paso a paso el trabajo realizado..."
                        />
                      </label>
                      <label>
                        Verificación final
                        <textarea
                          maxLength={2000}
                          value={workForFinding(finding.id).verification}
                          onChange={(event) =>
                            updateFindingWork(finding.id, {
                              verification: event.target.value,
                            })
                          }
                          placeholder="Explica cómo confirmaste que la avería quedó resuelta..."
                        />
                      </label>
                      <label>
                        Recomendaciones preventivas
                        <textarea
                          maxLength={2000}
                          value={workForFinding(finding.id).recommendations}
                          onChange={(event) =>
                            updateFindingWork(finding.id, {
                              recommendations: event.target.value,
                            })
                          }
                          placeholder="Mantenimiento, materiales o acciones recomendadas (opcional)..."
                        />
                      </label>
                      <label className="finding-photo-upload">
                        <span>
                          <Plus /> Evidencias fotográficas
                          <small>
                            1 imagen obligatoria · puedes agregar 2 o más adicionales · máximo 6 imágenes de 8 MB
                          </small>
                        </span>
                        <input
                          type="file"
                          multiple
                          accept="image/jpeg,image/png,image/webp"
                          capture={(()=>{const assignee=technicians.find(item=>item.id===finding.assignedTechnicianId);return assignee&&!(assignee.isAgencySupervisor||assignee.role==="GroupAdministrator")?"environment":undefined;})()}
                          onChange={async(event) => {
                            const input=event.currentTarget;
                            const photos = Array.from(input.files || []);
                            if (photos.length > 6) {
                              setError(
                                "Puedes adjuntar un máximo de 6 fotografías.",
                              );
                              input.value = "";
                              return;
                            }
                            const assignee=technicians.find(item=>item.id===finding.assignedTechnicianId);
                            if(assignee&&!(assignee.isAgencySupervisor||assignee.role==="GroupAdministrator")){
                              if(!("geolocation" in navigator)){setError("Este dispositivo no permite capturar el GPS requerido.");input.value="";return;}
                              try{const position=await new Promise<GeolocationPosition>((resolve,reject)=>navigator.geolocation.getCurrentPosition(resolve,reject,{enableHighAccuracy:true,timeout:20000,maximumAge:0}));setTicketEvidenceLocations(current=>({...current,[finding.id]:{latitude:position.coords.latitude,longitude:position.coords.longitude,accuracyMeters:position.coords.accuracy,capturedAt:new Date(position.timestamp).toISOString()}}));}catch{setError("Activa la ubicación y permite el GPS para tomar las evidencias del técnico.");input.value="";return;}
                            }
                            updateFindingWork(finding.id, { photos });
                          }}
                        />
                      </label>
                      {!!workForFinding(finding.id).photos.length && (
                        <div className="finding-evidence-gallery">
                          {workForFinding(finding.id).photos.map(
                            (photo, photoIndex) => (
                              <figure key={`${photo.name}-${photoIndex}`}>
                                <img
                                  src={URL.createObjectURL(photo)}
                                  alt={`Vista previa ${photo.name}`}
                                />
                                <figcaption>{photo.name}</figcaption>
                                <button
                                  type="button"
                                  onClick={() =>
                                    updateFindingWork(finding.id, {
                                      photos: workForFinding(
                                        finding.id,
                                      ).photos.filter(
                                        (_, index) => index !== photoIndex,
                                      ),
                                    })
                                  }
                                >
                                  <X />
                                </button>
                              </figure>
                            ),
                          )}
                        </div>
                      )}
                      {ticketEvidenceLocations[finding.id]&&<small className="ticket-captured-location"><MapPin/> GPS verificado · {ticketEvidenceLocations[finding.id].latitude.toFixed(6)}, {ticketEvidenceLocations[finding.id].longitude.toFixed(6)} · precisión ±{Math.round(ticketEvidenceLocations[finding.id].accuracyMeters)} m</small>}
                      <button
                        className="resolve-finding-submit"
                        disabled={saving}
                        onClick={() => void resolveFinding(finding)}
                      >
                        <ShieldCheck /> Guardar evidencia y marcar resuelto
                      </button>
                    </div>
                  )}
                  {findingActionMode==="assign"&&<div className="finding-assignment-help"><UserRoundCheck/><strong>{finding.assignedTechnicianName?`Caso asignado a ${finding.assignedTechnicianName}`:"Selecciona el responsable del caso"}</strong><small>Los técnicos deberán resolver con fotografías GPS. Los supervisores pueden adjuntar fotografías normales.</small></div>}
                </article>
              ))}
            </div>
          </section>
          </div>
        )}
      {session.permissions.canDeleteSupportTickets &&
        supportStage === "tickets" &&
        supportTab === "tickets" && (
          <section className="admin-ticket-control">
            <header>
              <div>
                <ShieldCheck />
                <span>
                  <strong>Control administrativo de soportes</strong>
                  <small>
                    Vista del departamento para eliminar registros incorrectos o
                    duplicados. Cada eliminación queda auditada.
                  </small>
                </span>
              </div>
              <button
                type="button"
                className="admin-ticket-control-toggle"
                aria-expanded={adminControlOpen}
                onClick={() => setAdminControlOpen((open) => !open)}
              >
                 {countedTickets.length} tickets · {adminControlOpen ? "Ocultar" : "Abrir control"}
                <ArrowRight />
              </button>
            </header>
            {adminControlOpen && <div>
               {countedTickets.map((ticket) => (
                <article key={`admin-${ticket.id}`}>
                  <div>
                    <strong>
                      #{ticket.ticketNumber} · {ticket.subject}
                    </strong>
                    <span>
                      {ticket.codigo} · {ticket.terminal}
                    </span>
                    <small>
                      {ticket.grupo} · {ticket.createdByName} ·{" "}
                      {statuses[ticket.status]}
                    </small>
                  </div>
                  <button
                    disabled={saving}
                    onClick={() => void deleteTicket(ticket)}
                  >
                    <X /> Eliminar soporte
                  </button>
                </article>
              ))}
            </div>}
          </section>
        )}
      {workspaceDepartment &&
        session.role !== "GroupAdministrator" &&
        session.permissions.canUseSupportChat && (
        <div id="support-chat-section" className="support-chat-section"><SupportChat
          session={session}
          onSessionExpired={onSessionExpired}
          departmentScope={workspaceDepartment}
          openConversationId={
            navigationTarget?.kind === "CHAT"
              ? navigationTarget.conversationId
              : null
          }
          openRequestKey={navigationTarget?.requestKey}
        /></div>
      )}
      {configOpen && (
        <div className="ticket-modal-backdrop">
          <section className="ticket-modal support-config-modal">
            <header>
              <div>
                <span>CONFIGURACIÓN CORPORATIVA</span>
                <h2>Catálogo de soporte</h2>
                <p>
                  Administra departamentos, servicios, prioridades y formularios
                  condicionales.
                </p>
              </div>
              <button onClick={() => setConfigOpen(false)}>
                <X />
              </button>
            </header>
            <div className="support-config-content">
              <form onSubmit={saveDepartment} className="config-editor">
                <h3>Agregar o editar departamento</h3>
                <div className="ticket-form-grid">
                  <label>
                    Código
                    <input
                      required
                      value={departmentEdit.code}
                      onChange={(event) =>
                        setDepartmentEdit({
                          ...departmentEdit,
                          code: event.target.value
                            .toUpperCase()
                            .replace(/[^A-Z0-9_]/g, ""),
                        })
                      }
                    />
                  </label>
                  <label>
                    Nombre
                    <input
                      required
                      value={departmentEdit.name}
                      onChange={(event) =>
                        setDepartmentEdit({
                          ...departmentEdit,
                          name: event.target.value,
                        })
                      }
                    />
                  </label>
                  <label>
                    Color
                    <input
                      type="color"
                      value={departmentEdit.accentColor}
                      onChange={(event) =>
                        setDepartmentEdit({
                          ...departmentEdit,
                          accentColor: event.target.value,
                        })
                      }
                    />
                  </label>
                  <label>
                    Orden
                    <input
                      type="number"
                      value={departmentEdit.displayOrder}
                      onChange={(event) =>
                        setDepartmentEdit({
                          ...departmentEdit,
                          displayOrder: Number(event.target.value),
                        })
                      }
                    />
                  </label>
                </div>
                <label>
                  Descripción
                  <input
                    value={departmentEdit.description}
                    onChange={(event) =>
                      setDepartmentEdit({
                        ...departmentEdit,
                        description: event.target.value,
                      })
                    }
                  />
                </label>
                <button className="primary" disabled={saving}>
                  <Plus /> Guardar departamento
                </button>
              </form>
              <form onSubmit={saveCategory} className="config-editor">
                <h3>{categoryEdit.id ? "Editar" : "Crear"} tipo de soporte</h3>
                <div className="ticket-form-grid">
                  <label>
                    Departamento
                    <select
                      required
                      value={categoryEdit.departmentCode || ""}
                      onChange={(event) =>
                        setCategoryEdit({
                          ...categoryEdit,
                          departmentCode: event.target.value,
                        })
                      }
                    >
                      <option value="">Seleccionar...</option>
                      {catalog.map((item) => (
                        <option key={item.code} value={item.code}>
                          {item.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Nombre
                    <input
                      required
                      value={categoryEdit.name || ""}
                      onChange={(event) =>
                        setCategoryEdit({
                          ...categoryEdit,
                          name: event.target.value,
                          code: categoryEdit.code || event.target.value,
                        })
                      }
                    />
                  </label>
                  <label>
                    Prioridad
                    <select
                      value={categoryEdit.defaultPriority}
                      onChange={(event) =>
                        setCategoryEdit({
                          ...categoryEdit,
                          defaultPriority: event.target.value,
                        })
                      }
                    >
                      {Object.entries(priorities).map(([value, label]) => (
                        <option value={value} key={value}>
                          {label}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Orden
                    <input
                      type="number"
                      value={categoryEdit.displayOrder || 0}
                      onChange={(event) =>
                        setCategoryEdit({
                          ...categoryEdit,
                          displayOrder: Number(event.target.value),
                        })
                      }
                    />
                  </label>
                </div>
                <label>
                  Descripción
                  <input
                    value={categoryEdit.description || ""}
                    onChange={(event) =>
                      setCategoryEdit({
                        ...categoryEdit,
                        description: event.target.value,
                      })
                    }
                  />
                </label>
                <label>
                  Etiqueta del detalle
                  <input
                    value={categoryEdit.detailLabel || ""}
                    onChange={(event) =>
                      setCategoryEdit({
                        ...categoryEdit,
                        detailLabel: event.target.value,
                      })
                    }
                  />
                </label>
                <label>
                  Opciones separadas por coma
                  <input
                    value={(categoryEdit.options || []).join(", ")}
                    onChange={(event) =>
                      setCategoryEdit({
                        ...categoryEdit,
                        options: event.target.value
                          .split(",")
                          .map((value) => value.trim())
                          .filter(Boolean),
                      })
                    }
                  />
                </label>
                <button className="primary" disabled={saving}>
                  <ShieldCheck /> Guardar tipo
                </button>
              </form>
              <div className="support-config-list">
                {catalog.map((department) => (
                  <article key={department.code}>
                    <header style={{ borderColor: department.accentColor }}>
                      <strong>{department.name}</strong>
                      <button
                        onClick={() =>
                          setDepartmentEdit({
                            code: department.code,
                            name: department.name,
                            description: department.description || "",
                            accentColor: department.accentColor,
                            displayOrder: department.displayOrder,
                            isActive: department.isActive,
                          })
                        }
                      >
                        Editar departamento
                      </button>
                    </header>
                    {department.categories.map((category) => (
                      <div key={category.id}>
                        <span>
                          <strong>{category.name}</strong>
                          <small>
                            {priorities[category.defaultPriority]} · Orden{" "}
                            {category.displayOrder}
                          </small>
                        </span>
                        <button
                          onClick={() =>
                            setCategoryEdit({
                              ...category,
                              departmentCode: department.code,
                            })
                          }
                        >
                          Editar
                        </button>
                        <button
                          className="danger"
                          onClick={() => void deleteCategory(category)}
                        >
                          Eliminar
                        </button>
                      </div>
                    ))}
                  </article>
                ))}
              </div>
            </div>
          </section>
        </div>
      )}
    </div>
  );
}







