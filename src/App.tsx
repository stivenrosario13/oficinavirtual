import {passwordChangeEvent,startSessionValidation} from "@/lib/session-validation";
import { apiFetch as fetch } from "@/lib/api-fetch";
import { lazy as reactLazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { lazySection as lazy } from './components/vite/LazySection';
const WarehouseRequesterWorkspace=lazy(()=>import('./components/vite/WarehouseRequesterWorkspace'));
import {canReceiveWarehouseNotifications,isWarehouseNotificationUser,playWarehouseAlert} from './lib/warehouse-alert';
const WarehouseNotificationDocument=lazy(()=>import('./components/vite/WarehouseNotificationDocument'));
import {canManageWorkshop} from './lib/workshop-access';
const WorkshopWorkspace=lazy(()=>import('./components/vite/WorkshopWorkspace'));
const WarehouseSupport=lazy(()=>import('./components/vite/WarehouseSupport'));
const WarehouseLiveQueuePage=lazy(()=>import('./components/vite/WarehouseLiveQueuePage'));
const WarehouseRequestComposer=lazy(()=>import('./components/vite/WarehouseRequestComposer'));
const AdminHeaderVoiceAssistant=reactLazy(()=>import('./components/vite/AdminHeaderVoiceAssistant'));
import {
  BarChart3,
  Bell,
  BellRing,
  Building2,
  CheckCircle2,
  CheckCheck,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
  FileText,
  FileSignature,
  FolderOpen,
  Home,
  LifeBuoy,
  KeyRound,
  LoaderCircle,
  LogOut,
  MapPin,
  Menu,
  MessageCircle,
  Monitor,
  Search,
  ScrollText,
  Settings,
  ShieldCheck,
  Trash2,
  UserRound,
  Warehouse,
  X,
} from "lucide-react";
const EvidenceSuccess=lazy(()=>import("@/components/vite/EvidenceSuccess"));
const EmployeeAuditForm=lazy(()=>import("@/components/vite/EmployeeAuditForm"));
import PortalLogin from "@/components/vite/PortalLogin";
const ProfileSettings=lazy(()=>import("@/components/vite/ProfileSettings"));
const SupportChat=lazy(()=>import("@/components/vite/SupportChat"));
import type { PortalSession } from "@/lib/session";
import { sessionRoleLabel } from "@/lib/session";
import {isAdministrativeProfile,isIncidentOperatorAccount} from './lib/administrative-profiles';
import {userDepartments} from './lib/user-departments';
const ManagerDepartmentSupport=lazy(()=>import('./components/vite/ManagerDepartmentSupport'));
const DepartmentWorkspace=lazy(()=>import('./components/vite/AdministrativeWorkspace'));
import DepartmentDocumentNavigation, {type DepartmentDocumentSection} from './components/vite/DepartmentDocumentNavigation';
import type { OperationsTicket } from "@/lib/operations-center";
import {deviceDate,deviceTimestamp} from "@/lib/display";
import type {VoiceWarehouseBatch} from "@/lib/voice-operation-assistant";

const EquipmentTracking = lazy(async()=>{const module=await import("./components/vite/WorkshopBoard");return {default:module.EquipmentTracking};});
const AdminPanel = lazy(() => import("@/components/vite/AdminPanel"));
const UsersPermissionsWorkspace = lazy(() => import('./components/vite/UsersPermissionsWorkspace'));
const TicketCenter = lazy(() => import("@/components/vite/TicketCenter"));
const OperationsCenter = lazy(() => import("@/components/vite/OperationsCenter"));
import AdministrativeNavigation, {type AdministrativeSection} from "./components/vite/AdministrativeNavigation";
const AdministrativePortal = lazy(() => import("@/components/vite/AdministrativePortal"));
const AdministrativeRequests = lazy(() => import("@/components/vite/AdministrativeRequests"));
const ExecutiveReports = lazy(() => import("@/components/vite/ExecutiveReports"));
import PortalHome, { type PortalHomeModule } from "@/components/vite/PortalHome";

const StorageMigrationPanel = lazy(() => import("./components/vite/StorageMigrationPanel"));
const AgencyLocations = lazy(() => import("./components/vite/AgencyLocations"));

type PortalView =
  | "home"
  | "locations"
  | "storage"
  | "audit"
  | "dashboard"
  | "users"
  | "executiveReports"
  | "managerSupport"
  | "managerFiles"
  | "managerQueue"
  | "managerAudit"
  | "tickets"
  | "createTicket"
  | "warehouse"
  | "warehouseQueue"
  | "workshop"
  | "reception"
  | "systemAudit"
  | "queue"
  | "incidentReports";

type DepartmentSidebarSection = "support" | DepartmentDocumentSection | "transitions" | "agencies";

type Step = "group" | "agency" | "details" | "success";
type Group = { grupo: string; pending: number };
type Agency = {
  id: string;
  codigo: string;
  terminal: string;
  expectedLatitude?: number | null;
  expectedLongitude?: number | null;
  auditCount?: number;
  lastAuditedAt?: string | null;
  status?: "PENDING" | "COMPLETED" | "REVIEW_REQUIRED";
};
type Geo = {
  latitude: number;
  longitude: number;
  accuracyMeters: number;
  capturedAt: string;
  source: string;
};
type Result = {
  profileId: string;
  uploadToken: string;
  geolocation?: Geo;
  address?: {
    direccion: string;
    sector: string;
    municipio: string;
    provincia: string;
  };
  distanceMeters?: number | null;
  photosUploaded?: number;
  submittedAt?: string;
  locationSpreadMeters?: number;
  bestPhotoTitle?: string;
};
type TicketNotificationEvent =
  | "CREATED"
  | "ASSIGNED"
  | "ESCALATED"
  | "PENDING"
  | "RESOLVED"
  | "CLOSED"
  | "CANCELLED"
  | "UPDATED";
type TicketNotification = {
  kind: "TICKET";
  id: string;
  ticketNumber: number;
  codigo: string;
  terminal: string;
  grupo: string;
  assignedDepartment: string;
  subject: string;
  resolution: string | null;
  status: string;
  updatedAt: string;
  eventType: TicketNotificationEvent;
  message: string | null;
  actorName: string | null;
  notificationId: string | null;
  ticketType: "SUPPORT" | "INTERNAL" | "FINDING" | "WAREHOUSE";
  isRead: boolean;
};
type ChatNotification = {
  kind: "CHAT";
  id: string;
  conversationId: string;
  assignedDepartment: string;
  supervisorName: string;
  senderName: string;
  senderRole: string;
  message: string;
  updatedAt: string;
  isRead: boolean;
};
export type SupportNavigationTarget = {
  kind: "TICKET" | "CHAT" | "FINDING";
  assignedDepartment: string;
  ticketId?: string;
  ticketNumber?: number;
  ticketType?: "SUPPORT" | "INTERNAL" | "FINDING";
  findingId?: string;
  ticketStatus?: string;
  conversationId?: string;
  requestKey: number;
};
type NotificationItem = TicketNotification | ChatNotification;
type NotificationToast = { key: string; item: NotificationItem };
type PendingNotificationReceipt = { key: string; at: string };
import {readLicenseStatus,type LicenseStatus} from './lib/license-status';

const stringValue = (value: unknown, fallback = "") =>
  typeof value === "string" || typeof value === "number"
    ? String(value)
    : fallback;

const normalizeTicketEvent = (
  value: unknown,
  status: string,
): TicketNotificationEvent => {
  const event = stringValue(value).trim().toUpperCase();
  const aliases: Record<string, TicketNotificationEvent> = {
    CREATE: "CREATED",
    CREATED: "CREATED",
    OPEN: "CREATED",
    OPENED: "CREATED",
    ASSIGN: "ASSIGNED",
    ASSIGNED: "ASSIGNED",
    REASSIGNED: "ASSIGNED",
    ESCALATE: "ESCALATED",
    ESCALATED: "ESCALATED",
    TRANSFERRED: "ESCALATED",
    PENDING: "PENDING",
    RESOLVE: "RESOLVED",
    RESOLVED: "RESOLVED",
    CLOSE: "CLOSED",
    CLOSED: "CLOSED",
    CANCEL: "CANCELLED",
    CANCELLED: "CANCELLED",
    ANULADO: "CANCELLED",
    UPDATE: "UPDATED",
    UPDATED: "UPDATED",
  };
  if (aliases[event]) return aliases[event];
  if (status === "OPEN") return "CREATED";
  if (status === "IN_PROGRESS") return "ASSIGNED";
  if (status === "PENDING") return "PENDING";
  if (status === "RESOLVED") return "RESOLVED";
  if (status === "CLOSED") return "CLOSED";
  if (status === "CANCELLED") return "CANCELLED";
  return "UPDATED";
};

const normalizeTicketNotification = (
  value: unknown,
): TicketNotification | null => {
  if (!value || typeof value !== "object") return null;
  const item = value as Record<string, unknown>;
  const ticketNumber = Number(item.ticketNumber ?? item.ticket_number);
  if (!Number.isFinite(ticketNumber)) return null;
  const status = stringValue(item.status, "OPEN").trim().toUpperCase();
  const timestamp = stringValue(
    item.updatedAt ??
      item.updated_at ??
      item.resolvedAt ??
      item.resolved_at ??
      item.createdAt ??
      item.created_at,
    "1970-01-01T00:00:00.000Z",
  );
  const updatedAt = Number.isNaN(Date.parse(timestamp))
    ? "1970-01-01T00:00:00.000Z"
    : timestamp;
  const eventType = normalizeTicketEvent(
    item.eventType ??
      item.event_type ??
      item.notificationType ??
      item.notification_type ??
      item.event ??
      item.action,
    status,
  );
  const resolution = stringValue(item.resolution).trim() || null;
  const message = stringValue(item.message ?? item.detail).trim() || null;
  const actorName =
    stringValue(item.actorName ?? item.actor_name ?? item.updatedByName).trim() ||
    null;

  return {
    kind: "TICKET",
    id: stringValue(
      item.id ?? item.ticketId ?? item.ticket_id,
      String(ticketNumber),
    ),
    ticketNumber,
    codigo: stringValue(item.codigo ?? item.code),
    terminal: stringValue(item.terminal ?? item.agencyName, "Agencia"),
    grupo: stringValue(item.grupo ?? item.group),
    assignedDepartment: stringValue(
      item.assignedDepartment ?? item.assigned_department ?? item.department,
    ),
    subject: stringValue(item.subject, "Solicitud de soporte"),
    resolution,
    status,
    updatedAt,
    eventType,
    message,
    actorName,
    notificationId:
      stringValue(item.notificationId ?? item.notification_id).trim() || null,
    ticketType:
      stringValue(item.ticketType ?? item.ticket_type).toUpperCase() === "WAREHOUSE"?"WAREHOUSE":stringValue(item.ticketType ?? item.ticket_type).toUpperCase() === "FINDING"?"FINDING":stringValue(item.ticketType ?? item.ticket_type).toUpperCase() === "INTERNAL"?"INTERNAL":"SUPPORT",
    isRead: Boolean(item.isRead ?? item.is_read),
  };
};

const ticketEventLabel: Record<TicketNotificationEvent, string> = {
  CREATED: "Caso abierto",
  ASSIGNED: "Caso asignado",
  ESCALATED: "Caso transferido",
  PENDING: "Caso pendiente",
  RESOLVED: "Caso resuelto",
  CLOSED: "Caso cerrado",
  CANCELLED: "Caso anulado",
  UPDATED: "Caso actualizado",
};

const ticketNotificationTitle = (item: TicketNotification) =>
  item.ticketType==="WAREHOUSE"?`Nueva solicitud a Almacén · ${item.codigo}`:item.ticketType==="FINDING"?`${ticketEventLabel[item.eventType]} · Hallazgo automático`:`${ticketEventLabel[item.eventType]} · Ticket #${item.ticketNumber}`;

const ticketNotificationDetail = (item: TicketNotification) =>
  item.message ||
  item.resolution ||
  (item.eventType === "CREATED"
    ? `Se abrió una solicitud de soporte en ${item.terminal}.`
    : item.eventType === "ASSIGNED"
      ? "El caso ya tiene responsable y está en atención."
      : item.eventType === "ESCALATED"
        ? "El caso fue compartido con otro equipo para continuar la atención."
        : item.eventType === "RESOLVED"
          ? "El técnico registró la solución del caso."
          : item.eventType === "CLOSED"
            ? "El caso fue cerrado y quedó registrado en el historial."
            : item.eventType === "CANCELLED"
              ? "El ticket fue anulado y el motivo quedó registrado en el historial."
            : "El caso recibió una actualización.");

const legacyNotificationIdentity = (item: NotificationItem) =>
  item.kind === "TICKET"
    ? `TICKET:${item.id}:${item.status}:${item.updatedAt}`
    : `CHAT:${item.id}:${item.updatedAt}`;

const notificationIdentity = (item: NotificationItem) =>
  item.kind === "TICKET" && item.notificationId
    ? `${item.ticketType==="WAREHOUSE"?"WAREHOUSE_REQUEST":item.ticketType==="FINDING"?"FINDING_EVENT":"TICKET_EVENT"}:${item.notificationId}`
    : item.kind === "CHAT"
      ? `CHAT_MESSAGE:${item.id}`
      : legacyNotificationIdentity(item);

async function api<T>(path: string, options?: RequestInit): Promise<T> {
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

const initialViewForSession = (
  authenticated: PortalSession,
): PortalView => {
  if (isIncidentOperatorAccount(authenticated.email, authenticated.displayName)) return "incidentReports";
  if (isAdministrativeProfile(authenticated) || authenticated.role === "Department" && authenticated.permissions.canManageDepartment) return "reception";
  if (authenticated.supportTeam === "WAREHOUSE") return "warehouse";
  if (authenticated.supportTeam === "WORKSHOP") return "workshop";
  if (authenticated.role === "Administrator" && authenticated.permissions.canViewSupportDashboard) return "tickets";
  if (authenticated.role === "GroupAdministrator" && authenticated.permissions.canAudit) return "audit";
  if (authenticated.permissions.canAudit) return "audit";
  if (authenticated.permissions.canViewDashboard) return "dashboard";
  if (authenticated.permissions.canViewSupportDashboard) return "tickets";
  return "home";
};

const decodePushApplicationKey=(value:string)=>{const padded=value.replace(/-/g,"+").replace(/_/g,"/")+"===".slice((value.length+3)%4);return Uint8Array.from(atob(padded),character=>character.charCodeAt(0));};
const samePushApplicationKey=(current:ArrayBuffer|null,key:Uint8Array)=>{if(!current)return false;const left=new Uint8Array(current);return left.length===key.length&&left.every((value,index)=>value===key[index]);};
const ensurePushSubscription=async(registration:ServiceWorkerRegistration,publicKey:string)=>{const applicationServerKey=decodePushApplicationKey(publicKey);let subscription=await registration.pushManager.getSubscription();if(subscription&&!samePushApplicationKey(subscription.options.applicationServerKey,applicationServerKey)){await subscription.unsubscribe();subscription=null;}return subscription||registration.pushManager.subscribe({userVisibleOnly:true,applicationServerKey});};
const supportNavigationFromUrl=(href:string):SupportNavigationTarget|null=>{
  const url=new URL(href,window.location.origin);const explicitKind=url.searchParams.get("notification")?.toUpperCase();const kind=explicitKind||(url.searchParams.has("findingId")?"FINDING":url.searchParams.has("ticket")?"TICKET":url.searchParams.has("conversationId")?"CHAT":undefined);const department=url.searchParams.get("department")||"";
  if(kind==="FINDING"){const findingId=url.searchParams.get("findingId");return findingId?{kind:"FINDING",assignedDepartment:department,findingId,requestKey:Date.now()}:null;}
  if(kind==="TICKET"){
    const ticketNumber=Number(url.searchParams.get("ticket"));if(!Number.isFinite(ticketNumber))return null;
    return{kind:"TICKET",assignedDepartment:department,ticketId:url.searchParams.get("ticketId")||undefined,ticketNumber,ticketType:url.searchParams.get("ticketType")==="INTERNAL"?"INTERNAL":"SUPPORT",ticketStatus:url.searchParams.get("ticketStatus")||undefined,requestKey:Date.now()};
  }
  if(kind==="CHAT"){
    const conversationId=url.searchParams.get("conversationId");if(!conversationId)return null;
    return{kind:"CHAT",assignedDepartment:department,conversationId,requestKey:Date.now()};
  }
  return null;
};
const notificationReceiptFromUrl=(href:string):PendingNotificationReceipt|null=>{const url=new URL(href,window.location.origin);const key=url.searchParams.get("notificationKey")||"";const at=url.searchParams.get("notificationAt")||"";return (key.startsWith("TICKET_EVENT:")||key.startsWith("FINDING_EVENT:")||key.startsWith("CHAT_MESSAGE:"))&&Number.isFinite(Date.parse(at))?{key,at}:null;};
const clearNotificationDeepLink=()=>{const url=new URL(window.location.href);["notification","findingId","ticketId","ticket","department","ticketType","ticketStatus","conversationId","notificationKey","notificationAt"].forEach(key=>url.searchParams.delete(key));window.history.replaceState(null,"",`${url.pathname}${url.search}${url.hash}`);};

const persistentSessionStorageKey="oficina-virtual-device-session";
const storedPersistentSession=()=>localStorage.getItem(persistentSessionStorageKey)||"";
const rememberPersistentSession=(session:Partial<PortalSession>)=>{if(session.persistentSessionToken)localStorage.setItem(persistentSessionStorageKey,session.persistentSessionToken);};

export default function App() {
  const [equipmentCode,setEquipmentCode]=useState(()=>new URLSearchParams(window.location.search).get("equipment"));
  const [pendingNotificationTarget,setPendingNotificationTarget]=useState<SupportNavigationTarget|null>(()=>supportNavigationFromUrl(window.location.href));
  const [pendingNotificationReceipt,setPendingNotificationReceipt]=useState<PendingNotificationReceipt|null>(()=>notificationReceiptFromUrl(window.location.href));
  const [administrativeSection,setAdministrativeSection]=useState<AdministrativeSection>("forms");
  const [departmentDocumentSection,setDepartmentDocumentSection]=useState<DepartmentDocumentSection>("files");
  const [license, setLicense] = useState<LicenseStatus | null>(null);
  const [session, setSession] = useState<PortalSession | null>(null);
  const [checkingSession, setCheckingSession] = useState(true);
  const [profileOpen, setProfileOpen] = useState(false);
  const [headerAccountOpen, setHeaderAccountOpen] = useState(false);
  const [portalSearch, setPortalSearch] = useState("");
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(
    () => localStorage.getItem("portal-sidebar-collapsed") === "1",
  );
  const [view, setView] = useState<PortalView>("audit");
  const [adminVoiceWarehouse,setAdminVoiceWarehouse]=useState<VoiceWarehouseBatch|null>(null);
  const [step, setStep] = useState<Step>("group");
  const [groups, setGroups] = useState<Group[]>([]);
  const [agencies, setAgencies] = useState<Agency[]>([]);
  const [group, setGroup] = useState("");
  const [agency, setAgency] = useState<Agency | null>(null);
  const [query, setQuery] = useState("");
  const [result, setResult] = useState<Result | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [ticketNotifications, setTicketNotifications] = useState<
    NotificationItem[]
  >([]);
  const [notificationToasts, setNotificationToasts] = useState<
    NotificationToast[]
  >([]);
  const [supportNavigation, setSupportNavigation] =
    useState<SupportNavigationTarget | null>(null);
  const [supportEntryRequest, setSupportEntryRequest] = useState(0);
  const [selectedSupportDepartment, setSelectedSupportDepartment] = useState<string | null>(null);
  const [departmentEntry, setDepartmentEntry] = useState<{
    key: number;
    department: string;
    section: DepartmentSidebarSection;
  } | null>(null);
  const [supervisorTicketInboxRequest, setSupervisorTicketInboxRequest] = useState(0);
  const [reportAgencyOpen, setReportAgencyOpen] = useState(false);
  const [, setUnreadNotifications] = useState(0);
  const [notificationCenterOpen, setNotificationCenterOpen] = useState(false);
  const [notificationCenterKind, setNotificationCenterKind] = useState<"TICKET" | "CHAT">("TICKET");
  const [pushPromptOpen,setPushPromptOpen]=useState(false);
  const subscribePhonePush=useCallback(async()=>{
    if(!('serviceWorker' in navigator)||!('PushManager' in window)||!('Notification' in window))throw new Error('Este navegador no admite notificaciones push.');
    const permission=Notification.permission==='granted'?'granted':await Notification.requestPermission();
    if(permission!=='granted')throw new Error('No se concedió el permiso de notificaciones.');
    const registration=await navigator.serviceWorker.register('/sw.js',{updateViaCache:'none'});await registration.update();
    const keyResponse=await fetch('/api/push/public-key',{cache:'no-store'});if(!keyResponse.ok){const failure=await keyResponse.json().catch(()=>({}));throw new Error(failure.error||'El servidor push todavía no está activo.');}
    const keyData=await keyResponse.json();if(!keyData.publicKey)throw new Error('Falta la clave pública VAPID en Monster.');
    const subscription=await ensurePushSubscription(registration,String(keyData.publicKey));
    const response=await fetch('/api/push/subscriptions',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(subscription)});if(!response.ok){const failure=await response.json().catch(()=>({}));throw new Error(failure.error||'No se pudo registrar este dispositivo.');}
    await registration.showNotification('Notificaciones activadas',{body:'Este dispositivo ya puede recibir tus alertas de Grupo Tejeda.',icon:'/loto-real-logo-transparent.png',tag:`push-enabled-${Date.now()}`,requireInteraction:true,data:{url:'/'}});
    setPushPromptOpen(false);localStorage.setItem('push-prompt-dismissed','1');
  },[]);
  useEffect(() => {
    if (!session || !('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) return;
    navigator.serviceWorker.register('/sw.js',{updateViaCache:'none'}).then(async registration => {
      await registration.update();
      if (Notification.permission === 'default') return;
      if (Notification.permission !== 'granted') return;
      const keyResponse=await fetch('/api/push/public-key',{cache:'no-store'});if(!keyResponse.ok)return;const keyData=await keyResponse.json();
      if (!keyData.publicKey) return;
      const subscription=await ensurePushSubscription(registration,String(keyData.publicKey));
      const saved=await fetch('/api/push/subscriptions',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(subscription)});if(!saved.ok)throw new Error('No se pudo renovar la suscripción push.');
    }).catch(()=>undefined);
  }, [session]);
  useEffect(() => {
    if(!session||!('Notification' in window))return;
    setPushPromptOpen(Notification.permission==='default'&&localStorage.getItem('push-prompt-dismissed')!=='1');
  }, [session]);
  const notificationAudio = useRef<AudioContext | null>(null);
  const [warehouseNotificationId,setWarehouseNotificationId]=useState<number|null>(null);
  const notificationSnapshot = useRef<Set<string> | null>(null);

  const openTicketFromOperations = useCallback((ticket: OperationsTicket) => {
    setSelectedSupportDepartment(ticket.assignedDepartment);
    setDepartmentEntry({ key: Date.now(), department: ticket.assignedDepartment, section: "support" });
    setSupportNavigation({
      kind: "TICKET",
      assignedDepartment: ticket.assignedDepartment,
      ticketId: ticket.id,
      ticketNumber: ticket.ticketNumber,
      ticketType: ticket.ticketType,
      ticketStatus: ticket.status,
      requestKey: Date.now(),
    });
    setView("tickets");
  }, []);

  useEffect(()=>{
    delete document.documentElement.dataset.portalTheme;
    if(session)localStorage.removeItem(`portal-theme:${session.id}`);
  },[session]);

  useEffect(() => {
    let active = true;
    fetch("/api/license/status", { cache: "no-store" })
      .then(async (response) => {
        const data = await readLicenseStatus(response);
        if (active) setLicense(data);
      })
      .catch(() => {
        if (active) setLicense({ enabled: true, allowed: false, status: "UNAVAILABLE", code: "LICENSE_UNAVAILABLE", message: "No fue posible comprobar la autorización del sistema. Contacte al soporte técnico.", usingGracePeriod: false });
      });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (!sidebarOpen) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setSidebarOpen(false);
    };
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [sidebarOpen]);

  useEffect(() => {
    const unlockAudio = () => {
      if (!notificationAudio.current)
        notificationAudio.current = new AudioContext();
      if (notificationAudio.current.state === "suspended")
        void notificationAudio.current.resume();
    };
    window.addEventListener("pointerdown", unlockAudio, { once: true });
    window.addEventListener("keydown", unlockAudio, { once: true });
    return () => {
      window.removeEventListener("pointerdown", unlockAudio);
      window.removeEventListener("keydown", unlockAudio);
    };
  }, []);
  const playNotificationChime = () => {
    const audio = notificationAudio.current;
    if (!audio || audio.state !== "running") return;
    const now = audio.currentTime;
    [
      [1318.51, 0, 0.34],
      [1760, 0.16, 0.42],
    ].forEach(([frequency, delay, duration]) => {
      const oscillator = audio.createOscillator();
      const gain = audio.createGain();
      oscillator.type = "sine";
      oscillator.frequency.setValueAtTime(frequency, now + delay);
      gain.gain.setValueAtTime(0.0001, now + delay);
      gain.gain.exponentialRampToValueAtTime(0.16, now + delay + 0.025);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + delay + duration);
      oscillator.connect(gain);
      gain.connect(audio.destination);
      oscillator.start(now + delay);
      oscillator.stop(now + delay + duration + 0.03);
    });
  };
  useEffect(() => {
    const restore=async()=>{
      // Las consultas conservan estas llamadas explícitas para el cookie persistente:
      // fetch("/api/admin/session", { cache: "no-store", credentials: "same-origin" })
      // y fetch("/api/admin/session/restore", ...).
      const readSession=async(url:string,init?:RequestInit)=>{
        let response: Response | undefined;
        let networkError: unknown;
        for(let attempt=0;attempt<2;attempt+=1){
          const controller = new AbortController();
          const timer = window.setTimeout(() => controller.abort(), 8000);
          try{
            response=await fetch(url,{...init,signal:controller.signal,cache:"no-store",credentials:"same-origin"});
            networkError=undefined;
            if(response.ok||!([500,502,503,504] as number[]).includes(response.status)||attempt===1)break;
          }catch(error){
            networkError=error;
            if(attempt===1)throw error;
          } finally { window.clearTimeout(timer); }
          await new Promise(resolve=>window.setTimeout(resolve,350*(attempt+1)));
        }
        if(!response)throw networkError instanceof Error?networkError:new Error("El servidor no respondió.");
        return response;
      };
      let response=await readSession("/api/admin/session");
      let restored=response.ok?(await response.json()) as Partial<PortalSession>:{authenticated:false};
      if(!restored.authenticated&&storedPersistentSession()){
        response=await readSession("/api/admin/session/restore",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({token:storedPersistentSession()})});
        restored=response.ok?(await response.json()) as Partial<PortalSession>:{authenticated:false};
        // No descartes el token por una caída temporal de SQL/Monster; solo
        // se invalida cuando el servidor confirma que la sesión ya no existe.
        if(!restored.authenticated&&response.status===401)localStorage.removeItem(persistentSessionStorageKey);
      }
      return restored;
    };
    restore()
      .then((restored) => {
        if (!restored.authenticated || !restored.permissions || !restored.role)
          return;
        const authenticated = restored as PortalSession;
        setSession(authenticated);
        setView(initialViewForSession(authenticated));
      })
      .catch(() => undefined)
      .finally(() => setCheckingSession(false));
  }, []);

  const accountUpdateRef = useRef({changing:false,generation:0,checking:false});
  useEffect(() => {
    const changed=(event:Event)=>{accountUpdateRef.current.changing=Boolean((event as CustomEvent<boolean>).detail);accountUpdateRef.current.generation++;};
    window.addEventListener(passwordChangeEvent,changed);
    return()=>window.removeEventListener(passwordChangeEvent,changed);
  },[]);
  const expireIfUnauthorized = useCallback((loadError: unknown) => {
    if ((loadError as Error & { status?: number }).status !== 401)return;
    // An old concurrent request can finish after a password change renewed the cookie.
    // Verify the current cookie before clearing the newly established session.
    const state=accountUpdateRef.current;if(state.changing||state.checking)return;
    state.checking=true;const generation=state.generation;
    void (async()=>{
      try {
        const response=await fetch('/api/admin/session',{cache:'no-store'});
        const revoked=response.status===401||response.ok&&(await response.json()).authenticated===false;
        if(!revoked||state.changing||generation!==state.generation)return;
        if(storedPersistentSession()){window.location.reload();return;}
        setSession(null);
      } catch { /* A temporary outage is not proof that this session expired. */ }
      finally {state.checking=false;}
    })();
  }, []);

  useEffect(() => {
    if (!session?.id) return;
    return startSessionValidation(() => {
      localStorage.removeItem(persistentSessionStorageKey);
      setSession(null);
      setProfileOpen(false);
    });
  }, [session?.id,session?.persistentSessionToken]);

  const loadGroups = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    if (!silent) setError("");
    try {
      setGroups((await api<{ groups: Group[] }>("/api/groups")).groups);
    } catch (loadError) {
      expireIfUnauthorized(loadError);
      if (!silent) {
        setGroups([]);
        setError(
          (loadError as Error).message || "No fue posible consultar tus grupos.",
        );
      }
    } finally {
      if (!silent) setLoading(false);
    }
  }, [expireIfUnauthorized]);

  const loadAgenciesForGroup = useCallback(async (value: string, silent = false) => {
    if (!silent) setLoading(true);
    if (!silent) setError("");
    try {
      const rows = (
        await api<{ agencies: Agency[] }>(
          `/api/agencies?grupo=${encodeURIComponent(value)}`,
        )
      ).agencies;
      setAgencies(rows);
      setAgency((current) => current ? rows.find((item) => item.id === current.id) ?? null : null);
    } catch (loadError) {
      expireIfUnauthorized(loadError);
      if (!silent) {
        setAgencies([]);
        setAgency(null);
        setError(
          (loadError as Error).message ||
            "No fue posible consultar tus agencias.",
        );
      }
    } finally {
      if (!silent) setLoading(false);
    }
  }, [expireIfUnauthorized]);

  const sessionId = session?.id;
  const sessionMustChangePassword = session?.mustChangePassword;
  const canAudit = session?.permissions.canAudit;
  const canUseSupportChat = session?.permissions.canUseSupportChat;
  const canViewTicketNotifications =
    session?.permissions.canViewTicketNotifications;
  const receivesWarehouseNotifications=canReceiveWarehouseNotifications(session);
  const usesWarehouseAlert=isWarehouseNotificationUser(session);

  useEffect(()=>{
    if(!sessionId||!pendingNotificationTarget)return;
    setSelectedSupportDepartment(pendingNotificationTarget.assignedDepartment || null);
    if (pendingNotificationTarget.assignedDepartment) setDepartmentEntry({key:Date.now(),department:pendingNotificationTarget.assignedDepartment,section:"support"});
    setView("tickets");
    setSupportNavigation({...pendingNotificationTarget,requestKey:Date.now()});
    setPendingNotificationTarget(null);
    clearNotificationDeepLink();
  },[pendingNotificationTarget,sessionId]);

  useEffect(() => {
    if (sessionId && !sessionMustChangePassword && canAudit && view === "audit")
      void loadGroups();
  }, [canAudit, loadGroups, sessionId, sessionMustChangePassword, view]);
  useEffect(() => {
    if (!sessionId || sessionMustChangePassword || !canAudit || view !== "audit" || step === "details" || step === "success") return;
    const refreshAgencyCatalog = () => {
      void loadGroups(true);
      if (group) void loadAgenciesForGroup(group, true);
    };
    window.addEventListener("support-live-refresh", refreshAgencyCatalog);
    return () => window.removeEventListener("support-live-refresh", refreshAgencyCatalog);
  }, [canAudit, group, loadAgenciesForGroup, loadGroups, sessionId, sessionMustChangePassword, view, step]);
  useEffect(() => {
    if (!sessionId || sessionMustChangePassword) return;
    let timer: number | null = null;
    let lastPublishedAt = 0;
    const publishRefresh = () => {
      if (document.visibilityState !== "visible" || !navigator.onLine) return;
      const now = Date.now();
      if (now - lastPublishedAt < 2_500) return;
      lastPublishedAt = now;
      window.dispatchEvent(new Event("support-live-refresh"));
    };
    const scheduleRefresh = () => {
      timer = window.setTimeout(() => {
        publishRefresh();
        scheduleRefresh();
      }, 15_000);
    };
    scheduleRefresh();
    window.addEventListener("focus", publishRefresh);
    window.addEventListener("online", publishRefresh);
    document.addEventListener("visibilitychange", publishRefresh);
    return () => {
      if (timer !== null) window.clearTimeout(timer);
      window.removeEventListener("focus", publishRefresh);
      window.removeEventListener("online", publishRefresh);
      document.removeEventListener("visibilitychange", publishRefresh);
    };
  }, [sessionId, sessionMustChangePassword]);
  useEffect(() => {
    if (
      !sessionId ||
      sessionMustChangePassword ||
      !canViewTicketNotifications
    ) {
      setTicketNotifications([]);
      setUnreadNotifications(0);
      return;
    }
    let refreshInFlight = false,cancelled=false;
    notificationSnapshot.current=null;
    setWarehouseNotificationId(null);
    const refreshNotifications = async () => {
      if (refreshInFlight || !navigator.onLine || document.hidden) return;
      refreshInFlight = true;
      try {
        const [ticketResponse, chatResponse,warehouseResponse] = await Promise.all([
          fetch("/api/tickets/notifications"),
          canUseSupportChat
            ? fetch("/api/support/chat/notifications")
            : Promise.resolve(null),
          receivesWarehouseNotifications?fetch('/api/warehouse/fulfillment/notifications',{cache:'no-store',credentials:'same-origin'}).catch(()=>null):Promise.resolve(null),
        ]);
        if (ticketResponse.status === 401 || chatResponse?.status === 401) {
          expireIfUnauthorized({status:401});
          return;
        }
        if (!ticketResponse.ok&&!warehouseResponse?.ok) return;
        const ticketBody: unknown = ticketResponse.ok?await ticketResponse.json():[];
        const chatBody = chatResponse?.ok
          ? await chatResponse.json()
          : { notifications: [] };
        const rawTickets = Array.isArray(ticketBody)
          ? ticketBody
          : ticketBody && typeof ticketBody === "object"
            ? ((ticketBody as { notifications?: unknown }).notifications ?? [])
            : [];
        const warehouseBody:unknown=warehouseResponse?.ok?await warehouseResponse.json():[];
        const tickets = [...(Array.isArray(rawTickets) ? rawTickets : []),...(Array.isArray(warehouseBody)?warehouseBody:[])]
          .map(normalizeTicketNotification)
          .filter((item): item is TicketNotification => item !== null);
        const chats = (
          (chatBody.notifications || []) as Array<
            Omit<ChatNotification, "kind" | "updatedAt"> & { createdAt: string }
          >
        ).map(({ createdAt, ...item }) => ({
          ...item,
          kind: "CHAT" as const,
          updatedAt: createdAt,
          isRead: Boolean(item.isRead),
        }));
        const items: NotificationItem[] = [...tickets, ...chats].sort(
          (a, b) =>
            deviceTimestamp(b.updatedAt) - deviceTimestamp(a.updatedAt),
        );
        if(cancelled)return;
        const incoming = new Set(items.map(notificationIdentity));
        const visibleItems = items;
        const newItems = notificationSnapshot.current
          ? visibleItems.filter(
              (item) =>
                !item.isRead &&
                !notificationSnapshot.current!.has(notificationIdentity(item)),
            )
          : [];
        if (notificationSnapshot.current && newItems.length){
          if(usesWarehouseAlert&&newItems.some(item=>item.kind==='TICKET'&&item.ticketType==='WAREHOUSE'))playWarehouseAlert(notificationAudio.current);
          else playNotificationChime();
        }
        if (newItems.length) {
          const toasts = newItems.slice(0, 3).map((item) => ({
            key: notificationIdentity(item),
            item,
          }));
          setNotificationToasts((current) =>
            [...toasts, ...current].slice(0, 4),
          );
          toasts.forEach((toast) =>
            window.setTimeout(
              () =>
                setNotificationToasts((current) =>
                  current.filter((item) => item.key !== toast.key),
                ),
              10000,
            ),
          );
        }
        notificationSnapshot.current = new Set([...(notificationSnapshot.current||[]),...incoming]);
        setTicketNotifications(visibleItems);
        setUnreadNotifications(visibleItems.filter((item) => !item.isRead).length);
      } catch {
        return;
      } finally {
        refreshInFlight = false;
      }
    };
    void refreshNotifications();
    const notificationInterval=window.matchMedia('(max-width: 720px)').matches?30000:10000;
    const notificationTimer=usesWarehouseAlert?window.setInterval(()=>void refreshNotifications(),notificationInterval):undefined;
    const refreshWhenVisible=()=>{if(!document.hidden)void refreshNotifications();};
    document.addEventListener('visibilitychange',refreshWhenVisible);
    window.addEventListener("support-live-refresh", refreshNotifications);
    return () => {
      cancelled=true;window.clearInterval(notificationTimer);
      document.removeEventListener('visibilitychange',refreshWhenVisible);
      window.removeEventListener("support-live-refresh", refreshNotifications);
    };
  }, [
    canUseSupportChat,
    canViewTicketNotifications,
    expireIfUnauthorized,
    sessionId,
    sessionMustChangePassword,
    receivesWarehouseNotifications,
    usesWarehouseAlert,
  ]);
  useEffect(() => {
    if (!notificationCenterOpen) return;
    const previousBodyOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setNotificationCenterOpen(false);
    };
    window.addEventListener("keydown", closeOnEscape);
    return () => {
      document.body.style.overflow = previousBodyOverflow;
      window.removeEventListener("keydown", closeOnEscape);
    };
  }, [notificationCenterOpen]);

  const chooseGroup = async (value: string) => {
    setGroup(value);
    setAgency(null);
    setQuery("");
    setStep("agency");
    await loadAgenciesForGroup(value);
  };

  const filtered = useMemo(() => {
    const q = query
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();
    if (step === "group")
      return groups.filter((item) =>
        item.grupo
          .normalize("NFD")
          .replace(/[\u0300-\u036f]/g, "")
          .toLowerCase()
          .includes(q),
      );
    return agencies.filter((item) =>
      `${item.terminal} ${item.codigo}`
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .toLowerCase()
        .includes(q),
    );
  }, [query, step, groups, agencies]);

  const reset = () => {
    setReportAgencyOpen(false);
    setStep("group");
    setGroup("");
    setAgency(null);
    setQuery("");
    setResult(null);
    void loadGroups();
  };
  const logout = async () => {
    const token=storedPersistentSession();
    localStorage.removeItem(persistentSessionStorageKey);
    await fetch("/api/admin/logout", { method: "POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({token}) }).catch(() => undefined);
    setSession(null);
    setGroups([]);
    setAgencies([]);
    setTicketNotifications([]);
    setUnreadNotifications(0);
    setNotificationCenterOpen(false);
    setSelectedSupportDepartment(null);
    setDepartmentEntry(null);
    setView("audit");
    setStep("group");
  };
  const openNotificationCenter = (kind: "TICKET" | "CHAT" = "TICKET") => {
    if (!session) return;
    const unread = ticketNotifications.filter((item) => item.kind === kind && !item.isRead);
    if (unread.length) {
      void api("/api/notifications/state", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ keys: unread.map(notificationIdentity), action: "READ" }),
      }).catch(expireIfUnauthorized);
      setTicketNotifications((current) => current.map((item) => ({ ...item, isRead: true })));
    }
    setUnreadNotifications((current) => Math.max(0, current - unread.length));
    setNotificationCenterKind(kind);
    setNotificationCenterOpen(true);
  };
  const sendPushTest = async () => {
    try {
      await subscribePhonePush();
      const response=await fetch('/api/push/test',{method:'POST',cache:'no-store'});const data=await response.json().catch(()=>({}));
      window.alert(response.ok?"Notificaciones activadas. La prueba fue enviada a este teléfono.":(data.error||"No fue posible enviar la prueba."));
    } catch (pushError) {
      window.alert(pushError instanceof Error ? pushError.message : "No fue posible activar las notificaciones en este teléfono.");
    }
  };
  const openNotificationDestination = (item: NotificationItem) => {
    const openedAt=deviceTimestamp(item.updatedAt);
    const readThrough=ticketNotifications.filter(candidate=>!candidate.isRead&&deviceTimestamp(candidate.updatedAt)<=openedAt);
    if(readThrough.length){const keys=[...new Set(readThrough.map(notificationIdentity))];void api("/api/notifications/state",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({keys,action:"READ"})}).catch(expireIfUnauthorized);const readKeys=new Set(keys);setTicketNotifications(current=>current.map(candidate=>readKeys.has(notificationIdentity(candidate))?{...candidate,isRead:true}:candidate));setUnreadNotifications(current=>Math.max(0,current-readKeys.size));}
    setNotificationCenterOpen(false);
    if(item.kind==='TICKET'&&item.ticketType==='WAREHOUSE'){setWarehouseNotificationId(Number(item.id));return;}
    setSelectedSupportDepartment(item.assignedDepartment || null);
    if (item.assignedDepartment) setDepartmentEntry({key:Date.now(),department:item.assignedDepartment,section:"support"});
    setView("tickets");
    setSupportNavigation({
      kind: item.kind === "TICKET"&&item.ticketType==="FINDING"?"FINDING":item.kind,
      assignedDepartment: item.assignedDepartment,
      ticketId: item.kind === "TICKET" ? item.id : undefined,
      ticketNumber: item.kind === "TICKET" ? item.ticketNumber : undefined,
      ticketType: item.kind === "TICKET" && item.ticketType!=="WAREHOUSE" ? item.ticketType : undefined,
      findingId:item.kind==="TICKET"&&item.ticketType==="FINDING"?item.id:undefined,
      ticketStatus: item.kind === "TICKET" ? item.status : undefined,
      conversationId: item.kind === "CHAT" ? item.conversationId : undefined,
      requestKey: Date.now(),
    });
  };
  const markNotificationsRead = (items: NotificationItem[]) => {
    if (!session || !items.length) return;
    const unreadKeys = new Set(items.filter((item) => !item.isRead).map(notificationIdentity));
    if (!unreadKeys.size) return;
    void api("/api/notifications/state", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ keys: [...unreadKeys], action: "READ" }),
    }).catch(expireIfUnauthorized);
    setTicketNotifications((current) => current.map((item) => unreadKeys.has(notificationIdentity(item)) ? { ...item, isRead: true } : item));
    setUnreadNotifications((current) => Math.max(0, current - unreadKeys.size));
  };
  const dismissNotifications = (items: NotificationItem[]) => {
    if (!session || !items.length) return;
    void api("/api/notifications/state", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ keys: items.map(notificationIdentity), action: "DISMISS" }),
    }).catch(expireIfUnauthorized);
    const removed = new Set(items.map(notificationIdentity));
    setTicketNotifications((current) =>
      current.filter((item) => !removed.has(notificationIdentity(item))),
    );
    setNotificationToasts((current) =>
      current.filter((toast) => !removed.has(toast.key)),
    );
    const removedUnread = items.filter((item) => !item.isRead).length;
    setUnreadNotifications((current) => Math.max(0, current - removedUnread));
  };
  useEffect(()=>{
    if(!session||!pendingNotificationReceipt)return;
    const cutoff=deviceTimestamp(pendingNotificationReceipt.at);
    const keys=new Set<string>([pendingNotificationReceipt.key]);
    ticketNotifications.forEach(item=>{if(!item.isRead&&deviceTimestamp(item.updatedAt)<=cutoff)keys.add(notificationIdentity(item));});
    setPendingNotificationReceipt(null);
    void api("/api/notifications/state",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({keys:[...keys],action:"READ"})}).then(()=>{setTicketNotifications(current=>current.map(item=>keys.has(notificationIdentity(item))?{...item,isRead:true}:item));setUnreadNotifications(current=>Math.max(0,current-keys.size));}).catch(expireIfUnauthorized);
  },[expireIfUnauthorized,pendingNotificationReceipt,session,ticketNotifications]);
  const ticketNotificationItems = ticketNotifications.filter(
    (item): item is TicketNotification => item.kind === "TICKET",
  );
  const chatNotificationItems = ticketNotifications.filter(
    (item): item is ChatNotification => item.kind === "CHAT",
  );
  const unreadTicketNotifications = ticketNotificationItems.filter((item) => !item.isRead).length;
  const unreadChatNotifications = chatNotificationItems.filter((item) => !item.isRead).length;
  const visibleCenterItems = notificationCenterKind === "TICKET" ? ticketNotificationItems : chatNotificationItems;
  const notificationCenterCopy =
    session?.role === "Administrator"
      ? {
          kicker: "CENTRO GLOBAL DE NOTIFICACIONES",
          title: "Todos los procesos de soporte",
          description:
            "Vista administrativa de tickets y mensajes de todos los departamentos.",
        }
      : session?.role === "GroupAdministrator"
        ? {
            kicker: "MIS NOTIFICACIONES",
            title: "Incidencias y respuestas",
            description:
              "Recibirás avisos al abrir, asignar, transferir o resolver tus tickets, además de las respuestas del chat.",
          }
        : {
            kicker: "BANDEJA DE SOPORTE",
            title: "Actividad que requiere tu atención",
            description:
              "Recibirás avisos cuando llegue un caso nuevo a tu departamento, se te asigne trabajo o un técnico o supervisor complete una resolución.",
          };
  const departmentLabel = (code: string) =>
    code === "TECHNOLOGY"
      ? "Tecnología"
      : code === "GENERAL_SERVICES"
        ? "Servicios Generales"
        : code === "HUMAN_RESOURCES"
          ? "Recursos Humanos"
          : code === "ADMINISTRATIVE"
            ? "Departamento Administrativo"
          : code;
  const renderNotificationRows = (items: NotificationItem[]) =>
    items.map((item) => (
      <article
        className={`resolved-ticket-row notification-clickable ${item.isRead ? "notification-read" : "notification-unread"} notification-event-${item.kind === "TICKET" ? item.eventType.toLowerCase() : "chat"}`}
        key={notificationIdentity(item)}
        role="button"
        tabIndex={0}
        aria-label={
          item.kind === "TICKET"
            ? `${ticketNotificationTitle(item)}. ${item.subject}`
            : `Nuevo mensaje de ${item.senderName}`
        }
        onClick={() => openNotificationDestination(item)}
        onKeyDown={(event) => {
          if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            openNotificationDestination(item);
          }
        }}
      >
        <span className="notification-primary">
          {!item.isRead && <i className="notification-new-dot" aria-label="Nueva" />}
          <strong>
            {item.kind === "TICKET" ? item.ticketType==="WAREHOUSE"?"Solicitud":item.ticketType==="FINDING"?"Hallazgo":`#${item.ticketNumber}` : "Chat"}
          </strong>
          <small>
            {item.kind === "TICKET"
              ? ticketEventLabel[item.eventType]
              : "Mensaje nuevo"}
          </small>
        </span>
        <span className="notification-context">
          <strong>
            {item.kind === "TICKET" ? item.terminal : item.supervisorName}
          </strong>
          <small>
            {item.kind === "TICKET"
              ? `${item.codigo} · ${item.grupo}`
              : `Escribió ${item.senderName}`}
          </small>
        </span>
        <span className="notification-department">
          {departmentLabel(item.assignedDepartment)}
        </span>
        <span className="notification-detail">
          <strong>
            {item.kind === "TICKET" ? item.subject : item.senderName}
          </strong>
          <small>
            {item.kind === "TICKET"
              ? ticketNotificationDetail(item)
              : item.message}
          </small>
          {item.kind === "TICKET" && item.actorName && (
            <em className="notification-actor">
              Gestionado por {item.actorName}
            </em>
          )}
        </span>
        <span className="notification-row-actions">
          <time title="Fecha del evento de la notificación">Actividad: {deviceDate(item.updatedAt).toLocaleString("es-DO")}</time>
          <span>
            {!item.isRead && <button type="button" onClick={(event) => { event.stopPropagation(); markNotificationsRead([item]); }}><CheckCheck /> Leído</button>}
            <button type="button" className="notification-dismiss" onClick={(event) => { event.stopPropagation(); dismissNotifications([item]); }}><Trash2 /> Descartar</button>
          </span>
        </span>
      </article>
    ));

  if (!license)
    return (
      <div className="portal-session-loading">
        <img src="/loto-real-logo-transparent.png" alt="Loto Real" />
        <LoaderCircle className="spin" />
        <span>Validando licencia segura...</span>
      </div>
    );
  if (!license.allowed)
    return (
      <main className={`license-screen license-${license.status.toLowerCase()}`}>
        <section>
          <img src="/loto-real-logo-transparent.png" alt="Loto Real" />
          <span className="license-kicker">Control de servicio</span>
          <ShieldCheck aria-hidden="true" />
          <h1>{license.status === "MAINTENANCE" ? "Sistema en mantenimiento" : "Sistema no disponible"}</h1>
          <p>{license.message}</p>
          <small>Código: {license.code}</small>
          <button type="button" onClick={() => window.location.reload()}>Volver a comprobar</button>
        </section>
      </main>
    );
  if (checkingSession)
    return (
      <div className="portal-session-loading">
        <img src="/loto-real-logo-transparent.png" alt="Loto Real" />
        <LoaderCircle className="spin" />
        <span>Validando acceso seguro...</span>
      </div>
    );
  if (!session)
    return (
      <PortalLogin
        onSuccess={(authenticated) => {
          rememberPersistentSession(authenticated);
          setCheckingSession(false);
          setSession(authenticated);
          setView(initialViewForSession(authenticated));
        }}
      />
    );

  if(session.permissions['profile:WAREHOUSE_REQUESTER'])return <Suspense fallback={<Loading/>}><WarehouseRequesterWorkspace session={session} onLogout={()=>void logout()} onSession={setSession}/></Suspense>;
  const isAdministrator = session.role === "Administrator";
  const managerDepartment=session.assignedDepartment||((session.role==='Warehouse'||session.supportTeam==='WAREHOUSE')?'WAREHOUSE':session.supportTeam==='WORKSHOP'?'WORKSHOP':session.supportTeam==='CALL_CENTER'||session.supportTeam==='TECHNICAL_FAILURE'?session.supportTeam:session.supportDepartment||(session.role==='Administrative'?'ADMINISTRATIVE':session.role==='GroupAdministrator'?'SUPERVISION':null));
  const scopedManager=!isAdministrator&&!!session.permissions.canManageDepartment&&!!managerDepartment;
  const departmentalPrincipal=session.role==="Department"&&scopedManager;
  const usesAdministrativeSidebar=isAdministrativeProfile(session)||departmentalPrincipal;
  const managerDepartmentName=userDepartments.find(([code])=>code===managerDepartment)?.[1]||'Departamento';
  const isSupervisor = session.role === "GroupAdministrator";
  const incidentOperator=isIncidentOperatorAccount(session.email,session.displayName);
  const canViewIncidentReports=isAdministrator||isSupervisor||incidentOperator;
  const isWarehouseOperator = session.role === "Warehouse" || session.supportTeam === "WAREHOUSE";
  const isWorkshopOperator = session.supportTeam === "WORKSHOP";
  const isMaintenanceOperator = isWarehouseOperator || isWorkshopOperator;
  const isDepartmentManager = Boolean(session.supportDepartment && !session.supportTeam && session.permissions.canAssignTickets);
  const technologyQueueTeam=session.role==="Technology"&&(session.supportTeam==="CALL_CENTER"||session.supportTeam==="TECHNICAL_FAILURE"||session.supportTeam==="TECHNICIANS")?session.supportTeam:null;
  const isPersonalTechnician = Boolean(
    !isAdministrator &&
    !usesAdministrativeSidebar &&
    !session.permissions.canManageDepartment &&
    session.supportDepartment &&
    !isMaintenanceOperator &&
    (session.supportTeam === "TECHNICIANS" || !session.permissions.canAssignTickets),
  );
  const canOpenLiveQueue=Boolean(!isPersonalTechnician&&(isAdministrator||session.supportDepartment)&&session.permissions.canViewSupportDashboard);
  const canOpenTicketWorkspace =
    session.permissions.canViewSupportDashboard || isSupervisor || isWarehouseOperator;
  const canOpenWarehouse = isAdministrator || isWarehouseOperator || (isDepartmentManager && session.role !== "Department");
  const canOpenWarehouseQueue = isAdministrator || isWarehouseOperator;
  const canOpenWorkshop = canManageWorkshop(session);
  const viewLabel: Record<PortalView, string> = {
    home: "Inicio",
    locations: "Ubicaciones de agencias",
    storage: "Almacenamiento",
    audit: "Auditoría de campo",
    dashboard: "Mi panel",
    users: "Usuarios y permisos",
    executiveReports: "Reportes ejecutivos",
    managerSupport: "Soporte del departamento",
    managerFiles: scopedManager?"Archivos y comunicaciones":({files:"Archivos y comunicaciones",forms:"Formularios administrativos",warehouse:"Solicitudes a Almacén",workshop:"Solicitudes a Taller"}[departmentDocumentSection]),
    managerQueue: "Cola en vivo",
    managerAudit: "Auditoría operativa",
    tickets: isWarehouseOperator ? "Soporte de Almacén" : isPersonalTechnician ? "Soporte" : selectedSupportDepartment ? departmentLabel(selectedSupportDepartment) : "Departamentos",
    createTicket: "Soporte",
    warehouse: "Departamento de Almacén",
    warehouseQueue: "Control diario de Almacén",
    workshop: "Departamento Taller",
    reception: "Recepción virtual",
    systemAudit: "Auditoría operativa",
    queue: "Cola en vivo",
    incidentReports: "Informe de incidentes · Juegos tradicionales",
  };
  const navigateTo = (target: PortalView) => {
    if(target === "storage" && !isAdministrator) return;
    // Entrar desde el menú principal es navegación normal, no una solicitud
    // para abrir el último ticket visto desde una notificación u operación.
    if (target === "tickets") {
      setSupportNavigation(null);
      setSupportEntryRequest((current) => current + 1);
    }
    setView(target);
    setSidebarOpen(false);
  };
  const openDepartments = () => {
    setSelectedSupportDepartment(null);
    setDepartmentEntry(null);
    navigateTo("tickets");
  };
  const openDepartmentSection = (section: DepartmentSidebarSection) => {
    if (!selectedSupportDepartment) return;
    if (section === "agencies" && selectedSupportDepartment !== "TECHNOLOGY") return;
    setSupportNavigation(null);
    setDepartmentEntry({
      key: Date.now(),
      department: selectedSupportDepartment,
      section,
    });
    setView("tickets");
    setSidebarOpen(false);
  };
  const toggleSidebar = () => {
    setSidebarCollapsed((current) => {
      const next = !current;
      localStorage.setItem("portal-sidebar-collapsed", next ? "1" : "0");
      return next;
    });
  };

  const homeModules = ([
    canOpenTicketWorkspace && {
      id: "support",
      title: "Soporte",
      description: "Tickets, incidencias y seguimiento.",
      icon: LifeBuoy,
      onSelect: () => scopedManager && !usesAdministrativeSidebar ? navigateTo("managerSupport") : isAdministrator ? openDepartments() : navigateTo("tickets"),
    },
    canViewIncidentReports && {
      id: "forms",
      title: "Formularios administrativos",
      description: "Solicitudes, registros y autorizaciones.",
      icon: FileSignature,
      accent: "violet" as const,
      onSelect: () => navigateTo("incidentReports"),
    },
    canOpenWarehouse && {
      id: "warehouse",
      title: "Almacén",
      description: "Inventario, movimientos y solicitudes.",
      icon: Warehouse,
      onSelect: () => navigateTo("warehouse"),
    },
    canOpenWorkshop && {
      id: "workshop",
      title: "Taller",
      description: "Órdenes de trabajo, servicios y mantenimientos.",
      icon: Settings,
      onSelect: () => navigateTo("workshop"),
    },
    (isAdministrator || session.permissions.canViewDashboard) && {
      id: "reports",
      title: "Reportes",
      description: "Indicadores, gestión operativa y reportes ejecutivos.",
      icon: BarChart3,
      accent: "violet" as const,
      onSelect: () => navigateTo(isAdministrator ? "executiveReports" : "dashboard"),
    },
    isAdministrator && session.permissions.canManageUsers && {
      id: "users",
      title: "Usuarios",
      description: "Gestión de cuentas, accesos y permisos.",
      icon: UserRound,
      onSelect: () => navigateTo("users"),
    },
    (session.permissions.canAudit || session.permissions.canViewFieldAudits) && {
      id: "audit",
      title: "Auditoría de campo",
      description: "Revisiones, evidencias y control territorial.",
      icon: ClipboardCheck,
      onSelect: () => navigateTo("audit"),
    },
    usesAdministrativeSidebar && {
      id: "administration",
      title: "Administración",
      description: "Recepción documental y gestión departamental.",
      icon: Building2,
      onSelect: () => navigateTo("reception"),
    },
    {
      id: "locations",
      title: "Ubicaciones de agencias",
      description: "Directorio territorial, mapas y coordenadas.",
      icon: MapPin,
      onSelect: () => navigateTo("locations"),
    },
    isAdministrator && {
      id: "storage",
      title: "Almacenamiento",
      description: "Documentos, capacidad y administración de archivos.",
      icon: FolderOpen,
      onSelect: () => navigateTo("storage"),
    },
    canOpenWarehouseQueue && {
      id: "warehouse-control",
      title: "Control diario de Almacén",
      description: "Solicitudes pendientes, recepción y despacho.",
      icon: Monitor,
      onSelect: () => navigateTo("warehouseQueue"),
    },
    scopedManager && !usesAdministrativeSidebar && {
      id: "manager-files",
      title: "Archivos y comunicaciones",
      description: "Documentos institucionales del departamento.",
      icon: FolderOpen,
      accent: "violet" as const,
      onSelect: () => navigateTo("managerFiles"),
    },
    !usesAdministrativeSidebar && (isAdministrator || session.permissions.canManageDepartment) && {
      id: "operational-audit",
      title: "Auditoría operativa",
      description: "Hallazgos, trazabilidad y control de procesos.",
      icon: ScrollText,
      onSelect: () => navigateTo(scopedManager ? "managerAudit" : "systemAudit"),
    },
    canOpenLiveQueue && {
      id: "live-queue",
      title: "Cola en vivo",
      description: "Casos activos y atención operativa en tiempo real.",
      icon: Monitor,
      onSelect: () => navigateTo(scopedManager ? "managerQueue" : "queue"),
    },
    isSupervisor && {
      id: "reception",
      title: "Recepción virtual",
      description: "Solicitudes administrativas y recepción documental.",
      icon: FileSignature,
      onSelect: () => navigateTo("reception"),
    },
  ] as Array<PortalHomeModule | false | undefined>).filter((item): item is PortalHomeModule => Boolean(item));

  const searchModules = homeModules.filter((module) =>
    `${module.title} ${module.description}`.toLocaleLowerCase("es").includes(portalSearch.trim().toLocaleLowerCase("es")),
  );
  const runPortalSearch = () => {
    if (!portalSearch.trim()) {
      navigateTo("home");
      return;
    }
    searchModules[0]?.onSelect();
    if (searchModules.length) setPortalSearch("");
  };

  return (
    <div className={`app-shell office-shell sidebar-layout portal-card-navigation${sidebarCollapsed ? " sidebar-collapsed" : ""}${['Administrative','Department'].includes(session.role)||selectedSupportDepartment?' sidebar-only-navigation':''}`}>
      <aside id="portal-sidebar" className={`portal-sidebar${sidebarOpen ? " open" : ""}`} aria-label="Menú principal">
        <div className="sidebar-brand">
          <img src="/loto-real-logo-transparent.png" alt="Loto Real" />
          <span>
            <strong>Grupo Tejeda</strong>
            <small>Oficina virtual</small>
          </span>
          <button type="button" className="sidebar-mobile-close" onClick={() => setSidebarOpen(false)} aria-label="Cerrar menú"><X /></button>
        </div>
        <nav className="sidebar-nav">
          <button type="button" className={view === "home" ? "active" : ""} aria-current={view === "home" ? "page" : undefined} onClick={() => navigateTo("home")} title="Inicio"><Home/><span>Inicio</span></button>
          <button type="button" className={view === 'locations' ? 'active' : ''} aria-current={view === 'locations' ? 'page' : undefined} onClick={()=>navigateTo('locations')}><MapPin/><span>Ubicaciones de agencias</span></button>
          {scopedManager&&!usesAdministrativeSidebar&&<><span className="sidebar-section-label">Gerencia · {managerDepartmentName}</span>{([['managerSupport','Soporte',LifeBuoy],['managerFiles','Archivos y comunicaciones',FolderOpen],['managerQueue','Cola en vivo',Monitor],['managerAudit','Auditoría operativa',ScrollText]] as const).map(([target,label,Icon])=><button key={target} className={view===target?'active':''} onClick={()=>navigateTo(target)}><Icon/><span>{label}</span></button>)}</>}
          {isAdministrator && <button type="button" className={view === "storage" ? "active" : ""} aria-current={view === "storage" ? "page" : undefined} onClick={() => navigateTo("storage")}><FolderOpen/><span>Almacenamiento</span></button>}
          {isAdministrator&&session.permissions.canManageUsers&&<button type="button" className={view==='users'?'active':''} aria-current={view==='users'?'page':undefined} onClick={()=>navigateTo('users')}><ShieldCheck/><span>Usuarios y permisos</span></button>}
          {isAdministrator&&<button type="button" className={view==='executiveReports'?'active':''} aria-current={view==='executiveReports'?'page':undefined} onClick={()=>navigateTo('executiveReports')}><BarChart3/><span>Reportes ejecutivos</span></button>}
          <span className="sidebar-section-label">Principal</span>
          {!departmentalPrincipal&&(session.permissions.canAudit || session.permissions.canViewFieldAudits) && (
            <button type="button" className={view === "audit" ? "active" : ""} aria-current={view === "audit" ? "page" : undefined} onClick={() => navigateTo("audit")} title="Auditoría de campo">
              <ClipboardCheck /><span>Auditoría de campo</span>
            </button>
          )}
          {!departmentalPrincipal&&session.permissions.canViewDashboard && (
            <button type="button" className={view === "dashboard" ? "active" : ""} aria-current={view === "dashboard" ? "page" : undefined} onClick={() => navigateTo("dashboard")} title="Mi panel">
              <BarChart3 /><span>Mi panel</span>
            </button>
          )}
          {(isAdministrator || isSupervisor || isMaintenanceOperator || isDepartmentManager || technologyQueueTeam) && <span className="sidebar-section-label">Organización</span>}
          {canOpenWarehouseQueue && <>
            <button type="button" className={view === "warehouse" ? "active" : ""} aria-current={view === "warehouse" ? "page" : undefined} onClick={() => navigateTo("warehouse")} title="Almacén central">
              <Warehouse/><span>Almacén central</span>
            </button>
            <button type="button" className={view === "warehouseQueue" ? "active" : ""} aria-current={view === "warehouseQueue" ? "page" : undefined} onClick={() => navigateTo("warehouseQueue")} title="Control diario de solicitudes de Almacén">
              <Monitor/><span>Control diario de Almacén</span>
              <i className="sidebar-live-dot" aria-label="Actualización automática activa" />
            </button>
          </>}
          {!isPersonalTechnician && !isSupervisor && !isMaintenanceOperator && session.role !== "Department" && session.role !== "Administrative" && session.permissions.canViewSupportDashboard && (
            <button type="button" className={view === "tickets" && !selectedSupportDepartment ? "active" : ""} aria-current={view === "tickets" && !selectedSupportDepartment ? "page" : undefined} onClick={openDepartments} title="Departamentos">
              <Building2 /><span>Departamentos</span>
            </button>
          )}
          {canOpenWorkshop&&<button type="button" className={view==="workshop"?"active":""} onClick={()=>navigateTo("workshop")} title="Taller"><Warehouse/><span>Taller</span></button>}
          {isWarehouseOperator&&<><button type="button" className={view==="createTicket"?"active":""} onClick={()=>navigateTo("createTicket")} title="Crear ticket"><LifeBuoy/><span>Crear ticket</span></button><button type="button" className={view==="tickets"?"active":""} onClick={()=>navigateTo("tickets")} title="Soporte de Almacén"><LifeBuoy/><span>Soporte de Almacén</span></button></>}
          {isPersonalTechnician && session.permissions.canViewSupportDashboard && (
            <button type="button" className={view === "tickets" ? "active" : ""} aria-current={view === "tickets" ? "page" : undefined} onClick={() => navigateTo("tickets")} title="Soporte">
              <LifeBuoy /><span>Soporte</span>
              {unreadTicketNotifications > 0 && <em>{unreadTicketNotifications > 99 ? "99+" : unreadTicketNotifications}</em>}
            </button>
          )}
          {session.role === "Department" && !usesAdministrativeSidebar && !isPersonalTechnician && <>
            <button type="button" className={view === "createTicket" ? "active" : ""} aria-current={view === "createTicket" ? "page" : undefined} onClick={() => navigateTo("createTicket")}><LifeBuoy/><span>Soporte</span></button>
            {session.permissions.canViewSupportDashboard && <button className={view === "tickets" ? "active" : ""} onClick={() => {setDepartmentEntry(null);navigateTo("tickets");}}><LifeBuoy/><span>Mis tickets</span></button>}
            <DepartmentDocumentNavigation section={view==="managerFiles"?departmentDocumentSection:null} onSelect={section=>{setDepartmentDocumentSection(section);navigateTo("managerFiles")}} canWorkshop={managerDepartment!=="ADMINISTRATIVE"&&Boolean(session.permissions.canViewWorkshop&&session.permissions.canCreateMaintenanceMovements)}/>
          </>}
          {usesAdministrativeSidebar && <AdministrativeNavigation section={view==="reception"?administrativeSection:null} canSupport={session.permissions.canCreateTickets} canAudit={Boolean(session.permissions.canManageDepartment)||session.email.toLowerCase().replace("@grupotejeda.local","")==="emma.mendoza"} canInventory={Boolean(session.permissions.canManageDepartment&&session.permissions.canManageAdministrativeAssets)} onSelect={section=>{setAdministrativeSection(section);navigateTo("reception")}}/>}
          {isAdministrator&&<button type="button" className={view==="incidentReports"?"active":""} aria-current={view==="incidentReports"?"page":undefined} onClick={()=>navigateTo("incidentReports")} title="Todos los formularios administrativos"><FileSignature/><span>Formularios administrativos</span></button>}
          {!isAdministrator&&!isSupervisor&&incidentOperator&&<button type="button" className={view==="incidentReports"?"active":""} aria-current={view==="incidentReports"?"page":undefined} onClick={()=>navigateTo("incidentReports")} title="Todos los informes de incidentes creados por supervisores"><FileText/><span>Informes de incidentes</span></button>}
          {isSupervisor && (
            <>
              <button type="button" className={view === "tickets" ? "active" : ""} aria-current={view === "tickets" ? "page" : undefined} onClick={() => { navigateTo("tickets"); setSupervisorTicketInboxRequest((current) => current + 1); }} title="Mis tickets">
                <LifeBuoy /><span>Mis tickets</span>
                {unreadTicketNotifications > 0 && <em>{unreadTicketNotifications > 99 ? "99+" : unreadTicketNotifications}</em>}
              </button>
              <button type="button" className={view === "reception" ? "active" : ""} aria-current={view === "reception" ? "page" : undefined} onClick={() => navigateTo("reception")} title="Recepción virtual">
                <FileSignature /><span>Recepción virtual</span>
              </button>
              <button type="button" className={view === "incidentReports" ? "active" : ""} aria-current={view === "incidentReports" ? "page" : undefined} onClick={() => navigateTo("incidentReports")} title="Informes de incidentes">
                <FileText /><span>Informes de incidentes</span>
              </button>
            </>
          )}
          {selectedSupportDepartment && !usesAdministrativeSidebar && !isPersonalTechnician && !isSupervisor && (
            <>
              <span className="sidebar-section-label">{departmentLabel(selectedSupportDepartment)}</span>
              <button type="button" className={view === "tickets" && (!departmentEntry || departmentEntry.section === "support") ? "active" : ""} onClick={() => openDepartmentSection("support")} title="Soporte del departamento">
                <LifeBuoy /><span>Soporte</span>
                {unreadTicketNotifications > 0 && <em>{unreadTicketNotifications > 99 ? "99+" : unreadTicketNotifications}</em>}
              </button>
              <DepartmentDocumentNavigation section={view==="tickets"&&departmentEntry&&["files","forms","warehouse","workshop"].includes(departmentEntry.section)?departmentEntry.section as DepartmentDocumentSection:null} onSelect={openDepartmentSection} canWorkshop={selectedSupportDepartment!=="ADMINISTRATIVE"&&Boolean(session.permissions.canViewWorkshop&&session.permissions.canCreateMaintenanceMovements)}/>
              {(session.supportTeam!=="CALL_CENTER"||session.permissions.canViewAgencyTransitions)&&<button type="button" className={view === "tickets" && departmentEntry?.section === "transitions" ? "active" : ""} onClick={() => openDepartmentSection("transitions")} title="Agencias en construcción">
                <Building2 /><span>Agencias en construcción</span>
              </button>}
              {selectedSupportDepartment === "TECHNOLOGY" && session.permissions.canManageAgencies && <button type="button" className={view === "tickets" && departmentEntry?.section === "agencies" ? "active" : ""} onClick={() => openDepartmentSection("agencies")} title="Actualizar agencias">
                <MapPin /><span>Actualizar agencias</span>
              </button>}
            </>
          )}
          {!usesAdministrativeSidebar && (isAdministrator && session.permissions.canManageAudits || session.permissions.canManageDepartment) && (
            <button type="button" className={view === "systemAudit" ? "active" : ""} aria-current={view === "systemAudit" ? "page" : undefined} onClick={() => navigateTo("systemAudit")} title="Auditoría operativa">
              <ScrollText /><span>Auditoría operativa</span>
            </button>
          )}
          {canOpenLiveQueue && (isAdministrator || selectedSupportDepartment || session.supportDepartment) && (
            <button type="button" className={view === "queue" || view === "managerQueue" ? "active" : ""} aria-current={view === "queue" || view === "managerQueue" ? "page" : undefined} onClick={() => navigateTo(usesAdministrativeSidebar && scopedManager ? "managerQueue" : "queue")} title="Cola en vivo">
              <Monitor /><span>Cola en vivo</span>
              <i className="sidebar-live-dot" aria-label="Actualización automática activa" />
            </button>
          )}
        </nav>
      </aside>
      {sidebarOpen && <button type="button" className="sidebar-overlay" onClick={() => setSidebarOpen(false)} aria-label="Cerrar menú lateral" />}
      <div className="portal-main-column">
      <header className="site-header">
        <div className="site-header-inner">
          <button type="button" className="sidebar-mobile-trigger" onClick={() => setSidebarOpen(true)} aria-label="Abrir menú principal" aria-expanded={sidebarOpen} aria-controls="portal-sidebar"><Menu /></button>
          <button type="button" className="sidebar-desktop-collapse" onClick={toggleSidebar} title={sidebarCollapsed ? "Expandir sidebar" : "Contraer sidebar"} aria-label={sidebarCollapsed ? "Expandir sidebar" : "Contraer sidebar"}>
            {sidebarCollapsed ? <ChevronRight /> : <ChevronLeft />}
          </button>
          <div className="topbar-view-context">
            <button type="button" className="portal-header-home" onClick={() => navigateTo("home")} aria-label="Ir al inicio">
              <img src="/loto-real-logo-transparent.png" alt="" />
            </button>
            <span>
              <small>Oficina virtual · navegación</small>
              <strong>{view==="reception"&&usesAdministrativeSidebar?({support:"Soporte",files:"Archivos y comunicaciones",forms:"Formularios administrativos",warehouse:"Solicitudes a Almacén",audit:"Auditoría operativa",inventory:"Inventario administrativo"}[administrativeSection]):viewLabel[view]}</strong>
            </span>
          </div>
          <label className="portal-global-search">
            <Search />
            <input
              value={portalSearch}
              onChange={(event) => setPortalSearch(event.target.value)}
              onKeyDown={(event) => { if (event.key === "Enter") runPortalSearch(); }}
              placeholder="Buscar en la Oficina Virtual..."
              aria-label="Buscar un módulo"
            />
            {portalSearch && <button type="button" className="portal-search-clear" onClick={() => setPortalSearch("")} aria-label="Limpiar búsqueda"><X /></button>}
          </label>
          <div className="header-account">
            {session.permissions.canViewTicketNotifications && (
              <button
                className="notification-bell"
                onClick={() => openNotificationCenter("TICKET")}
                title="Centro de notificaciones"
                aria-label={`Centro de notificaciones${unreadTicketNotifications ? `, ${unreadTicketNotifications} nuevas` : ""}`}
              >
                <Bell />
                {unreadTicketNotifications > 0 && <em>{unreadTicketNotifications > 99 ? "99+" : unreadTicketNotifications}</em>}
              </button>
            )}
            {session.permissions.canUseSupportChat && (
              <button className="notification-bell message-bell" onClick={() => openNotificationCenter("CHAT")} title="Centro de mensajes" aria-label={`Centro de mensajes${unreadChatNotifications ? `, ${unreadChatNotifications} nuevos` : ""}`}>
                <MessageCircle />
                {unreadChatNotifications > 0 && <em>{unreadChatNotifications > 99 ? "99+" : unreadChatNotifications}</em>}
              </button>
            )}
            <div className="header-user-menu">
              <button className="header-profile" onClick={() => setHeaderAccountOpen((open) => !open)} aria-expanded={headerAccountOpen} aria-haspopup="menu" aria-label="Abrir menú de cuenta">
                {session.avatarUrl ? <img src={session.avatarUrl} alt={session.displayName} /> : <UserRound />}
                <ChevronDown />
              </button>
              {headerAccountOpen && <>
                <button type="button" className="header-menu-dismiss" onClick={() => setHeaderAccountOpen(false)} aria-label="Cerrar menú de cuenta" />
                <div className="header-user-popover" role="menu">
                  <header>
                    {session.avatarUrl ? <img src={session.avatarUrl} alt="" /> : <UserRound />}
                    <span><small>{sessionRoleLabel(session)}</small><strong>{session.displayName}</strong></span>
                  </header>
                  {isAdministrator && <Suspense fallback={null}><AdminHeaderVoiceAssistant onWarehouseCommand={text=>setAdminVoiceWarehouse(text)}/></Suspense>}
                  <button type="button" onClick={() => { setHeaderAccountOpen(false); setProfileOpen(true); }}><Settings /><span><strong>Mi perfil</strong><small>Cuenta, foto y contraseña</small></span></button>
                  {session.permissions.canViewTicketNotifications && <button type="button" onClick={() => { setHeaderAccountOpen(false); void sendPushTest(); }}><BellRing /><span><strong>Probar notificaciones</strong><small>Verificar avisos del navegador</small></span></button>}
                  <button type="button" className="header-user-signout" onClick={() => void logout()}><LogOut /><span><strong>Cerrar sesión</strong><small>Salir de la Oficina Virtual</small></span></button>
                </div>
              </>}
            </div>
          </div>
          {!['Administrative','Department'].includes(session.role)&&!selectedSupportDepartment&&(          <nav className={`main-nav mobile-main-nav ${isSupervisor||session.role==='Administrative'?'requester-mobile-nav':''}`} aria-label="Secciones principales en móvil">
            <button type="button" className={view === 'locations' ? 'active' : ''} aria-current={view === 'locations' ? 'page' : undefined} onClick={()=>navigateTo('locations')}><MapPin/><span>Ubicaciones</span></button>
            {isAdministrator && <button type="button" className={view === "storage" ? "active" : ""} aria-current={view === "storage" ? "page" : undefined} onClick={() => navigateTo("storage")}><FolderOpen/><span>Almacenamiento</span></button>}
          {isAdministrator&&session.permissions.canManageUsers&&<button className={view==='users'?'active':''} onClick={()=>navigateTo('users')}><ShieldCheck/><span>Usuarios y permisos</span></button>}
            {isAdministrator&&<button className={view==='executiveReports'?'active':''} onClick={()=>navigateTo('executiveReports')}><BarChart3/><span>Reportes</span></button>}
            {scopedManager&&!usesAdministrativeSidebar&&<>{([['managerSupport','Soporte',LifeBuoy],['managerFiles','Archivos y comunicaciones',FolderOpen],['managerQueue','Cola en vivo',Monitor],['managerAudit','Auditoría operativa',ScrollText]] as const).map(([target,label,Icon])=><button key={target} className={view===target?'active':''} onClick={()=>navigateTo(target)}><Icon/><span>{label}</span></button>)}</>}
            {!departmentalPrincipal&&(session.permissions.canAudit || session.permissions.canViewFieldAudits) && (
              <button type="button" className={view === "audit" ? "active" : ""} aria-current={view === "audit" ? "page" : undefined} onClick={() => navigateTo("audit")}>
                <ClipboardCheck /><span>Auditoría</span>
              </button>
            )}
            {!departmentalPrincipal&&session.permissions.canViewDashboard && (
              <button type="button" className={view === "dashboard" ? "active" : ""} aria-current={view === "dashboard" ? "page" : undefined} onClick={() => navigateTo("dashboard")}>
                <BarChart3 /><span>Mi panel</span>
              </button>
            )}
            {canOpenWarehouse&&<button type="button" className={view==='warehouse'?'active':''} aria-current={view==='warehouse'?'page':undefined} onClick={()=>navigateTo('warehouse')}><Warehouse/><span>Almacén central</span></button>}
            {canOpenWarehouseQueue&&<button type="button" className={view==='warehouseQueue'?'active':''} aria-current={view==='warehouseQueue'?'page':undefined} onClick={()=>navigateTo('warehouseQueue')}><Monitor/><span>Control diario de Almacén</span></button>}
            {canOpenWorkshop&&<button type="button" className={view==='workshop'?'active':''} aria-current={view==='workshop'?'page':undefined} onClick={()=>navigateTo('workshop')}><Warehouse/><span>Taller</span></button>}
            {canOpenTicketWorkspace && (isPersonalTechnician||isSupervisor||!isMaintenanceOperator&&session.role!=="Department") && (
              <button type="button" className={view === "tickets" ? "active" : ""} aria-current={view === "tickets" ? "page" : undefined} onClick={() => { if (isPersonalTechnician || isSupervisor) navigateTo("tickets"); else openDepartments(); if (session.role === "GroupAdministrator") setSupervisorTicketInboxRequest((current) => current + 1); }}>
                {isPersonalTechnician || isSupervisor ? <LifeBuoy /> : <Building2 />}<span>{isSupervisor ? "Mis tickets" : isPersonalTechnician ? "Soporte" : "Departamentos"}</span>
                {unreadTicketNotifications > 0 && <em className="ticket-alert-badge">{unreadTicketNotifications > 99 ? "99+" : unreadTicketNotifications}</em>}
              </button>
            )}
            {isWarehouseOperator&&<><button type="button" className={view==='createTicket'?'active':''} aria-current={view==='createTicket'?'page':undefined} onClick={()=>navigateTo('createTicket')}><LifeBuoy/><span>Crear ticket</span></button><button type="button" className={view==='tickets'?'active':''} aria-current={view==='tickets'?'page':undefined} onClick={()=>navigateTo('tickets')}><LifeBuoy/><span>Soporte de Almacén</span></button></>}
            {session.role==='Department'&&!usesAdministrativeSidebar&&!isPersonalTechnician&&<><button type="button" className={view==='createTicket'?'active':''} aria-current={view==='createTicket'?'page':undefined} onClick={()=>navigateTo('createTicket')}><LifeBuoy/><span>Crear ticket</span></button>{session.permissions.canViewSupportDashboard&&<button type="button" className={view==='tickets'?'active':''} aria-current={view==='tickets'?'page':undefined} onClick={()=>{setDepartmentEntry(null);navigateTo('tickets')}}><LifeBuoy/><span>Mis tickets</span></button>}<DepartmentDocumentNavigation section={view==="managerFiles"?departmentDocumentSection:null} onSelect={section=>{setDepartmentDocumentSection(section);navigateTo("managerFiles")}} canWorkshop={managerDepartment!=="ADMINISTRATIVE"&&Boolean(session.permissions.canViewWorkshop&&session.permissions.canCreateMaintenanceMovements)}/></>}
            {selectedSupportDepartment&&!usesAdministrativeSidebar&&!isPersonalTechnician&&!isSupervisor&&<>
              <button type="button" className={view==='tickets'&&(!departmentEntry||departmentEntry.section==='support')?'active':''} aria-current={view==='tickets'&&(!departmentEntry||departmentEntry.section==='support')?'page':undefined} onClick={()=>openDepartmentSection('support')}><LifeBuoy/><span>Soporte</span></button>
              <DepartmentDocumentNavigation section={view==="tickets"&&departmentEntry&&["files","forms","warehouse","workshop"].includes(departmentEntry.section)?departmentEntry.section as DepartmentDocumentSection:null} onSelect={openDepartmentSection} canWorkshop={selectedSupportDepartment!=="ADMINISTRATIVE"&&Boolean(session.permissions.canViewWorkshop&&session.permissions.canCreateMaintenanceMovements)}/>
              <button type="button" className={view==='tickets'&&departmentEntry?.section==='transitions'?'active':''} aria-current={view==='tickets'&&departmentEntry?.section==='transitions'?'page':undefined} onClick={()=>openDepartmentSection('transitions')}><Building2/><span>Agencias en construcción</span></button>
              {selectedSupportDepartment==='TECHNOLOGY'&&session.permissions.canManageAgencies&&<button type="button" className={view==='tickets'&&departmentEntry?.section==='agencies'?'active':''} aria-current={view==='tickets'&&departmentEntry?.section==='agencies'?'page':undefined} onClick={()=>openDepartmentSection('agencies')}><MapPin/><span>Actualizar agencias</span></button>}
            </>}
            {usesAdministrativeSidebar && <AdministrativeNavigation section={view==="reception"?administrativeSection:null} canSupport={session.permissions.canCreateTickets} canAudit={Boolean(session.permissions.canManageDepartment)||session.email.toLowerCase().replace("@grupotejeda.local","")==="emma.mendoza"} canInventory={Boolean(session.permissions.canManageDepartment&&session.permissions.canManageAdministrativeAssets)} onSelect={section=>{setAdministrativeSection(section);navigateTo("reception")}}/>}
            {isAdministrator&&<button type="button" className={view==="incidentReports"?"active":""} aria-current={view==="incidentReports"?"page":undefined} onClick={()=>navigateTo("incidentReports")}><FileSignature/><span>Formularios</span></button>}
            {!isAdministrator&&!isSupervisor&&incidentOperator&&<button type="button" className={view==="incidentReports"?"active":""} aria-current={view==="incidentReports"?"page":undefined} onClick={()=>navigateTo("incidentReports")} title="Todos los informes de incidentes creados por supervisores"><FileText/><span>Incidentes</span></button>}
            {isSupervisor && (
              <>
                <button type="button" className={view === "reception" ? "active" : ""} aria-current={view === "reception" ? "page" : undefined} onClick={() => navigateTo("reception")}>
                  <FileSignature /><span>Recepción</span>
                </button>
                <button type="button" className={view === "incidentReports" ? "active" : ""} aria-current={view === "incidentReports" ? "page" : undefined} onClick={() => navigateTo("incidentReports")}>
                  <FileText /><span>Incidentes</span>
                </button>
              </>
            )}
            {(isAdministrator && session.permissions.canManageAudits || session.permissions.canManageDepartment) && (
              <button type="button" className={view === "systemAudit" ? "active" : ""} aria-current={view === "systemAudit" ? "page" : undefined} onClick={() => navigateTo("systemAudit")}>
                <ScrollText /><span>Operación</span>
              </button>
            )}
            {canOpenLiveQueue && (isAdministrator || selectedSupportDepartment || session.supportDepartment) && (
              <button type="button" className={view === "queue" ? "active" : ""} aria-current={view === "queue" ? "page" : undefined} onClick={() => navigateTo("queue")}>
                <Monitor /><span>Cola en vivo</span>
              </button>
            )}
          </nav>)}
        </div>
      </header>
      <main
        className={`workspace${reportAgencyOpen ? " incident-report-workspace" : ""}`}
      >
        {session.mustChangePassword ? (
          <section className="portal-session-loading" role="status">
            <KeyRound />
            <strong>Completa el cambio de contraseña</strong>
            <span>Guarda una contraseña personal en la ventana abierta para activar todas las funciones y notificaciones.</span>
          </section>
        ) : view === "home" ? (
          <PortalHome
            displayName={session.displayName}
            modules={homeModules}
            unreadTickets={unreadTicketNotifications}
            unreadMessages={unreadChatNotifications}
          />
        ) : view === "incidentReports" && canViewIncidentReports ? (
          <Suspense fallback={<Loading/>}><AdministrativeRequests session={session} department={isSupervisor?'SUPERVISION':isAdministrator?undefined:'CALL_CENTER'} incidentOnly={!isAdministrator}/></Suspense>
        ) : view === "locations" ? (
          <Suspense fallback={<Loading/>}><AgencyLocations key={session.id}/></Suspense>
        ) : view === "storage" && isAdministrator ? (
          <Suspense fallback={<Loading/>}><StorageMigrationPanel/></Suspense>
        ) : view === "executiveReports" && isAdministrator ? (
          <Suspense fallback={<Loading/>}><ExecutiveReports/></Suspense>
        ) : view === "workshop" && canOpenWorkshop ? (
          <Suspense fallback={<Loading/>}><WorkshopWorkspace session={session}/></Suspense>
        ) : view === "tickets" && isWarehouseOperator ? (
          <Suspense fallback={<Loading/>}><WarehouseSupport session={session}/></Suspense>
        ) : view === "createTicket" && (session.role === "Department" || isWarehouseOperator) ? (
          <Suspense fallback={<Loading/>}><WarehouseSupport session={session} createOnly/></Suspense>
        ) : view === "warehouseQueue" && canOpenWarehouseQueue ? (
          <Suspense fallback={<Loading/>}><WarehouseLiveQueuePage session={session}/></Suspense>
        ) : ((view === "tickets" || session.role === "Department" && view === "managerSupport") && canOpenTicketWorkspace) ||
        (view === "warehouse" && canOpenWarehouse) ||
        (view === "workshop" && canOpenWorkshop) ? (
          <Suspense fallback={<Loading />}>
            <TicketCenter
              key={`${view}:${supportNavigation?.requestKey ?? "normal"}:${supportEntryRequest}`}
              session={session}
              onSessionExpired={() => expireIfUnauthorized({status:401})}
              navigationTarget={supportNavigation}
              supervisorInboxRequestKey={supervisorTicketInboxRequest}
              maintenanceEntry={view === "warehouse" ? "WAREHOUSE" : view === "workshop" ? "WORKSHOP" : null}
              departmentEntry={view === "tickets" ? departmentEntry : null}
              onDepartmentChange={(department) => {
                setSelectedSupportDepartment(department);
                if (!department) setDepartmentEntry(null);
              }}
              onOpenMaintenanceDepartment={canOpenWarehouse && canOpenWorkshop ? (area) => {
                setSelectedSupportDepartment(null);
                setDepartmentEntry(null);
                navigateTo(area === "WAREHOUSE" ? "warehouse" : "workshop");
              } : undefined}
            />
          </Suspense>
        ) : scopedManager&&['managerSupport','managerFiles','managerQueue','managerAudit'].includes(view) ? (
          <Suspense fallback={<Loading/>}>{view==='managerSupport'?<ManagerDepartmentSupport name={managerDepartmentName}/>:view==='managerFiles'?<DepartmentWorkspace session={session} departmentCode={managerDepartment!} section={scopedManager?"files":departmentDocumentSection}/>:<OperationsCenter mode={view==='managerQueue'?'queue':'audit'} scopedManager departmentScope={managerDepartment!} departmentAuditScope={view==='managerAudit'?managerDepartment:null} onSessionExpired={()=>expireIfUnauthorized({status:401})} onOpenTicket={()=>navigateTo('managerSupport')}/>}</Suspense>
        ) : usesAdministrativeSidebar && view==='reception' ? (
          <Suspense fallback={<Loading/>}><AdministrativePortal session={session} departmentCode={managerDepartment!} section={administrativeSection} onSessionExpired={()=>expireIfUnauthorized({status:401})}/></Suspense>
        ) : session.role==='Department' && !['queue','systemAudit'].includes(view) ? (
          <Suspense fallback={<Loading/>}>{scopedManager?<ManagerDepartmentSupport name={managerDepartmentName}/>:<DepartmentWorkspace session={session} departmentCode={managerDepartment!} section={scopedManager?"files":departmentDocumentSection}/>}</Suspense>
        ) : (view === "reception" || session.role === "Administrative") && (isSupervisor || session.role === "Administrative") ? (
          <Suspense fallback={<Loading />}>
            <AdministrativePortal session={session} onSessionExpired={()=>expireIfUnauthorized({status:401})} section={session.role==="Administrative"?administrativeSection:undefined} onSectionChange={session.role==="Administrative"?setAdministrativeSection:undefined} />
          </Suspense>
        ) : ((view === "systemAudit" && (isAdministrator || session.permissions.canManageDepartment)) || (view === "queue" && canOpenLiveQueue)) ? (
          <Suspense fallback={<Loading />}>
            <OperationsCenter
              mode={view === "queue" ? "queue" : "audit"}
              globalManager={isAdministrator}
              directoryScope={isAdministrator?selectedSupportDepartment:null}
              teamScope={!isAdministrator&&(session.supportTeam==='CALL_CENTER'||session.supportTeam==='TECHNICAL_FAILURE')?session.supportTeam:null}
              departmentScope={!isAdministrator ? (isWarehouseOperator?'WAREHOUSE':session.supportDepartment || selectedSupportDepartment || null) : null}
              departmentAuditScope={view==='systemAudit'?(isAdministrator?'ALL':isWarehouseOperator?'WAREHOUSE':session.supportTeam==='WORKSHOP'?'WORKSHOP':session.supportTeam==='CALL_CENTER'||session.supportTeam==='TECHNICAL_FAILURE'?session.supportTeam:session.supportDepartment||null):null}
              onSessionExpired={() => expireIfUnauthorized({status:401})}
              onOpenTicket={openTicketFromOperations}
            />
          </Suspense>
        ) : view === "users" && isAdministrator && session.permissions.canManageUsers ? (
          <Suspense fallback={<Loading/>}><UsersPermissionsWorkspace session={session} onClose={()=>navigateTo('dashboard')}/></Suspense>
        ) : view === "audit" && !session.permissions.canAudit && session.permissions.canViewFieldAudits ? (
          <Suspense fallback={<Loading />}><AdminPanel key="field-audit-reader" session={session} initialSection="audits" onSessionExpired={() => expireIfUnauthorized({status:401})}/></Suspense>
        ) : view === "audit" && session.permissions.canAudit ? (
          <div
            className={`form-wrap audit-wrap${reportAgencyOpen ? " incident-audit-wrap" : ""}`}
          >
            {!reportAgencyOpen && (
              <div className="page-heading atlas-page-heading">
                <div>
                  <span className="section-kicker">
                    Grupo Tejeda ·{" "}
                    {session.scope === "all"
                      ? "Cobertura corporativa"
                      : "Mis grupos asignados"}
                  </span>
                  <h1>Auditoría inteligente de campo</h1>
                  <p>
                    Selecciona una de tus bancas y comienza con fotografías que
                    capturan ubicación, hora y dirección.
                  </p>
                </div>
                <span className="secure-pill">
                  <ShieldCheck /> Acceso personalizado
                </span>
              </div>
            )}
            {error && (
              <div className="alert" role="alert">
                {error}
              </div>
            )}
            {step === "group" && (
              <Panel
                title="Selecciona tu grupo"
                subtitle={
                  session.scope === "assigned-groups"
                    ? "Solo aparecen los grupos asignados a tu cuenta."
                    : "Consulta todos los grupos de la organización."
                }
              >
                <SearchBox
                  value={query}
                  onChange={setQuery}
                  placeholder="Buscar un grupo..."
                />
                {loading ? (
                  <Loading />
                ) : (
                  <div className="choice-grid">
                    {(filtered as Group[]).map((item) => (
                      <button
                        className="choice"
                        key={item.grupo}
                        onClick={() => void chooseGroup(item.grupo)}
                      >
                        <span className="choice-icon">
                          <Building2 />
                        </span>
                        <span>
                          <strong>{item.grupo}</strong>
                          <small>
                            {item.pending}{" "}
                            {item.pending === 1
                              ? "agencia disponible"
                              : "agencias disponibles"}
                          </small>
                        </span>
                        <ChevronRight className="arrow" />
                      </button>
                    ))}
                  </div>
                )}
                {!loading && filtered.length === 0 && (
                  <Empty text="No hay grupos disponibles dentro de tu alcance." />
                )}
              </Panel>
            )}
            {step === "agency" && (
              <Panel
                title="Selecciona la agencia"
                subtitle={
                  <>
                    Agencias disponibles del grupo <strong>{group}</strong>. Las ya auditadas permanecen aquí para próximas visitas.
                  </>
                }
                back={() => {
                  setStep("group");
                  setQuery("");
                }}
              >
                <SearchBox
                  value={query}
                  onChange={setQuery}
                  placeholder="Buscar por nombre o código..."
                />
                {loading ? (
                  <Loading />
                ) : (
                  <div className="choice-grid">
                    {(filtered as Agency[]).map((item) => (
                      <button
                        className="choice"
                        key={item.id}
                        onClick={() => {
                          setReportAgencyOpen(false);
                          setAgency(item);
                          setStep("details");
                        }}
                      >
                        <span className="choice-icon">
                          <MapPin />
                        </span>
                        <span>
                          <strong>{item.terminal}</strong>
                          <small>Código {item.codigo}</small>
                          <small>
                            {item.auditCount
                              ? `${item.auditCount} ${item.auditCount === 1 ? "auditoría completada" : "auditorías completadas"}${item.lastAuditedAt ? ` · Última ${deviceDate(item.lastAuditedAt).toLocaleDateString("es-DO")}` : ""}`
                              : "Sin auditorías realizadas"}
                          </small>
                        </span>
                        <ChevronRight className="arrow" />
                      </button>
                    ))}
                  </div>
                )}
                {!loading && filtered.length === 0 && (
                  <Empty text="No hay agencias disponibles en este grupo." />
                )}
              </Panel>
            )}
            {step === "details" &&
              agency &&
              (reportAgencyOpen ? (
                <Suspense fallback={<Loading />}>
                  <TicketCenter
                    session={session}
                    onSessionExpired={() => expireIfUnauthorized({status:401})}
                    reportAgency={{ ...agency, grupo: group }}
                    onCloseReport={() => setReportAgencyOpen(false)}
                  />
                </Suspense>
              ) : (
                <div className="agency-audit-workspace">
                  {(session.role === "GroupAdministrator" || session.role === "Administrator") &&
                    step === "details" &&
                    agency && (
                    <button
                      type="button"
                      className="report-incidence-button"
                      onClick={() => setReportAgencyOpen(true)}
                    >
                      <LifeBuoy />
                      <span>
                        <strong>Reportar incidencia en mi agencia</strong>
                        <small>
                          {agency.terminal} · {agency.codigo}
                        </small>
                      </span>
                      <ChevronRight />
                    </button>
                  )}
                  <Suspense fallback={<Loading/>}><EmployeeAuditForm
                    agency={agency}
                    group={group}
                    onBack={() => setStep("agency")}
                    onComplete={(data) => {
                      setResult(data);
                      setStep("success");
                    }}
                  /></Suspense>
                </div>
              ))}
            {step === "success" && result && (
              <EvidenceSuccess result={result} reset={reset} />
            )}
          </div>
        ) : (
          <Suspense fallback={<Loading />}>
            <AdminPanel
              session={session}
              onSessionExpired={() => expireIfUnauthorized({status:401})}
            />
          </Suspense>
        )}
        {equipmentCode&&<Suspense fallback={<p>Abriendo seguimiento…</p>}><EquipmentTracking initialCode={equipmentCode!} onClose={()=>{const url=new URL(window.location.href);url.searchParams.delete("equipment");window.history.replaceState(null,"",url);setEquipmentCode(null);}}/></Suspense>}
        <footer>© 2026 Oficina Virtual · Control seguro y confidencial · Web V528</footer>
      </main>
      </div>
      <nav className="portal-mobile-dock" aria-label="Accesos principales">
        <button type="button" className={view === "home" ? "active" : ""} onClick={() => navigateTo("home")}><Home/><span>Inicio</span></button>
        {homeModules.slice(0, 5).map(({ id, title, icon: Icon, onSelect }) => (
          <button type="button" key={id} className={
            id === "forms" && view === "incidentReports" ||
            id === "warehouse" && view === "warehouse" ||
            id === "workshop" && view === "workshop" ||
            id === "reports" && (view === "executiveReports" || view === "dashboard") ||
            id === "users" && view === "users" ||
            id === "support" && view === "tickets" ? "active" : ""
          } onClick={onSelect}><Icon/><span>{title.split(" ")[0]}</span></button>
        ))}
      </nav>
      {pushPromptOpen&&<div className="push-permission-backdrop"><section className="push-permission-card" role="dialog" aria-modal="true" aria-label="Activar notificaciones"><BellRing/><h2>Activa las notificaciones del teléfono</h2><p>Recibe avisos de tickets y asignaciones aunque no tengas la página abierta.</p><button type="button" onClick={()=>void subscribePhonePush().catch(error=>window.alert(error.message))}>Activar notificaciones</button><button type="button" className="secondary" onClick={()=>{setPushPromptOpen(false);localStorage.setItem('push-prompt-dismissed','1');}}>Ahora no</button></section></div>}
      {isAdministrator&&adminVoiceWarehouse&&<Suspense fallback={<Loading/>}><WarehouseRequestComposer session={session} department="ADMINISTRATIVE" agencies={[]} initialVoiceBatch={adminVoiceWarehouse} autoSubmitVoice onClose={()=>setAdminVoiceWarehouse(null)} onSaved={()=>{const total=adminVoiceWarehouse.targets.length;setAdminVoiceWarehouse(null);setView('warehouse');const spoken=new SpeechSynthesisUtterance(total===1?'Listo. La solicitud a Almacén fue creada correctamente.':`Listo. Se crearon ${total} solicitudes a Almacén, una para cada agencia.`);spoken.lang='es-DO';window.speechSynthesis?.speak(spoken);window.dispatchEvent(new Event('support-live-refresh'));}}/></Suspense>}
      {!!notificationToasts.length && (
        <aside className="notification-toast-stack" aria-live="assertive" aria-atomic="false" aria-label="Notificaciones nuevas en pantalla">
          {notificationToasts.map((toast) => (
            <article
              key={toast.key}
              role="status"
              className={`notification-event-${toast.item.kind === "TICKET" ? toast.item.eventType.toLowerCase() : "chat"}`}
            >
              <button
                type="button"
                className="notification-toast-open"
                onClick={() => {
                  setNotificationToasts((current) =>
                    current.filter((item) => item.key !== toast.key),
                  );
                  markNotificationsRead([toast.item]);
                  openNotificationDestination(toast.item);
                }}
              >
                {toast.item.kind === "CHAT" ? (
                  <MessageCircle />
                ) : ["RESOLVED", "CLOSED"].includes(
                    toast.item.eventType,
                  ) ? (
                  <CheckCircle2 />
                ) : (
                  <LifeBuoy />
                )}
                <span>
                  <strong>
                    {toast.item.kind === "CHAT"
                      ? `Nuevo mensaje de ${toast.item.senderName}`
                      : ticketNotificationTitle(toast.item)}
                  </strong>
                  <small>
                    {toast.item.kind === "CHAT"
                      ? toast.item.message
                      : ticketNotificationDetail(toast.item)}
                  </small>
                </span>
              </button>
              <button
                type="button"
                className="notification-toast-close"
                aria-label="Cerrar aviso"
                onClick={() =>
                  setNotificationToasts((current) =>
                    current.filter((item) => item.key !== toast.key),
                  )
                }
              >
                <X />
              </button>
            </article>
          ))}
        </aside>
      )}
      {warehouseNotificationId!==null&&<Suspense fallback={<p>Cargando solicitud…</p>}><WarehouseNotificationDocument id={warehouseNotificationId} session={session} onClose={()=>setWarehouseNotificationId(null)}/></Suspense>}
      {notificationCenterOpen && createPortal((
        <div
          className="notification-center-backdrop"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget)
              setNotificationCenterOpen(false);
          }}
        >
          <section
            className="notification-center"
            role="dialog"
            aria-modal="true"
            aria-label="Centro de notificaciones"
          >
            <header>
              <div>
                <span>
                  {notificationCenterKind === "TICKET" ? <Bell /> : <MessageCircle />} {notificationCenterKind === "TICKET" ? notificationCenterCopy.kicker : "CENTRO DE MENSAJES"}
                </span>
                <h2>{notificationCenterKind === "TICKET" ? notificationCenterCopy.title : "Conversaciones de soporte"}</h2>
                <p>{notificationCenterKind === "TICKET" ? notificationCenterCopy.description.replace(" y mensajes", "").replace(", además de las respuestas del chat", "") : "Mensajes nuevos separados de las notificaciones operativas para mantener cada bandeja organizada."}</p>
              </div>
              <button
                onClick={() => setNotificationCenterOpen(false)}
                aria-label="Cerrar"
              >
                <X />
              </button>
            </header>
            {usesWarehouseAlert&&<div className="notification-summary"><button type="button" onClick={async()=>{try{if(!notificationAudio.current)notificationAudio.current=new AudioContext();await notificationAudio.current.resume();playWarehouseAlert(notificationAudio.current);}catch{window.alert('El navegador no permitió activar el audio. Revisa el permiso de sonido de esta página.');}}}><BellRing/>Activar y probar alerta de Almacén</button><small>Alerta fuerte de tres tonos. Ajusta el volumen del dispositivo.</small></div>}
            <div className="notification-summary">
              {visibleCenterItems.some(item=>!item.isRead) ? (notificationCenterKind === "TICKET" ? <Bell /> : <MessageCircle />) : <CheckCircle2 />}
              <span>
                <strong>{visibleCenterItems.filter(item=>!item.isRead).length ? `${visibleCenterItems.filter(item=>!item.isRead).length} ${notificationCenterKind === "TICKET" ? "nuevas" : "nuevos"}` : "Todo al día"}</strong>
                <small>
                  {visibleCenterItems.length} {notificationCenterKind === "TICKET" ? "notificaciones operativas" : "mensajes reales"} · sincronizados por usuario.
                </small>
              </span>
              <button type="button" className="notification-read-all" onClick={() => markNotificationsRead(visibleCenterItems)} disabled={!visibleCenterItems.some(item=>!item.isRead)}>
                <CheckCheck /> Marcar todas leídas
              </button>
              <button
                type="button"
                onClick={() => dismissNotifications(visibleCenterItems)}
                disabled={!visibleCenterItems.length}
              >
                <Trash2 /> Limpiar bandeja
              </button>
            </div>
            <div className="notification-tables">
              {notificationCenterKind === "TICKET" && <section className="notification-table-section">
                <header>
                  <span>
                    <LifeBuoy /> {receivesWarehouseNotifications?'Tickets y solicitudes de Almacén':'Tickets'}
                  </span>
                  <strong>{ticketNotificationItems.length}</strong>
                </header>
                <div className="resolved-ticket-table">
                  <div className="resolved-ticket-row resolved-ticket-head">
                    <span>Ticket</span>
                    <span>Agencia</span>
                    <span>Departamento</span>
                    <span>Solicitud y respuesta</span>
                    <span>Actualizado / acción</span>
                  </div>
                  {renderNotificationRows(ticketNotificationItems)}
                  {!ticketNotificationItems.length && (
                    <div className="notification-empty compact">
                      <LifeBuoy />
                      <strong>No hay notificaciones de tickets</strong>
                    </div>
                  )}
                </div>
              </section>}
              {notificationCenterKind === "CHAT" && <section className="notification-table-section">
                <header>
                  <span>
                    <MessageCircle /> Chat de soporte
                  </span>
                  <strong>{chatNotificationItems.length}</strong>
                </header>
                <div className="resolved-ticket-table">
                  <div className="resolved-ticket-row resolved-ticket-head">
                    <span>Tipo</span>
                    <span>Conversación</span>
                    <span>Departamento</span>
                    <span>Mensaje recibido</span>
                    <span>Actualizado / acción</span>
                  </div>
                  {renderNotificationRows(chatNotificationItems)}
                  {!chatNotificationItems.length && (
                    <div className="notification-empty compact">
                      <MessageCircle />
                      <strong>No hay mensajes nuevos</strong>
                    </div>
                  )}
                </div>
              </section>}
            </div>
          </section>
        </div>
      ), document.body)}
      {session.role === "GroupAdministrator" &&
        session.permissions.canUseSupportChat && (
          <Suspense fallback={<Loading/>}><SupportChat
            session={session}
            onSessionExpired={() => expireIfUnauthorized({status:401})}
            openConversationId={
              supportNavigation?.kind === "CHAT"
                ? supportNavigation.conversationId
                : null
            }
            openRequestKey={supportNavigation?.requestKey}
          /></Suspense>
        )}
      {(profileOpen || session.mustChangePassword) && (
        <Suspense fallback={<Loading/>}><ProfileSettings
          session={session}
          forced={session.mustChangePassword}
          onClose={() => setProfileOpen(false)}
          onSession={(updated) => {
            if ('persistentSessionToken' in updated) {
              localStorage.removeItem(persistentSessionStorageKey);
              rememberPersistentSession(updated);
            }
            setSession(updated);
            if (session.mustChangePassword && !updated.mustChangePassword) setProfileOpen(false);
          }}
        /></Suspense>
      )}
    </div>
  );
}

function Panel({
  title,
  subtitle,
  children,
  back,
}: {
  title: string;
  subtitle: React.ReactNode;
  children: React.ReactNode;
  back?: () => void;
}) {
  return (
    <section className="panel">
      {back && (
        <button className="back" onClick={back}>
          <ChevronLeft /> Volver
        </button>
      )}
      <header>
        <h2>{title}</h2>
        <p>{subtitle}</p>
      </header>
      {children}
    </section>
  );
}
function SearchBox({
  value,
  onChange,
  placeholder,
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
}) {
  return (
    <label className="search">
      <Search />
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
      />
    </label>
  );
}
function Loading() {
  return (
    <div className="loading">
      <LoaderCircle className="spin" /> Cargando información...
    </div>
  );
}
function Empty({ text }: { text: string }) {
  return (
    <div className="empty">
      <Building2 />
      <p>{text}</p>
    </div>
  );
}
