import { apiFetch as fetch } from "@/lib/api-fetch";
import "../../operations-center-v250.css";
import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  CalendarDays,
  CheckCircle2,
  Clock3,
  Download,
  ExternalLink,
  Filter,
  Fullscreen,
  History,
  LoaderCircle,
  Maximize2,
  Minimize2,
  Monitor,
  RefreshCw,
  Search,
  ShieldCheck,
  TicketCheck,
  UsersRound,
  ZoomIn,
  ZoomOut,
} from "lucide-react";
import {
  activeTicketsByDepartment,
  activeTicketsByTechnologyArea,
  averageClosedMinutes,
  buildTicketAuditEvents,
  departmentLabels,
  priorityOrder,
  sortTicketsByPriority,
  technologyAreaLabels,
  turnCode,
  ticketsInOperationalQueue,
  ticketResultForOperationalDate,
  type OperationsAuditEvent,
  type OperationsTicket,
} from "@/lib/operations-center";
import {deviceDate,deviceTimestamp} from "@/lib/display";
import {summarizeSantoDomingoFindings,type LiveFindingInput} from "@/lib/automatic-findings";
import {findingAgencyKey,unassignedFindingRegion} from "./findingAreaFilters";
import {userDepartments} from '@/lib/user-departments';
import {exportOperationalAuditPdf,exportOperationalUserPdf,type OperationalPdfEvent} from './operationalAuditPdf';

type OperationsCenterProps = {
  mode: "audit" | "queue";
  onSessionExpired: () => void;
  onOpenTicket: (ticket: OperationsTicket) => void;
  teamScope?: "CALL_CENTER" | "TECHNICAL_FAILURE" | "TECHNICIANS" | null;
  departmentScope?: string | null;
  departmentAuditScope?: string | null;
  scopedManager?: boolean;
  globalManager?: boolean;
  directoryScope?: string | null;
};

type LiveFindingAgency={codigo:string;grupo:string;region?:string|null};
type ManagerTeamMember={id:string;displayName:string;email:string;role:string;supportTeam?:string|null;department?:string|null;firstLoginAt?:string|null;lastLoginAt?:string|null;lastActivityAt?:string|null;lastLogoutAt?:string|null;lastPath?:string|null;hasOpenSession?:boolean;isOnline?:boolean;inactiveSeconds?:number|null;lastSessionSeconds?:number|null;sessionCount?:number;todaySessionCount?:number;scheduledOfflineSeconds?:number;scheduledWindowSeconds?:number;totalConnectedSeconds?:number};
type FieldProgress={audits:{submittedByLogin?:string|null;employeeName?:string;submittedAt?:string;codigo?:string;terminal?:string;grupo?:string;profileId?:string}[];administrators:{displayName:string;email:string;completed:number;pending:number;review:number}[]};
type UserMetric=ManagerTeamMember&{created:number;resolved:number;active:number;movements:number;auditsCompleted:number;agenciesCompleted:number;agenciesPending:number};
const auditDepartmentLabels={...departmentLabels,...technologyAreaLabels,...Object.fromEntries(userDepartments)} as Record<string,string>;
const allQueueDepartments:string[]=userDepartments.map(([code])=>code).filter(code=>!['CALL_CENTER','TECHNICAL_FAILURE'].includes(code));

const actionLabels: Record<string, string> = {
  CREATED: "Ticket creado",
  ASSIGNED: "Responsable asignado",
  REASSIGNED: "Responsable reasignado",
  ROUTED: "Transferido de departamento",
  ESCALATED: "Ticket escalado",
  SHARED: "Compartido",
  IN_PROGRESS: "Atención iniciada",
  PENDING: "Marcado como pendiente",
  RESOLVED: "Ticket resuelto",
  CLOSED: "Ticket cerrado",
  CANCELLED: "Ticket anulado",
  UPDATED: "Información actualizada",
  ENTRY: "Entrada registrada", RECEIVE: "Recibido", APPROVE: "Autorizado", REJECT: "Rechazado", CANCEL: "Anulado",
  WORK: "Trabajo registrado", DELIVER: "Entregado", WAREHOUSE_RECEIVE: "Recibido en Almacén", SIGN: "Firma registrada",
  PROFILE_CREATED: "Auditoría de campo completada",
  API_ERROR: "Error de operación API",
  INTERFACE_ERROR: "Error de interfaz",
  AVAILABLE_ENTRY: "Entrada disponible registrada",
  STOCK_EXIT: "Salida de inventario",
  SIGN_DELIVERY: "Firma de entrega registrada",
  SIGN_RECEIPT: "Firma de recepción registrada",
  CREATED_DOCUMENT: "Documento creado",
  EDIT_FORM: "Formulario editado",
  EDIT: "Registro editado",
  DELETE: "Registro eliminado",
  SENT: "Enviado",
  RECEIVED: "Recibido",
  COMPLETED: "Completado",
  REVIEW_REQUIRED: "Enviado a revisión",
  EXIT: "Salida registrada",
  REQUEST: "Solicitud creada",
  NEW_DELIVERY: "Entrega nueva registrada",
  DAMAGED_RETURN: "Devolución por daño",
  TRANSFER_TO_WORKSHOP: "Transferencia al Taller",
  REPLACEMENT: "Reemplazo de equipo",
  COMPONENT_REPLACEMENT: "Reemplazo de componente",
  PART_REPLACEMENT: "Reemplazo de pieza",
  REPAIR: "Reparación realizada",
  DISCHARGE: "Equipo descargado",
  DISCHARGED: "Sin reparación",
  INVENTORY: "Ingresado al inventario",
  OPEN: "Abierto",
};

const moduleLabels:Record<string,string>={SUPPORT:"Soporte y tickets",WORKSHOP:"Taller",WAREHOUSE:"Almacén",FIELD_AUDIT:"Auditoría de campo",OPERATION_ERROR:"Error operativo",FINDING:"Hallazgos",TRANSITION:"Transición de agencia",DOCUMENT:"Documentos",TECHNOLOGY:"Tecnología",GENERAL_SERVICES:"Servicios Generales",HUMAN_RESOURCES:"Recursos Humanos"};
const roleLabels:Record<string,string>={Administrator:'Administración',Administrative:'Administración',Technology:'Tecnología',GeneralServices:'Servicios Generales',HumanResources:'Recursos Humanos',GroupAdministrator:'Supervisión',Fiscalizador:'Fiscalización',Viewer:'Consulta',Department:'Departamento',Warehouse:'Almacén'};
const readableCode=(value:string,labels:Record<string,string>)=>labels[value]||value.toLocaleLowerCase('es').replaceAll('_',' ').replace(/(^|\s)\p{L}/gu,letter=>letter.toLocaleUpperCase('es'));

const statusLabels: Record<string, string> = {
  OPEN: "En espera",
  IN_PROGRESS: "En atención",
  PENDING: "Pendiente",
  RESOLVED: "Resuelto",
  CLOSED: "Cerrado",
  CANCELLED: "Anulado",
};
const translatedDetail=(value?:string|null)=>{let result=value||'';for(const [code,label] of Object.entries({...actionLabels,...statusLabels,...moduleLabels}))result=result.replace(new RegExp(`\\b${code}\\b`,'g'),label);return result};

const priorityLabels: Record<string, string> = {
  LOW: "Baja",
  MEDIUM: "Media",
  HIGH: "Alta",
  CRITICAL: "Crítica",
};

const priorityDescriptions: Record<string, string> = {
  CRITICAL: "Atención inmediata",
  HIGH: "Atención prioritaria",
  MEDIUM: "Prioridad regular",
  LOW: "Puede programarse",
};

const formatDuration = (minutes: number) => {
  if (minutes < 60) return `${minutes} min`;
  if (minutes < 1440) return `${Math.floor(minutes / 60)} h ${minutes % 60} min`;
  return `${Math.floor(minutes / 1440)} d ${Math.floor((minutes % 1440) / 60)} h`;
};

const elapsed = (createdAt: string, now: number) => {
  const minutes = Math.max(0, Math.floor((now - deviceTimestamp(createdAt)) / 60000));
  return formatDuration(minutes);
};
const durationFromSeconds=(seconds?:number|null)=>seconds==null?'Sin registro':formatDuration(Math.max(0,Math.round(seconds/60)));
const dateTime=(value?:string|null)=>value?deviceDate(value).toLocaleString('es-DO',{dateStyle:'medium',timeStyle:'short'}):'Nunca';
const presenceState=(member:ManagerTeamMember)=>member.isOnline?'online':member.hasOpenSession?'idle':'offline';
const presenceLabel=(member:ManagerTeamMember)=>presenceState(member)==='online'?'Conectado y activo':presenceState(member)==='idle'?'Conectado sin actividad':'Desconectado';

const csvCell = (value: unknown) => `"${String(value ?? "").replaceAll('"', '""')}"`;

const downloadAuditCsv = (events: OperationsAuditEvent[]) => {
  const header = ["Fecha", "Ticket", "Acción", "Actor", "Departamento", "Agencia", "Grupo", "Prioridad", "Estado", "Comentario"];
  const rows = events.map((event) => [
    deviceDate(event.createdAt).toLocaleString("es-DO"),
    event.reference||`#${event.ticketNumber}`,
    readableCode(event.action,actionLabels),
    event.actorName,
    auditDepartmentLabels[event.department] || event.department,
    event.agency,
    event.group,
    priorityLabels[event.priority] || event.priority,
    statusLabels[event.status] || event.status,
    translatedDetail(event.comment),
  ]);
  const blob = new Blob(["\ufeff", [header, ...rows].map((row) => row.map(csvCell).join(",")).join("\r\n")], {
    type: "text/csv;charset=utf-8",
  });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `auditoria-tickets-${new Date().toISOString().slice(0, 10)}.csv`;
  link.click();
  URL.revokeObjectURL(url);
};

export default function OperationsCenter({ mode, onSessionExpired, onOpenTicket, teamScope = null, departmentScope = null,departmentAuditScope=null,scopedManager=false,globalManager=false,directoryScope=null }: OperationsCenterProps) {
  const [departmentEvents,setDepartmentEvents]=useState<OperationsAuditEvent[]>([]),[auditOffset,setAuditOffset]=useState(0),[auditHasNext,setAuditHasNext]=useState(false);
  const [tickets, setTickets] = useState<OperationsTicket[]>([]);
  const [teamMembers,setTeamMembers]=useState<ManagerTeamMember[]>([]);
  const [fieldProgress,setFieldProgress]=useState<FieldProgress>({audits:[],administrators:[]});
  const [findings,setFindings]=useState<LiveFindingInput[]>([]);
  const [findingAgencies,setFindingAgencies]=useState<LiveFindingAgency[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null);
  const [query, setQuery] = useState("");
  const [userQuery,setUserQuery]=useState('');
  const [userPdfBusy,setUserPdfBusy]=useState<string|null>(null);
  const [department, setDepartment] = useState(teamScope || (departmentScope && departmentScope !== "TECHNOLOGY" ? departmentScope : "ALL"));
  const [action, setAction] = useState("ALL");
  const [period, setPeriod] = useState("7");
  const [selectedEvent, setSelectedEvent] = useState<string | null>(null);
  const [now, setNow] = useState(Date.now());
  const [isFullscreen, setIsFullscreen] = useState(Boolean(document.fullscreenElement));
  const [queueZoom, setQueueZoom] = useState<number | null>(null);
  const [queueDate,setQueueDate]=useState(()=>{const date=new Date();return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,"0")}-${String(date.getDate()).padStart(2,"0")}`});
  const [queueResult,setQueueResult]=useState<'ALL'|'COMPLETED'|'PENDING'>('ALL');
  const [directoryPanelScope,setDirectoryPanelScope]=useState<string|null>(null);
  const [directoryLoading,setDirectoryLoading]=useState(false);
  const today=useMemo(()=>{const date=new Date();return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`},[]);
  const [reportTo,setReportTo]=useState(today);
  const [reportFrom,setReportFrom]=useState(()=>{const date=new Date();date.setDate(date.getDate()-30);return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`});
  const [pdfBusy,setPdfBusy]=useState(false);
  const [viewportWidth, setViewportWidth] = useState(window.innerWidth);
  const loadInFlightRef = useRef(false);
  const loadSequenceRef = useRef(0);
  const hasLoadedRef = useRef(false);
  const agencyRefreshAfter = useRef(0);
  const directoryCacheRef = useRef(new Map<string,ManagerTeamMember[]>());

  const load = useCallback(async (silent = false) => {
    if (silent && loadInFlightRef.current) return;
    const sequence=++loadSequenceRef.current;
    if (!navigator.onLine) {
      if (!silent) {
        setLoading(false);
        setError("No hay conexión. La actualización continuará automáticamente al recuperar internet.");
      }
      return;
    }
    loadInFlightRef.current = true;
    if (silent) setRefreshing(true);
    else setLoading(true);
    try {
      const response = await fetch(scopedManager?"/api/department-support":"/api/tickets", { cache: "no-store" });
      if(sequence!==loadSequenceRef.current)return;
      if (response.status === 401) {
        onSessionExpired();
        return;
      }
      const data = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(data.error || "No fue posible cargar la operación de tickets.");
      setTickets(Array.isArray(data.tickets) ? data.tickets : []);
      if(Array.isArray(data.members))setTeamMembers(data.members);
      if(mode==='audit'&&departmentAuditScope){
        const to=new Date(),from=new Date(to);from.setDate(from.getDate()-(period==='ALL'?365:Number(period)));
        const area=department==='ALL'?departmentAuditScope:department;
        const result=await fetch(`/api/department-audit?from=${from.toISOString().slice(0,10)}&to=${to.toISOString().slice(0,10)}&offset=${auditOffset}&area=${encodeURIComponent(area)}`,{cache:'no-store'});
        const entries=await result.json();if(!result.ok||!Array.isArray(entries))throw Error(entries.error||'No se pudo cargar la auditoría de tu área.');
        if(sequence!==loadSequenceRef.current)return;
        setAuditHasNext(entries.length>200);
        setDepartmentEvents(entries.slice(0,200).map((entry:{id:string;at:string;action:string;actor?:string;login?:string;module:string;department?:string;reference?:string;detail?:string})=>({id:entry.id,ticketId:'',ticketNumber:0,reference:entry.reference||'',module:entry.module,action:entry.action.toUpperCase(),actorName:entry.actor||entry.login||'Sistema',comment:entry.detail||null,fromDepartment:null,toDepartment:null,department:entry.department||area,subject:entry.reference||entry.module,agency:entry.module,group:'',priority:'',status:'',ticketType:'INTERNAL',createdAt:entry.at})));
      }
      // Publish the primary section before optional directories finish.
      setLoading(false); hasLoadedRef.current = true;
      const optionalErrors: string[] = [];
      if (!scopedManager) await Promise.all([
        ...(mode==='audit'&&departmentAuditScope ? [(async()=>{
          try{
            const scope=department==='ALL'?departmentAuditScope:department;
            const result=await fetch(`/api/manager-team-members?scope=${encodeURIComponent(scope)}`,{cache:'no-store'});
            const body=await result.json();if(!result.ok)throw new Error(body.error||'No se pudo cargar la actividad de usuarios.');
            if(sequence===loadSequenceRef.current&&Array.isArray(body.members)){setTeamMembers(body.members);directoryCacheRef.current.set(scope,body.members)}
          }catch(error){optionalErrors.push((error as Error).message)}
        })()] : []),
        ...(mode==='queue' ? [
        (async () => {
          try {
            const result = await fetch("/api/tickets/findings", {cache: "no-store"});
            const data = await result.json(); if (!result.ok) throw new Error(data.error || 'No se pudieron consultar los hallazgos.');
            if (sequence === loadSequenceRef.current) setFindings(data.findings || []);
          } catch (error) { optionalErrors.push((error as Error).message); }
        })(),
        (async () => {
          if (Date.now() < agencyRefreshAfter.current) return;
          try {
            const result = await fetch("/api/tickets/agencies", {cache: "no-store"});
            const data = await result.json(); if (!result.ok) throw new Error(data.error || 'No se pudieron consultar las agencias.');
            if (sequence === loadSequenceRef.current) { setFindingAgencies(data.agencies || []); agencyRefreshAfter.current = Date.now() + 300000; }
          } catch (error) { optionalErrors.push((error as Error).message); }
        })(),
        ] : []),
      ]);
      if(mode==='audit'){
        try{
          const dashboard=await fetch('/api/dashboard',{cache:'no-store'});
          const summary=await dashboard.json();
          if(sequence===loadSequenceRef.current&&dashboard.ok)setFieldProgress({audits:Array.isArray(summary.audits)?summary.audits:[],administrators:Array.isArray(summary.administrators)?summary.administrators:[]});
        }catch{/* Las métricas de presencia y tickets permanecen disponibles. */}
      }
      if (sequence !== loadSequenceRef.current) return;
      setUpdatedAt(new Date());
      // Los paneles secundarios se reintentan sin bloquear la auditoría que ya cargó.
      setError("");
      hasLoadedRef.current = true;
    } catch (loadError) {
      if(sequence!==loadSequenceRef.current)return;
      if (!silent || !hasLoadedRef.current)
        setError(loadError instanceof Error ? loadError.message : "No fue posible sincronizar la información.");
    } finally {
      if(sequence===loadSequenceRef.current){
      loadInFlightRef.current = false;
      setLoading(false);
      setRefreshing(false);
      }
    }
  }, [onSessionExpired,mode,departmentAuditScope,department,period,auditOffset,scopedManager,globalManager]);

  const openDepartmentDirectory = useCallback(async (scope: string) => {
    setDirectoryPanelScope(scope);
    const cached=directoryCacheRef.current.get(scope);
    if(cached){setTeamMembers(cached);return}
    setDirectoryLoading(true);
    try {
      const response = await fetch(`/api/manager-team-members?scope=${encodeURIComponent(scope)}`, {cache: "no-store"});
      if (response.status === 401) {
        onSessionExpired();
        return;
      }
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(body.error || "No fue posible cargar los usuarios del departamento.");
      const members=Array.isArray(body.members)?body.members:[];
      setTeamMembers(members);
      directoryCacheRef.current.set(scope,members);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "No fue posible cargar los usuarios del departamento.");
    } finally {
      setDirectoryLoading(false);
    }
  }, [onSessionExpired]);

  useEffect(()=>{
    const scope=directoryPanelScope||(mode==='audit'?(department==='ALL'?(departmentAuditScope||departmentScope||'ALL'):department):(scopedManager?departmentScope:null));
    if(!scope)return;
    let cancelled=false;
    const refreshPresence=async()=>{
      try{
        const response=await fetch(`/api/manager-team-members?scope=${encodeURIComponent(scope)}`,{cache:'no-store'});
        if(response.status===401){onSessionExpired();return}
        const body=await response.json();
        if(!cancelled&&response.ok&&Array.isArray(body.members)){setTeamMembers(body.members);directoryCacheRef.current.set(scope,body.members)}
      }catch{/* Se conserva el último directorio válido y se reintenta automáticamente. */}
    };
    void refreshPresence();
    const timer=window.setInterval(()=>void refreshPresence(),20000);
    return()=>{cancelled=true;window.clearInterval(timer)};
  },[department,departmentAuditScope,departmentScope,directoryPanelScope,mode,onSessionExpired,scopedManager]);

  useEffect(() => {
    void load();
    const refresh = () => {
      if (!document.hidden) void load(true);
    };
    window.addEventListener("support-live-refresh", refresh);
    return () => window.removeEventListener("support-live-refresh", refresh);
  }, [load]);

  // La cola se sincroniza también en el corte operativo diario de las 07:30
  // del dispositivo. El temporizador se vuelve a programar cada 24 horas,
  // incluso si la pestaña permaneció abierta toda la noche.
  useEffect(() => {
    let timer: number | null = null;
    const scheduleDailyRefresh = () => {
      const now = new Date();
      const next = new Date(now);
      next.setHours(7, 30, 0, 0);
      if (next.getTime() <= now.getTime()) next.setDate(next.getDate() + 1);
      timer = window.setTimeout(() => {
        setNow(Date.now());
        void load(true);
        scheduleDailyRefresh();
      }, Math.max(1000, next.getTime() - now.getTime()));
    };
    scheduleDailyRefresh();
    return () => {
      if (timer !== null) window.clearTimeout(timer);
    };
  }, [load]);

  useEffect(() => {
    const clock = window.setInterval(() => setNow(Date.now()), 30000);
    const onFullscreen = () => setIsFullscreen(Boolean(document.fullscreenElement));
    document.addEventListener("fullscreenchange", onFullscreen);
    return () => {
      window.clearInterval(clock);
      document.removeEventListener("fullscreenchange", onFullscreen);
    };
  }, []);

  useEffect(() => {
    const updateViewport = () => setViewportWidth(window.innerWidth);
    window.addEventListener("resize", updateViewport);
    return () => window.removeEventListener("resize", updateViewport);
  }, []);

  useEffect(() => setDepartment(mode === "queue" ? teamScope || (departmentScope && departmentScope !== "TECHNOLOGY" ? departmentScope : "ALL") : "ALL"), [departmentScope, mode, teamScope]);

  useEffect(()=>setAuditOffset(0),[department,period]);
  const auditEvents = useMemo(() => departmentAuditScope?departmentEvents:buildTicketAuditEvents(tickets), [tickets,departmentAuditScope,departmentEvents]);
  const availableActions = useMemo(
    () => Array.from(new Set(auditEvents.map((event) => event.action))).sort(),
    [auditEvents],
  );
  const filteredEvents = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase("es");
    const cutoff = period === "ALL" ? 0 : Date.now() - Number(period) * 86400000;
    return auditEvents.filter((event) => {
      // Las llamadas de presencia son telemetría interna, no acciones operativas del personal.
      // Si una red falla, no deben inundar ni distorsionar el historial de auditoría.
      if (event.module === "OPERATION_ERROR" && /\/api\/(manager-team-members|admin\/presence)/i.test(`${event.reference} ${event.comment}`)) return false;
      if (department !== "ALL" && event.department !== department && tickets.find(t=>t.id===event.ticketId)?.assignedTeam!==department) return false;
      if (action !== "ALL" && event.action !== action) return false;
      if (cutoff && deviceTimestamp(event.createdAt) < cutoff) return false;
      if (!normalizedQuery) return true;
      return `${event.ticketNumber} ${event.subject} ${event.agency} ${event.group} ${event.actorName} ${event.comment || ""}`
        .toLocaleLowerCase("es")
        .includes(normalizedQuery);
    });
  }, [action, auditEvents, department, period, query,tickets]);

  const usesTechnologyAreas = !!teamScope || departmentScope === "TECHNOLOGY";
  const scopedTickets = useMemo(() => scopedManager?tickets:tickets.filter(ticket=>(!departmentScope||departmentScope==="WAREHOUSE"||ticket.assignedDepartment===departmentScope)&&(!teamScope||(ticket.assignedDepartment==="TECHNOLOGY"&&(ticket.assignedTeam||(ticket.assignedTechnicianName?"TECHNICIANS":"TECHNICAL_FAILURE"))===teamScope))), [departmentScope, teamScope, tickets,scopedManager]);
  const findingDepartmentScope = departmentScope || (!usesTechnologyAreas && department !== "ALL" ? department : null);
  const scopedFindings = useMemo(() => teamScope ? [] : findingDepartmentScope ? findings.filter(finding => finding.department === findingDepartmentScope) : findings, [findingDepartmentScope, findings, teamScope]);
  const departmentQueues = useMemo(() => activeTicketsByDepartment(scopedTickets), [scopedTickets]);
  const queueTickets = useMemo(() => ticketsInOperationalQueue(scopedTickets, new Date(now)), [now, scopedTickets]);
  const technologyQueues = useMemo(() => activeTicketsByTechnologyArea(queueTickets), [queueTickets]);
  const dailyDepartmentQueues = useMemo(() => activeTicketsByDepartment(queueTickets), [queueTickets]);
  const queues = scopedManager&&departmentScope&&departmentScope!=='TECHNOLOGY'?new Map([[departmentScope,queueTickets.filter(t=>['OPEN','PENDING','IN_PROGRESS'].includes(t.status))]]):usesTechnologyAreas ? technologyQueues : dailyDepartmentQueues;
  const queueLabels = usesTechnologyAreas ? technologyAreaLabels : auditDepartmentLabels;
  const queueDepartments = (usesTechnologyAreas ? ["CALL_CENTER", "TECHNICAL_FAILURE", "TECHNICIANS"] : departmentScope?[departmentScope]:allQueueDepartments).filter(
    (code) => department === "ALL" || department === code,
  );
  const totalActive = Array.from(queues.values()).reduce((total, items) => total + items.length, 0);
  const totalWaiting = Array.from(queues.values()).flat().filter((ticket) => ticket.status !== "IN_PROGRESS").length;
  const totalInProgress = Array.from(queues.values()).flat().filter((ticket) => ticket.status === "IN_PROGRESS").length;
  const averageMinutes = averageClosedMinutes(scopedTickets);
  const santoDomingoFindingCategories=useMemo(()=>summarizeSantoDomingoFindings(scopedFindings,new Map(findingAgencies.map(agency=>[findingAgencyKey(agency.codigo,agency.grupo),agency.region?.trim()||unassignedFindingRegion]))),[findingAgencies,scopedFindings]);
  const santoDomingoFindingTotal=santoDomingoFindingCategories.reduce((total,category)=>total+category.total,0);
  const auditedActive = Array.from(departmentQueues.values()).reduce((total, items) => total + items.length, 0);
  const areaMetrics = useMemo(() => (teamScope ? [teamScope] : usesTechnologyAreas ? ["CALL_CENTER", "TECHNICAL_FAILURE", "TECHNICIANS"] : departmentScope?[departmentScope]:allQueueDepartments).map((code) => {
    const areaTickets = queueTickets.filter((ticket) => scopedManager&&!usesTechnologyAreas?true:usesTechnologyAreas
      ? ticket.assignedDepartment === "TECHNOLOGY" && (ticket.assignedTeam || (ticket.assignedTechnicianName ? "TECHNICIANS" : "TECHNICAL_FAILURE")) === code
      : ticket.assignedDepartment === code);
    const pending = areaTickets.filter((ticket) => ticket.status === "OPEN" || ticket.status === "PENDING").length;
    const inProgress = areaTickets.filter((ticket) => ticket.status === "IN_PROGRESS").length;
    const completed = areaTickets.filter((ticket) => ticket.status === "RESOLVED" || ticket.status === "CLOSED").length;
    const total = pending + inProgress + completed;
    const percent = (value: number) => total ? Math.round(value * 100 / total) : 0;
    return { code, total, pending, inProgress, completed, pendingPercent: percent(pending), inProgressPercent: percent(inProgress), completedPercent: percent(completed) };
  }), [departmentScope, teamScope, queueTickets, usesTechnologyAreas,scopedManager]);
  const dailyResults=useMemo(()=>{
    const search=query.trim().toLocaleLowerCase('es');
    return scopedTickets.flatMap(ticket=>{const result=ticketResultForOperationalDate(ticket,queueDate);return result?[{ticket,...result}]:[]})
      .filter(row=>{
        const ticketArea=usesTechnologyAreas?(row.ticket.assignedTeam||(row.ticket.assignedTechnicianName?'TECHNICIANS':'TECHNICAL_FAILURE')):row.ticket.assignedDepartment;
        const matchesDepartment=department==='ALL'||ticketArea===department||(scopedManager&&department===departmentScope);
        const matchesResult=queueResult==='ALL'||row.result===queueResult;
        const matchesSearch=!search||`${row.ticket.ticketNumber} ${row.ticket.codigo} ${row.ticket.terminal} ${row.ticket.grupo} ${row.ticket.subject}`.toLocaleLowerCase('es').includes(search);
        return matchesDepartment&&matchesResult&&matchesSearch;
      })
      .sort((left,right)=>deviceTimestamp(left.ticket.createdAt)-deviceTimestamp(right.ticket.createdAt));
  },[department,query,queueDate,queueResult,scopedTickets,usesTechnologyAreas,scopedManager,departmentScope]);
  const completedForDate=dailyResults.filter(row=>row.result==='COMPLETED').length,pendingForDate=dailyResults.filter(row=>row.result==='PENDING').length;
  const managerTeamMetrics=useMemo(()=>{
    const created=new Map<string,number>(),resolved=new Map<string,number>(),active=new Map<string,number>(),movements=new Map<string,number>(),audits=new Map<string,number>();
    const add=(map:Map<string,number>,value?:string|null)=>{if(!value)return;const key=value.trim().toLocaleLowerCase('es');map.set(key,(map.get(key)||0)+1)};
    for(const ticket of tickets){add(created,ticket.createdByName);add(resolved,ticket.resolvedByName);if(['OPEN','PENDING','IN_PROGRESS'].includes(ticket.status))add(active,ticket.assignedTechnicianName)}
    for(const event of auditEvents)add(movements,event.actorName);
    for(const audit of fieldProgress.audits)add(audits,audit.submittedByLogin);
    return teamMembers.map(member=>{
      const keys=[member.displayName,member.email,member.email.replace('@grupotejeda.local','')].filter(Boolean).map(value=>value.trim().toLocaleLowerCase('es'));
      const count=(map:Map<string,number>)=>Math.max(0,...keys.map(key=>map.get(key)||0));
      const progress=fieldProgress.administrators.find(row=>keys.includes(row.email.trim().toLocaleLowerCase('es'))||keys.includes(row.displayName.trim().toLocaleLowerCase('es')));
      return{...member,created:count(created),resolved:count(resolved),active:count(active),movements:count(movements),auditsCompleted:count(audits),agenciesCompleted:progress?.completed||0,agenciesPending:(progress?.pending||0)+(progress?.review||0)};
    }).sort((left,right)=>(left.isOnline?0:left.hasOpenSession?1:2)-(right.isOnline?0:right.hasOpenSession?1:2)||(left.displayName||left.email).localeCompare(right.displayName||right.email,'es'));
  },[teamMembers,tickets,auditEvents,fieldProgress]);
  const activeDirectoryScope=globalManager?(directoryScope||(department!=='ALL'?department:null)):departmentScope;
  const directoryMemberScope=(member:ManagerTeamMember,scope=activeDirectoryScope)=>{
    const role=(member.role||'').trim().toUpperCase();
    const team=(member.supportTeam||'').trim().toUpperCase();
    if(!scope)return true;
    if(member.department?.toUpperCase()===scope)return true;
    if(['CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS'].includes(team))return scope==='TECHNOLOGY'||scope===team;
    if(team&&team===scope)return true;
    return role===scope||
      scope==='GENERAL_SERVICES'&&role==='GENERALSERVICES'||
      scope==='HUMAN_RESOURCES'&&role==='HUMANRESOURCES'||
      (scope==='GROUP_ADMIN'||scope==='SUPERVISION')&&['GROUPADMINISTRATOR','GROUP_ADMIN'].includes(role);
  };
  const visibleManagerTeamMetrics=useMemo(()=>activeDirectoryScope?managerTeamMetrics.filter(member=>directoryMemberScope(member,activeDirectoryScope)):managerTeamMetrics,[activeDirectoryScope,managerTeamMetrics]);
  const displayedUserMetrics=useMemo(()=>{const search=userQuery.trim().toLocaleLowerCase('es');const rank=(member:ManagerTeamMember)=>member.isOnline?0:member.hasOpenSession?1:2;return visibleManagerTeamMetrics.filter(member=>!search||`${member.displayName} ${member.email} ${member.department||''} ${member.supportTeam||''}`.toLocaleLowerCase('es').includes(search)).sort((left,right)=>rank(left)-rank(right)||(left.displayName||left.email).localeCompare(right.displayName||right.email,'es'))},[userQuery,visibleManagerTeamMetrics]);
  const panelManagerTeamMetrics=useMemo(()=>directoryPanelScope?managerTeamMetrics.filter(member=>directoryMemberScope(member,directoryPanelScope)):[],[directoryPanelScope,managerTeamMetrics]);
  const panelConnectedUserCount=panelManagerTeamMetrics.filter(member=>member.hasOpenSession).length;
  const connectedUserCount=visibleManagerTeamMetrics.filter(member=>member.hasOpenSession).length;
  const activeUserCount=visibleManagerTeamMetrics.filter(member=>member.isOnline).length;
  const idleUserCount=visibleManagerTeamMetrics.filter(member=>member.hasOpenSession&&!member.isOnline).length;
  const downloadOperationalPdf=useCallback(async()=>{
    if(!reportFrom||!reportTo||reportFrom>reportTo){setError('Selecciona un período válido para crear el PDF.');return}
    setPdfBusy(true);setError('');
    try{
      let reportEvents:OperationalPdfEvent[]=[];
      if(departmentAuditScope){
        const scope=department==='ALL'?departmentAuditScope:department;let offset=0;
        while(offset<=1000000){
          const response=await fetch(`/api/department-audit?from=${reportFrom}&to=${reportTo}&offset=${offset}&area=${encodeURIComponent(scope)}`,{cache:'no-store'});
          const entries=await response.json();if(!response.ok||!Array.isArray(entries))throw new Error(entries.error||'No se pudo preparar el reporte.');
          reportEvents.push(...entries.slice(0,200).map((entry:{at:string;action:string;actor?:string;login?:string;module:string;department?:string;reference?:string;detail?:string})=>({at:deviceDate(entry.at).toLocaleString('es-DO'),action:readableCode(entry.action.toUpperCase(),{...statusLabels,...actionLabels}),actor:entry.actor||entry.login||'Sistema',department:auditDepartmentLabels[entry.department||scope]||readableCode(entry.department||scope,{}),module:readableCode(entry.module,moduleLabels),reference:entry.reference||'Sin referencia',detail:translatedDetail(entry.detail)||'Sin comentario'})));
          if(entries.length<=200)break;offset+=200;
        }
      }else{
        const fromTime=new Date(`${reportFrom}T00:00:00`).getTime(),toTime=new Date(`${reportTo}T23:59:59`).getTime();
        reportEvents=filteredEvents.filter(event=>{const time=deviceTimestamp(event.createdAt);return time>=fromTime&&time<=toTime}).map(event=>({at:deviceDate(event.createdAt).toLocaleString('es-DO'),action:readableCode(event.action,{...statusLabels,...actionLabels}),actor:event.actorName,department:auditDepartmentLabels[event.department]||readableCode(event.department,{}),module:readableCode(event.module||'SUPPORT',moduleLabels),reference:event.reference||`#${event.ticketNumber}`,detail:translatedDetail(event.comment)||'Sin comentario'}));
      }
      await exportOperationalAuditPdf({from:reportFrom,to:reportTo,area:department==='ALL'?'Todos los departamentos':auditDepartmentLabels[department]||department,events:reportEvents,users:visibleManagerTeamMetrics.map(member=>({name:member.displayName||member.email,email:member.email,department:auditDepartmentLabels[member.department||'']||roleLabels[member.role]||readableCode(member.role,{}),status:presenceLabel(member),lastLogin:dateTime(member.lastLoginAt),lastActivity:dateTime(member.lastActivityAt),inactive:durationFromSeconds(member.inactiveSeconds),lastSession:durationFromSeconds(member.lastSessionSeconds)}))});
    }catch(reason){setError(reason instanceof Error?reason.message:'No se pudo crear el PDF.')}finally{setPdfBusy(false)}
  },[department,departmentAuditScope,filteredEvents,reportFrom,reportTo,visibleManagerTeamMetrics]);
  const downloadUserPdf=useCallback(async(member:UserMetric)=>{
    setUserPdfBusy(member.id);setError('');
    try{
      const normalize=(value?:string|null)=>(value||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').trim().toLocaleLowerCase('es').replace('@grupotejeda.local','');
      const identities=new Set([normalize(member.displayName),normalize(member.email)]);
      const historicalAudits=fieldProgress.audits.filter(audit=>identities.has(normalize(audit.submittedByLogin)));
      const earliestAudit=historicalAudits.map(audit=>audit.submittedAt||'').filter(Boolean).sort()[0];
      let cursor=new Date(earliestAudit||member.firstLoginAt||new Date(Date.now()-365*86400000).toISOString());cursor.setHours(0,0,0,0);const reportStart=cursor.toISOString().slice(0,10);
      const end=new Date();end.setHours(23,59,59,999);const entries:{id:string;at:string;action:string;actor?:string;login?:string;module:string;reference?:string;detail?:string;department?:string}[]=[];
      const scope=departmentAuditScope==='ALL'?'ALL':member.department||departmentAuditScope||departmentScope||'ALL';
      while(cursor<end){const chunkEnd=new Date(Math.min(end.getTime(),cursor.getTime()+365*86400000));let offset=0;while(offset<=1000000){const response=await fetch(`/api/department-audit?from=${cursor.toISOString().slice(0,10)}&to=${chunkEnd.toISOString().slice(0,10)}&offset=${offset}&area=${encodeURIComponent(scope)}`,{cache:'no-store'});const body=await response.json();if(!response.ok||!Array.isArray(body))throw new Error(body.error||'No se pudieron consultar los movimientos del usuario.');entries.push(...body.slice(0,200).filter((entry:{actor?:string;login?:string})=>identities.has(normalize(entry.login))||identities.has(normalize(entry.actor))));if(body.length<=200)break;offset+=200}cursor=new Date(chunkEnd.getTime()+86400000)}
      const events:OperationalPdfEvent[]=entries.map(entry=>({at:deviceDate(entry.at).toLocaleString('es-DO'),action:readableCode(entry.action.toUpperCase(),{...statusLabels,...actionLabels}),actor:entry.actor||entry.login||member.displayName,department:auditDepartmentLabels[entry.department||scope]||readableCode(entry.department||scope,{}),module:readableCode(entry.module,moduleLabels),reference:entry.reference||'Sin referencia',detail:translatedDetail(entry.detail)||'Sin comentario'}));
      for(const audit of historicalAudits)if(!entries.some(entry=>entry.id===`field-audit:${audit.profileId}`))events.push({at:audit.submittedAt?deviceDate(audit.submittedAt).toLocaleString('es-DO'):'Fecha no registrada',action:'Auditoría de campo completada',actor:member.displayName||member.email,department:'Supervisión',module:'Auditoría de campo',reference:`${audit.codigo||'Agencia'} · ${audit.terminal||'Sin nombre'}`,detail:`Auditoría histórica completada${audit.grupo?` · Grupo ${audit.grupo}`:''}`});
      events.sort((left,right)=>right.at.localeCompare(left.at));
      await exportOperationalUserPdf({from:reportStart,to:today,events,user:{name:member.displayName||member.email,email:member.email,department:auditDepartmentLabels[member.department||'']||roleLabels[member.role]||readableCode(member.role,{}),status:presenceLabel(member),lastLogin:dateTime(member.lastLoginAt),lastActivity:dateTime(member.lastActivityAt),inactive:durationFromSeconds(member.inactiveSeconds),lastSession:durationFromSeconds(member.lastSessionSeconds),totalConnected:durationFromSeconds(member.totalConnectedSeconds),sessions:member.sessionCount||0,todaySessions:member.todaySessionCount||0,scheduleOffline:durationFromSeconds(member.scheduledOfflineSeconds),ticketsCreated:member.created,ticketsActive:member.active,ticketsResolved:member.resolved,auditsCompleted:member.auditsCompleted,agenciesCompleted:member.agenciesCompleted,agenciesPending:member.agenciesPending,movements:events.length}});
    }catch(reason){setError(reason instanceof Error?reason.message:'No se pudo crear el expediente PDF del usuario.')}finally{setUserPdfBusy(null)}
  },[departmentAuditScope,departmentScope,fieldProgress.audits,today]);
  const directoryTitle=activeDirectoryScope?auditDepartmentLabels[activeDirectoryScope]||activeDirectoryScope:'Todos los departamentos';
  const largestVisibleQueue = Math.max(0, ...queueDepartments.map((code) => (queues.get(code) || []).length));
  const automaticQueueZoom = viewportWidth < 520
    ? largestVisibleQueue > 15 ? 0.72 : largestVisibleQueue > 9 ? 0.84 : 1
    : viewportWidth < 900
      ? largestVisibleQueue > 18 ? 0.68 : largestVisibleQueue > 11 ? 0.8 : 0.92
      : largestVisibleQueue > 28 ? 0.55 : largestVisibleQueue > 20 ? 0.64 : largestVisibleQueue > 14 ? 0.74 : largestVisibleQueue > 8 ? 0.86 : 1;
  const effectiveQueueZoom = queueZoom ?? automaticQueueZoom;
  const adjustQueueZoom = (delta: number) => {
    const next = Math.round((effectiveQueueZoom + delta) * 10) / 10;
    setQueueZoom(Math.min(1.2, Math.max(0.5, next)));
  };

  const toggleFullscreen = async () => {
    try {
      if (document.fullscreenElement) await document.exitFullscreen();
      else await document.querySelector(".operations-center")?.requestFullscreen();
    } catch {
      setError("El navegador no permitió activar la pantalla completa.");
    }
  };

  if (loading && !tickets.length)
    return <div className="operations-loading"><LoaderCircle className="spin" /> Preparando centro operativo...</div>;

  return (
    <section className={`operations-center operations-${mode}`}>
      <header className="operations-hero">
        <div className="operations-hero-icon">{mode === "audit" ? <ShieldCheck /> : <Monitor />}</div>
        <div>
          <span>{mode === "audit" ? "CONTROL, SEGURIDAD Y TRAZABILIDAD" : "OPERACIÓN EN TIEMPO REAL"}</span>
          <h1>{mode === "audit" ? departmentAuditScope?'Auditoría operativa · '+(departmentAuditScope==='ALL'?'Todos los departamentos':auditDepartmentLabels[departmentAuditScope]||departmentAuditScope):"Auditoría completa de tickets" : "Pantalla de turnos y cola"}</h1>
          <p>{mode === "audit" ? departmentAuditScope==='ALL'?"Consulta las acciones registradas por supervisores y personal de todos los departamentos.":"Consulta quién creó, asignó, transfirió, puso pendiente o resolvió cada caso." : "Visualiza los tickets que van entrando, su posición y el estado de atención por departamento."}</p>
        </div>
        <div className="operations-live">
          <i />
          <span><strong>Actualización automática</strong><small>{updatedAt ? `Última sincronización ${updatedAt.toLocaleTimeString("es-DO")}` : "Conectando..."}</small></span>
          <button type="button" onClick={() => void load(true)} disabled={refreshing} title="Actualizar ahora"><RefreshCw className={refreshing ? "spin" : ""} /></button>
          {mode === "queue" && <button type="button" onClick={() => void toggleFullscreen()} title="Pantalla completa">{isFullscreen ? <Minimize2 /> : <Maximize2 />}</button>}
        </div>
      </header>

      {error && <div className="operations-error"><AlertTriangle /><span><strong>No fue posible sincronizar</strong><small>{error}</small></span><button onClick={() => void load(true)}>Reintentar</button></div>}

      {scopedManager&&visibleManagerTeamMetrics.length>0&&<section className="manager-team-control" aria-label="Personal bajo supervisión">
        <header><span><UsersRound/><small>{globalManager&&activeDirectoryScope?'DIRECTORIO DEL DEPARTAMENTO':globalManager?'DIRECTORIO GLOBAL':'PERSONAL BAJO SUPERVISIÓN'}</small><h2>{globalManager&&activeDirectoryScope?`Usuarios de ${directoryTitle}`:globalManager?'Todos los usuarios de todos los departamentos':departmentScope==='CALL_CENTER'?'Equipo completo de Call Center':departmentScope==='TECHNICAL_FAILURE'?'Avería Técnica y Técnicos':'Personal del departamento'}</h2></span><strong>{connectedUserCount}<small>conectados ahora · {visibleManagerTeamMetrics.length} registrados</small></strong></header>
        <div>{visibleManagerTeamMetrics.map(member=><article key={member.id}><span><b>{member.displayName||member.email}</b><small>{technologyAreaLabels[member.supportTeam||'']||auditDepartmentLabels[member.supportTeam||'']||member.role}</small></span><em><b>{member.created}</b><small>creados</small></em><em><b>{member.resolved}</b><small>resueltos</small></em><em><b>{member.active}</b><small>activos</small></em></article>)}</div>
      </section>}

      {mode === "audit" ? (
        <>
          <div className="operations-kpis">
            <article><History /><span>Eventos auditados<small>Según los filtros actuales</small></span><strong>{filteredEvents.length}</strong></article>
            <article><Activity /><span>Tickets activos<small>Abiertos, en proceso o pendientes</small></span><strong>{auditedActive}</strong></article>
            <article><CheckCircle2 /><span>Tickets finalizados<small>Resueltos o cerrados</small></span><strong>{scopedTickets.filter((ticket) => ["RESOLVED", "CLOSED"].includes(ticket.status)).length}</strong></article>
            <article><Clock3 /><span>Tiempo medio<small>Hasta la última actualización final</small></span><strong>{formatDuration(averageMinutes)}</strong></article>
          </div>

          <div className="operations-toolbar">
            <label className="operations-search"><Search /><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Buscar ticket, agencia, actor o comentario..." /></label>
            <label><Filter /><select aria-label="Área de auditoría" value={department} onChange={(event) => {setDepartment(event.target.value);setAuditOffset(0)}}><option value="ALL">{departmentAuditScope==='ALL'?'Todos los departamentos':departmentAuditScope?'Toda mi área':'Todas las áreas'}</option>{Object.entries(departmentAuditScope==='ALL'?Object.fromEntries(userDepartments):departmentAuditScope?auditDepartmentLabels:{...departmentLabels,...technologyAreaLabels,...Object.fromEntries(userDepartments)}).filter(([code])=>!departmentAuditScope||departmentAuditScope==='ALL'||code===departmentAuditScope||departmentAuditScope==='TECHNOLOGY'&&['CALL_CENTER','TECHNICAL_FAILURE'].includes(code)).map(([code, label]) => <option key={code} value={code}>{label}</option>)}</select></label>
            <label><History /><select value={action} onChange={(event) => setAction(event.target.value)}><option value="ALL">Todas las acciones</option>{availableActions.map((code) => <option key={code} value={code}>{readableCode(code,actionLabels)}</option>)}</select></label>
            <label><Clock3 /><select value={period} onChange={(event) => setPeriod(event.target.value)}><option value="1">Últimas 24 horas</option><option value="7">Últimos 7 días</option><option value="30">Últimos 30 días</option><option value="ALL">{departmentAuditScope?'Último año':'Todo el historial'}</option></select></label>
            <button type="button" className="operations-export" onClick={() => downloadAuditCsv(filteredEvents)} disabled={!filteredEvents.length}><Download /> Exportar CSV</button>
          </div>

	          <div className="audit-pdf-range" aria-label="Exportar auditoría operativa en PDF"><strong>Informe PDF</strong><label>Desde<input type="date" value={reportFrom} max={reportTo||undefined} onChange={event=>setReportFrom(event.target.value)}/></label><label>Hasta<input type="date" value={reportTo} min={reportFrom||undefined} onChange={event=>setReportTo(event.target.value)}/></label><button type="button" onClick={()=>void downloadOperationalPdf()} disabled={pdfBusy||!reportFrom||!reportTo||reportFrom>reportTo}><Download/>{pdfBusy?'Creando PDF…':'Imprimir / descargar PDF'}</button></div>

          {!!visibleManagerTeamMetrics.length&&<section className="operational-user-activity" aria-label="Actividad y sesiones de usuarios">
            <header><span><UsersRound/><small>CONTROL DE ACCESO Y ACTIVIDAD</small><h2>Actividad de todos los usuarios</h2><p>Presencia real: {activeUserCount} activos, {idleUserCount} conectados sin actividad y {visibleManagerTeamMetrics.length-connectedUserCount} desconectados. Actualización automática cada 20 segundos.</p></span><strong>{connectedUserCount}<small>conectados ahora</small></strong></header>
	            <label className="operational-user-search"><Search/><input value={userQuery} onChange={event=>setUserQuery(event.target.value)} placeholder="Buscar usuario, correo o departamento…"/><span>{displayedUserMetrics.length} resultados</span></label>
	            <div>{displayedUserMetrics.map(member=><article key={member.id} className={`presence-${presenceState(member)}`}><span><i/><b>{member.displayName||member.email}</b><small>{member.email}</small><em>{auditDepartmentLabels[member.department||'']||roleLabels[member.role]||readableCode(member.role,{})}</em></span><dl><div><dt>Estado real</dt><dd>{presenceLabel(member)}</dd></div><div><dt>Conexiones hoy</dt><dd>{member.todaySessionCount||0}</dd></div><div><dt>Inactividad 7:30–10:00</dt><dd>{durationFromSeconds(member.scheduledOfflineSeconds)}</dd></div><div><dt>Tickets activos</dt><dd>{member.active}</dd></div><div><dt>Tickets creados</dt><dd>{member.created}</dd></div><div><dt>Tickets resueltos</dt><dd>{member.resolved}</dd></div><div><dt>Auditorías completadas</dt><dd>{member.auditsCompleted}</dd></div><div><dt>Agencias completadas</dt><dd>{member.agenciesCompleted}</dd></div><div><dt>Agencias pendientes</dt><dd>{member.agenciesPending}</dd></div><div><dt>Movimientos</dt><dd>{member.movements}</dd></div><div><dt>Último inicio</dt><dd>{dateTime(member.lastLoginAt)}</dd></div><div><dt>Último movimiento</dt><dd>{dateTime(member.lastActivityAt)}</dd></div><div><dt>Tiempo sin actividad</dt><dd>{durationFromSeconds(member.inactiveSeconds)}</dd></div><div><dt>Duración última sesión</dt><dd>{durationFromSeconds(member.lastSessionSeconds)}</dd></div><div><dt>Total conectado</dt><dd>{durationFromSeconds(member.totalConnectedSeconds)}</dd></div></dl><button type="button" className="operational-user-pdf" disabled={userPdfBusy!==null} onClick={()=>void downloadUserPdf(member)}><Download/>{userPdfBusy===member.id?'Preparando expediente…':'Descargar PDF completo'}</button></article>)}</div>
          </section>}
            <div className="audit-ledger">
            <div className="audit-ledger-head"><span>Fecha y hora</span><span>Evento</span><span>Ticket / agencia</span><span>Responsable</span><span>Departamento</span><span>Detalle</span></div>
            {filteredEvents.map((event) => {
              const expanded = selectedEvent === event.id;
              return <article key={event.id} className={`audit-ledger-row action-${event.action.toLowerCase()}${expanded ? " expanded" : ""}`}>
                <time>{deviceDate(event.createdAt).toLocaleDateString("es-DO")}<small>{deviceDate(event.createdAt).toLocaleTimeString("es-DO")}</small></time>
                <span className="audit-action"><i>{event.action === "RESOLVED" || event.action === "CLOSED" ? <CheckCircle2 /> : event.action === "CREATED" ? <TicketCheck /> : <Activity />}</i><strong>{readableCode(event.action,{...statusLabels,...actionLabels})}</strong><small>{event.status?readableCode(event.status,statusLabels):'Movimiento registrado'}</small></span>
                <span><strong>{event.reference??`#${event.ticketNumber} · ${event.subject}`}</strong><small>{event.agency} · {event.group}</small></span>
                <span><strong>{event.actorName}</strong><small>{event.module?readableCode(event.module,moduleLabels):(event.ticketType === "INTERNAL" ? "Ticket interno" : "Soporte")}</small></span>
                <span><strong>{auditDepartmentLabels[event.department] || event.department}</strong><small>{priorityLabels[event.priority] || event.priority}</small></span>
                <button type="button" onClick={() => setSelectedEvent(expanded ? null : event.id)}>Ver detalle <ArrowRight /></button>
                {expanded && <div className="audit-ledger-detail"><div><small>Comentario o evidencia textual</small><p>{translatedDetail(event.comment)||"Esta acción no incluyó comentarios adicionales."}</p></div><div><small>Ruta del caso</small><p>{event.fromDepartment ? auditDepartmentLabels[event.fromDepartment] || readableCode(event.fromDepartment,{}) : "Origen"} <ArrowRight /> {event.toDepartment ? auditDepartmentLabels[event.toDepartment] || readableCode(event.toDepartment,{}) : auditDepartmentLabels[event.department] || readableCode(event.department,{})}</p></div>{tickets.some(ticket=>ticket.id===event.ticketId)&&<button type="button" onClick={() => onOpenTicket(tickets.find((ticket) => ticket.id === event.ticketId)!)}><ExternalLink /> Abrir expediente completo</button>}</div>}
              </article>;
            })}
            {!filteredEvents.length && <div className="operations-empty"><History /><strong>No hay movimientos con estos filtros</strong><span>Ajusta el periodo o los criterios de búsqueda.</span></div>}
            {departmentAuditScope&&<footer className="operations-toolbar"><button disabled={loading||refreshing||auditOffset===0} onClick={()=>setAuditOffset(v=>Math.max(0,v-200))}>Anterior</button><span>Página {auditOffset/200+1} · filtros y exportación sobre esta página</span><button disabled={loading||refreshing||!auditHasNext} onClick={()=>setAuditOffset(v=>v+200)}>Siguiente</button></footer>}
          </div>
        </>
      ) : (
        <>
          {(globalManager||scopedManager)&&directoryPanelScope?<section className="manager-team-control manager-team-directory-view" aria-label={`Usuarios de ${auditDepartmentLabels[directoryPanelScope]||directoryPanelScope}`}>
            <header><button type="button" className="manager-team-back" onClick={()=>setDirectoryPanelScope(null)}>← Volver atrás</button><span><UsersRound/><small>PERSONAL DEL DEPARTAMENTO</small><h2>Usuarios de {auditDepartmentLabels[directoryPanelScope]||directoryPanelScope}</h2></span><strong>{panelConnectedUserCount}<small>conectados ahora · {panelManagerTeamMetrics.length} registrados</small></strong></header>
            <div>{panelManagerTeamMetrics.map(member=><article className={`manager-user-activity presence-${presenceState(member)}`} key={member.id}><span><b>{member.displayName||member.email}</b><small>{auditDepartmentLabels[member.department||'']||technologyAreaLabels[member.supportTeam||'']||auditDepartmentLabels[member.supportTeam||'']||roleLabels[member.role]||readableCode(member.role,{})}</small><small>{member.email}</small></span><em><b>{presenceLabel(member)}</b><small>estado real</small></em><em><b>{member.active}</b><small>tickets activos</small></em><em><b>{member.created}</b><small>tickets creados</small></em><em><b>{member.resolved}</b><small>tickets resueltos</small></em><em><b>{dateTime(member.lastLoginAt)}</b><small>último acceso</small></em><em><b>{durationFromSeconds(member.inactiveSeconds)}</b><small>sin actividad</small></em></article>)}</div>
            {directoryLoading?<div className="manager-team-empty"><LoaderCircle className="spin"/> Cargando usuarios del departamento…</div>:!panelManagerTeamMetrics.length&&<div className="manager-team-empty">No hay usuarios activos asignados a este departamento.</div>}
          </section>:<>
          <div className="queue-summary">
            <article><TicketCheck /><span><small>Tickets activos</small><strong>{totalActive}</strong></span></article>
            <article><UsersRound /><span><small>Esperando turno</small><strong>{totalWaiting}</strong></span></article>
            <article><Activity /><span><small>En atención</small><strong>{totalInProgress}</strong></span></article>
            <label><Filter /><select value={department} disabled={!!teamScope || (!!departmentScope && departmentScope !== "TECHNOLOGY")} onChange={(event) => setDepartment(event.target.value)}>{!teamScope&&<option value="ALL">{departmentScope==='TECHNOLOGY'?'Todas las áreas de Tecnología':'Todos los departamentos'}</option>}{Object.entries(queueLabels).filter(([code])=>(teamScope?code===teamScope:departmentScope&&departmentScope!=="TECHNOLOGY"?code===departmentScope:usesTechnologyAreas||allQueueDepartments.includes(code))).map(([code, label]) => <option key={code} value={code}>{teamScope||(departmentScope&&departmentScope!=="TECHNOLOGY")?`Mi cola · ${label}`:label}</option>)}</select></label>
            <label className="queue-search"><Search /><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Buscar turno o agencia" /></label>
            <div className="queue-zoom-controls" aria-label="Tamaño de la cola en vivo">
              <button type="button" onClick={() => adjustQueueZoom(-0.1)} title="Alejar: reducir tamaño" aria-label="Alejar, reducir tamaño"><ZoomOut /><span>−</span></button>
              <button type="button" className={queueZoom===null?"auto active":"auto"} onClick={() => setQueueZoom(null)} title="Ajustar automáticamente" aria-label="Ajustar tamaño automáticamente"><small>VISTA</small><strong>{Math.round(effectiveQueueZoom*100)}%</strong></button>
              <button type="button" onClick={() => adjustQueueZoom(0.1)} title="Acercar: aumentar tamaño" aria-label="Acercar, aumentar tamaño"><ZoomIn /><span>+</span></button>
            </div>
            <button type="button" onClick={() => void toggleFullscreen()}>{isFullscreen ? <Minimize2 /> : <Fullscreen />} {isFullscreen ? "Salir" : "Pantalla completa"}</button>
          </div>

          <div className="queue-zoom-stage" style={{zoom:effectiveQueueZoom,width:`${100/effectiveQueueZoom}%`} as CSSProperties}>
          <section className="queue-area-metrics" aria-label="Porcentajes de tickets por área operativa">
            {areaMetrics.filter((metric) => department === "ALL" || metric.code === department).map((metric) => <article key={metric.code} className={`area-${metric.code.toLowerCase()}`}>
              <header><span><i />{queueLabels[metric.code] || metric.code}</span><strong>{metric.total}<small>tickets</small></strong></header>
              <div className="queue-area-stat pending"><span><b>Pendientes</b><small>{metric.pending} tickets</small></span><strong>{metric.pendingPercent}%</strong><i><em style={{width:`${metric.pendingPercent}%`}} /></i></div>
              <div className="queue-area-stat progress"><span><b>En progreso</b><small>{metric.inProgress} tickets</small></span><strong>{metric.inProgressPercent}%</strong><i><em style={{width:`${metric.inProgressPercent}%`}} /></i></div>
              <div className="queue-area-stat completed"><span><b>Completados</b><small>{metric.completed} tickets</small></span><strong>{metric.completedPercent}%</strong><i><em style={{width:`${metric.completedPercent}%`}} /></i></div>
              {(globalManager||scopedManager)&&<button type="button" className="queue-area-users-button" onClick={()=>void openDepartmentDirectory(metric.code)}><UsersRound/>Ver usuarios</button>}
            </article>)}
          </section>

          <section className="queue-finding-card" aria-labelledby="santo-domingo-findings-title">
            <header><span><AlertTriangle/><small>HALLAZGOS AUTOMÁTICOS · SANTO DOMINGO</small><strong id="santo-domingo-findings-title">Hallazgos activos por categoría</strong></span><b>{santoDomingoFindingTotal}<small> activos</small></b></header>
            <div className="queue-finding-categories">{santoDomingoFindingCategories.length?santoDomingoFindingCategories.map(category=><article key={category.key}><span><strong>{category.title}</strong><small>{departmentLabels[category.department]||category.department}</small></span><b>{category.total}</b><footer><em>{category.open} pendientes</em><em>{category.inProgress} en progreso</em></footer></article>):<div className="queue-finding-empty"><CheckCircle2/><span><strong>Sin hallazgos activos</strong><small>No hay hallazgos automáticos de Santo Domingo pendientes o en progreso.</small></span></div>}</div>
          </section>

          <div className={`queue-board${department !== "ALL" ? " queue-board-focused" : ""}`}>
            {queueDepartments.map((code) => {
              const normalizedQuery = query.trim().toLocaleLowerCase("es");
              const departmentTickets = (queues.get(code) || []).filter((ticket) => !normalizedQuery || `${turnCode(ticket)} ${ticket.ticketNumber} ${ticket.codigo} ${ticket.terminal} ${ticket.grupo} ${ticket.subject} ${ticket.assignedTechnicianName || ""}`.toLocaleLowerCase("es").includes(normalizedQuery));
              const attending = departmentTickets.filter((ticket) => ticket.status === "IN_PROGRESS");
              const waiting = departmentTickets.filter((ticket) => ticket.status !== "IN_PROGRESS");
              const focused = department !== "ALL";
              const orderedWaiting = sortTicketsByPriority(waiting);
              const density = departmentTickets.length > 12 ? " queue-density-high" : departmentTickets.length > 7 ? " queue-density-medium" : "";
              return <section className={`queue-lane area-${code.toLowerCase()}${focused ? " queue-lane-focused" : ""}${density}`} style={{"--queue-items":Math.max(1,departmentTickets.length)} as CSSProperties} key={code}>
                <header><span><i />{queueLabels[code] || code}</span><strong>{departmentTickets.length}<small>en cola</small></strong></header>
                <div className={`queue-serving${focused ? " queue-serving-focused" : ""}`}><small>ATENDIENDO AHORA</small><div className="queue-serving-grid">{attending.length ? attending.map((ticket) => <button type="button" key={ticket.id} onClick={() => onOpenTicket(ticket)}><span className="queue-turn-code">{turnCode(ticket)}</span><strong>{ticket.subject}</strong><small>{ticket.codigo} · {ticket.terminal}</small><em>{ticket.assignedTechnicianName || "Personal del departamento"}</em></button>) : <div className="queue-available"><Clock3 /><span>Disponible para el próximo turno</span></div>}</div></div>
                {focused ? (
                  <div className="queue-priority-board">
                    {priorityOrder.map((priority) => {
                      const priorityTickets = orderedWaiting.filter((ticket) => ticket.priority === priority);
                      if (!priorityTickets.length) return null;
                      return <section className={`queue-priority-group priority-group-${priority.toLowerCase()}`} key={priority}>
                        <header><span><i /> PRIORIDAD {priorityLabels[priority].toUpperCase()}</span><small>{priorityDescriptions[priority]}</small><strong>{priorityTickets.length} {priorityTickets.length === 1 ? "ticket" : "tickets"}</strong></header>
                        <div className="queue-priority-grid">{priorityTickets.map((ticket) => <button type="button" key={ticket.id} onClick={() => onOpenTicket(ticket)}><div className="priority-card-top"><b>{turnCode(ticket)}</b><span className={`queue-priority priority-${ticket.priority.toLowerCase()}`}>{priorityLabels[ticket.priority]}</span></div><strong>{ticket.subject}</strong><small>{ticket.codigo} · {ticket.terminal}</small><p>{ticket.grupo}</p><footer><span><Clock3 /> Espera {elapsed(ticket.createdAt, now)}</span><em>Orden {orderedWaiting.indexOf(ticket) + 1}</em><ArrowRight /></footer></button>)}</div>
                      </section>;
                    })}
                    {!orderedWaiting.length && <div className="queue-empty focused-empty"><CheckCircle2 /><span>Sin tickets esperando en este departamento</span></div>}
                  </div>
                ) : (
                  <div className="queue-waiting"><div className="queue-waiting-title"><span>PRÓXIMOS TURNOS</span><small>Orden de creación</small></div>{orderedWaiting.map((ticket, index) => <button type="button" key={ticket.id} onClick={() => onOpenTicket(ticket)}><b>{index + 1}</b><span><strong>{turnCode(ticket)}</strong><small>{ticket.codigo} · {ticket.terminal}</small></span><span className={`queue-priority priority-${ticket.priority.toLowerCase()}`}>{priorityLabels[ticket.priority] || ticket.priority}</span><time><Clock3 /> {elapsed(ticket.createdAt, now)}</time><ArrowRight /></button>)}{!orderedWaiting.length && <div className="queue-empty"><CheckCircle2 /><span>Sin tickets esperando</span></div>}</div>
                )}
              </section>;
            })}
          </div>
          </div>
          <section className="queue-day-ledger" aria-label="Resultado diario de tickets">
            <header><div><small>CONTROL DIARIO · 07:30 A. M. — 10:00 P. M.</small><h2>Completados y pendientes por fecha</h2><p>Al finalizar el período, todo ticket no completado queda registrado como pendiente en esta tabla.</p></div><div><label><CalendarDays/><input type="date" value={queueDate} onChange={event=>setQueueDate(event.target.value)}/></label><label><Filter/><select value={queueResult} onChange={event=>setQueueResult(event.target.value as typeof queueResult)}><option value="ALL">Completados y pendientes</option><option value="COMPLETED">Solo completados</option><option value="PENDING">Solo pendientes</option></select></label></div></header>
            <div className="queue-day-totals"><span><CheckCircle2/><strong>{completedForDate}</strong><small>completados</small></span><span><Clock3/><strong>{pendingForDate}</strong><small>pendientes</small></span></div>
            <div className="queue-day-table"><div className="head"><span>Turno</span><span>Ticket / agencia</span><span>Creado por</span><span>Resuelto por</span><span>Entrada</span><span>Resultado</span><span>Hora final</span></div>{dailyResults.map(({ticket,result,completedAt})=><button type="button" key={ticket.id} onClick={()=>onOpenTicket(ticket)}><strong>{turnCode(ticket)}</strong><span><b>{ticket.subject}</b><small>{ticket.codigo} · {ticket.terminal} · {ticket.grupo}</small></span><span><b>{ticket.createdByName||'Sin identificar'}</b><small>{technologyAreaLabels[ticket.assignedTeam||'']||departmentLabels[ticket.assignedDepartment]||ticket.assignedDepartment}</small></span><span><b>{ticket.resolvedByName||ticket.assignedTechnicianName||'Pendiente'}</b><small>{ticket.resolvedByName?'Cierre registrado':'Responsable actual'}</small></span><time>{deviceDate(ticket.createdAt).toLocaleTimeString('es-DO',{hour:'2-digit',minute:'2-digit'})}</time><em className={result.toLowerCase()}>{result==='COMPLETED'?'Completado':'Pendiente'}</em><time>{completedAt?new Date(completedAt).toLocaleTimeString('es-DO',{hour:'2-digit',minute:'2-digit'}):'10:00 p. m.'}</time></button>)}{!dailyResults.length&&<div className="empty"><History/><span><strong>No hay tickets para esta fecha y filtros</strong><small>Selecciona otra fecha, departamento o estado.</small></span></div>}</div>
          </section>
          <footer className="queue-footer"><span><i /> EN VIVO</span><p>Ciclo diario de 07:30 a. m. a 10:00 p. m. · al cierre, los tickets no completados quedan en la tabla de pendientes.</p><time>{new Date(now).toLocaleString("es-DO", { dateStyle: "full", timeStyle: "medium" })}</time></footer>
          </>}
        </>
      )}
    </section>
  );
}
