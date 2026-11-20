import { apiFetch as fetch } from "@/lib/api-fetch";
import { useCallback, useEffect, useId, useRef, useState } from "react";
import {
  ArrowLeft,
  BarChart3,
  Building2,
  Camera,
  Check,
  CheckCircle2,
  ClipboardCheck,
  Clock3,
  Edit3,
  Download,
  Eye,
  KeyRound,
  LifeBuoy,
  LoaderCircle,
  Layers3,
  Map,
  MapPin,
  Maximize2,
  Navigation,
  PackageSearch,
  Printer,
  Save,
  Search,
  ShieldCheck,
  Trash2,
  UserCog,
  UserPlus,
  UserRound,
  UsersRound,
  X,
} from "lucide-react";
import type { PortalSession } from "@/lib/session";
import { roleLabel } from "@/lib/session";
import {deviceDate} from "@/lib/display";
import './usersPermissionsPage.css';
import {accessProfiles,userAccessProfile} from '@/lib/user-access-profiles';
import {allPermissionCatalog} from '@/lib/permission-catalog';
import UsersPermissionsWorkspace from './UsersPermissionsWorkspace';
import {userDepartments,departmentAssignment,assignedUserDepartment,type UserDepartment} from '@/lib/user-departments';

type Group = {
  grupo: string;
  total: number;
  completed: number;
  auditCount: number;
  completedRounds: number;
  roundsTotal: number;
  pending: number;
  percent: number;
  lastUpdated: string | null;
};
type AgencyProgress = {
  codigo: string;
  terminal: string;
  grupo: string;
  status: "PENDING" | "COMPLETED" | "REVIEW_REQUIRED";
  percent: number;
  lastUpdated: string | null;
  auditCount?: number;
};
type AdministratorProgress = {
  id: string;
  displayName: string;
  email: string;
  region?: string | null;
  groups: number;
  total: number;
  completed: number;
  pending: number;
  review: number;
  percent: number;
  lastUpdated: string | null;
};
type Answers = {
  painted?: string;
  razaSticker?: string;
  realSticker?: string;
  lotekaRemoved?: string;
  damageFound?: string;
  printerMaintained?: string;
  inverterPresent?: string;
  batteryPresent?: string;
  cashBoxType?: string;
};
type AuditPhoto = {
  photoType: string;
  url: string;
  latitude: number;
  longitude: number;
  accuracy: number;
  capturedAt: string;
};
type Audit = {
  codigo: string;
  terminal: string;
  grupo: string;
  municipio: string;
  provincia: string;
  type: string;
  latitude: number;
  longitude: number;
  accuracy: number;
  submittedAt: string;
  submittedByLogin?: string | null;
  profileId: string;
  observations?: string | null;
  employeeName?: string;
  employeeCode?: string | null;
  direccion?: string;
  sector?: string;
  answers?: Answers;
  photos?: AuditPhoto[];
};
type Stats = {
  total: number;
  completed: number;
  pending: number;
  review: number;
  percent: number;
  groupsTotal: number;
  groupsReady: number;
  auditsTotal: number;
  groups: Group[];
  agencies: AgencyProgress[];
  administrators: AdministratorProgress[];
  audits: Audit[];
};
type AdminUser = {
  id: string;
  email: string;
  displayName: string;
  role:
    | "ADMINISTRATOR"
    | "ADMINISTRATIVE"
    | "DEPARTMENT"
    | "VIEWER"
    | "GROUP_ADMIN"
    | "FISCALIZADOR"
    | "TECHNOLOGY"
    | "GENERAL_SERVICES"
    | "HUMAN_RESOURCES"
    | "WAREHOUSE";
  isActive: boolean;
  lastLoginAt: string | null;
  createdAt: string;
  password?: string;
  region?: string | null;
  contact?: string | null;
  avatarUrl?: string | null;
  mustChangePassword: boolean;
  groups: string[];
  agencyCount: number;
  permissions: Record<string, boolean>;
  supportTeam?: TechnologyTeam | UserDepartment | null;
};
// Shared with the dedicated users workspace.
const defaultUserPermissions = (
  role: AdminUser["role"],
): Record<string, boolean> => {
  const admin = role === "ADMINISTRATOR";
  const supervisor = role === "GROUP_ADMIN";
  const support = [
    "TECHNOLOGY",
    "GENERAL_SERVICES",
    "HUMAN_RESOURCES",
  ].includes(role);
  return {
    canViewFieldAudits: admin,
    canManageDepartment: admin,
    canView: true,
    canManage: admin,
    canManageUsers: admin,
    canManageAudits: admin,
    canViewDashboard: [
      "ADMINISTRATOR",
      "VIEWER",
      "GROUP_ADMIN",
      "FISCALIZADOR",
    ].includes(role),
    canAudit: admin || supervisor,
    canCreateTickets: admin || supervisor,
    canAssignTickets: admin || support,
    canResolveTickets: admin || support,
    canConfigureProfile: true,
    canExportAudits: admin || role === "FISCALIZADOR",
    canViewSupportDashboard: admin || support,
    canViewAllSupportDepartments: admin,
    canConfigureSupport: admin,
    canViewSupportFindings: admin || support,
    canCancelFindingWithEvidence: admin || support,
    canCancelFindingWithoutEvidence: false,
    canCancelFindingWithReason: admin || support,
    canCancelFindingWithoutReason: false,
    canUseSupportChat: admin || supervisor || support,
    canManageSupportChat: admin || support,
    canModerateSupportChat: admin || support,
    canDeleteSupportConversations: admin,
    canShareSupportConversations: admin || supervisor || support,
    canDeleteSupportTickets: admin,
    canViewTicketNotifications: admin || supervisor || support,
    canAuthorizeWarehouseRequests: admin,
    canLoadWarehouseInventory: admin,
    canCreateWarehouseArticles: admin,
  };
};
type UserAccessProfile = import('@/lib/user-access-profiles').AccessProfile;
type SupportDepartmentRole = UserDepartment;
type TechnologyTeam = "CALL_CENTER" | "TECHNICAL_FAILURE" | "TECHNICIANS" | "WAREHOUSE" | "WORKSHOP";
const accessProfilePermissions = (
  profile: UserAccessProfile,
  department: SupportDepartmentRole,
) => {
  if (profile === "WAREHOUSE_REQUESTER") return userAccessProfile(profile,department).permissions;
  if (profile === "ADMINISTRATIVE") return defaultUserPermissions("ADMINISTRATIVE");
  if (profile === "ADMINISTRATOR")
    return defaultUserPermissions("ADMINISTRATOR");
  if (profile === "SUPERVISOR") return defaultUserPermissions("GROUP_ADMIN");
  const permissions = defaultUserPermissions(departmentAssignment(department).role as AdminUser['role']);
  if(departmentAssignment(department).role === "DEPARTMENT")Object.assign(permissions,userAccessProfile(profile,department).permissions);
  permissions.canManageDepartment = profile === "DEPARTMENT_MANAGER";
  if(profile === "DEPARTMENT_MANAGER")Object.assign(permissions,{canViewFieldAudits:true,canViewDashboard:true,canViewSupportDashboard:true,canManageUsers:false,canManageAudits:false,canViewAllSupportDepartments:false});
  if (profile === "TECHNICIAN") {
    permissions.canAssignTickets = false;
    permissions.canModerateSupportChat = false;
    permissions.canConfigureSupport = false;
    permissions.canViewAllSupportDepartments = false;
    permissions.canDeleteSupportTickets = false;
    permissions.canDeleteSupportConversations = false;
  }
  return permissions;
};
const technologyTeamPermissions=(team:TechnologyTeam):Record<string,boolean>=>{
  const permissions=defaultUserPermissions("TECHNOLOGY");
  Object.assign(permissions,{canViewDashboard:false,canAudit:false,canExportAudits:false,canManageUsers:false,canManageAudits:false,canViewAllSupportDepartments:false,canConfigureSupport:false,canDeleteSupportTickets:false,canDeleteSupportConversations:false});
  if(team==="WAREHOUSE"||team==="WORKSHOP")Object.assign(permissions,{canCreateTickets:false,canAssignTickets:false,canResolveTickets:false,canViewSupportDashboard:true,canViewSupportFindings:false,canUseSupportChat:true,canManageSupportChat:false,canModerateSupportChat:false,canShareSupportConversations:false,canViewTicketNotifications:true});
  else if(team==="TECHNICIANS")Object.assign(permissions,{canCreateTickets:false,canAssignTickets:false,canResolveTickets:true,canViewSupportDashboard:true,canViewSupportFindings:true,canUseSupportChat:true,canManageSupportChat:false,canModerateSupportChat:false,canShareSupportConversations:true,canViewTicketNotifications:true});
  else if(team==="CALL_CENTER")Object.assign(permissions,{canCreateTickets:true,canAssignTickets:true,canResolveTickets:true,canViewSupportDashboard:true,canViewSupportFindings:false,canUseSupportChat:true,canManageSupportChat:true,canModerateSupportChat:false,canShareSupportConversations:true,canViewTicketNotifications:true});
  else Object.assign(permissions,{canCreateTickets:true,canAssignTickets:true,canResolveTickets:true,canViewSupportDashboard:true,canViewSupportFindings:true,canUseSupportChat:true,canManageSupportChat:true,canModerateSupportChat:false,canShareSupportConversations:true,canViewTicketNotifications:true});
  return permissions;
};
type _AssignedGroup = { grupo: string; pending?: number };
type _AssignedAgency = {
  codigo: string;
  terminal: string;
  grupo?: string;
  status?: AgencyProgress["status"];
  percent?: number;
  lastUpdated?: string | null;
};

const answerLabels: Array<[keyof Answers, string]> = [
  ["painted", "¿La agencia fue pintada?"],
  ["razaSticker", "¿Fue colocado el sticker de raza?"],
  ["realSticker", "¿Fue colocado el sticker de Real?"],
  ["lotekaRemoved", "¿Fue retirada la publicidad de Loteka?"],
  ["damageFound", "¿Hubo alguna avería durante el levantamiento?"],
  ["printerMaintained", "¿Le dieron mantenimiento a la impresora?"],
  ["inverterPresent", "¿Tiene inversor?"],
  ["batteryPresent", "¿Tiene batería?"],
  ["cashBoxType", "¿La agencia tiene caja registradora o caja de madera?"],
];
const equipmentAnswerKeys = new Set<keyof Answers>([
  "inverterPresent",
  "batteryPresent",
  "printerMaintained",
]);
const photoLabels: Record<string, string> = {
  frontal: "Foto frontal",
  operativa: "Pantalla de Raza",
  entorno: "Rapidita y entorno",
  inversor: "Evidencia del inversor",
  bateria: "Evidencia de la batería",
};
const emptyStats: Stats = {
  total: 0,
  completed: 0,
  pending: 0,
  review: 0,
  percent: 0,
  groupsTotal: 0,
  groupsReady: 0,
  auditsTotal: 0,
  groups: [],
  agencies: [],
  administrators: [],
  audits: [],
};
const usernameValue = (email: string) =>
  email.replace(/@grupotejeda\.local$/i, "");
const auditOrdinal = (index: number) => {
  const names = [
    "Primer levantamiento",
    "Segundo levantamiento",
    "Tercer levantamiento",
    "Cuarto levantamiento",
    "Quinto levantamiento",
    "Sexto levantamiento",
    "Séptimo levantamiento",
    "Octavo levantamiento",
    "Noveno levantamiento",
    "Décimo levantamiento",
  ];
  return names[index] || `${index + 1}.º levantamiento`;
};
const usernameEmail = (username: string) =>
  `${username
    .toLowerCase()
    .replace(/[^a-z0-9.]/g, "")
    .replace(/^\.+/g, "")
    .replace(/\.{2,}/g, ".")}@grupotejeda.local`;

type PreparedImage = { data: string; width: number; height: number };
async function assetAsDataUrl(url: string): Promise<string> {
  const response = await fetch(url);
  if (!response.ok) throw new Error("No se pudo cargar la identidad visual.");
  const blob = await response.blob();
  return await new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () =>
      reject(new Error("No se pudo preparar la identidad visual."));
    reader.readAsDataURL(blob);
  });
}

async function mapWithConcurrency<T, R>(
  items: T[],
  limit: number,
  task: (item: T, index: number) => Promise<R>,
): Promise<R[]> {
  const results = new Array<R>(items.length);
  let cursor = 0;
  await Promise.all(
    Array.from({ length: Math.min(limit, items.length) }, async () => {
      while (cursor < items.length) {
        const index = cursor++;
        results[index] = await task(items[index], index);
      }
    }),
  );
  return results;
}

async function evidenceAsJpeg(url: string): Promise<PreparedImage> {
  const controller = new AbortController();
  const requestTimeout = window.setTimeout(() => controller.abort(), 15_000);
  const response = await fetch(url, { signal: controller.signal }).finally(() =>
    window.clearTimeout(requestTimeout),
  );
  if (!response.ok)
    throw new Error("No se pudo incorporar una fotografía al PDF.");
  const objectUrl = URL.createObjectURL(await response.blob());
  try {
    const image = await new Promise<HTMLImageElement>((resolve, reject) => {
      const element = new Image();
      const decodeTimeout = window.setTimeout(
        () => reject(new Error("La fotografía tardó demasiado en responder.")),
        12_000,
      );
      element.onload = () => {
        window.clearTimeout(decodeTimeout);
        resolve(element);
      };
      element.onerror = () => {
        window.clearTimeout(decodeTimeout);
        reject(new Error("Una fotografía no pudo procesarse para el PDF."));
      };
      element.src = objectUrl;
    });
    const maximum = 1100;
    const scale = Math.min(
      1,
      maximum / Math.max(image.naturalWidth, image.naturalHeight),
    );
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
    canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
    const context = canvas.getContext("2d");
    if (!context)
      throw new Error("El navegador no pudo preparar las imágenes del PDF.");
    context.fillStyle = "#ffffff";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    return {
      data: canvas.toDataURL("image/jpeg", 0.74),
      width: canvas.width,
      height: canvas.height,
    };
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
}

const safeFileName = (value: string) =>
  value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^a-z0-9_-]+/gi, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 80) || "grupo";

export default function AdminPanel({
  session,
  onSessionExpired,
  initialSection = "groups",
  usersOnly = false,
  onCloseUsers,
}: {
  session: PortalSession;
  onSessionExpired: () => void;
  initialSection?: "groups" | "agencies" | "administrators" | "audits";
  usersOnly?: boolean;
  onCloseUsers?: () => void;
}) {
  const onSessionExpiredRef = useRef(onSessionExpired);
  useEffect(() => {
    onSessionExpiredRef.current = onSessionExpired;
  }, [onSessionExpired]);

  const [stats, setStats] = useState<Stats | null>(null);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState("all");
  const [sectionTab, setSectionTab] = useState<
    "groups" | "agencies" | "administrators" | "audits"
  >(initialSection);
  const [agencyPage, setAgencyPage] = useState(1);
  const [selectedAudit, setSelectedAudit] = useState<Audit | null>(null);
  const [editDraft, setEditDraft] = useState<Audit | null>(null);
  const [auditLoading, setAuditLoading] = useState(false);
  const [exportingAudits, setExportingAudits] = useState(false);
  const [exportGroup, setExportGroup] = useState("");
  const preparedEvidenceCache = useRef(
    new globalThis.Map<string, Promise<PreparedImage>>(),
  );
  const [lightboxPhoto, setLightboxPhoto] = useState<{
    url: string;
    label: string;
  } | null>(null);
  const [userManagerOpen, setUserManagerOpen] = useState(false);
  const [newUserOpen, setNewUserOpen] = useState(false);
  const [creatingUser, setCreatingUser] = useState(false);
  const creatingUserRef = useRef(false);
  const [deletingUserId, setDeletingUserId] = useState<string | null>(null);
  const deletingUserRef = useRef<string | null>(null);
  const [exportingUsers, setExportingUsers] = useState(false);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [userQuery, setUserQuery] = useState("");
  const [userDepartmentFilter,setUserDepartmentFilter]=useState<"ADMINISTRATION"|"SUPERVISORS"|"WAREHOUSE"|SupportDepartmentRole|null>(null);
  const [selectedUserId,setSelectedUserId]=useState<string|null>(null);
  const [userDetailSection,setUserDetailSection]=useState<"information"|"permissions"|"groups">("information");
  const [groupCatalog, setGroupCatalog] = useState<string[]>([]);
  const [newUserAccessProfile, setNewUserAccessProfile] =
    useState<UserAccessProfile>("SUPPORT");
  const [newUserDepartment, setNewUserDepartment] =
    useState<SupportDepartmentRole>("GENERAL_SERVICES");
  const [newUser, setNewUser] = useState({
    email: "",
    displayName: "",
    role: "GENERAL_SERVICES" as AdminUser["role"],
    password: "123456",
    region: "",
    contact: "",
    supportTeam: null as TechnologyTeam | UserDepartment | null,
    groups: [] as string[],
    permissions: userAccessProfile("SUPPORT","GENERAL_SERVICES").permissions,
  });

  const prepareEvidence = (url: string) => {
    const key = url.split("?")[0];
    const cached = preparedEvidenceCache.current.get(key);
    if (cached) return cached;
    const pending = evidenceAsJpeg(url).catch((error) => {
      preparedEvidenceCache.current.delete(key);
      throw error;
    });
    preparedEvidenceCache.current.set(key, pending);
    return pending;
  };

  // PDF libraries load when an export is requested.

  const loadVisibleCatalog = useCallback(async () => {
    const response = await fetch("/api/panel/catalog");
    if (response.status === 401) { onSessionExpiredRef.current(); throw new Error("La sesión expiró."); }
    const body = await response.json();
    if (!response.ok) throw new Error(body.error || "No fue posible consultar las agencias asignadas.");
    const agencies = (body.agencies || []) as AgencyProgress[];
    const grouped = new globalThis.Map<string, AgencyProgress[]>();
    for (const agency of agencies) {
      const rows = grouped.get(agency.grupo) || [];
      rows.push(agency); grouped.set(agency.grupo, rows);
    }
    const groups: Group[] = [...grouped].map(([grupo, rows]) => ({
      grupo, total: rows.length, completed: rows.filter(row => row.status === 'COMPLETED').length,
      auditCount: rows.reduce((total, row) => total + (row.auditCount || 0), 0),
      completedRounds: rows.length ? Math.min(...rows.map(row => row.auditCount || 0)) : 0,
      roundsTotal: Math.max(1, ...rows.map(row => row.auditCount || 0)),
      pending: rows.filter(row => row.status === 'PENDING').length,
      percent: Math.round(rows.filter(row => row.status === 'COMPLETED').length * 100 / rows.length), lastUpdated: null,
    }));
    return { agencies, groups };
  }, []);
  const load = useCallback(async () => {
    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), 12_000);
    const response = await fetch("/api/dashboard", {
      signal: controller.signal,
    }).finally(() => window.clearTimeout(timeout));
    if (response.status === 401) {
      onSessionExpiredRef.current();
      return;
    }
    const data = await response.json().catch(() => ({}));
    if (!response.ok)
      throw new Error(
        data.error || `No se pudo cargar el panel (HTTP ${response.status}).`,
      );
    let hydrated = { ...emptyStats, ...data } as Stats;
    if (session.role === "GroupAdministrator" && hydrated.agencies.length < hydrated.total) {
      const catalog = await loadVisibleCatalog();
      const progress = new globalThis.Map<string, AgencyProgress>(
        (hydrated.agencies || []).map((agency) => [
          `${agency.grupo}\u0000${agency.codigo}`,
          agency,
        ]),
      );
      const agencies = catalog.agencies.map(
        (agency) =>
          progress.get(`${agency.grupo}\u0000${agency.codigo}`) || agency,
      );
      hydrated = {
        ...hydrated,
        total: agencies.length,
        pending: agencies.filter((agency) => agency.status === "PENDING")
          .length,
        completed: agencies.filter((agency) => agency.status === "COMPLETED")
          .length,
        review: agencies.filter((agency) => agency.status === "REVIEW_REQUIRED")
          .length,
        percent: agencies.length
          ? Math.round(
              (agencies.filter((agency) => agency.status === "COMPLETED")
                .length *
                100) /
                agencies.length,
            )
          : 0,
        groups: hydrated.groups.length ? hydrated.groups : catalog.groups,
        groupsTotal: hydrated.groups.length
          ? hydrated.groupsTotal
          : catalog.groups.length,
        agencies,
      };
    }
    setStats(hydrated);
    setError("");
    setNotice("");
  }, [loadVisibleCatalog, session.role]);

  useEffect(() => {
    let cancelled = false;
    const loadDashboardFallback = async () => {
      const { agencies, groups } = await loadVisibleCatalog();
      if (cancelled) return;
      setStats({
        ...emptyStats,
        total: agencies.length,
        pending: agencies.filter(agency => agency.status === 'PENDING').length,
        completed: agencies.filter(agency => agency.status === 'COMPLETED').length,
        review: agencies.filter(agency => agency.status === 'REVIEW_REQUIRED').length,
        percent: agencies.length ? Math.round(100 * agencies.filter(agency => agency.status === 'COMPLETED').length / agencies.length) : 0,
        groupsReady: groups.filter(group => group.completed === group.total).length,
        groupsTotal: groups.length,
        groups,
        agencies,
      });
      setSectionTab(
        session.role === "GroupAdministrator" ? "agencies" : "groups",
      );
      setError("");
      setNotice(
        session.role === "GroupAdministrator"
          ? `Se cargaron tus ${agencies.length} agencias asignadas. Las evidencias se actualizarán cuando vuelva a responder el resumen SQL Server.`
          : `Se cargó el avance básico de ${groups.length} grupos y ${agencies.length} agencias. Las evidencias se actualizarán cuando vuelva a responder el resumen SQL Server.`,
      );
    };

    void load().catch(() => {
      void loadDashboardFallback().catch((fallbackError) => {
        if (cancelled) return;
        setStats(emptyStats);
        setError(
          (fallbackError as Error).message ||
            "No fue posible consultar SQL Server.",
        );
      });
    });
    return () => {
      cancelled = true;
    };
  }, [load, loadVisibleCatalog, session.id, session.role]);
  useEffect(() => {
    let lastRefresh = Date.now();
    const refreshAgencyCatalog = () => {
      if (document.hidden || Date.now() - lastRefresh < 60000) return;
      lastRefresh = Date.now();
      void load().catch(() => undefined);
    };
    window.addEventListener("support-live-refresh", refreshAgencyCatalog);
    return () => window.removeEventListener("support-live-refresh", refreshAgencyCatalog);
  }, [load]);
  useEffect(() => {
    setAgencyPage(1);
  }, [query, filter, sectionTab]);
  useEffect(() => {
    if (session.role === "GroupAdministrator") setSectionTab("agencies");
  }, [session.role]);
  const openAudit = async (audit: Audit) => {
    setAuditLoading(true);
    setError("");
    try {
      const response = await fetch(`/api/admin/audits/${audit.profileId}`);
      const detail = await response.json().catch(() => ({}));
      if (!response.ok)
        throw new Error(detail.error || "No se pudo cargar la auditoría.");
      setSelectedAudit({ ...audit, ...detail });
    } catch (detailError) {
      setError((detailError as Error).message);
    } finally {
      setAuditLoading(false);
    }
  };

  const saveAudit = async () => {
    if (!editDraft || !stats || !session?.permissions.canManageAudits) return;
    setAuditLoading(true);
    setError("");
    try {
      const response = await fetch(`/api/admin/audits/${editDraft.profileId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(editDraft),
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok)
        throw new Error(body.error || "No se pudo guardar la corrección.");
      const saved = { ...editDraft, ...body };
      setStats({
        ...stats,
        audits: stats.audits.map((audit) =>
          audit.profileId === saved.profileId ? saved : audit,
        ),
      });
      setSelectedAudit(saved);
      setEditDraft(null);
      await load();
      setNotice("La auditoría fue corregida correctamente.");
    } catch (saveError) {
      setError((saveError as Error).message);
    } finally {
      setAuditLoading(false);
    }
  };

  const deleteAudit = async () => {
    if (
      !selectedAudit ||
      !stats ||
      !session?.permissions.canManageAudits ||
      !window.confirm(
        `¿Eliminar esta auditoría de ${selectedAudit.codigo}? Las demás auditorías de la agencia se conservarán.`,
      )
    )
      return;
    setAuditLoading(true);
    setError("");
    try {
      const response = await fetch(
        `/api/admin/audits/${selectedAudit.profileId}`,
        { method: "DELETE" },
      );
      const body = await response.json().catch(() => ({}));
      if (!response.ok)
        throw new Error(body.error || "No se pudo eliminar la auditoría.");
      await load();
      setSelectedAudit(null);
      setEditDraft(null);
      setNotice("La auditoría fue eliminada. El estado de la agencia se recalculó según su historial restante.");
    } catch (deleteError) {
      setError((deleteError as Error).message);
    } finally {
      setAuditLoading(false);
    }
  };

  const _openUsers = async () => {
    if (!session?.permissions.canManageUsers) return;
    setUserManagerOpen(true);
    setError("");
    try {
      const [usersResponse, groupsResponse] = await Promise.all([
        fetch("/api/admin/users"),
        fetch("/api/admin/groups-catalog"),
      ]);
      const usersBody = await usersResponse.json().catch(() => ({}));
      const groupsBody = await groupsResponse.json().catch(() => ({}));
      if (!usersResponse.ok)
        throw new Error(
          usersBody.error || "No se pudieron cargar los usuarios.",
        );
      if (!groupsResponse.ok)
        throw new Error(
          groupsBody.error || "No se pudo cargar el catálogo de grupos.",
        );
      setUsers(usersBody.users);
      setGroupCatalog(groupsBody.groups);
    } catch (usersError) {
      setError((usersError as Error).message);
    }
  };
  const closeUsers=()=>{if(usersOnly)onCloseUsers?.();else setUserManagerOpen(false)};
  const createUser = async (event: React.FormEvent) => {
    event.preventDefault();
    if (creatingUserRef.current) return;
    creatingUserRef.current = true;
    setCreatingUser(true);
    setError("");
    try {
      const response = await fetch("/api/admin/users", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(newUser),
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok)
        throw new Error(body.error || "No se pudo crear el usuario.");
      setUsers((current) => [...current, body]);
    } catch (createError) {
      setError((createError as Error).message);
      creatingUserRef.current = false;
      setCreatingUser(false);
      return;
    }
    setNewUser({
      email: "",
      displayName: "",
      role: "GENERAL_SERVICES",
      password: "123456",
      region: "",
      contact: "",
      supportTeam: null,
      groups: [],
      permissions: userAccessProfile("SUPPORT","GENERAL_SERVICES").permissions,
    });
    setNewUserAccessProfile("SUPPORT");
    setNewUserDepartment("GENERAL_SERVICES");
    setNewUserOpen(false);
    if(!usersOnly)setUserManagerOpen(false);
    creatingUserRef.current = false;
    setCreatingUser(false);
    setNotice("Usuario creado correctamente.");
  };
  const savingUserRef=useRef(false);
  const [savingUserId,setSavingUserId]=useState<string|null>(null);
  const saveUser = async (user: AdminUser) => {
    if(savingUserRef.current)return;
    savingUserRef.current=true;setSavingUserId(user.id);
    setError("");
    try {
      const response = await fetch(`/api/admin/users/${user.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(user),
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok)
        throw new Error(body.error || "No se pudo actualizar el usuario.");
      setUsers((current) =>
        current.map((item) => (item.id === user.id ? body : item)),
      );
    } catch (updateError) {
      setError((updateError as Error).message);
      return;
    }finally{savingUserRef.current=false;setSavingUserId(null);}
    setNotice("Permisos y contraseña actualizados.");
    setNewUserOpen(false);
    if(!usersOnly)setUserManagerOpen(false);
  };
  const deleteUser = async (user: AdminUser) => {
    if (user.id === session.id || deletingUserRef.current) return;
    if (!window.confirm(`¿Eliminar el acceso de ${user.displayName}? La cuenta quedará desactivada y no podrá iniciar sesión. El historial de tickets se conservará.`)) return;
    deletingUserRef.current = user.id;
    setDeletingUserId(user.id);
    setError("");
    try {
      const response = await fetch(`/api/admin/users/${user.id}`, { method: "DELETE" });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(body.error || "No se pudo eliminar el usuario.");
      setUsers((current) => current.filter((item) => item.id !== user.id));
      setSelectedUserId(null);
      setUserDetailSection("information");
      setNotice(`El acceso de ${user.displayName} fue eliminado correctamente.`);
    } catch (deleteError) {
      setError((deleteError as Error).message);
    } finally {
      deletingUserRef.current = null;
      setDeletingUserId(null);
    }
  };
  const userDirectoryGroups = () => {
    const normalize = (value: string) =>
      value
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .trim()
        .toLowerCase();
    const included = users.filter((user) => {
      const username = normalize(usernameValue(user.email));
      const name = normalize(user.displayName);
      return (
        username !== "admin" &&
        username !== "stiven.rosario" &&
        name !== "administrador principal"
      );
    });
    const targets = [
      { name: "Gledys Tejeda", aliases: ["gledys tejeda", "gledys"] },
      { name: "Javier Tejeda", aliases: ["javier tejeda", "javier"] },
      {
        name: "Luis Manuel",
        aliases: ["luis manuel", "luis.manuel", "manuel"],
      },
      {
        name: "Richard Tejeda",
        aliases: ["richard tejeda", "richar tejeda", "richard", "richar"],
      },
      {
        name: "Jordy Arias",
        aliases: ["jordy arias", "yordy arias", "jordy", "yordy"],
      },
      {
        name: "Luis Alberto",
        aliases: ["luis alberto", "luis.alberto", "luisalberto", "alberto"],
      },
      {
        name: "Michael Tejeda",
        aliases: ["michael tejeda", "maicol tejeda", "michael", "maicol"],
      },
      { name: "Emely Tejeda", aliases: ["emely tejeda", "emely"] },
      {
        name: "Eduar Guzman",
        aliases: [
          "eduar guzman",
          "eduard guzman",
          "eduar guzmán",
          "eduard guzmán",
          "eduar",
          "eduard",
        ],
      },
    ];
    const matchedIds = new Set<string>();
    const management = targets.flatMap((target) => {
      const found = included.find((user) => {
        if (matchedIds.has(user.id)) return false;
        const username = normalize(usernameValue(user.email));
        const identity = `${normalize(user.displayName)} ${username} ${username.replaceAll(".", " ")}`;
        return target.aliases.some((alias) =>
          identity.includes(normalize(alias)),
        );
      });
      if (!found && target.name === "Luis Alberto")
        return [
          {
            id: "directory-luis-alberto",
            email: "luis.alberto@grupotejeda.local",
            displayName: "Luis Alberto",
            role: "GROUP_ADMIN" as const,
            isActive: true,
            lastLoginAt: null,
            createdAt: "",
            region: null,
            contact: null,
            avatarUrl: null,
            mustChangePassword: false,
            groups: [],
            agencyCount: 0,
            permissions: {},
          },
        ];
      if (!found) return [];
      matchedIds.add(found.id);
      const correctedEmail =
        target.name === "Jordy Arias"
          ? "jordy.arias@grupotejeda.local"
          : target.name === "Michael Tejeda"
            ? "michael.tejeda@grupotejeda.local"
            : found.email;
      return [{ ...found, displayName: target.name, email: correctedEmail }];
    });
    const supervisors = included
      .filter((user) => !matchedIds.has(user.id))
      .sort((a, b) => a.displayName.localeCompare(b.displayName, "es"));
    return {
      included: [...management, ...supervisors],
      management,
      supervisors,
    };
  };
  const printUserDirectory = () => {
    const {
      included: printableUsers,
      management,
      supervisors,
    } = userDirectoryGroups();
    const escapeHtml = (value: unknown) =>
      String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
    const printableRole = (role: AdminUser["role"]) =>
      ({
        DEPARTMENT: "Usuario de departamento", VIEWER: "Consulta general",
        FISCALIZADOR: "Fiscalizador",
        TECHNOLOGY: "Tecnología",
        GENERAL_SERVICES: "Servicios Generales",
        HUMAN_RESOURCES: "Recursos Humanos",
        WAREHOUSE: "Almacén",
        GROUP_ADMIN: "Supervisor",
        ADMINISTRATOR: "Administrador principal",
        ADMINISTRATIVE: "Administrativo",
      })[role];
    const rows = (items: AdminUser[]) =>
      items.length
        ? items
            .map(
              (user, index) =>
                `<tr><td>${index + 1}</td><td><strong>${escapeHtml(user.displayName)}</strong><small>${escapeHtml(usernameValue(user.email).toUpperCase())}</small></td><td>${escapeHtml(printableRole(user.role))}</td><td>${escapeHtml(user.region || "Sin región")}<small>${escapeHtml(user.contact || "Sin contacto")}</small></td><td>${user.groups.length ? escapeHtml(user.groups.join(" · ")) : "—"}</td><td>${user.isActive ? "Activo" : "Bloqueado"}</td></tr>`,
            )
            .join("")
        : '<tr><td colspan="6" class="empty">No hay usuarios en esta categoría.</td></tr>';
    const reportWindow = window.open("", "_blank", "width=1200,height=850");
    if (!reportWindow) {
      setError(
        "El navegador bloqueó la ventana de impresión. Habilita las ventanas emergentes e inténtalo de nuevo.",
      );
      return;
    }
    reportWindow.document.write(
      // The escaped closing tag prevents an HTML parser from ending the injected script early.
      // eslint-disable-next-line no-useless-escape
      `<!doctype html><html lang="es"><head><meta charset="utf-8"><title>Listado de usuarios</title><style>@page{size:letter landscape;margin:12mm}*{box-sizing:border-box}body{margin:0;color:#17283b;font:12px Arial,sans-serif}header{padding-bottom:12px;border-bottom:3px solid #168ec5}h1{margin:0 0 5px;font-size:25px}header p{margin:0;color:#52677c}.summary{display:flex;gap:10px;margin:14px 0}.summary span{padding:7px 11px;border-radius:999px;background:#e9f6fc;font-weight:700}section{margin-top:18px;break-inside:avoid}h2{margin:0;padding:8px 10px;background:#0b3657;color:#fff;font-size:15px}table{width:100%;border-collapse:collapse}th,td{padding:7px;border:1px solid #bed0dd;vertical-align:top;text-align:left}th{background:#e8f1f7;font-size:10px;text-transform:uppercase}td:first-child{width:34px;text-align:center}td:nth-child(2){min-width:165px}td:nth-child(3){width:125px}td:nth-child(4){width:155px}td:nth-child(6){width:62px}small{display:block;margin-top:3px;color:#60788c}.empty{text-align:center;color:#60788c}footer{margin-top:16px;color:#60788c;font-size:10px}</style></head><body><header><h1>Listado de usuarios</h1><p>Gerencia y supervisores registrados · Administrador principal y stiven.rosario excluidos</p></header><div class="summary"><span>${management.length} de Gerencia</span><span>${supervisors.length} Supervisores</span><span>${printableUsers.length} usuarios incluidos</span></div><section><h2>Gerencia</h2><table><thead><tr><th>#</th><th>Nombre / usuario</th><th>Rol</th><th>Región / contacto</th><th>Alcance</th><th>Estado</th></tr></thead><tbody>${rows(management)}</tbody></table></section><section><h2>Supervisores</h2><table><thead><tr><th>#</th><th>Nombre / usuario</th><th>Rol</th><th>Región / contacto</th><th>Grupos asignados</th><th>Estado</th></tr></thead><tbody>${rows(supervisors)}</tbody></table></section><footer>Generado el ${escapeHtml(new Date().toLocaleString("es-DO"))}. Incluye todas las cuentas vigentes del sistema, entre ellas cualquier coincidencia con Tejeda, Eduard Guzmán y Luis Manuel.</footer><script>window.addEventListener("load",()=>window.print());<\/script></body></html>`,
    );
    reportWindow.document.close();
    reportWindow.opener = null;
  };
  const _downloadUserDirectoryPdf = async () => {
    if (exportingUsers) return;
    setExportingUsers(true);
    setError("");
    try {
      const {
        included: printableUsers,
        management,
        supervisors,
      } = userDirectoryGroups();
      const roleName = (role: AdminUser["role"]) =>
        ({
          DEPARTMENT: "Usuario de departamento", VIEWER: "Consulta general",
          FISCALIZADOR: "Fiscalizador",
          TECHNOLOGY: "Tecnologia",
          GENERAL_SERVICES: "Servicios Generales",
          HUMAN_RESOURCES: "Recursos Humanos",
          WAREHOUSE: "Almacen",
          GROUP_ADMIN: "Supervisor",
          ADMINISTRATOR: "Administrador principal",
        ADMINISTRATIVE: "Administrativo",
        })[role];
      const { jsPDF } = await import("jspdf");
      const pdf = new jsPDF({
        orientation: "landscape",
        unit: "mm",
        format: "letter",
        compress: true,
      });
      const pageWidth = pdf.internal.pageSize.getWidth();
      const pageHeight = pdf.internal.pageSize.getHeight();
      const drawTitle = () => {
        pdf.setFillColor(5, 29, 54);
        pdf.rect(0, 0, pageWidth, 29, "F");
        pdf.setTextColor(105, 216, 255);
        pdf.setFont("helvetica", "bold");
        pdf.setFontSize(18);
        pdf.text("GRUPO TEJEDA", 12, 11);
        pdf.setTextColor(255, 255, 255);
        pdf.setFontSize(12);
        pdf.text("LISTADO DE USUARIOS", 12, 20);
      };
      drawTitle();
      pdf.setTextColor(31, 56, 78);
      pdf.setFont("helvetica", "normal");
      pdf.setFontSize(9);
      pdf.text(
        `Gerencia: ${management.length}   Supervisores: ${supervisors.length}   Total incluido: ${printableUsers.length}`,
        12,
        38,
      );
      pdf.setFontSize(7.5);
      pdf.setTextColor(89, 111, 130);
      pdf.text(
        "Excluye Administrador principal y stiven.rosario. Incluye todas las coincidencias existentes con Tejeda, Eduard Guzman y Luis Manuel.",
        12,
        44,
      );
      let y = 51;
      const columns = [12, 20, 73, 112, 153, 244];
      const widths = [8, 53, 39, 41, 91, 23];
      const tableHeader = () => {
        pdf.setFillColor(224, 239, 248);
        pdf.rect(12, y, pageWidth - 24, 8, "F");
        pdf.setTextColor(35, 77, 106);
        pdf.setFont("helvetica", "bold");
        pdf.setFontSize(7);
        [
          "#",
          "NOMBRE / USUARIO",
          "ROL",
          "REGION / CONTACTO",
          "GRUPOS ASIGNADOS",
          "ESTADO",
        ].forEach((label, index) =>
          pdf.text(label, columns[index] + 1.5, y + 5.2),
        );
        y += 8;
      };
      const newPage = () => {
        pdf.addPage();
        drawTitle();
        y = 36;
      };
      const drawSection = (title: string, items: AdminUser[]) => {
        if (y > pageHeight - 30) newPage();
        pdf.setFillColor(9, 57, 91);
        pdf.rect(12, y, pageWidth - 24, 9, "F");
        pdf.setTextColor(255, 255, 255);
        pdf.setFont("helvetica", "bold");
        pdf.setFontSize(9);
        pdf.text(`${title} (${items.length})`, 15, y + 6);
        y += 9;
        tableHeader();
        if (!items.length) {
          pdf.setTextColor(95, 119, 137);
          pdf.setFont("helvetica", "normal");
          pdf.text("No hay usuarios en esta categoria.", 15, y + 7);
          y += 12;
          return;
        }
        items.forEach((user, index) => {
          if (y > pageHeight - 22) {
            newPage();
            tableHeader();
          }
          const rowHeight = 12;
          if (index % 2 === 0) {
            pdf.setFillColor(246, 250, 252);
            pdf.rect(12, y, pageWidth - 24, rowHeight, "F");
          }
          pdf.setDrawColor(199, 216, 227);
          pdf.line(12, y + rowHeight, pageWidth - 12, y + rowHeight);
          pdf.setFontSize(7.3);
          pdf.setTextColor(31, 56, 78);
          pdf.setFont("helvetica", "normal");
          pdf.text(String(index + 1), columns[0] + 2, y + 7);
          pdf.setFont("helvetica", "bold");
          pdf.text(
            pdf.splitTextToSize(user.displayName, widths[1] - 3).slice(0, 1),
            columns[1] + 1.5,
            y + 4.5,
          );
          pdf.setFont("helvetica", "normal");
          pdf.setTextColor(91, 116, 135);
          pdf.text(usernameValue(user.email).toUpperCase(), columns[1] + 1.5, y + 9);
          pdf.setTextColor(31, 56, 78);
          pdf.text(roleName(user.role), columns[2] + 1.5, y + 7);
          pdf.text(
            pdf
              .splitTextToSize(user.region || "Sin region", widths[3] - 3)
              .slice(0, 1),
            columns[3] + 1.5,
            y + 4.5,
          );
          pdf.setTextColor(91, 116, 135);
          pdf.text(user.contact || "Sin contacto", columns[3] + 1.5, y + 9);
          pdf.setTextColor(31, 56, 78);
          pdf.text(
            user.role === "GROUP_ADMIN"
              ? `${user.groups.length} grupos - ${user.agencyCount} agencias`
              : "Cobertura segun rol",
            columns[4] + 1.5,
            y + 7,
          );
          pdf.setTextColor(
            user.isActive ? 24 : 165,
            user.isActive ? 120 : 55,
            user.isActive ? 89 : 70,
          );
          pdf.setFont("helvetica", "bold");
          pdf.text(
            user.isActive ? "Activo" : "Bloqueado",
            columns[5] + 1.5,
            y + 7,
          );
          y += rowHeight;
        });
        y += 7;
      };
      drawSection("GERENCIA", management);
      drawSection("SUPERVISORES", supervisors);
      const pages = pdf.getNumberOfPages();
      for (let page = 1; page <= pages; page += 1) {
        pdf.setPage(page);
        pdf.setFont("helvetica", "normal");
        pdf.setFontSize(7);
        pdf.setTextColor(91, 112, 128);
        pdf.text(
          `Generado ${new Date().toLocaleString("es-DO")} - Pagina ${page} de ${pages}`,
          pageWidth / 2,
          pageHeight - 6,
          { align: "center" },
        );
      }
      pdf.save(
        `listado-usuarios-gerencia-supervisores-${new Date().toISOString().slice(0, 10)}.pdf`,
      );
      setNotice("Listado de usuarios descargado en PDF.");
    } catch (exportError) {
      setError(
        (exportError as Error).message ||
          "No se pudo generar el PDF de usuarios.",
      );
    } finally {
      setExportingUsers(false);
    }
  };

  const downloadSeparatedUserPdfs = async () => {
    if (exportingUsers) return;
    setExportingUsers(true);
    setError("");
    try {
      const { management, supervisors } = userDirectoryGroups();
      const { jsPDF } = await import("jspdf");
      let realLogo: string | null = null;
      try {
        realLogo = await assetAsDataUrl("/loto-real-logo-transparent.png");
      } catch {
        /* conserva las marcas dibujadas */
      }
      const createDirectory = (
        title: string,
        subtitle: string,
        items: AdminUser[],
        filename: string,
      ) => {
        const pdf = new jsPDF({
          orientation: "landscape",
          unit: "mm",
          format: "letter",
          compress: true,
        });
        const width = pdf.internal.pageSize.getWidth();
        const height = pdf.internal.pageSize.getHeight();
        let y = 0;
        const header = () => {
          pdf.setFillColor(5, 29, 54);
          pdf.rect(0, 0, width, 31, "F");
          if (realLogo)
            pdf.addImage(realLogo, "PNG", 12, 4, 34, 22, undefined, "FAST");
          pdf.setTextColor(119, 216, 255);
          pdf.setFont("helvetica", "bold");
          pdf.setFontSize(12);
          pdf.text("Grupo Tejeda", 50, 12);
          pdf.setTextColor(255, 255, 255);
          pdf.setFontSize(14);
          pdf.text(title, 50, 21);
          y = 39;
          pdf.setTextColor(63, 87, 107);
          pdf.setFontSize(8);
          pdf.setFont("helvetica", "normal");
          pdf.text(subtitle, 12, y);
          y += 7;
        };
        const tableHeader = () => {
          pdf.setFillColor(220, 238, 248);
          pdf.rect(12, y, width - 24, 8, "F");
          pdf.setTextColor(31, 72, 101);
          pdf.setFont("helvetica", "bold");
          pdf.setFontSize(7);
          [
            "#",
            "NOMBRE / USUARIO",
            "REGION / CONTACTO",
            "GRUPOS Y AGENCIAS",
            "ESTADO",
          ].forEach((label, index) =>
            pdf.text(label, [14, 23, 92, 151, 245][index], y + 5.2),
          );
          y += 8;
        };
        header();
        tableHeader();
        items.forEach((user, index) => {
          if (y > height - 20) {
            pdf.addPage();
            header();
            tableHeader();
          }
          const row = 13;
          if (index % 2 === 0) {
            pdf.setFillColor(246, 250, 252);
            pdf.rect(12, y, width - 24, row, "F");
          }
          pdf.setDrawColor(200, 217, 228);
          pdf.line(12, y + row, width - 12, y + row);
          pdf.setFontSize(7.6);
          pdf.setTextColor(27, 54, 75);
          pdf.setFont("helvetica", "normal");
          pdf.text(String(index + 1), 15, y + 7.5);
          pdf.setFont("helvetica", "bold");
          pdf.text(
            pdf.splitTextToSize(user.displayName, 64).slice(0, 1),
            23,
            y + 5,
          );
          pdf.setFont("helvetica", "normal");
          pdf.setTextColor(88, 112, 130);
          pdf.text(usernameValue(user.email).toUpperCase(), 23, y + 10);
          pdf.setTextColor(27, 54, 75);
          pdf.text(
            pdf.splitTextToSize(user.region || "Sin region", 54).slice(0, 1),
            92,
            y + 5,
          );
          pdf.setTextColor(88, 112, 130);
          pdf.text(user.contact || "Sin contacto", 92, y + 10);
          pdf.setTextColor(27, 54, 75);
          pdf.text(
            `${user.groups.length} grupos - ${user.agencyCount} agencias`,
            151,
            y + 7.5,
          );
          pdf.setTextColor(
            user.isActive ? 21 : 170,
            user.isActive ? 125 : 55,
            user.isActive ? 88 : 65,
          );
          pdf.setFont("helvetica", "bold");
          pdf.text(user.isActive ? "Activo" : "Bloqueado", 245, y + 7.5);
          y += row;
        });
        if (!items.length) {
          pdf.setTextColor(90, 113, 130);
          pdf.text(
            "No se encontraron usuarios creados para este listado.",
            15,
            y + 9,
          );
        }
        const pages = pdf.getNumberOfPages();
        for (let page = 1; page <= pages; page++) {
          pdf.setPage(page);
          pdf.setTextColor(91, 112, 128);
          pdf.setFont("helvetica", "normal");
          pdf.setFontSize(7);
          pdf.text(
            `Grupo Tejeda - ${new Date().toLocaleDateString("es-DO")} - Pagina ${page} de ${pages}`,
            width / 2,
            height - 6,
            { align: "center" },
          );
        }
        pdf.save(filename);
      };
      const date = new Date().toISOString().slice(0, 10);
      createDirectory(
        "LISTADO DE GERENCIA",
        `Gerencia autorizada - ${management.length} usuarios`,
        management,
        `listado-gerencia-${date}.pdf`,
      );
      createDirectory(
        "LISTADO DE SUPERVISORES",
        `Supervisores registrados - ${supervisors.length} usuarios`,
        supervisors,
        `listado-supervisores-${date}.pdf`,
      );
      setNotice("Se descargaron dos PDF separados: Gerencia y Supervisores.");
    } catch (exportError) {
      setError(
        (exportError as Error).message ||
          "No se pudieron generar los PDF de usuarios.",
      );
    } finally {
      setExportingUsers(false);
    }
  };

  if (!stats)
    return <div className="loading-admin">Cargando Grupo Tejeda...</div>;
  const canManage = session.permissions.canManage;
  const canManageAudits = session.permissions.canManageAudits;
  const isGroupAdministrator = session.role === "GroupAdministrator";
  const normalizedUserQuery = userQuery
    .trim()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
  const visibleUsers = users.filter((user) =>
    `${user.displayName} ${user.email}`
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase()
      .includes(normalizedUserQuery),
  );
  const userDepartmentKey=(user:AdminUser):string=>user.role==="GROUP_ADMIN"?"SUPERVISORS":user.role==="ADMINISTRATIVE"?"ADMINISTRATIVE":user.role==="ADMINISTRATOR"||user.role==="VIEWER"||user.role==="FISCALIZADOR"?"ADMINISTRATION":assignedUserDepartment(user);
  const adminUserRoleLabel=(role:AdminUser["role"])=>({DEPARTMENT:"Usuario de departamento",ADMINISTRATIVE:"Administrativo",ADMINISTRATOR:"Administración",VIEWER:"Consulta general",GROUP_ADMIN:"Supervisión de grupos",FISCALIZADOR:"Fiscalización",TECHNOLOGY:"Tecnología",GENERAL_SERVICES:"Servicios Generales",HUMAN_RESOURCES:"Recursos Humanos",WAREHOUSE:"Almacén"})[role];
  const departmentVisibleUsers=visibleUsers.filter(user=>user.id===selectedUserId||!userDepartmentFilter||userDepartmentKey(user)===userDepartmentFilter);
  const userDepartmentCards=([
    ["ADMINISTRATION","Administración","Administradores, fiscalizadores y consulta",UserCog],
    ["SUPERVISORS","Supervisores","Responsables de grupos y agencias",UsersRound],
    ["TECHNOLOGY","Tecnología","Soporte y técnicos de Tecnología",LifeBuoy],
    ["GENERAL_SERVICES","Servicios Generales","Soporte y técnicos operativos",Building2],
    ["HUMAN_RESOURCES","Recursos Humanos","Equipo de gestión humana",UserRound],
    ["WAREHOUSE","Almacén","Inventario, solicitudes y despacho",PackageSearch],
    ...userDepartments.filter(([code])=>!['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','WAREHOUSE'].includes(code)).map(([code,name])=>[code,name,'Usuarios del departamento',Building2] as const),
  ] as const).map(([key,title,description,Icon])=>({key,title,description,Icon,total:visibleUsers.filter(user=>userDepartmentKey(user)===key).length}));
  const normalizedQuery = query.trim().toLowerCase();
  const visibleGroups = stats.groups.filter(
    (group) =>
      group.grupo.toLowerCase().includes(normalizedQuery) &&
      (filter === "all" ||
        (filter === "complete" && group.percent === 100) ||
        (filter === "progress" && group.percent > 0 && group.percent < 100) ||
        (filter === "none" && group.percent === 0)),
  );
  const visibleAgencies = (stats.agencies || []).filter(
    (agency) =>
      (!normalizedQuery ||
        `${agency.codigo} ${agency.terminal} ${agency.grupo}`
          .toLowerCase()
          .includes(normalizedQuery)) &&
      (filter === "all" ||
        (filter === "complete" && agency.status === "COMPLETED") ||
        (filter === "progress" && agency.status === "REVIEW_REQUIRED") ||
        (filter === "none" && agency.status === "PENDING")),
  );
  const visibleAdministrators = (stats.administrators || []).filter(
    (administrator) =>
      (!normalizedQuery ||
        `${administrator.displayName} ${administrator.email} ${administrator.region || ""}`
          .toLowerCase()
          .includes(normalizedQuery)) &&
      (filter === "all" ||
        (filter === "complete" && administrator.percent === 100) ||
        (filter === "progress" &&
          administrator.percent > 0 &&
          administrator.percent < 100) ||
        (filter === "none" && administrator.percent === 0)),
  );
  const visibleAudits = (stats.audits || []).filter(
    (audit) =>
      !normalizedQuery ||
      `${audit.codigo} ${audit.terminal} ${audit.grupo} ${audit.employeeName || ""} ${audit.submittedByLogin || ""} ${audit.municipio} ${audit.provincia}`
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .toLowerCase()
        .includes(
          normalizedQuery.normalize("NFD").replace(/[\u0300-\u036f]/g, ""),
        ),
  );
  const auditGroups = Array.from(
    new Set(visibleAudits.map((audit) => audit.grupo)),
  ).sort((a, b) => a.localeCompare(b, "es"));
  const selectedExportGroup = auditGroups.includes(exportGroup)
    ? exportGroup
    : "";
  const displayedAudits = selectedExportGroup
    ? visibleAudits.filter((audit) => audit.grupo === selectedExportGroup)
    : visibleAudits;
  const auditAgencyCards = Array.from(
    displayedAudits.reduce((groups, audit) => {
      const key = `${audit.grupo}\u0000${audit.codigo}`;
      const current = groups.get(key) || [];
      current.push(audit);
      groups.set(key, current);
      return groups;
    }, new globalThis.Map<string, Audit[]>()),
  )
    .map(([, audits]) => ({
      codigo: audits[0].codigo,
      terminal: audits[0].terminal,
      grupo: audits[0].grupo,
      audits: [...audits].sort(
        (left, right) =>
          deviceDate(left.submittedAt).getTime() -
          deviceDate(right.submittedAt).getTime(),
      ),
    }))
    .sort((left, right) =>
      `${left.grupo} ${left.terminal}`.localeCompare(
        `${right.grupo} ${right.terminal}`,
        "es",
      ),
    );
  const selectedAuditSequence = selectedAudit
    ? [...(stats.audits || [])]
        .filter(
          (audit) =>
            audit.codigo === selectedAudit.codigo &&
            audit.grupo === selectedAudit.grupo,
        )
        .sort(
          (left, right) =>
            deviceDate(left.submittedAt).getTime() -
            deviceDate(right.submittedAt).getTime(),
        )
        .findIndex((audit) => audit.profileId === selectedAudit.profileId)
    : -1;

  const openEvidencePhoto = async (photo: { url: string; label: string }) => {
    setLightboxPhoto({ url: "", label: photo.label });
    try {
      const repaired = await prepareEvidence(`${photo.url}?v=${Date.now()}`);
      setLightboxPhoto({ url: repaired.data, label: photo.label });
    } catch {
      setLightboxPhoto(null);
      setError(
        "Esta fotografía está dañada y no puede ampliarse. Las nuevas capturas se guardarán corregidas.",
      );
    }
  };

  const exportAudits = async () => {
    if (!session.permissions.canExportAudits) {
      setError("Tu cuenta no tiene permiso para descargar expedientes PDF.");
      return;
    }
    if (!selectedExportGroup) {
      setError(
        "Selecciona un grupo para confirmar las agencias que se incluirán en el PDF.",
      );
      return;
    }
    if (!displayedAudits.length) {
      setError("No hay auditorías para exportar con el filtro actual.");
      return;
    }
    setExportingAudits(true);
    setError("");
    try {
      const targetGroup = selectedExportGroup;
      const auditsToExport = visibleAudits.filter(
        (audit) => audit.grupo === targetGroup,
      );
      const details = await mapWithConcurrency(auditsToExport, 4, async (audit, index) => {
          setNotice(
            `Cargando auditoría ${index + 1} de ${auditsToExport.length}: ${audit.codigo}`,
          );
          const response = await fetch(`/api/admin/audits/${audit.profileId}`);
          const detail = await response.json().catch(() => ({}));
          if (!response.ok)
            throw new Error(
              detail.error || `No se pudo exportar ${audit.codigo}.`,
            );
          return { ...audit, ...detail, grupo: audit.grupo } as Audit;
        });
      const answerText = (value?: string) =>
        value === "yes"
          ? "Sí"
          : value === "no"
            ? "No"
            : value === "not_applicable"
              ? "No aplica"
              : value === "cash_register"
                ? "Caja registradora"
                : value === "wooden_box"
                  ? "Caja de madera"
                  : value === "none"
                    ? "Ninguna"
              : "Sin respuesta";
      const { jsPDF } = await import("jspdf");
      let pdfLogo: string | null = null;
      try {
        pdfLogo = await assetAsDataUrl("/loto-real-logo-transparent.png");
      } catch {
        /* El texto de marca permanece disponible. */
      }
      const groups = new globalThis.Map<string, Audit[]>();
      details.forEach((audit) =>
        groups.set(audit.grupo, [...(groups.get(audit.grupo) || []), audit]),
      );
      let generatedPdf: InstanceType<typeof jsPDF> | null = null;
      for (const [_groupName, audits] of groups) {
        const pdf = new jsPDF({ unit: "mm", format: "a4", compress: true });
        let firstAudit = true;
        for (const audit of audits) {
          if (!firstAudit) pdf.addPage();
          firstAudit = false;
          pdf.setFillColor(5, 29, 54);
          pdf.rect(0, 0, 210, 32, "F");
          if (pdfLogo)
            pdf.addImage(pdfLogo, "PNG", 14, 5, 26, 16, undefined, "FAST");
          pdf.setTextColor(105, 216, 255);
          pdf.setFont("helvetica", "bold");
          pdf.setFontSize(17);
          pdf.text("GRUPO TEJEDA", 46, 12);
          pdf.setTextColor(255, 255, 255);
          pdf.setFontSize(9);
          pdf.text(
            `AUDITORÍA COMPLETADA | ${targetGroup} | ${audit.codigo}`,
            46,
            20,
          );
          pdf.setTextColor(20, 45, 68);
          pdf.setFontSize(13);
          pdf.text(audit.terminal, 14, 42, { maxWidth: 182 });
          pdf.setFont("helvetica", "normal");
          pdf.setFontSize(9);
          const summary = [
            `Empleado: ${audit.employeeName || "Sin nombre"}${audit.employeeCode ? ` (${audit.employeeCode})` : ""}`,
            `Login auditor: ${audit.submittedByLogin ? usernameValue(audit.submittedByLogin) : "No registrado en auditorías anteriores"}`,
            `Dirección: ${audit.direccion || "Sin dirección"}`,
            `Sector / Municipio / Provincia: ${audit.sector || "-"} / ${audit.municipio} / ${audit.provincia}`,
            `Coordenadas: ${audit.latitude.toFixed(6)}, ${audit.longitude.toFixed(6)} | Precisión: ±${Math.round(audit.accuracy)} m`,
            `Fecha: ${deviceDate(audit.submittedAt).toLocaleString("es-DO")}`,
          ];
          let y = 51;
          summary.forEach((line) => {
            const wrapped = pdf.splitTextToSize(line, 182);
            pdf.text(wrapped, 14, y);
            y += wrapped.length * 4.5;
          });
          pdf.setFillColor(232, 245, 252);
          pdf.roundedRect(12, y + 1, 186, 8, 2, 2, "F");
          pdf.setFont("helvetica", "bold");
          pdf.text("RESPUESTAS DEL FORMULARIO", 16, y + 6.5);
          y += 15;
          pdf.setFontSize(8.5);
          answerLabels.forEach(([key, label], index) => {
            if (y > 275) {
              pdf.addPage();
              y = 16;
            }
            pdf.setFont("helvetica", "bold");
            pdf.text(`${index + 1}.`, 14, y);
            pdf.setFont("helvetica", "normal");
            const wrapped = pdf.splitTextToSize(label, 145);
            pdf.text(wrapped, 21, y);
            pdf.setFont("helvetica", "bold");
            pdf.text(answerText(audit.answers?.[key]), 178, y, {
              align: "right",
            });
            y += Math.max(6, wrapped.length * 4.2);
          });
          if (audit.observations) {
            if (y > 255) {
              pdf.addPage();
              y = 16;
            }
            pdf.setFont("helvetica", "bold");
            pdf.text("Observaciones:", 14, y);
            y += 5;
            pdf.setFont("helvetica", "normal");
            const observation = pdf.splitTextToSize(audit.observations, 180);
            pdf.text(observation, 14, y);
            y += observation.length * 4.2 + 5;
          }
          const preparedPhotos = await mapWithConcurrency(audit.photos || [], 3, async (photo) => {
              try {
                return { photo, jpeg: await prepareEvidence(photo.url) };
              } catch {
                return { photo, jpeg: null };
              }
            });
          for (const prepared of preparedPhotos) {
            const { photo } = prepared;
            if (y > 205) {
              pdf.addPage();
              y = 16;
            }
            if (!prepared.jpeg) {
              pdf.setTextColor(170, 45, 65);
              pdf.text(
                "La evidencia no pudo incorporarse porque el archivo está dañado.",
                14,
                y + 8,
              );
              y += 16;
              continue;
            }
            const jpeg = prepared.jpeg;
            const maximumWidth = 182;
            const maximumHeight = 118;
            const ratio = jpeg.width / jpeg.height;
            let photoWidth = maximumWidth;
            let photoHeight = photoWidth / ratio;
            if (photoHeight > maximumHeight) {
              photoHeight = maximumHeight;
              photoWidth = photoHeight * ratio;
            }
            if (y + photoHeight + 28 > 282) {
              pdf.addPage();
              y = 16;
            }
            pdf.setFont("helvetica", "bold");
            pdf.setFontSize(9);
            pdf.text(
              photoLabels[photo.photoType] || `Evidencia ${photo.photoType}`,
              14,
              y,
            );
            const photoX = 14 + (maximumWidth - photoWidth) / 2;
            pdf.addImage(
              jpeg.data,
              "JPEG",
              photoX,
              y + 4,
              photoWidth,
              photoHeight,
              undefined,
              "FAST",
            );
            pdf.setFont("helvetica", "normal");
            pdf.setFontSize(8);
            pdf.text(
              `${photo.latitude.toFixed(6)}, ${photo.longitude.toFixed(6)} | Precisión: ±${Math.round(photo.accuracy)} m | ${deviceDate(photo.capturedAt).toLocaleString("es-DO")}`,
              105,
              y + photoHeight + 10,
              { align: "center" },
            );
            y += photoHeight + 18;
          }
        }
        const pages = pdf.getNumberOfPages();
        for (let page = 1; page <= pages; page += 1) {
          pdf.setPage(page);
          pdf.setFontSize(7);
          pdf.setTextColor(90, 110, 128);
          pdf.text(
            `Grupo Tejeda | ${targetGroup} | Página ${page} de ${pages}`,
            105,
            291,
            { align: "center" },
          );
        }
        generatedPdf = pdf;
      }
      if (!generatedPdf)
        throw new Error("No se pudo generar el PDF del grupo seleccionado.");
      const url = URL.createObjectURL(generatedPdf.output("blob"));
      const link = document.createElement("a");
      link.href = url;
      link.download = `${safeFileName(targetGroup)}-auditorias-${new Date().toISOString().slice(0, 10)}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      setNotice(
        `Se descargó el PDF de ${targetGroup} con ${details.length} auditorías y sus fotografías.`,
      );
    } catch (exportError) {
      setError(
        (exportError as Error).message ||
          "No fue posible exportar las auditorías.",
      );
    } finally {
      setExportingAudits(false);
    }
  };
  const agenciesPerPage = 50;
  const agencyPages = Math.max(
    1,
    Math.ceil(visibleAgencies.length / agenciesPerPage),
  );
  const pagedAgencies = visibleAgencies.slice(
    (Math.min(agencyPage, agencyPages) - 1) * agenciesPerPage,
    Math.min(agencyPage, agencyPages) * agenciesPerPage,
  );
  const latest = stats.audits[0];
  const mapUrl = latest
    ? `https://www.openstreetmap.org/export/embed.html?bbox=${latest.longitude - 0.12}%2C${latest.latitude - 0.08}%2C${latest.longitude + 0.12}%2C${latest.latitude + 0.08}&layer=mapnik&marker=${latest.latitude}%2C${latest.longitude}`
    : "";

  return (
    usersOnly ? <UsersPermissionsWorkspace session={session} onClose={onCloseUsers}/> : <div className="atlas-admin">
      <header className="atlas-admin-hero">
        <div>
          <span>GRUPO TEJEDA · CENTRO DE CONTROL</span>
          <h1>Panel administrativo</h1>
          <p>
            Estado operativo, avance por grupos y trazabilidad geográfica de las
            auditorías.
          </p>
        </div>
        <div className="admin-hero-actions">
          <div
            className={`role-chip ${canManage ? "role-admin" : "role-viewer"}`}
          >
            {session.avatarUrl ? (
              <img src={session.avatarUrl} alt={session.displayName} />
            ) : (
              <ShieldCheck />
            )}
            <span>
              <small>{roleLabel(session.role)}</small>
              <strong>{session.displayName}</strong>
            </span>
          </div>
        </div>
      </header>
      {!canManage && (
        <div className="permission-banner">
          <Eye />
          <div>
            <strong>
              {session.role === "GroupAdministrator"
                ? "Vista protegida de tus grupos"
                : "Acceso de consulta"}
            </strong>
            <span>
              {session.role === "GroupAdministrator"
                ? "El servidor filtra automáticamente grupos, agencias, fotografías, formularios y estadísticas asignadas a tu cuenta."
                : "Puedes ver estadísticas, mapas, fotografías y respuestas. La edición y eliminación están bloqueadas por el servidor."}
            </span>
          </div>
        </div>
      )}
      {notice && (
        <div className="admin-notice">
          <CheckCircle2 /> {notice}
          <button onClick={() => setNotice("")} aria-label="Cerrar aviso">
            <X />
          </button>
        </div>
      )}
      {error && <div className="alert admin-global-alert">{error}</div>}

      <div className="atlas-admin-stats">
        <article className="atlas-global">
          <div
            className="global-ring"
            style={
              { "--admin-progress": `${stats.percent}%` } as React.CSSProperties
            }
          >
            <strong>{stats.percent}%</strong>
          </div>
          <div>
            <span>Avance Global</span>
            <small>de agencias auditadas</small>
          </div>
          <BarChart3 />
        </article>
        <article>
          <Clock3 />
          <div>
            <span>Pendientes</span>
            <strong>{stats.pending}</strong>
            <small>Esperando auditoría</small>
          </div>
        </article>
        <article>
          <CheckCircle2 />
          <div>
            <span>Completadas</span>
            <strong>{stats.completed}</strong>
            <small>Con evidencia digital</small>
          </div>
        </article>
        <article>
          <Building2 />
          <div>
            <span>Grupos Totales</span>
            <strong>{stats.groupsTotal}</strong>
            <small>Unidades corporativas</small>
          </div>
        </article>
        <article>
          <UsersRound />
          <div>
            <span>Grupos Listos</span>
            <strong>{stats.groupsReady}</strong>
            <small>Completados al 100%</small>
          </div>
        </article>
      </div>

      <section className="admin-atlas-overview">
        <div className="admin-map-panel">
          <div className="admin-map-title">
            <span>
              <Map /> Atlas de evidencias
            </span>
            <small>Último levantamiento registrado</small>
          </div>
          {latest ? (
            <iframe title="Mapa administrativo" src={mapUrl} loading="lazy" />
          ) : (
            <div className="admin-map-empty">
              <MapPin /> Sin auditorías todavía
            </div>
          )}
          <div className="admin-map-caption">
            <MapPin />
            <div>
              <strong>{latest?.terminal || "Esperando evidencia"}</strong>
              <small>
                {latest
                  ? `${latest.latitude.toFixed(5)}, ${latest.longitude.toFixed(5)} · ±${Math.round(latest.accuracy)} m`
                  : "La primera ubicación aparecerá aquí"}
              </small>
            </div>
          </div>
        </div>
        <div className="admin-readiness">
          <div>
            <span>
              <Layers3 /> Cobertura corporativa
            </span>
            <strong>
              {stats.groupsReady} de {stats.groupsTotal}
            </strong>
            <small>grupos listos</small>
          </div>
          <div className="readiness-bar">
            <i
              style={{
                width: `${stats.groupsTotal ? Math.round((stats.groupsReady * 100) / stats.groupsTotal) : 0}%`,
              }}
            />
          </div>
          <ul>
            <li>
              <span>Auditorías completadas</span>
              <strong>{stats.completed}</strong>
            </li>
            <li>
              <span>Con evidencia pendiente</span>
              <strong>{stats.pending}</strong>
            </li>
            <li>
              <span>En revisión</span>
              <strong>{stats.review}</strong>
            </li>
          </ul>
        </div>
      </section>

      <section className="atlas-admin-section admin-tabbed-section">
        <div className="atlas-section-head">
          <div>
            <span>ANÁLISIS Y EVIDENCIA</span>
            <h2>
              {isGroupAdministrator
                ? "Mis agencias asignadas y evidencias"
                : "Avance operativo y auditorías"}
            </h2>
            <p>
              {isGroupAdministrator
                ? "Consulta el avance de tus agencias y abre sus evidencias georreferenciadas."
                : "Consulta responsables, grupos y agencias o abre el expediente completo del empleado."}
            </p>
          </div>
          <label>
            <Search />
            <input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={
                sectionTab === "audits"
                  ? "Buscar auditoría, agencia, empleado o ubicación..."
                  : sectionTab === "groups"
                    ? "Buscar grupo..."
                    : sectionTab === "administrators"
                      ? "Buscar administrador..."
                      : "Buscar código, agencia o grupo..."
              }
            />
          </label>
        </div>
        <div className="admin-section-tabs">
          {isGroupAdministrator ? (
            <button
              className={sectionTab === "agencies" ? "active" : ""}
              onClick={() => setSectionTab("agencies")}
            >
              <Building2 /> Avance por agencias asignadas{" "}
              <strong>{stats.agencies.length}</strong>
            </button>
          ) : (
            <button
              className={sectionTab === "groups" ? "active" : ""}
              onClick={() => setSectionTab("groups")}
            >
              <BarChart3 /> Avance por grupos{" "}
              <strong>{stats.groups.length}</strong>
            </button>
          )}
          <button
            className={sectionTab === "audits" ? "active" : ""}
            onClick={() => setSectionTab("audits")}
          >
            <ClipboardCheck />{" "}
            {isGroupAdministrator ? "Evidencias" : "Auditorías completadas"}{" "}
            <strong>{stats.auditsTotal}</strong>
          </button>
        </div>
        {sectionTab === "groups" && (
          <>
            <div className="atlas-filter-tabs">
              {[
                ["all", "Todos"],
                ["complete", "Completados"],
                ["progress", "En progreso"],
                ["none", "Sin iniciar"],
              ].map((item) => (
                <button
                  className={filter === item[0] ? "active" : ""}
                  onClick={() => setFilter(item[0])}
                  key={item[0]}
                >
                  {item[1]}
                </button>
              ))}
            </div>
            <div className="admin-table group-progress-table">
              <div className="admin-tr admin-th">
                <span>Grupo económico</span>
                <span>Esperadas</span>
                <span>Completadas</span>
                <span>Levantamientos</span>
                <span>Pendientes</span>
                <span>% Avance</span>
                <span>Última actualización</span>
              </div>
              {visibleGroups.map((group) => (
                <div className="admin-tr" key={group.grupo}>
                  <strong>{group.grupo}</strong>
                  <span>{group.total}</span>
                  <span>{group.completed}</span>
                  <span className="group-audit-count" title={`${group.auditCount || 0} auditorías individuales registradas`}>
                    {group.completedRounds || 0}/{group.roundsTotal || 1}
                  </span>
                  <span>{group.pending}</span>
                  <div className="percent-cell">
                    <i>
                      <b style={{ width: `${group.percent}%` }} />
                    </i>
                    <em>{group.percent}%</em>
                  </div>
                  <span>
                    {group.lastUpdated
                      ? deviceDate(group.lastUpdated).toLocaleString("es-DO")
                      : "Sin registros"}
                  </span>
                </div>
              ))}
            </div>
          </>
        )}
        {sectionTab === "agencies" && (
          <>
            <div className="atlas-filter-tabs">
              {[
                ["all", "Todas"],
                ["complete", "Completadas"],
                ["progress", "En revisión"],
                ["none", "Pendientes"],
              ].map((item) => (
                <button
                  className={filter === item[0] ? "active" : ""}
                  onClick={() => setFilter(item[0])}
                  key={item[0]}
                >
                  {item[1]}
                </button>
              ))}
            </div>
            <div className="admin-table agency-progress-table">
              <div className="admin-tr admin-th">
                <span>Código / Agencia</span>
                <span>Grupo</span>
                <span>Estado</span>
                <span>Formulario</span>
                <span>% Avance</span>
                <span>Última actualización</span>
              </div>
              {pagedAgencies.map((agency) => (
                <div className="admin-tr" key={agency.codigo}>
                  <span className="agency-identity">
                    <strong>{agency.codigo}</strong>
                    <small>{agency.terminal}</small>
                  </span>
                  <span>{agency.grupo}</span>
                  <span
                    className={`agency-status ${agency.status.toLowerCase()}`}
                  >
                    {agency.status === "COMPLETED"
                      ? "Completada"
                      : agency.status === "REVIEW_REQUIRED"
                        ? "En revisión"
                        : "Pendiente"}
                  </span>
                  <span>
                    {agency.status === "COMPLETED"
                      ? `${agency.auditCount || 1} ${agency.auditCount === 1 ? "auditoría realizada" : "auditorías realizadas"}`
                      : "Sin completar"}
                  </span>
                  <div className="percent-cell">
                    <i>
                      <b style={{ width: `${agency.percent}%` }} />
                    </i>
                    <em>{agency.percent}%</em>
                  </div>
                  <span>
                    {agency.lastUpdated
                      ? deviceDate(agency.lastUpdated).toLocaleString("es-DO")
                      : "Sin registros"}
                  </span>
                </div>
              ))}
            </div>
            <div className="agency-pagination">
              <span>
                Mostrando{" "}
                {visibleAgencies.length
                  ? (Math.min(agencyPage, agencyPages) - 1) * agenciesPerPage +
                    1
                  : 0}
                –
                {Math.min(
                  Math.min(agencyPage, agencyPages) * agenciesPerPage,
                  visibleAgencies.length,
                )}{" "}
                de {visibleAgencies.length} agencias
              </span>
              <div>
                <button
                  disabled={agencyPage <= 1}
                  onClick={() => setAgencyPage((page) => Math.max(1, page - 1))}
                >
                  Anterior
                </button>
                <strong>
                  Página {Math.min(agencyPage, agencyPages)} de {agencyPages}
                </strong>
                <button
                  disabled={agencyPage >= agencyPages}
                  onClick={() =>
                    setAgencyPage((page) => Math.min(agencyPages, page + 1))
                  }
                >
                  Siguiente
                </button>
              </div>
            </div>
          </>
        )}
        {sectionTab === "administrators" && canManage && (
          <>
            <div className="atlas-filter-tabs">
              {[
                ["all", "Todos"],
                ["complete", "100% completado"],
                ["progress", "En progreso"],
                ["none", "Sin iniciar"],
              ].map((item) => (
                <button
                  className={filter === item[0] ? "active" : ""}
                  onClick={() => setFilter(item[0])}
                  key={item[0]}
                >
                  {item[1]}
                </button>
              ))}
            </div>
            <div className="admin-table administrator-progress-table">
              <div className="admin-tr admin-th">
                <span>Administrador</span>
                <span>Región / Grupos</span>
                <span>Agencias</span>
                <span>Completadas</span>
                <span>Pendientes</span>
                <span>% Avance</span>
              </div>
              {visibleAdministrators.map((administrator) => (
                <div className="admin-tr" key={administrator.id}>
                  <span className="administrator-identity">
                    <strong>{administrator.displayName}</strong>
                    <small>@{usernameValue(administrator.email)}</small>
                  </span>
                  <span>
                    {administrator.region || "Sin región"}
                    <small>{administrator.groups} grupos asignados</small>
                  </span>
                  <span>{administrator.total}</span>
                  <span className="administrator-completed">
                    {administrator.completed}
                  </span>
                  <span>{administrator.pending + administrator.review}</span>
                  <div className="percent-cell">
                    <i>
                      <b style={{ width: `${administrator.percent}%` }} />
                    </i>
                    <em>{administrator.percent}%</em>
                    <small>
                      {administrator.lastUpdated
                        ? deviceDate(administrator.lastUpdated).toLocaleString(
                            "es-DO",
                          )
                        : "Sin registros"}
                    </small>
                  </div>
                </div>
              ))}
            </div>
          </>
        )}
        {sectionTab === "audits" && (
          <div className="audit-results">
            <div className="audit-export-bar">
              <span>
                <strong>{displayedAudits.length}</strong> auditorías autorizadas
              </span>
              <label>
                <span>Filtrar y exportar grupo</span>
                <select
                  value={selectedExportGroup}
                  onChange={(event) => setExportGroup(event.target.value)}
                  disabled={exportingAudits || !auditGroups.length}
                >
                  <option value="">Todos los grupos</option>
                  {auditGroups.map((group) => (
                    <option key={group} value={group}>
                      {group}
                    </option>
                  ))}
                </select>
              </label>
              {session.permissions.canExportAudits ? (
                <button
                  type="button"
                  onClick={() => void exportAudits()}
                  disabled={
                    exportingAudits ||
                    !selectedExportGroup ||
                    !displayedAudits.length
                  }
                >
                  <Download />{" "}
                  {exportingAudits
                    ? "Creando PDF..."
                    : "Descargar PDF del grupo"}
                </button>
              ) : (
                <small>
                  Descarga disponible para Fiscalizador y Administrador
                  principal.
                </small>
              )}
            </div>
            <div className="audit-agency-grid">
              {auditAgencyCards.map((agency) => (
                <article
                  className="audit-agency-card"
                  key={`${agency.grupo}-${agency.codigo}`}
                >
                  <header>
                    <span className="audit-agency-icon"><Building2 /></span>
                    <div>
                      <small>{agency.grupo}</small>
                      <strong>{agency.terminal}</strong>
                      <span>Código {agency.codigo}</span>
                    </div>
                    <em>
                      {agency.audits.length}{" "}
                      {agency.audits.length === 1
                        ? "auditoría realizada"
                        : "auditorías realizadas"}
                    </em>
                  </header>
                  <div className="audit-lift-list">
                    {agency.audits.map((audit, index) => (
                      <button
                        type="button"
                        key={audit.profileId}
                        onClick={() => void openAudit(audit)}
                        disabled={auditLoading}
                      >
                        <span className="audit-lift-number">{index + 1}</span>
                        <span>
                          <strong>{auditOrdinal(index)}</strong>
                          <small>
                            {deviceDate(audit.submittedAt).toLocaleString("es-DO")}
                          </small>
                          <small className="audit-authorized-status"><CheckCircle2 /> Autorizada</small>
                          <small>
                            {audit.employeeName || "Empleado no registrado"} ·{" "}
                            {audit.submittedByLogin
                              ? `@${usernameValue(audit.submittedByLogin)}`
                              : "Auditor no registrado"}
                          </small>
                        </span>
                        <Eye />
                      </button>
                    ))}
                  </div>
                </article>
              ))}
              {!auditAgencyCards.length && (
                <div className="audit-cards-empty">
                  <ClipboardCheck />
                  <strong>No hay levantamientos con este filtro.</strong>
                </div>
              )}
            </div>
          </div>
        )}
      </section>

      {selectedAudit && (
        <div
          className="audit-modal-backdrop"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              setSelectedAudit(null);
              setEditDraft(null);
            }
          }}
        >
          <section
            className="audit-detail-modal"
            role="dialog"
            aria-modal="true"
            aria-label={`Auditoría ${selectedAudit.codigo}`}
          >
            <header>
              <div>
                <span>{selectedAuditSequence >= 0 ? auditOrdinal(selectedAuditSequence).toUpperCase() : "LEVANTAMIENTO"} · EXPEDIENTE DIGITAL COMPLETO</span>
                <h2>{selectedAudit.terminal}</h2>
                <p>
                  {selectedAudit.codigo} · {selectedAudit.grupo}
                </p>
              </div>
              <div className="audit-header-actions">
                {canManageAudits && !editDraft && (
                  <>
                    <button
                      className="edit-action"
                      onClick={() =>
                        setEditDraft({
                          ...selectedAudit,
                          answers: { ...selectedAudit.answers },
                        })
                      }
                      aria-label="Editar auditoría"
                    >
                      <Edit3 />
                    </button>
                    <button
                      className="delete-action"
                      onClick={() => void deleteAudit()}
                      aria-label="Eliminar auditoría"
                    >
                      <Trash2 />
                    </button>
                  </>
                )}
                <button
                  onClick={() => {
                    setSelectedAudit(null);
                    setEditDraft(null);
                  }}
                  aria-label="Cerrar"
                >
                  <X />
                </button>
              </div>
            </header>
            {editDraft ? (
              <AuditEditor
                draft={editDraft}
                setDraft={setEditDraft}
                saving={auditLoading}
                onSave={() => void saveAudit()}
                onCancel={() => setEditDraft(null)}
              />
            ) : (
              <AuditViewer
                audit={selectedAudit}
                onPhotoClick={(photo) => void openEvidencePhoto(photo)}
              />
            )}
            {lightboxPhoto && (
              <div
                className="evidence-lightbox"
                role="dialog"
                aria-modal="true"
                aria-label={lightboxPhoto.label}
                onClick={() => setLightboxPhoto(null)}
              >
                <button
                  type="button"
                  onClick={() => setLightboxPhoto(null)}
                  aria-label="Cerrar imagen"
                >
                  <X />
                </button>
                {lightboxPhoto.url ? (
                  <img
                    src={lightboxPhoto.url}
                    alt={lightboxPhoto.label}
                    onClick={(event) => event.stopPropagation()}
                  />
                ) : (
                  <div className="evidence-lightbox-loading">
                    <Clock3 /> Preparando imagen...
                  </div>
                )}
                <strong>{lightboxPhoto.label}</strong>
              </div>
            )}
          </section>
        </div>
      )}

      {userManagerOpen && (
        <div
          className="audit-modal-backdrop"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget&&!usersOnly) closeUsers();
          }}
        >
          <section
            className={`user-manager-modal scoped-users-modal ${userDepartmentFilter ? "mobile-department-open" : ""} ${selectedUserId ? "mobile-user-open" : ""}`}
            role="dialog"
            aria-modal="true"
            aria-label="Usuarios y permisos"
          >
            <header>
              <div>
                <span>SEGURIDAD, REGIONES Y ALCANCE</span>
                <h2>Usuarios y permisos</h2>
                <p>
                  Selecciona un departamento, abre un usuario y administra su información, permisos y grupos visibles.
                </p>
              </div>
              <button
                onClick={closeUsers}
                aria-label="Cerrar"
              >
                <X />
              </button>
            </header>
            {usersOnly&&error&&<p role="alert">{error}</p>}
            {usersOnly&&notice&&<p role="status">{notice}</p>}
            <div className="admin-search-topbar">
              <div className="admin-search-copy">
                <Search />
                <span>
                  <strong>Buscar usuario</strong>
                  <small>
                    Encuentra rápidamente una cuenta antes de editarla.
                  </small>
                </span>
              </div>
              <label className="user-manager-search">
                <Search />
                <input
                  type="search"
                  value={userQuery}
                  onChange={(event) => setUserQuery(event.target.value)}
                  placeholder="Buscar por nombre, apellido o usuario..."
                  autoFocus
                />
                {userQuery && (
                  <button
                    type="button"
                    onClick={() => setUserQuery("")}
                    aria-label="Limpiar búsqueda"
                  >
                    <X />
                  </button>
                )}
                <small>
                  {visibleUsers.length} de {users.length}
                </small>
              </label>
              <div className="user-manager-actions">
                <button
                  type="button"
                  className="download-user-pdf-button"
                  onClick={() => void downloadSeparatedUserPdfs()}
                  disabled={exportingUsers}
                >
                  {exportingUsers ? (
                    <LoaderCircle className="spin" />
                  ) : (
                    <Download />
                  )}
                  <span>
                    <strong>
                      {exportingUsers ? "Generando PDF..." : "Descargar 2 PDF"}
                    </strong>
                    <small>Gerencia y Supervisores</small>
                  </span>
                </button>
                <button
                  type="button"
                  className="print-user-list-button"
                  onClick={printUserDirectory}
                >
                  <Printer />
                  <span>
                    <strong>Imprimir listado</strong>
                    <small>Gerencia y supervisores</small>
                  </span>
                </button>
                <button
                  type="button"
                  className="open-new-user-button"
                  onClick={() => setNewUserOpen(true)}
                >
                  <UserPlus />
                  <span>
                    <strong>Agregar usuario</strong>
                    <small>Nueva cuenta</small>
                  </span>
                </button>
              </div>
            </div>
            {newUserOpen && (
              <div
                className="new-user-modal-backdrop"
                role="presentation"
                onMouseDown={(event) => {
                  if (event.target === event.currentTarget)
                    setNewUserOpen(false);
                }}
              >
                <section
                  className="new-user-dialog"
                  role="dialog"
                  aria-modal="true"
                  aria-label="Agregar nuevo usuario"
                >
                  <header className="new-user-dialog-header">
                    <div>
                      <span>NUEVA CUENTA CORPORATIVA</span>
                      <h3>Agregar nuevo usuario</h3>
                      <p>
                        Configura su identidad, permisos y grupos asignados.
                      </p>
                    </div>
                    <button
                      type="button"
                      onClick={() => setNewUserOpen(false)}
                      aria-label="Cerrar formulario"
                    >
                      <X />
                    </button>
                  </header>
                  <form
                    className="new-user-form scoped-new-user"
                    onSubmit={createUser}
                  >
                    <div className="new-user-intro">
                      <div className="new-user-intro-icon">
                        <UserPlus />
                      </div>
                      <span>
                        <small>NUEVA CUENTA CORPORATIVA</small>
                        <strong>Agregar administrador</strong>
                        <p>Define su identidad, acceso y alcance operativo.</p>
                      </span>
                      <em>Acceso protegido</em>
                    </div>
                    <section className="new-user-block">
                      <header>
                        <UserRound />
                        <span>
                          <strong>Identidad y credenciales</strong>
                          <small>
                            Información utilizada para iniciar sesión.
                          </small>
                        </span>
                      </header>
                      <div className="new-user-field-grid">
                        <label>
                          Nombre y apellido
                          <input
                            type="text"
                            required
                            placeholder="Ej. María Rodríguez"
                            value={newUser.displayName}
                            onChange={(event) =>
                              setNewUser({
                                ...newUser,
                                displayName: event.target.value,
                              })
                            }
                          />
                        </label>
                        <label>
                          Usuario
                          <div className="username-field">
                            <UserRound />
                            <input
                              type="text"
                              required
                              pattern="[a-z0-9]+(?:\.[a-z0-9]+)+"
                              title="Usa el formato nombre.apellido"
                              placeholder="nombre.apellido"
                              value={usernameValue(newUser.email)}
                              onChange={(event) =>
                                setNewUser({
                                  ...newUser,
                                  email: usernameEmail(event.target.value),
                                })
                              }
                            />
                          </div>
                          <small className="field-help">
                            Formato obligatorio: nombre.apellido
                          </small>
                        </label>
                        <label>
                          Contraseña temporal
                          <div className="password-field">
                            <KeyRound />
                            <input
                              type="password"
                              required
                              minLength={6}
                              readOnly
                              value={newUser.password}
                            />
                          </div>
                          <small className="field-help">
                            Contraseña inicial: 123456. Deberá cambiarla al
                            entrar.
                          </small>
                        </label>
                      </div>
                    </section>
                    <section className="new-user-block">
                      <header>
                        <MapPin />
                        <span>
                          <strong>Información operativa</strong>
                          <small>Datos para identificar al responsable.</small>
                        </span>
                      </header>
                      <div className="new-user-field-grid two-columns">
                        <label>
                          Región
                          <input
                            type="text"
                            placeholder="Ej. Región Norte"
                            value={newUser.region}
                            onChange={(event) =>
                              setNewUser({
                                ...newUser,
                                region: event.target.value,
                              })
                            }
                          />
                        </label>
                        <label>
                          Contacto
                          <input
                            type="text"
                            placeholder="Teléfono alternativo"
                            value={newUser.contact}
                            onChange={(event) =>
                              setNewUser({
                                ...newUser,
                                contact: event.target.value,
                              })
                            }
                          />
                        </label>
                      </div>
                    </section>
                    <section className="new-user-block new-user-permission-block">
                      <header>
                        <ShieldCheck />
                        <span>
                          <strong>Nivel de permisos</strong>
                          <small>Selecciona un perfil de acceso seguro.</small>
                        </span>
                      </header>
                      <div className="new-role-options">
                        {accessProfiles.map(([profile,title])=><button type="button" key={profile} className={newUserAccessProfile===profile?'active':''} onClick={()=>{
                          const preset=userAccessProfile(profile,newUserDepartment);
                          setNewUserAccessProfile(profile);setNewUser({...newUser,...preset,role:preset.role as AdminUser['role'],supportTeam:preset.supportTeam as TechnologyTeam|UserDepartment|null,groups:profile==='SUPERVISOR'?newUser.groups:[]});
                        }}><ShieldCheck/><span><strong>{title}</strong><small>{profile==='ADMINISTRATOR'?'Acceso global del administrador principal':profile==='SUPPORT'?'Acceso inicial al departamento, sin gerencia ni permisos globales':profile==='ADMINISTRATIVE'?'Mismas funciones administrativas dentro del departamento seleccionado':'Aplicar permisos de este perfil'}</small></span></button>)}
                      </div>
                      {(newUserAccessProfile === "DEPARTMENT_MANAGER" || newUserAccessProfile === "SUPPORT" ||
                        newUserAccessProfile === "TECHNICIAN" || newUserAccessProfile === "WAREHOUSE_REQUESTER" ||
                        newUserAccessProfile === "ADMINISTRATIVE") && (
                        <label className="new-user-department-select">
                          Departamento asignado
                          <select
                            value={newUserDepartment}
                            onChange={(event) => {
                              const department = event.target
                                .value as SupportDepartmentRole;
                              setNewUserDepartment(department);
                              const preset=userAccessProfile(newUserAccessProfile,department);
                              setNewUser({
                                ...newUser,
                                role: preset.role as AdminUser['role'],
                                supportTeam:preset.supportTeam as TechnologyTeam | UserDepartment | null,
                                groups: [],
                                permissions:preset.permissions,
                              });
                            }}
                          >
                            {userDepartments.map(([code,name])=><option key={code} value={code}>{name}</option>)}
                          </select>
                          <small>
                            Solo verá y administrará la tarjeta, tickets,
                            hallazgos y conversaciones de este departamento.
                          </small>
                        </label>
                      )}
                      {(newUserAccessProfile === "SUPPORT" ||
                        newUserAccessProfile === "TECHNICIAN") &&
                        newUserDepartment === "TECHNOLOGY" && (
                          <label className="new-user-department-select">
                            Equipo de Tecnología
                            <select
                              value={newUser.supportTeam || ""}
                              onChange={(event) =>{const team=(event.target.value||null) as TechnologyTeam|null;setNewUser({...newUser,supportTeam:team,permissions:team?{...technologyTeamPermissions(team),usesExplicitPermissions:true}:userAccessProfile('SUPPORT','TECHNOLOGY').permissions});setNewUserAccessProfile(team==="TECHNICIANS"?"TECHNICIAN":"SUPPORT");}}
                            >
                              <option value="">Soporte de Tecnología</option>
                              <option value="CALL_CENTER">Call Center</option>
                              <option value="TECHNICAL_FAILURE">
                                Avería Técnica
                              </option>
                              <option value="TECHNICIANS">Técnicos</option>
                              <option value="WAREHOUSE">Almacén central</option>
                              <option value="WORKSHOP">Taller técnico</option>
                            </select>
                            <small>
                              Call Center recibe casos, Avería Técnica los diagnostica y Técnicos ejecuta los trabajos asignados.
                            </small>
                          </label>
                        )}
                      {newUserDepartment==="TECHNOLOGY"&&newUserAccessProfile==="DEPARTMENT_MANAGER"&&<label>Área del gerente<select value={newUser.supportTeam||''} onChange={event=>setNewUser({...newUser,supportTeam:(event.target.value||null) as TechnologyTeam|null})}><option value="">Tecnología completa (incluye Avería y Call Center)</option><option value="TECHNICAL_FAILURE">Solo Avería Técnica</option><option value="CALL_CENTER">Solo Call Center</option></select></label>}
                      {newUserDepartment==="TECHNOLOGY"&&(newUserAccessProfile==="SUPPORT"||newUserAccessProfile==="TECHNICIAN")&&<div className="technology-quick-permissions"><strong>Permisos rápidos de Tecnología</strong><small>Aplica automáticamente el acceso correcto para soporte, campo, Almacén o Taller.</small><div>{(["CALL_CENTER","TECHNICAL_FAILURE","TECHNICIANS","WAREHOUSE","WORKSHOP"] as TechnologyTeam[]).map(team=><button type="button" key={team} className={newUser.supportTeam===team?"active":""} onClick={()=>{setNewUser({...newUser,role:"TECHNOLOGY",supportTeam:team,permissions:technologyTeamPermissions(team)});setNewUserAccessProfile(team==="TECHNICIANS"?"TECHNICIAN":"SUPPORT");}}>{team==="CALL_CENTER"?"Call Center":team==="TECHNICAL_FAILURE"?"Avería Técnica":team==="TECHNICIANS"?"Técnico de campo":team==="WAREHOUSE"?"Almacén":"Taller"}</button>)}</div></div>}
                      {newUserDepartment==="GENERAL_SERVICES"&&(newUserAccessProfile==="SUPPORT"||newUserAccessProfile==="TECHNICIAN")&&<div className="technology-quick-permissions"><strong>Área especializada de Servicios Generales</strong><small>El perfil Taller solo verá el control de entradas, reparaciones y entregas de su departamento.</small><div><button type="button" className={newUser.supportTeam==="WORKSHOP"?"active":""} onClick={()=>{setNewUser({...newUser,role:"GENERAL_SERVICES",supportTeam:"WORKSHOP",permissions:technologyTeamPermissions("WORKSHOP")});setNewUserAccessProfile("SUPPORT");}}>Taller de Servicios Generales</button><button type="button" className={!newUser.supportTeam?"active":""} onClick={()=>setNewUser({...newUser,role:"GENERAL_SERVICES",supportTeam:null,permissions:accessProfilePermissions(newUserAccessProfile,"GENERAL_SERVICES")})}>Soporte / técnico normal</button></div></div>}
                      <div className="granular-permission-grid new-user-granular-permissions">
                        {allPermissionCatalog.map((permission) => (
                          <label key={permission.key}>
                            <input
                              type="checkbox"
                              checked={
                                newUser.permissions[permission.key] ?? false
                              }
                              onChange={(event) =>
                                setNewUser({
                                  ...newUser,
                                  permissions: {
                                    ...newUser.permissions,
                                    [permission.key]: event.target.checked,
                                  },
                                })
                              }
                            />
                            <span>
                              <ShieldCheck />
                              <strong>{permission.title}</strong>
                              <small>{permission.description}</small>
                            </span>
                          </label>
                        ))}
                      </div>
                    </section>
                    {newUser.role === "GROUP_ADMIN" && (
                      <section className="new-user-groups">
                        <span>Grupos asignados</span>
                        <small>
                          Toca cada grupo para marcarlo o desmarcarlo.
                        </small>
                        <GroupChecklist
                          groups={groupCatalog}
                          selected={newUser.groups}
                          onChange={(groups) =>
                            setNewUser({ ...newUser, groups })
                          }
                        />
                        <em>{newUser.groups.length} grupos seleccionados</em>
                      </section>
                    )}
                    <footer className="new-user-footer">
                      <div>
                        <ShieldCheck />
                        <span>
                          <strong>Cuenta lista para crear</strong>
                          <small>
                            Los permisos serán validados por el servidor.
                          </small>
                        </span>
                      </div>
                      <button type="submit" disabled={creatingUser}>
                        {creatingUser ? (
                          <LoaderCircle className="spin" />
                        ) : (
                          <UserPlus />
                        )}{" "}
                        {creatingUser
                          ? "Creando usuario..."
                          : "Crear usuario y guardar permisos"}
                      </button>
                    </footer>
                  </form>
                </section>
              </div>
            )}
            <nav className={`user-department-cards ${userDepartmentFilter ? "department-open" : ""}`} aria-label="Usuarios por departamento">
              {userDepartmentCards.map(({key,title,description,Icon,total})=><button type="button" key={key} className={userDepartmentFilter===key?"active":""} onClick={()=>{setUserDepartmentFilter(current=>current===key?null:key);setSelectedUserId(null);setUserDetailSection("information");}}><span><Icon/></span><em>DEPARTAMENTO</em><strong>{title}</strong><small>{description}</small><b>{total} usuarios</b></button>)}
            </nav>
            {(userDepartmentFilter||selectedUserId)&&<button type="button" className="mobile-section-back user-mobile-back" onClick={()=>{if(selectedUserId){setSelectedUserId(null);setUserDetailSection("information");}else{setUserDepartmentFilter(null);}}}><ArrowLeft/> {selectedUserId?"Volver a usuarios":"Volver a departamentos"}</button>}
            <div className="user-directory-context"><div><Layers3/><span><strong>{userDepartmentFilter?userDepartmentCards.find(card=>card.key===userDepartmentFilter)?.title:"Todos los departamentos"}</strong><small>Selecciona un usuario para consultar y editar su expediente.</small></span></div><b>{departmentVisibleUsers.length} resultados</b></div>
            <div className={`user-list scoped-user-list department-user-list ${selectedUserId?"has-selected-user":""}`}>
              {departmentVisibleUsers.map((user) => (
                <article
                  key={user.id}
                  className={`${!user.isActive ? "disabled-user" : ""} ${selectedUserId===user.id?"selected-user-card":""}`}
                >
                  <button type="button" className="user-card-identity" aria-expanded={selectedUserId===user.id} onClick={()=>{setSelectedUserId(current=>current===user.id?null:user.id);setUserDetailSection("information");}}>
                    <div className="user-avatar">
                      {user.avatarUrl ? (
                        <img src={user.avatarUrl} alt={user.displayName} />
                      ) : (
                        <UserRound />
                      )}
                    </div>
                    <div>
                      <strong>{user.displayName}</strong>
                      <span>@{usernameValue(user.email)}</span>
                      <small>
                        {user.region || "Sin región"} ·{" "}
                        {user.contact || "Sin contacto"}
                      </small>
                      {user.role === "TECHNOLOGY" && (
                        <small>
                          Equipo: {user.supportTeam === "CALL_CENTER"
                            ? "Call Center"
                            : user.supportTeam === "TECHNICAL_FAILURE"
                              ? "Avería Técnica"
                              : user.supportTeam === "TECHNICIANS"
                                ? "Técnicos"
                                : user.supportTeam === "WAREHOUSE"
                                  ? "Almacén central"
                                  : user.supportTeam === "WORKSHOP"
                                    ? "Taller técnico"
                              : "Sin equipo asignado"}
                        </small>
                      )}
                    </div>
                    <em>{user.agencyCount} agencias</em><b>{selectedUserId===user.id?"Cerrar expediente":"Abrir expediente"}</b>
                  </button>
                  {selectedUserId===user.id&&<nav className="user-detail-cards" aria-label={`Secciones de ${user.displayName}`}>
                    <button type="button" className={userDetailSection==="information"?"active":""} onClick={()=>setUserDetailSection("information")}><UserRound/><span><strong>Información</strong><small>Identidad, rol y credenciales</small></span></button>
                    <button type="button" className={userDetailSection==="permissions"?"active":""} onClick={()=>setUserDetailSection("permissions")}><ShieldCheck/><span><strong>Permisos</strong><small>{Object.values(user.permissions||{}).filter(Boolean).length} accesos habilitados</small></span></button>
                    <button type="button" className={userDetailSection==="groups"?"active":""} onClick={()=>setUserDetailSection("groups")}><Layers3/><span><strong>Grupos que puede ver</strong><small>{user.groups.length} grupos · {user.agencyCount} agencias</small></span></button>
                  </nav>}
                  {selectedUserId===user.id&&userDetailSection==="information"&&<>
                    <form id={`user-edit-${user.id}`} onSubmit={event=>{event.preventDefault();void saveUser(user);}} />
                  <div className="user-edit-grid">
                    <label>
                      Nombre
                      <input
                        value={user.displayName}
                        onChange={(event) =>
                          setUsers((current) =>
                            current.map((item) =>
                              item.id === user.id
                                ? { ...item, displayName: event.target.value }
                                : item,
                            ),
                          )
                        }
                      />
                    </label>
                    <label>
                      Usuario
                      <input
                        type="text"
                        pattern="[a-z0-9]+(?:\.[a-z0-9]+)+"
                        title="Usa el formato nombre.apellido"
                        value={usernameValue(user.email)}
                        onChange={(event) =>
                          setUsers((current) =>
                            current.map((item) =>
                              item.id === user.id
                                ? {
                                    ...item,
                                    email: usernameEmail(event.target.value),
                                  }
                                : item,
                            ),
                          )
                        }
                      />
                    </label>
                    <label>
                      Región
                      <input
                        value={user.region || ""}
                        onChange={(event) =>
                          setUsers((current) =>
                            current.map((item) =>
                              item.id === user.id
                                ? { ...item, region: event.target.value }
                                : item,
                            ),
                          )
                        }
                      />
                    </label>
                    <label>
                      Contacto
                      <input
                        value={user.contact || ""}
                        onChange={(event) =>
                          setUsers((current) =>
                            current.map((item) =>
                              item.id === user.id
                                ? { ...item, contact: event.target.value }
                                : item,
                            ),
                          )
                        }
                      />
                    </label>
                    <label>
                      Rol
                      <select
                        value={user.role!=="ADMINISTRATOR"&&user.permissions.canManageDepartment?"DEPARTMENT_MANAGER":user.role}
                        onChange={(event) =>
                          setUsers((current) =>
                            current.map((item) =>
                              item.id === user.id
                                ? event.target.value === "DEPARTMENT_MANAGER" ? {
                                    ...item,
                                    role: item.role==="DEPARTMENT"||item.role==="TECHNOLOGY"||item.role==="GENERAL_SERVICES"||item.role==="HUMAN_RESOURCES"||item.role==="WAREHOUSE"?item.role:"WAREHOUSE",
                                    supportTeam: item.role==="DEPARTMENT"||item.role==="TECHNOLOGY"||item.role==="GENERAL_SERVICES"||item.role==="HUMAN_RESOURCES"?item.supportTeam:"WAREHOUSE",
                                    permissions:{...item.permissions,canManageDepartment:true,canViewFieldAudits:true,canViewDashboard:true,canViewSupportDashboard:true,canViewAllSupportDepartments:false,canManageUsers:false,canManageAudits:false,canManage:false,canManageAgencies:false,canConfigureSupport:false,canDeleteSupportTickets:false,canDeleteSupportConversations:false,canAudit:false,canExportAudits:false},
                                  } : {
                                    ...item,
                                     role: event.target
                                      .value as AdminUser["role"],
                                    supportTeam:
                                      event.target.value === "TECHNOLOGY"
                                        ? item.supportTeam || "TECHNICAL_FAILURE"
                                        : event.target.value === "WAREHOUSE" ? "WAREHOUSE" : null,
                                    permissions: defaultUserPermissions(
                                      event.target.value as AdminUser["role"],
                                    ),
                                  }
                                : item,
                            ),
                          )
                        }
                      >
                        <option value="ADMINISTRATIVE">Administrativo</option>
                        <option value="DEPARTMENT" disabled>Usuario de departamento</option>
                        <option value="ADMINISTRATOR">
                          Administrador principal
                        </option>
                        <option value="GROUP_ADMIN">Supervisor</option>
                        <option value="FISCALIZADOR">Fiscalizador</option>
                        <option value="TECHNOLOGY">
                          Técnico de Tecnología
                        </option>
                        <option value="GENERAL_SERVICES">
                          Técnico de Servicios Generales
                        </option>
                        <option value="HUMAN_RESOURCES">
                          Recursos Humanos
                        </option>
                        <option value="WAREHOUSE">Almacén</option>
                        <option value="DEPARTMENT_MANAGER">Gerente</option>
                        <option value="VIEWER">Consulta general</option>
                      </select>
                    </label>
                    {user.role!=="ADMINISTRATOR"&&<label>Departamento asignado<select value={assignedUserDepartment(user)} onChange={event=>{const department=event.target.value as UserDepartment;const assignment=departmentAssignment(department);setUsers(current=>current.map(item=>item.id===user.id?{...item,role:assignment.role as AdminUser['role'],supportTeam:assignment.supportTeam as TechnologyTeam | UserDepartment | null,permissions:item.permissions.canManageDepartment?{...item.permissions,...accessProfilePermissions('DEPARTMENT_MANAGER',department)}:item.permissions}:item));}}><option value="" disabled>Seleccionar departamento</option>{userDepartments.map(([code,name])=><option key={code} value={code}>{name}</option>)}</select><small>Un departamento por usuario. La gerencia consulta únicamente su área.</small></label>}
                    {user.role === "TECHNOLOGY" && (
                      <label>
                        Equipo de Tecnología
                        <select
                          value={user.supportTeam || ""}
                          onChange={(event) => {
                            const team = event.target.value as TechnologyTeam | "";
                            setUsers((current) =>
                              current.map((item) =>
                                item.id === user.id
                                  ? {
                                      ...item,
                                      supportTeam: team || null,
                                      permissions: item.permissions.canManageDepartment ? {...item.permissions,canManageDepartment:true} : team ? technologyTeamPermissions(team) : accessProfilePermissions("DEPARTMENT_MANAGER","TECHNOLOGY"),
                                    }
                                  : item,
                              ),
                            );
                          }}
                        >
                          <option value="">Administrador del departamento</option>
                          <option value="CALL_CENTER">Call Center</option>
                          <option value="TECHNICAL_FAILURE">
                            Avería Técnica
                          </option>
                          <option value="TECHNICIANS">Técnicos</option>
                          <option value="WAREHOUSE">Almacén central</option>
                          <option value="WORKSHOP">Taller técnico</option>
                        </select>
                      </label>
                    )}
                    <label>
                      Nueva contraseña
                      <div className="password-field">
                        <KeyRound />
                        <input
                          type="password"
                          form={`user-edit-${user.id}`}
                          autoComplete="new-password"
                          minLength={6}
                          pattern="123456|.{8,128}"
                          placeholder="Dejar igual"
                          value={user.password || ""}
                          onChange={(event) =>
                            setUsers((current) =>
                              current.map((item) =>
                                item.id === user.id
                                  ? { ...item, password: event.target.value }
                                  : item,
                              ),
                            )
                          }
                        />
                      </div>
                    </label>
                  </div>
                  </>}
                  {selectedUserId===user.id&&userDetailSection==="permissions"&&<section className="user-permissions-editor">
                    <div className="technology-quick-permissions"><strong>Permisos rápidos por rol</strong><small>Solo modifica esta cuenta al pulsar Guardar cambios. Superadministrador equivale al administrador principal.</small><div>{accessProfiles.map(([profile,title])=><button type="button" key={profile} onClick={()=>{
                      if(profile==='ADMINISTRATOR'&&!window.confirm('¿Asignar acceso global de administrador principal a esta cuenta?'))return;
                      const department=(userDepartments.some(([code])=>code===assignedUserDepartment(user))?assignedUserDepartment(user):'GENERAL_SERVICES') as UserDepartment;
                      const preset=userAccessProfile(profile,department);
                      setUsers(current=>current.map(item=>item.id===user.id?{...item,...preset,role:preset.role as AdminUser['role'],supportTeam:preset.supportTeam as TechnologyTeam|UserDepartment|null,groups:profile==='SUPERVISOR'?item.groups:[]}:item));
                    }}>{title}</button>)}</div></div>
                    {user.role!=="ADMINISTRATOR"&&<label>Departamento asignado<select value={assignedUserDepartment(user)} onChange={event=>{const department=event.target.value as UserDepartment;const assignment=departmentAssignment(department);setUsers(current=>current.map(item=>item.id===user.id?{...item,role:assignment.role as AdminUser['role'],supportTeam:assignment.supportTeam as TechnologyTeam | UserDepartment | null,permissions:item.permissions.canManageDepartment?{...item.permissions,...accessProfilePermissions('DEPARTMENT_MANAGER',department)}:item.permissions}:item));}}><option value="" disabled>Seleccionar departamento</option>{userDepartments.map(([code,name])=><option key={code} value={code}>{name}</option>)}</select><small>Un departamento por usuario. La gerencia consulta únicamente su área.</small></label>}
                    <header>
                      <div>
                        <ShieldCheck />
                        <span>
                          <strong>Permisos de acceso</strong>
                          <small>
                            Selecciona las funciones que podrá utilizar esta
                            cuenta.
                          </small>
                        </span>
                      </div>
                      <em>
                        {user.role === "ADMINISTRATOR"
                          ? "Control total"
                          : user.role === "GROUP_ADMIN"
                            ? "Supervisor"
                            : user.role === "FISCALIZADOR"
                              ? "Fiscalizador"
                              : user.role === "TECHNOLOGY"
                                ? "Tecnología"
                                : user.role === "WAREHOUSE"
                                  ? "Almacén"
                                : user.role === "GENERAL_SERVICES"
                                  ? "Servicios Generales"
                                  : user.role === "DEPARTMENT"
                                    ? `${user.permissions.canManageDepartment ? 'Gerente' : user.permissions.canAssignTickets ? 'Soporte' : user.permissions.canResolveTickets ? 'Técnico' : 'Usuario'} · ${userDepartments.find(([code])=>code===user.supportTeam)?.[1] || 'Departamento'}`
                                  : "Solo consulta"}
                      </em>
                    </header>
                    {user.role==="TECHNOLOGY"&&<div className="technology-quick-permissions existing-user"><strong>Aplicar permisos rápidos</strong><small>Define el equipo y reemplaza permisos incompatibles.</small><div>{(["CALL_CENTER","TECHNICAL_FAILURE","TECHNICIANS","WAREHOUSE","WORKSHOP"] as TechnologyTeam[]).map(team=><button type="button" key={team} className={user.supportTeam===team?"active":""} onClick={()=>setUsers(current=>current.map(item=>item.id===user.id?{...item,supportTeam:team,permissions:technologyTeamPermissions(team)}:item))}>{team==="CALL_CENTER"?"Call Center":team==="TECHNICAL_FAILURE"?"Avería Técnica":team==="TECHNICIANS"?"Técnico de campo":team==="WAREHOUSE"?"Almacén":"Taller"}</button>)}</div></div>}
                    {(user.role==="GENERAL_SERVICES"||user.role==="HUMAN_RESOURCES")&&<div className="technology-quick-permissions existing-user"><strong>Rol dentro del departamento</strong><small>Soporte administra la bandeja; Técnico atiende casos y Taller controla exclusivamente los equipos del departamento.</small><div><button type="button" className={user.permissions.canAssignTickets?"active":""} onClick={()=>setUsers(current=>current.map(item=>item.id===user.id?{...item,supportTeam:null,permissions:accessProfilePermissions("SUPPORT",user.role as SupportDepartmentRole)}:item))}>Soporte del departamento</button><button type="button" className={!user.permissions.canAssignTickets&&!user.supportTeam?"active":""} onClick={()=>setUsers(current=>current.map(item=>item.id===user.id?{...item,supportTeam:null,permissions:accessProfilePermissions("TECHNICIAN",user.role as SupportDepartmentRole)}:item))}>Técnico de campo</button><button type="button" className={user.supportTeam==="WORKSHOP"?"active":""} onClick={()=>setUsers(current=>current.map(item=>item.id===user.id?{...item,supportTeam:"WORKSHOP",permissions:technologyTeamPermissions("WORKSHOP")}:item))}>Taller del departamento</button></div></div>}
                    <div className="permission-check-grid">
                      <label>
                        <input type="checkbox" checked disabled />
                        <span>
                          <Eye />
                          <strong>Ver panel y evidencias</strong>
                          <small>
                            Consulta estadísticas, grupos y auditorías.
                          </small>
                        </span>
                      </label>
                      <label>
                        <input
                          type="checkbox"
                          checked={
                            user.role === "GROUP_ADMIN" ||
                            user.role === "ADMINISTRATOR"
                          }
                          onChange={(event) =>
                            setUsers((current) =>
                              current.map((item) =>
                                item.id === user.id
                                  ? {
                                      ...item,
                                      role: event.target.checked
                                        ? "GROUP_ADMIN"
                                        : "VIEWER",
                                    }
                                  : item,
                              ),
                            )
                          }
                        />
                        <span>
                          <Camera />
                          <strong>Realizar auditorías</strong>
                          <small>
                            Registra formularios y evidencia georreferenciada.
                          </small>
                        </span>
                      </label>
                      <label>
                        <input
                          type="checkbox"
                          checked={
                            user.role === "FISCALIZADOR" ||
                            user.role === "ADMINISTRATOR"
                          }
                          onChange={(event) =>
                            setUsers((current) =>
                              current.map((item) =>
                                item.id === user.id
                                  ? {
                                      ...item,
                                      role: event.target.checked
                                        ? "FISCALIZADOR"
                                        : "VIEWER",
                                    }
                                  : item,
                              ),
                            )
                          }
                        />
                        <span>
                          <Download />
                          <strong>Fiscalizar y descargar PDF</strong>
                          <small>
                            Consulta todas las auditorías y exporta expedientes.
                          </small>
                        </span>
                      </label>
                      <label>
                        <input
                          type="checkbox"
                          checked={user.role === "ADMINISTRATOR"}
                          onChange={(event) =>
                            setUsers((current) =>
                              current.map((item) =>
                                item.id === user.id
                                  ? {
                                      ...item,
                                      role: event.target.checked
                                        ? "ADMINISTRATOR"
                                        : "GROUP_ADMIN",
                                    }
                                  : item,
                              ),
                            )
                          }
                        />
                        <span>
                          <UserCog />
                          <strong>Administración total</strong>
                          <small>
                            Edita, elimina y administra usuarios y permisos.
                          </small>
                        </span>
                      </label>
                    </div>
                    <div className="granular-permission-grid">
                      {allPermissionCatalog.map((permission) => {
                        const checked =
                          user.permissions?.[permission.key] ??
                          defaultUserPermissions(user.role)[permission.key] ??
                          false;
                        return (
                          <label key={permission.key}>
                            <input
                              type="checkbox"
                              checked={checked}
                              onChange={(event) =>
                                setUsers((current) =>
                                  current.map((item) =>
                                    item.id === user.id
                                      ? {
                                          ...item,
                                          permissions: {
                                            ...defaultUserPermissions(
                                              item.role,
                                            ),
                                            ...item.permissions,
                                            [permission.key]:
                                              event.target.checked,
                                          },
                                        }
                                      : item,
                                  ),
                                )
                              }
                            />
                            <span>
                              <ShieldCheck />
                              <strong>{permission.title}</strong>
                              <small>{permission.description}</small>
                            </span>
                          </label>
                        );
                      })}
                    </div>
                  </section>}
                  {selectedUserId===user.id&&userDetailSection==="groups"&&(
                    <section className="user-group-selector">
                      <strong>Grupos que puede ver</strong>
                      {user.role === "GROUP_ADMIN" ? <GroupChecklist
                        groups={groupCatalog}
                        selected={user.groups}
                        onChange={(groups) =>
                          setUsers((current) =>
                            current.map((item) =>
                              item.id === user.id ? { ...item, groups } : item,
                            ),
                          )
                        }
                      />:<div className="user-group-scope-note"><Layers3/><strong>Acceso definido por departamento</strong><small>Esta cuenta ve los procesos de {adminUserRoleLabel(user.role)}. La selección manual de grupos está reservada para los supervisores.</small></div>}
                      <small>
                        {user.groups.length} grupos asignados · toca una casilla
                        para cambiarla
                      </small>
                    </section>
                  )}
                  {selectedUserId===user.id&&<div className="user-card-actions">
                    <div className="user-scope-chips">
                      {user.groups.slice(0, 5).map((group) => (
                        <span key={group}>{group}</span>
                      ))}
                      {user.groups.length > 5 && (
                        <span>+{user.groups.length - 5} más</span>
                      )}
                    </div>
                    <label className="active-check">
                      <input
                        type="checkbox"
                        checked={user.isActive}
                        onChange={(event) =>
                          setUsers((current) =>
                            current.map((item) =>
                              item.id === user.id
                                ? { ...item, isActive: event.target.checked }
                                : item,
                            ),
                          )
                        }
                      />
                      <span>{user.isActive ? "Activo" : "Bloqueado"}</span>
                    </label>
                    <button type="button" onClick={() => void saveUser(user)} disabled={deletingUserId===user.id||savingUserId!==null}>
                      <Save /> Guardar cambios
                    </button>
                    <button
                      type="button"
                      className="danger user-delete-button"
                      disabled={user.id===session.id || deletingUserId!==null}
                      title={user.id===session.id ? "No puedes eliminar tu propia cuenta" : "Eliminar acceso de usuario"}
                      onClick={() => void deleteUser(user)}
                    >
                      {deletingUserId===user.id ? <LoaderCircle className="spin" /> : <Trash2 />} {deletingUserId===user.id ? "Eliminando…" : "Eliminar usuario"}
                    </button>
                  </div>}
                </article>
              ))}
              {departmentVisibleUsers.length === 0 && (
                <div className="user-search-empty">
                  <UserRound />
                  <strong>No encontramos administradores</strong>
                  <span>Prueba con otro nombre, apellido o usuario.</span>
                </div>
              )}
            </div>
          </section>
        </div>
      )}
    </div>
  );
}

function GroupChecklist({
  groups,
  selected,
  onChange,
}: {
  groups: string[];
  selected: string[];
  onChange: (groups: string[]) => void;
}) {
  const [groupQuery,setGroupQuery]=useState("");
  const searchId=useId();
  const normalizedQuery=groupQuery.normalize("NFD").replace(/[\u0300-\u036f]/g,"").trim().toLowerCase();
  const visibleGroups=groups.filter(group=>!normalizedQuery||group.normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLowerCase().includes(normalizedQuery));
  const toggle = (group: string) =>
    onChange(
      selected.includes(group)
        ? selected.filter((item) => item !== group)
        : [...selected, group],
    );

  return (
    <div className="group-checklist-shell">
      <div className="group-checklist-search">
        <div className="group-search-heading"><Search/><span><strong>Buscar dentro de los grupos</strong><small>Filtra las casillas por número o nombre del grupo.</small></span></div>
        <label htmlFor={searchId}><Search/><input id={searchId} name={`groupSearch-${searchId}`} value={groupQuery} onChange={event=>setGroupQuery(event.target.value)} placeholder="Ejemplo: RIFERO, OZAMA, ROMANA..." autoComplete="off" /></label>
        {groupQuery&&<button type="button" onClick={()=>setGroupQuery("")} aria-label="Limpiar búsqueda"><X/></button>}
        <span><strong>{visibleGroups.length}</strong> resultados · <strong>{selected.length}</strong> seleccionados</span>
      </div>
      <div className="group-checklist">
      {visibleGroups.map((group) => {
        const checked = selected.includes(group);
        return (
          <label key={group} className={checked ? "selected" : ""}>
            <input
              type="checkbox"
              checked={checked}
              onChange={() => toggle(group)}
            />
            <span>{group}</span>
            <Check />
          </label>
        );
      })}
      {!visibleGroups.length&&<div className="group-checklist-empty"><Search/><strong>No encontramos ese grupo</strong><small>Revisa el nombre e inténtalo nuevamente.</small></div>}
      </div>
    </div>
  );
}

function AuditViewer({
  audit,
  onPhotoClick,
}: {
  audit: Audit;
  onPhotoClick: (photo: { url: string; label: string }) => void;
}) {
  const answerText = (value?: string) =>
    value === "yes"
      ? "Sí"
      : value === "no"
        ? "No"
        : value === "not_applicable"
          ? "No aplica"
          : value === "cash_register"
            ? "Caja registradora"
            : value === "wooden_box"
              ? "Caja de madera"
              : value === "none"
                ? "Ninguna"
          : "Sin respuesta";
  return (
    <>
      <div className="audit-detail-summary">
        <article>
          <UserRound />
          <div>
            <small>Empleado responsable</small>
            <strong>{audit.employeeName || "Sin nombre registrado"}</strong>
            <span>{audit.employeeCode || "Sin código"}</span>
          </div>
        </article>
        <article>
          <Navigation />
          <div>
            <small>Dirección GPS</small>
            <strong>{audit.direccion || audit.municipio}</strong>
            <span>
              {[audit.sector, audit.municipio, audit.provincia]
                .filter(Boolean)
                .join(" · ")}
            </span>
          </div>
        </article>
        <article>
          <MapPin />
          <div>
            <small>Coordenada verificada</small>
            <strong>
              {audit.latitude.toFixed(6)}, {audit.longitude.toFixed(6)}
            </strong>
            <a
              href={`https://www.openstreetmap.org/?mlat=${audit.latitude}&mlon=${audit.longitude}#map=20/${audit.latitude}/${audit.longitude}`}
              target="_blank"
              rel="noreferrer"
            >
              Abrir punto exacto · ±{Math.round(audit.accuracy)} m
            </a>
          </div>
        </article>
        <article className="audit-submission-card">
          <Clock3 />
          <div>
            <small>Registro de la auditoría</small>
            <strong>{audit.submittedByLogin ? `@${usernameValue(audit.submittedByLogin)}` : "Login no disponible"}</strong>
            <span>{deviceDate(audit.submittedAt).toLocaleString("es-DO")}</span>
          </div>
        </article>
      </div>
      <div className="audit-detail-block">
        <div className="audit-detail-title">
          <Camera />
          <div>
            <strong>Fotografías georreferenciadas</strong>
            <span>{audit.photos?.length || 0} evidencias vinculadas</span>
          </div>
        </div>
        <div className="audit-detail-photos">
          {audit.photos?.map((photo, index) => (
            <article key={`${photo.photoType}-${index}`}>
              <button
                className="audit-photo-preview"
                type="button"
                onClick={() =>
                  onPhotoClick({
                    url: photo.url,
                    label:
                      photoLabels[photo.photoType] ||
                      `Evidencia ${photo.photoType}`,
                  })
                }
                aria-label="Ver fotografía en pantalla completa"
              >
                <img src={photo.url} alt={`Evidencia ${photo.photoType}`} loading="lazy" decoding="async" />
                <span>
                  <Maximize2 /> Clic para ampliar
                </span>
              </button>
              <div>
                <strong>
                  {photoLabels[photo.photoType] ||
                    `Evidencia ${photo.photoType}`}
                </strong>
                <span>
                  {photo.latitude.toFixed(6)}, {photo.longitude.toFixed(6)} · ±
                  {Math.round(photo.accuracy)} m
                </span>
                <small>
                  {deviceDate(photo.capturedAt).toLocaleString("es-DO")}
                </small>
              </div>
            </article>
          ))}
        </div>
      </div>
      <div className="audit-detail-block">
        <div className="audit-detail-title">
          <ClipboardCheck />
          <div>
            <strong>Respuestas del formulario</strong>
            <span>Verificación estructural realizada por el empleado</span>
          </div>
        </div>
        <div className="audit-answer-list">
          {answerLabels.map(([key, label], index) => (
            <article key={key}>
              <em>{String(index + 1).padStart(2, "0")}</em>
              <span>{label}</span>
              <strong
                className={
                  audit.answers?.[key] === "yes"
                    ? "answer-yes"
                    : audit.answers?.[key] === "no"
                      ? "answer-no"
                      : ""
                }
              >
                {answerText(audit.answers?.[key])}
              </strong>
            </article>
          ))}
        </div>
      </div>
      <div className="audit-observation">
        <strong>Observación del levantamiento</strong>
        <p>{audit.observations || "El empleado no agregó observaciones."}</p>
        <small>
          Enviada el {deviceDate(audit.submittedAt).toLocaleString("es-DO")}
        </small>
      </div>
    </>
  );
}

function AuditEditor({
  draft,
  setDraft,
  saving,
  onSave,
  onCancel,
}: {
  draft: Audit;
  setDraft: (audit: Audit) => void;
  saving: boolean;
  onSave: () => void;
  onCancel: () => void;
}) {
  const field = (key: keyof Audit, value: string | number) =>
    setDraft({ ...draft, [key]: value });
  return (
    <div className="audit-editor">
      <div className="audit-editor-heading">
        <Edit3 />
        <div>
          <strong>Corregir expediente</strong>
          <span>
            Todos los cambios quedarán registrados en el historial de auditoría.
          </span>
        </div>
      </div>
      <div className="audit-edit-grid">
        <label>
          Código
          <input
            value={draft.codigo}
            onChange={(e) => field("codigo", e.target.value)}
          />
        </label>
        <label>
          Terminal
          <input
            value={draft.terminal}
            onChange={(e) => field("terminal", e.target.value)}
          />
        </label>
        <label>
          Grupo
          <input
            value={draft.grupo}
            onChange={(e) => field("grupo", e.target.value)}
          />
        </label>
        <label>
          Empleado
          <input
            value={draft.employeeName || ""}
            onChange={(e) => field("employeeName", e.target.value)}
          />
        </label>
        <label>
          Código empleado
          <input
            value={draft.employeeCode || ""}
            onChange={(e) => field("employeeCode", e.target.value)}
          />
        </label>
        <label>
          Dirección
          <input
            value={draft.direccion || ""}
            onChange={(e) => field("direccion", e.target.value)}
          />
        </label>
        <label>
          Sector
          <input
            value={draft.sector || ""}
            onChange={(e) => field("sector", e.target.value)}
          />
        </label>
        <label>
          Municipio
          <input
            value={draft.municipio}
            onChange={(e) => field("municipio", e.target.value)}
          />
        </label>
        <label>
          Provincia
          <input
            value={draft.provincia}
            onChange={(e) => field("provincia", e.target.value)}
          />
        </label>
        <label>
          Latitud
          <input
            type="number"
            step="0.000001"
            value={draft.latitude}
            onChange={(e) => field("latitude", Number(e.target.value))}
          />
        </label>
        <label>
          Longitud
          <input
            type="number"
            step="0.000001"
            value={draft.longitude}
            onChange={(e) => field("longitude", Number(e.target.value))}
          />
        </label>
        <label>
          Precisión (metros)
          <input
            type="number"
            min="1"
            value={draft.accuracy}
            onChange={(e) => field("accuracy", Number(e.target.value))}
          />
        </label>
      </div>
      <div className="audit-edit-answers">
        {answerLabels.map(([key, label]) => (
          <label key={key}>
            <span>{label}</span>
            <select
              value={draft.answers?.[key] || ""}
              onChange={(e) =>
                setDraft({
                  ...draft,
                  answers: { ...draft.answers, [key]: e.target.value },
                })
              }
            >
              <option value="">Elegir...</option>
              {key === "cashBoxType" ? <>
                <option value="cash_register">Caja registradora</option>
                <option value="wooden_box">Caja de madera</option>
                <option value="none">Ninguna</option>
              </> : <>
                <option value="yes">Sí</option>
                <option value="no">No</option>
              </>}
              {key !== "cashBoxType" && equipmentAnswerKeys.has(key) && (
                <option value="not_applicable">No aplica</option>
              )}
            </select>
          </label>
        ))}
      </div>
      <label className="audit-edit-observation">
        Observación
        <textarea
          rows={4}
          maxLength={1000}
          value={draft.observations || ""}
          onChange={(e) => field("observations", e.target.value)}
        />
      </label>
      <div className="audit-editor-actions">
        <button onClick={onCancel}>Cancelar</button>
        <button className="save-audit" onClick={onSave} disabled={saving}>
          <Save /> {saving ? "Guardando..." : "Guardar correcciones"}
        </button>
      </div>
    </div>
  );
}
