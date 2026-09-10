import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  BarChart3,
  Bell,
  BellRing,
  Building2,
  CheckCircle2,
  CheckCheck,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
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
  X,
} from "lucide-react";
import EvidenceSuccess from "@/components/vite/EvidenceSuccess";
import EmployeeAuditForm from "@/components/vite/EmployeeAuditForm";
import PortalLogin from "@/components/vite/PortalLogin";
import ProfileSettings from "@/components/vite/ProfileSettings";
import SupportChat from "@/components/vite/SupportChat";
import type { PortalSession } from "@/lib/session";
import { sessionRoleLabel } from "@/lib/session";
import type { OperationsTicket } from "@/lib/operations-center";

const EquipmentTracking = lazy(()=>import("./components/vite/WorkshopBoard").then(module=>({default:module.EquipmentTracking})));
const AdminPanel = lazy(() => import("@/components/vite/AdminPanel"));
const TicketCenter = lazy(() => import("@/components/vite/TicketCenter"));
const OperationsCenter = lazy(() => import("@/components/vite/OperationsCenter"));

type PortalView =
  | "audit"
  | "dashboard"
  | "tickets"
  | "warehouse"
  | "workshop"
  | "systemAudit"
  | "queue";

type Step = "group" | "agency" | "details" | "success";
type Group = { grupo: string; pending: number };
type Agency = {
  id: string;
  codigo: string;
  terminal: string;
  expectedLatitude?: number | null;
  expectedLongitude?: number | null;
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
  ticketType: "SUPPORT" | "INTERNAL";
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
  kind: "TICKET" | "CHAT";
  assignedDepartment: string;
  ticketId?: string;
  ticketNumber?: number;
  ticketType?: "SUPPORT" | "INTERNAL";
  ticketStatus?: string;
  conversationId?: string;
  requestKey: number;
};
type NotificationItem = TicketNotification | ChatNotification;
type NotificationToast = { key: string; item: NotificationItem };
type PendingNotificationReceipt = { key: string; at: string };
type LicenseStatus = {
  enabled: boolean;
  allowed: boolean;
  status: string;
  code: string;
  message: string;
  expiresAt?: string | null;
  usingGracePeriod: boolean;
};

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
      stringValue(item.ticketType ?? item.ticket_type).toUpperCase() === "INTERNAL"
        ? "INTERNAL"
        : "SUPPORT",
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
  `${ticketEventLabel[item.eventType]} · Ticket #${item.ticketNumber}`;

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
    ? `TICKET_EVENT:${item.notificationId}`
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
  if (authenticated.supportTeam === "WAREHOUSE") return "warehouse";
  if (authenticated.supportTeam === "WORKSHOP") return "workshop";
  if (
    authenticated.role === "Administrator" &&
    authenticated.permissions.canViewSupportDashboard
  )
    return "tickets";
  if (
    authenticated.role === "GroupAdministrator" &&
    authenticated.permissions.canAudit
  )
    return "audit";
  if (authenticated.permissions.canAudit) return "audit";
  if (authenticated.permissions.canViewDashboard) return "dashboard";
  if (authenticated.permissions.canViewSupportDashboard) return "tickets";
  return "dashboard";
};

const decodePushApplicationKey=(value:string)=>{const padded=value.replace(/-/g,"+").replace(/_/g,"/")+"===".slice((value.length+3)%4);return Uint8Array.from(atob(padded),character=>character.charCodeAt(0));};
const samePushApplicationKey=(current:ArrayBuffer|null,key:Uint8Array)=>{if(!current)return false;const left=new Uint8Array(current);return left.length===key.length&&left.every((value,index)=>value===key[index]);};
const ensurePushSubscription=async(registration:ServiceWorkerRegistration,publicKey:string)=>{const applicationServerKey=decodePushApplicationKey(publicKey);let subscription=await registration.pushManager.getSubscription();if(subscription&&!samePushApplicationKey(subscription.options.applicationServerKey,applicationServerKey)){await subscription.unsubscribe();subscription=null;}return subscription||registration.pushManager.subscribe({userVisibleOnly:true,applicationServerKey});};
const supportNavigationFromUrl=(href:string):SupportNavigationTarget|null=>{
  const url=new URL(href,window.location.origin);const explicitKind=url.searchParams.get("notification")?.toUpperCase();const kind=explicitKind||(url.searchParams.has("ticket")?"TICKET":url.searchParams.has("conversationId")?"CHAT":undefined);const department=url.searchParams.get("department")||"";
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
const notificationReceiptFromUrl=(href:string):PendingNotificationReceipt|null=>{const url=new URL(href,window.location.origin);const key=url.searchParams.get("notificationKey")||"";const at=url.searchParams.get("notificationAt")||"";return (key.startsWith("TICKET_EVENT:")||key.startsWith("CHAT_MESSAGE:"))&&Number.isFinite(Date.parse(at))?{key,at}:null;};
const clearNotificationDeepLink=()=>{const url=new URL(window.location.href);["notification","ticketId","ticket","department","ticketType","ticketStatus","conversationId","notificationKey","notificationAt"].forEach(key=>url.searchParams.delete(key));window.history.replaceState(null,"",`${url.pathname}${url.search}${url.hash}`);};

export default function App() {
  const [equipmentCode,setEquipmentCode]=useState(()=>new URLSearchParams(window.location.search).get("equipment"));
  const [pendingNotificationTarget,setPendingNotificationTarget]=useState<SupportNavigationTarget|null>(()=>supportNavigationFromUrl(window.location.href));
  const [pendingNotificationReceipt,setPendingNotificationReceipt]=useState<PendingNotificationReceipt|null>(()=>notificationReceiptFromUrl(window.location.href));
  const [license, setLicense] = useState<LicenseStatus | null>(null);
  const [session, setSession] = useState<PortalSession | null>(null);
  const [checkingSession, setCheckingSession] = useState(true);
  const [profileOpen, setProfileOpen] = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(
    () => localStorage.getItem("portal-sidebar-collapsed") === "1",
  );
  const [view, setView] = useState<PortalView>("audit");
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
  const notificationSnapshot = useRef<Set<string> | null>(null);

  const openTicketFromOperations = useCallback((ticket: OperationsTicket) => {
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
        const data = (await response.json()) as LicenseStatus;
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
    fetch("/api/admin/session", { cache: "no-store", credentials: "same-origin" })
      .then(async (response) => {
        if (!response?.ok) return;
        const restored = (await response.json()) as Partial<PortalSession>;
        if (!restored.authenticated || !restored.permissions || !restored.role)
          return;
        const authenticated = restored as PortalSession;
        setSession(authenticated);
        setView(initialViewForSession(authenticated));
      })
      .catch(() => undefined)
      .finally(() => setCheckingSession(false));
  }, []);

  const expireIfUnauthorized = useCallback((loadError: unknown) => {
    if ((loadError as Error & { status?: number }).status === 401)
      setSession(null);
  }, []);

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

  useEffect(()=>{
    if(!sessionId||!pendingNotificationTarget)return;
    setView("tickets");
    setSupportNavigation({...pendingNotificationTarget,requestKey:Date.now()});
    setPendingNotificationTarget(null);
    clearNotificationDeepLink();
  },[pendingNotificationTarget,sessionId]);

  useEffect(() => {
    if (sessionId && !sessionMustChangePassword && canAudit)
      void loadGroups();
  }, [canAudit, loadGroups, sessionId, sessionMustChangePassword]);
  useEffect(() => {
    if (!sessionId || sessionMustChangePassword || !canAudit) return;
    const refreshAgencyCatalog = () => {
      void loadGroups(true);
      if (group) void loadAgenciesForGroup(group, true);
    };
    window.addEventListener("support-live-refresh", refreshAgencyCatalog);
    return () => window.removeEventListener("support-live-refresh", refreshAgencyCatalog);
  }, [canAudit, group, loadAgenciesForGroup, loadGroups, sessionId, sessionMustChangePassword]);
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
    let refreshInFlight = false;
    const refreshNotifications = async () => {
      if (refreshInFlight || !navigator.onLine) return;
      refreshInFlight = true;
      try {
        const [ticketResponse, chatResponse] = await Promise.all([
          fetch("/api/tickets/notifications"),
          canUseSupportChat
            ? fetch("/api/support/chat/notifications")
            : Promise.resolve(null),
        ]);
        if (ticketResponse.status === 401 || chatResponse?.status === 401) {
          setSession(null);
          return;
        }
        if (!ticketResponse.ok) return;
        const ticketBody: unknown = await ticketResponse.json();
        const chatBody = chatResponse?.ok
          ? await chatResponse.json()
          : { notifications: [] };
        const rawTickets = Array.isArray(ticketBody)
          ? ticketBody
          : ticketBody && typeof ticketBody === "object"
            ? ((ticketBody as { notifications?: unknown }).notifications ?? [])
            : [];
        const tickets = (Array.isArray(rawTickets) ? rawTickets : [])
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
            new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime(),
        );
        const incoming = new Set(items.map(notificationIdentity));
        const visibleItems = items;
        const newItems = notificationSnapshot.current
          ? visibleItems.filter(
              (item) =>
                !item.isRead &&
                !notificationSnapshot.current!.has(notificationIdentity(item)),
            )
          : [];
        if (notificationSnapshot.current && newItems.length)
          playNotificationChime();
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
        notificationSnapshot.current = incoming;
        setTicketNotifications(visibleItems);
        setUnreadNotifications(visibleItems.filter((item) => !item.isRead).length);
      } catch {
        return;
      } finally {
        refreshInFlight = false;
      }
    };
    void refreshNotifications();
    window.addEventListener("support-live-refresh", refreshNotifications);
    return () => {
      window.removeEventListener("support-live-refresh", refreshNotifications);
    };
  }, [
    canUseSupportChat,
    canViewTicketNotifications,
    sessionId,
    sessionMustChangePassword,
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
    await fetch("/api/admin/logout", { method: "POST" }).catch(() => undefined);
    setSession(null);
    setGroups([]);
    setAgencies([]);
    setTicketNotifications([]);
    setUnreadNotifications(0);
    setNotificationCenterOpen(false);
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
    const openedAt=Date.parse(item.updatedAt);
    const readThrough=ticketNotifications.filter(candidate=>!candidate.isRead&&Date.parse(candidate.updatedAt)<=openedAt);
    if(readThrough.length){const keys=[...new Set(readThrough.map(notificationIdentity))];void api("/api/notifications/state",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({keys,action:"READ"})}).catch(expireIfUnauthorized);const readKeys=new Set(keys);setTicketNotifications(current=>current.map(candidate=>readKeys.has(notificationIdentity(candidate))?{...candidate,isRead:true}:candidate));setUnreadNotifications(current=>Math.max(0,current-readKeys.size));}
    setNotificationCenterOpen(false);
    setView("tickets");
    setSupportNavigation({
      kind: item.kind,
      assignedDepartment: item.assignedDepartment,
      ticketId: item.kind === "TICKET" ? item.id : undefined,
      ticketNumber: item.kind === "TICKET" ? item.ticketNumber : undefined,
      ticketType: item.kind === "TICKET" ? item.ticketType : undefined,
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
    const cutoff=Date.parse(pendingNotificationReceipt.at);
    const keys=new Set<string>([pendingNotificationReceipt.key]);
    ticketNotifications.forEach(item=>{if(!item.isRead&&Date.parse(item.updatedAt)<=cutoff)keys.add(notificationIdentity(item));});
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
            {item.kind === "TICKET" ? `#${item.ticketNumber}` : "Chat"}
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
          <time>{new Date(item.updatedAt).toLocaleString("es-DO")}</time>
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
          setSession(authenticated);
          setView(initialViewForSession(authenticated));
        }}
      />
    );

  const isAdministrator = session.role === "Administrator";
  const isSupervisor = session.role === "GroupAdministrator";
  const isWarehouseOperator = session.supportTeam === "WAREHOUSE";
  const isWorkshopOperator = session.supportTeam === "WORKSHOP";
  const isMaintenanceOperator = isWarehouseOperator || isWorkshopOperator;
  const isDepartmentManager = Boolean(session.supportDepartment === "TECHNOLOGY" && !session.supportTeam && session.permissions.canAssignTickets);
  const technologyQueueTeam=session.role==="Technology"&&(session.supportTeam==="CALL_CENTER"||session.supportTeam==="TECHNICAL_FAILURE"||session.supportTeam==="TECHNICIANS")?session.supportTeam:null;
  const canOpenLiveQueue=Boolean((isAdministrator||isDepartmentManager||technologyQueueTeam)&&session.permissions.canViewSupportDashboard);
  const canOpenTicketWorkspace =
    session.permissions.canViewSupportDashboard || isSupervisor;
  const canOpenWarehouse = isAdministrator || isWarehouseOperator || isDepartmentManager;
  const canOpenWorkshop = isAdministrator || isWorkshopOperator || isDepartmentManager;
  const viewLabel: Record<PortalView, string> = {
    audit: "Auditoría de campo",
    dashboard: "Mi panel",
    tickets: isSupervisor ? "Mis tickets" : "Soporte",
    warehouse: "Departamento de Almacén",
    workshop: "Departamento Taller",
    systemAudit: "Auditoría operativa",
    queue: "Cola en vivo",
  };
  const navigateTo = (target: PortalView) => {
    // Entrar desde el menú principal es navegación normal, no una solicitud
    // para abrir el último ticket visto desde una notificación u operación.
    if (target === "tickets") {
      setSupportNavigation(null);
      setSupportEntryRequest((current) => current + 1);
    }
    setView(target);
    setSidebarOpen(false);
  };
  const toggleSidebar = () => {
    setSidebarCollapsed((current) => {
      const next = !current;
      localStorage.setItem("portal-sidebar-collapsed", next ? "1" : "0");
      return next;
    });
  };

  return (
    <div className={`app-shell office-shell sidebar-layout${sidebarCollapsed ? " sidebar-collapsed" : ""}`}>
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
          <span className="sidebar-section-label">Principal</span>
          {session.permissions.canAudit && (
            <button type="button" className={view === "audit" ? "active" : ""} aria-current={view === "audit" ? "page" : undefined} onClick={() => navigateTo("audit")} title="Auditoría de campo">
              <ClipboardCheck /><span>Auditoría de campo</span>
            </button>
          )}
          {session.permissions.canViewDashboard && (
            <button type="button" className={view === "dashboard" ? "active" : ""} aria-current={view === "dashboard" ? "page" : undefined} onClick={() => navigateTo("dashboard")} title="Mi panel">
              <BarChart3 /><span>Mi panel</span>
            </button>
          )}
          {(isAdministrator || isSupervisor || isMaintenanceOperator || isDepartmentManager || technologyQueueTeam) && <span className="sidebar-section-label">Tickets y operación</span>}
          {!isSupervisor && !isMaintenanceOperator && session.permissions.canViewSupportDashboard && (
            <button type="button" className={view === "tickets" ? "active" : ""} aria-current={view === "tickets" ? "page" : undefined} onClick={() => navigateTo("tickets")} title="Soporte">
              <LifeBuoy /><span>Soporte</span>
              {unreadTicketNotifications > 0 && <em>{unreadTicketNotifications > 99 ? "99+" : unreadTicketNotifications}</em>}
            </button>
          )}
          {isSupervisor && (
            <button type="button" className={view === "tickets" ? "active" : ""} aria-current={view === "tickets" ? "page" : undefined} onClick={() => { navigateTo("tickets"); setSupervisorTicketInboxRequest((current) => current + 1); }} title="Mis tickets">
              <LifeBuoy /><span>Mis tickets</span>
              {unreadTicketNotifications > 0 && <em>{unreadTicketNotifications > 99 ? "99+" : unreadTicketNotifications}</em>}
            </button>
          )}
          {canOpenWarehouse && (
            <button type="button" className={view === "warehouse" ? "active" : ""} aria-current={view === "warehouse" ? "page" : undefined} onClick={() => navigateTo("warehouse")} title="Departamento de Almacén">
              <Building2 /><span>Almacén</span>
            </button>
          )}
          {canOpenWorkshop && (
            <button type="button" className={view === "workshop" ? "active" : ""} aria-current={view === "workshop" ? "page" : undefined} onClick={() => navigateTo("workshop")} title="Departamento Taller">
              <Settings /><span>Taller</span>
            </button>
          )}
          {isAdministrator && session.permissions.canManageAudits && (
            <button type="button" className={view === "systemAudit" ? "active" : ""} aria-current={view === "systemAudit" ? "page" : undefined} onClick={() => navigateTo("systemAudit")} title="Auditoría operativa">
              <ScrollText /><span>Auditoría operativa</span>
            </button>
          )}
          {canOpenLiveQueue && (
            <button type="button" className={view === "queue" ? "active" : ""} aria-current={view === "queue" ? "page" : undefined} onClick={() => navigateTo("queue")} title="Cola en vivo">
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
            <span>
              <small>Oficina virtual · navegación</small>
              <strong>{viewLabel[view]}</strong>
            </span>
          </div>
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
            {session.permissions.canViewTicketNotifications && <button className="notification-bell" onClick={()=>void sendPushTest()} title="Enviar notificación de prueba" aria-label="Enviar notificación de prueba"><BellRing /></button>}
            {session.permissions.canUseSupportChat && (
              <button className="notification-bell message-bell" onClick={() => openNotificationCenter("CHAT")} title="Centro de mensajes" aria-label={`Centro de mensajes${unreadChatNotifications ? `, ${unreadChatNotifications} nuevos` : ""}`}>
                <MessageCircle />
                {unreadChatNotifications > 0 && <em>{unreadChatNotifications > 99 ? "99+" : unreadChatNotifications}</em>}
              </button>
            )}
            <button className="header-profile" onClick={() => setProfileOpen(true)}>
              {session.avatarUrl ? <img src={session.avatarUrl} alt={session.displayName} /> : <UserRound />}
              <span><small>{sessionRoleLabel(session)}</small><strong>{session.displayName}</strong></span>
              <Settings />
            </button>
            <button className="header-logout" onClick={() => void logout()} title="Cerrar sesión" aria-label="Cerrar sesión"><LogOut /></button>
          </div>
          <nav className="main-nav mobile-main-nav" aria-label="Secciones principales en móvil">
            {session.permissions.canAudit && (
              <button type="button" className={view === "audit" ? "active" : ""} aria-current={view === "audit" ? "page" : undefined} onClick={() => navigateTo("audit")}>
                <ClipboardCheck /><span>Auditoría</span>
              </button>
            )}
            {session.permissions.canViewDashboard && (
              <button type="button" className={view === "dashboard" ? "active" : ""} aria-current={view === "dashboard" ? "page" : undefined} onClick={() => navigateTo("dashboard")}>
                <BarChart3 /><span>Mi panel</span>
              </button>
            )}
            {canOpenTicketWorkspace && !isMaintenanceOperator && (
              <button type="button" className={view === "tickets" ? "active" : ""} aria-current={view === "tickets" ? "page" : undefined} onClick={() => { navigateTo("tickets"); if (session.role === "GroupAdministrator") setSupervisorTicketInboxRequest((current) => current + 1); }}>
                <LifeBuoy /><span>{isSupervisor ? "Mis tickets" : "Soporte"}</span>
                {unreadTicketNotifications > 0 && <em className="ticket-alert-badge">{unreadTicketNotifications > 99 ? "99+" : unreadTicketNotifications}</em>}
              </button>
            )}
            {canOpenWarehouse && (
              <button type="button" className={view === "warehouse" ? "active" : ""} aria-current={view === "warehouse" ? "page" : undefined} onClick={() => navigateTo("warehouse")}>
                <Building2 /><span>Almacén</span>
              </button>
            )}
            {canOpenWorkshop && (
              <button type="button" className={view === "workshop" ? "active" : ""} aria-current={view === "workshop" ? "page" : undefined} onClick={() => navigateTo("workshop")}>
                <Settings /><span>Taller</span>
              </button>
            )}
            {isAdministrator && session.permissions.canManageAudits && (
              <button type="button" className={view === "systemAudit" ? "active" : ""} aria-current={view === "systemAudit" ? "page" : undefined} onClick={() => navigateTo("systemAudit")}>
                <ScrollText /><span>Operación</span>
              </button>
            )}
            {canOpenLiveQueue && (
              <button type="button" className={view === "queue" ? "active" : ""} aria-current={view === "queue" ? "page" : undefined} onClick={() => navigateTo("queue")}>
                <Monitor /><span>Cola en vivo</span>
              </button>
            )}
          </nav>
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
        ) : (view === "tickets" && canOpenTicketWorkspace) ||
        (view === "warehouse" && canOpenWarehouse) ||
        (view === "workshop" && canOpenWorkshop) ? (
          <Suspense fallback={<Loading />}>
            <TicketCenter
              key={`${view}:${supportNavigation?.requestKey ?? "normal"}:${supportEntryRequest}`}
              session={session}
              onSessionExpired={() => setSession(null)}
              navigationTarget={supportNavigation}
              supervisorInboxRequestKey={supervisorTicketInboxRequest}
              maintenanceEntry={view === "warehouse" ? "WAREHOUSE" : view === "workshop" ? "WORKSHOP" : null}
            />
          </Suspense>
        ) : ((view === "systemAudit" && isAdministrator) || (view === "queue" && canOpenLiveQueue)) ? (
          <Suspense fallback={<Loading />}>
            <OperationsCenter
              mode={view === "queue" ? "queue" : "audit"}
              teamScope={view==="queue"?technologyQueueTeam:null}
              onSessionExpired={() => setSession(null)}
              onOpenTicket={openTicketFromOperations}
            />
          </Suspense>
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
                              ? "agencia pendiente"
                              : "agencias pendientes"}
                          </small>
                        </span>
                        <ChevronRight className="arrow" />
                      </button>
                    ))}
                  </div>
                )}
                {!loading && filtered.length === 0 && (
                  <Empty text="No hay grupos pendientes dentro de tu alcance." />
                )}
              </Panel>
            )}
            {step === "agency" && (
              <Panel
                title="Selecciona la agencia"
                subtitle={
                  <>
                    Agencias pendientes del grupo <strong>{group}</strong>.
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
                        </span>
                        <ChevronRight className="arrow" />
                      </button>
                    ))}
                  </div>
                )}
                {!loading && filtered.length === 0 && (
                  <Empty text="No quedan agencias pendientes en este grupo." />
                )}
              </Panel>
            )}
            {step === "details" &&
              agency &&
              (reportAgencyOpen ? (
                <Suspense fallback={<Loading />}>
                  <TicketCenter
                    session={session}
                    onSessionExpired={() => setSession(null)}
                    reportAgency={{ ...agency, grupo: group }}
                    onCloseReport={() => setReportAgencyOpen(false)}
                  />
                </Suspense>
              ) : (
                <div className="agency-audit-workspace">
                  {session.role === "GroupAdministrator" &&
                    step === "details" &&
                    agency && (
                    <button
                      type="button"
                      className="report-incidence-button"
                      onClick={() => setReportAgencyOpen(true)}
                    >
                      <LifeBuoy />
                      <span>
                        <strong>Reportar incidencia</strong>
                        <small>
                          {agency.terminal} · {agency.codigo}
                        </small>
                      </span>
                      <ChevronRight />
                    </button>
                  )}
                  <EmployeeAuditForm
                    agency={agency}
                    group={group}
                    onBack={() => setStep("agency")}
                    onComplete={(data) => {
                      setResult(data);
                      setStep("success");
                    }}
                  />
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
              onSessionExpired={() => setSession(null)}
            />
          </Suspense>
        )}
        {equipmentCode&&<Suspense fallback={<p>Abriendo seguimiento…</p>}><EquipmentTracking initialCode={equipmentCode!} onClose={()=>{const url=new URL(window.location.href);url.searchParams.delete("equipment");window.history.replaceState(null,"",url);setEquipmentCode(null);}}/></Suspense>}
        <footer>© 2026 Oficina Virtual · Control seguro y confidencial · Web V318</footer>
      </main>
      </div>
      {pushPromptOpen&&<div className="push-permission-backdrop"><section className="push-permission-card" role="dialog" aria-modal="true" aria-label="Activar notificaciones"><BellRing/><h2>Activa las notificaciones del teléfono</h2><p>Recibe avisos de tickets y asignaciones aunque no tengas la página abierta.</p><button type="button" onClick={()=>void subscribePhonePush().catch(error=>window.alert(error.message))}>Activar notificaciones</button><button type="button" className="secondary" onClick={()=>{setPushPromptOpen(false);localStorage.setItem('push-prompt-dismissed','1');}}>Ahora no</button></section></div>}
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
      {notificationCenterOpen && (
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
                    <LifeBuoy /> Tickets
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
      )}
      {session.role === "GroupAdministrator" &&
        session.permissions.canUseSupportChat && (
          <SupportChat
            session={session}
            onSessionExpired={() => setSession(null)}
            openConversationId={
              supportNavigation?.kind === "CHAT"
                ? supportNavigation.conversationId
                : null
            }
            openRequestKey={supportNavigation?.requestKey}
          />
        )}
      {(profileOpen || session.mustChangePassword) && (
        <ProfileSettings
          session={session}
          forced={session.mustChangePassword}
          onClose={() => setProfileOpen(false)}
          onSession={(updated) => {
            setSession(updated);
            if (!updated.mustChangePassword) setProfileOpen(false);
          }}
        />
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





