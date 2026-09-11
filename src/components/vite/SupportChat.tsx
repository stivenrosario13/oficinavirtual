import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  ArrowLeft,
  Bell,
  Bot,
  Building2,
  CheckCircle2,
  Copy,
  FileAudio,
  Headphones,
  LoaderCircle,
  LockKeyhole,
  Keyboard,
  Mic,
  MessageCircle,
  Plus,
  Search,
  Send,
  Settings,
  Reply,
  ShieldBan,
  Square,
  Trash2,
  UserRound,
  Users,
  Volume2,
  VolumeX,
  Wifi,
  X,
} from "lucide-react";
import type { PortalSession } from "@/lib/session";

type Conversation = {
  id: string;
  supervisorUserId?: string | null;
  supervisorName: string;
  supervisorUsername: string;
  supervisorContact: string | null;
  supervisorAvatarUrl: string | null;
  groups?: string[] | null;
  assignedDepartment: string;
  category: string | null;
  status: string;
  requestedAgent: boolean;
  createdAt: string;
  updatedAt: string;
  lastMessageAt: string;
  lastMessage: string;
  isMuted?: boolean;
  isRestricted?: boolean;
  isBlocked?: boolean;
};
type Message = {
  id: string;
  conversationId: string;
  senderUserId?: string | null;
  senderName: string;
  senderRole: string;
  avatarUrl?: string | null;
  message: string;
  isBot: boolean;
  createdAt: string;
  voiceNoteUrl?: string | null;
  isDeleted?: boolean;
};
type ContactProfile = {
  id: string;
  email: string;
  displayName: string;
  role: string;
  contact: string | null;
  avatarUrl: string | null;
  groups?: string[] | null;
};
type SupportContact = {
  id: string;
  displayName: string;
  username: string;
  role: string;
  contact: string | null;
  avatarUrl: string | null;
  groups?: string[] | null;
};
type Presence = {
  supportOnline: boolean;
  typing: string[];
  onlineCount: number;
};
type Category = {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
};
type Department = {
  code: string;
  name: string;
  description?: string | null;
  accentColor: string;
  isActive: boolean;
  categories?: Category[] | null;
};
type RouteStep = "departments" | "categories" | "agent_departments" | null;
type ConversationViewFilter =
  | "ALL"
  | "WAITING_SUPPORT"
  | "IN_PROGRESS"
  | "FINISHED";

const statusLabels: Record<string, string> = {
  WAITING_SUPPORT: "Esperando soporte",
  IN_PROGRESS: "En conversación",
  RESOLVED: "Resuelto",
  CLOSED: "Cerrado",
};
const chatDate = (value: string) => {
  const normalized = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(value)
    ? value
    : `${value}Z`;
  return new Date(normalized);
};
const conversationTimeLabel = (value: string) => {
  const date = chatDate(value);
  if (Number.isNaN(date.getTime())) return "";
  const today = new Date();
  const isToday =
    date.getFullYear() === today.getFullYear() &&
    date.getMonth() === today.getMonth() &&
    date.getDate() === today.getDate();
  return isToday
    ? date.toLocaleTimeString("es-DO", {
        hour: "numeric",
        minute: "2-digit",
      })
    : date.toLocaleDateString("es-DO", {
        day: "2-digit",
        month: "2-digit",
      });
};
const departmentFallback: Record<string, string> = {
  TECHNOLOGY: "Tecnología",
  GENERAL_SERVICES: "Servicios Generales",
  HUMAN_RESOURCES: "Recursos Humanos",
};
const contactRoleLabels: Record<string, string> = {
  Administrator: "Administración",
  GroupAdministrator: "Supervisor",
  Technology: "Técnico · Tecnología",
  GeneralServices: "Técnico · Servicios Generales",
  HumanResources: "Técnico · Recursos Humanos",
};
const quickReplies = [
  "Hola, recibimos tu solicitud. Ya estamos revisando el caso.",
  "Estamos trabajando en tu ticket y te mantendremos informado.",
  "Por favor, confirma si la incidencia continúa en este momento.",
  "Necesitamos una foto o detalle adicional para continuar.",
  "La incidencia fue corregida. Confirma que el servicio funciona correctamente.",
];

const conversationGroups = (conversation: Conversation) =>
  Array.isArray(conversation.groups) ? conversation.groups : [];

const normalizePresence = (value?: Partial<Presence> | null): Presence => ({
  supportOnline: Boolean(value?.supportOnline),
  typing: Array.isArray(value?.typing) ? value.typing : [],
  onlineCount:
    typeof value?.onlineCount === "number" && Number.isFinite(value.onlineCount)
      ? value.onlineCount
      : 0,
});

const departmentCategories = (department?: Department | null) =>
  Array.isArray(department?.categories) ? department.categories : [];

async function chatRequest<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(path, options);
  const body = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(
      body.error || "No fue posible completar la solicitud.",
    ) as Error & { status?: number };
    error.status = response.status;
    throw error;
  }
  return body as T;
}

export default function SupportChat({
  session,
  onSessionExpired,
  openConversationId,
  openRequestKey,
}: {
  session: PortalSession;
  onSessionExpired: () => void;
  departmentScope?: string | null;
  openConversationId?: string | null;
  openRequestKey?: number;
}) {
  const [open, setOpen] = useState(false);
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [contacts, setContacts] = useState<SupportContact[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [catalog, setCatalog] = useState<Department[]>([]);
  const [routeStep, setRouteStep] = useState<RouteStep>(null);
  const [selectedDepartment, setSelectedDepartment] = useState<string | null>(
    null,
  );
  const [draft, setDraft] = useState("");
  const [loading, setLoading] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [sidebarMenu, setSidebarMenu] = useState<Conversation | null>(null);
  const [recording, setRecording] = useState(false);
  const [recordingSeconds, setRecordingSeconds] = useState(0);
  const [replyTo, setReplyTo] = useState<Message | null>(null);
  const [copiedMessageId, setCopiedMessageId] = useState<string | null>(null);
  const [messageQuery, setMessageQuery] = useState("");
  const [compactMode, setCompactMode] = useState(() => localStorage.getItem("supportChatCompact") === "1");
  const [enterToSend, setEnterToSend] = useState(() => localStorage.getItem("supportChatEnterToSend") !== "0");
  const [desktopNotifications, setDesktopNotifications] = useState(() => localStorage.getItem("supportChatNotifications") === "1");
  const [conversationQuery, setConversationQuery] = useState("");
  const [conversationViewFilter, setConversationViewFilter] =
    useState<ConversationViewFilter>("ALL");
  const [contactQuery, setContactQuery] = useState("");
  const [contactDirectoryOpen, setContactDirectoryOpen] = useState(false);
  const [groupFilter, setGroupFilter] = useState("ALL");
  const [profileOpen, setProfileOpen] = useState(false);
  const [profile, setProfile] = useState<ContactProfile | null>(null);
  const [profileContact, setProfileContact] = useState("");
  const [presence, setPresence] = useState<Presence>({
    supportOnline: false,
    typing: [],
    onlineCount: 0,
  });
  const [presenceEnabled, setPresenceEnabled] = useState(false);
  const presenceRequestInFlight = useRef(false);
  const returnToContactsRef = useRef(false);
  const longPressTimer = useRef<number | null>(null);
  const recorderRef = useRef<MediaRecorder | null>(null);
  const recorderChunks = useRef<Blob[]>([]);
  const recordingTimer = useRef<number | null>(null);
  const recordingSecondsRef = useRef(0);
  const messageEndRef = useRef<HTMLDivElement | null>(null);
  const composerTextareaRef = useRef<HTMLTextAreaElement | null>(null);
  const onSessionExpiredRef = useRef(onSessionExpired);
  const closeChat = useCallback(() => {
    setOpen(false);
    setActiveId(null);
    setMessages([]);
    setReplyTo(null);
    setRouteStep(null);
    setSelectedDepartment(null);
    setContactDirectoryOpen(false);
    setProfileOpen(false);
    setSidebarMenu(null);
    returnToContactsRef.current = false;
  }, []);
  useEffect(() => {
    onSessionExpiredRef.current = onSessionExpired;
  }, [onSessionExpired]);
  useEffect(() => {
    if (!open) return;
    const previousBodyOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      if (sidebarMenu) setSidebarMenu(null);
      else if (contactDirectoryOpen) setContactDirectoryOpen(false);
      else if (profileOpen) setProfileOpen(false);
      else if (activeId) {
        setActiveId(null);
        setMessages([]);
        setReplyTo(null);
      } else closeChat();
    };
    window.addEventListener("keydown", closeOnEscape);
    return () => {
      document.body.style.overflow = previousBodyOverflow;
      window.removeEventListener("keydown", closeOnEscape);
    };
  }, [open, closeChat, activeId, contactDirectoryOpen, profileOpen, sidebarMenu]);
  const isSupervisor = session.role === "GroupAdministrator";
  const canServe = session.permissions.canManageSupportChat;
  const canModerate = session.permissions.canModerateSupportChat ?? canServe;
  const canDeleteConversation =
    session.permissions.canDeleteSupportConversations ??
    session.role === "Administrator";
  const scopedConversations = useMemo(() => {
    const needle = conversationQuery
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();
    return conversations.filter(
      (item) =>
        (groupFilter === "ALL" ||
          conversationGroups(item).includes(groupFilter)) &&
        (conversationViewFilter === "ALL" ||
          item.status === conversationViewFilter ||
          (conversationViewFilter === "FINISHED" &&
            ["RESOLVED", "CLOSED"].includes(item.status))) &&
        (!needle ||
          `${item.supervisorName} ${item.supervisorUsername} ${item.supervisorContact || ""} ${conversationGroups(item).join(" ")} ${item.lastMessage}`
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .toLowerCase()
            .includes(needle)),
    );
  }, [
    conversations,
    conversationQuery,
    conversationViewFilter,
    groupFilter,
  ]);
  const visibleContacts = useMemo(() => {
    const needle = contactQuery
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();
    return contacts.filter(
      (contact) =>
        !needle ||
        `${contact.displayName} ${contact.username} ${contact.contact || ""} ${(contact.groups || []).join(" ")}`
          .normalize("NFD")
          .replace(/[\u0300-\u036f]/g, "")
          .toLowerCase()
          .includes(needle),
    );
  }, [contacts, contactQuery]);
  const availableGroups = useMemo(
    () =>
      Array.from(
        new Set(conversations.flatMap((item) => conversationGroups(item))),
      ).sort((a, b) => a.localeCompare(b, "es")),
    [conversations],
  );
  const current = conversations.find((item) => item.id === activeId) || null;
  const conversationFilterCounts = useMemo(
    () => ({
      ALL: conversations.length,
      WAITING_SUPPORT: conversations.filter(
        (item) => item.status === "WAITING_SUPPORT",
      ).length,
      IN_PROGRESS: conversations.filter(
        (item) => item.status === "IN_PROGRESS",
      ).length,
      FINISHED: conversations.filter((item) =>
        ["RESOLVED", "CLOSED"].includes(item.status),
      ).length,
    }),
    [conversations],
  );
  const visibleMessages = useMemo(() => {
    const needle = messageQuery.trim().toLocaleLowerCase("es");
    return needle ? messages.filter((item) => `${item.senderName} ${item.message}`.toLocaleLowerCase("es").includes(needle)) : messages;
  }, [messages, messageQuery]);
  const isDirect = Boolean(current?.category?.startsWith("DIRECT:"));
  const conversationLocked = Boolean(
    current?.isBlocked || (isSupervisor && current?.isRestricted),
  );

  useEffect(() => {
    if (!replyTo) return;
    const frame = window.requestAnimationFrame(() => {
      composerTextareaRef.current?.focus();
    });
    return () => window.cancelAnimationFrame(frame);
  }, [replyTo]);
  const departmentName = (code: string) =>
    catalog.find((item) => item.code === code)?.name ||
    departmentFallback[code] ||
    code;
  const waitingCount = scopedConversations.filter(
    (item) => item.status === "WAITING_SUPPORT",
  ).length;
  const activeCategories = useMemo(
    () =>
      catalog.find((item) => item.code === selectedDepartment)
        ? departmentCategories(
            catalog.find((item) => item.code === selectedDepartment),
          ).filter((item) => item.isActive)
        : [],
    [catalog, selectedDepartment],
  );

  const handleError = useCallback((value: unknown) => {
    if ((value as Error & { status?: number }).status === 401)
      onSessionExpiredRef.current();
    else setError((value as Error).message);
  }, []);
  const touchPresence = useCallback(
    async (isTyping = false) => {
      if (!presenceEnabled || presenceRequestInFlight.current) return;
      presenceRequestInFlight.current = true;
      try {
        const next = await chatRequest<Presence>(
          "/api/support/chat/presence",
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ conversationId: activeId, isTyping }),
          },
        );
        setPresence(normalizePresence(next));
      } catch (value) {
        if (
          (value as Error & { status?: number }).status === 404 ||
          (value as Error & { status?: number }).status === 405
        )
          setPresenceEnabled(false);
      } finally {
        presenceRequestInFlight.current = false;
      }
    },
    [activeId, presenceEnabled],
  );
  const loadConversations = useCallback(async () => {
    try {
      const data = await chatRequest<{ conversations: Conversation[] }>(
        "/api/support/chat/conversations",
      );
      const rows = (
        Array.isArray(data.conversations) ? data.conversations : []
      ).map((item) => ({
        ...item,
        groups: conversationGroups(item),
        supervisorContact: item.supervisorContact || null,
        supervisorAvatarUrl: item.supervisorAvatarUrl || null,
        status: item.status || "WAITING_SUPPORT",
        lastMessage: item.lastMessage || "",
        lastMessageAt: item.lastMessageAt || item.updatedAt || item.createdAt,
        isMuted: Boolean(item.isMuted),
        isRestricted: Boolean(item.isRestricted),
        isBlocked: Boolean(item.isBlocked),
      }));
      setConversations((currentRows) => {
        const signature = (items: Conversation[]) =>
          items
            .map(
              (item) =>
                `${item.id}:${item.updatedAt}:${item.lastMessageAt}:${item.status}:${item.lastMessage}`,
            )
            .join("|");
        return signature(currentRows) === signature(rows) ? currentRows : rows;
      });
    } catch (value) {
      handleError(value);
    }
  }, [handleError]);
  const loadMessages = useCallback(async (id: string) => {
    try {
      const data = await chatRequest<{ messages: Message[] }>(
        `/api/support/chat/conversations/${id}/messages`,
      );
      const rows = (Array.isArray(data.messages) ? data.messages : []).map((message) => ({
          ...message,
          senderUserId: message.senderUserId || null,
          avatarUrl: message.avatarUrl || null,
        }));
      setMessages((currentRows) => {
        const signature = (items: Message[]) =>
          items
            .map(
              (item) =>
                `${item.id}:${item.createdAt}:${item.message}:${item.voiceNoteUrl || ""}`,
            )
            .join("|");
        return signature(currentRows) === signature(rows) ? currentRows : rows;
      });
    } catch (value) {
      handleError(value);
    }
  }, [handleError]);
  useEffect(() => {
    if (!open) return;
    setLoading(true);
    Promise.all([
      loadConversations(),
      chatRequest<{ contacts: SupportContact[] }>("/api/support/chat/contacts")
        .then((data) =>
          setContacts(Array.isArray(data.contacts) ? data.contacts : []),
        )
        .catch(handleError),
      chatRequest<{ departments: Department[] }>("/api/support/catalog")
        .then((data) =>
          setCatalog(
            (Array.isArray(data.departments) ? data.departments : []).map(
              (department) => ({
                ...department,
                categories: departmentCategories(department),
              }),
            ),
          ),
        )
        .catch(handleError),
      chatRequest<ContactProfile>("/api/account/contact-profile")
        .then((data) => {
          setProfile({
            ...data,
            groups: Array.isArray(data.groups) ? data.groups : [],
          });
          setProfileContact(data.contact || "");
        })
        .catch(handleError),
    ]).finally(() => setLoading(false));
  }, [open, session.id, handleError, loadConversations]);
  useEffect(() => {
    if (!open) return;
    const refresh = () => {
      if (document.hidden) return;
      void loadConversations();
      if (activeId) void loadMessages(activeId);
    };
    const timer = window.setInterval(refresh, 30_000);
    window.addEventListener("support-live-refresh", refresh);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("support-live-refresh", refresh);
    };
  }, [open, activeId, loadConversations, loadMessages]);
  useEffect(() => {
    if (!open) return;
    chatRequest<{ supportChat?: string }>("/api/version")
      .then((data) =>
        setPresenceEnabled(
          String(data.supportChat || "").includes("presence-touch-v2"),
        ),
      )
      .catch(() => setPresenceEnabled(false));
  }, [open]);
  useEffect(() => {
    if (!open || !presenceEnabled) return;
    void touchPresence(false);
    const heartbeat = window.setInterval(
      () => void touchPresence(false),
      20_000,
    );
    return () => window.clearInterval(heartbeat);
  }, [open, activeId, session.id, presenceEnabled, touchPresence]);
  useEffect(() => {
    if (!open || !activeId || !presenceEnabled) return;
    const refreshPresence = () => {
      if (document.hidden) return Promise.resolve();
      return chatRequest<Presence>(`/api/support/chat/presence/${activeId}`)
        .then((value) => setPresence(normalizePresence(value)))
        .catch(() => undefined);
    };
    void refreshPresence();
    const timer = window.setInterval(refreshPresence, 5_000);
    window.addEventListener("support-live-refresh", refreshPresence);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("support-live-refresh", refreshPresence);
    };
  }, [open, activeId, session.id, presenceEnabled]);
  useEffect(() => {
    if (!open || !activeId) return;
    const timer = window.setTimeout(
      () => void touchPresence(Boolean(draft.trim())),
      350,
    );
    return () => window.clearTimeout(timer);
  }, [draft, open, activeId, touchPresence]);
  useEffect(() => {
    if (activeId) void loadMessages(activeId);
    else setMessages([]);
    setReplyTo(null);
    setNotice("");
  }, [activeId, loadMessages]);
  useEffect(() => {
    if (!notice) return;
    const timer = window.setTimeout(() => setNotice(""), 3500);
    return () => window.clearTimeout(timer);
  }, [notice]);
  useEffect(() => {
    messageEndRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages.length, activeId]);
  useEffect(() => {
    setReplyTo(null);
  }, [activeId]);
  useEffect(() => {
    if (!openConversationId) return;
    setRouteStep(null);
    setSelectedDepartment(null);
    setActiveId(openConversationId);
    setOpen(true);
  }, [openConversationId, openRequestKey]);

  const controlConversation = async (
    action:
      | "MUTE"
      | "UNMUTE"
      | "RESTRICT"
      | "UNRESTRICT"
      | "BLOCK"
      | "UNBLOCK"
      | "DELETE",
    conversationId = activeId,
  ) => {
    if (!conversationId || sending) return;
    if (
      action === "DELETE" &&
      !window.confirm(
        "¿Eliminar esta conversación? Se ocultará del CRM y la acción quedará auditada.",
      )
    )
      return;
    setSending(true);
    setError("");
    setNotice("");
    try {
      await chatRequest(`/api/support/chat/conversations/${conversationId}/control`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ action }),
      });
      setSidebarMenu(null);
      if (action === "DELETE") {
        if (activeId === conversationId) { setActiveId(null); setMessages([]); }
      }
      await loadConversations();
      setNotice(
        action === "DELETE"
          ? "Conversación eliminada de forma segura."
          : "Control de conversación actualizado.",
      );
    } catch (value) {
      handleError(value);
    } finally {
      setSending(false);
    }
  };

  const beginLongPress = (item: Conversation) => {
    if (longPressTimer.current) window.clearTimeout(longPressTimer.current);
    longPressTimer.current = window.setTimeout(() => {
      setSidebarMenu(item);
      longPressTimer.current = null;
    }, 550);
  };
  const cancelLongPress = () => {
    if (longPressTimer.current) window.clearTimeout(longPressTimer.current);
    longPressTimer.current = null;
  };

  const stopRecording = () => {
    if (recorderRef.current?.state === "recording") recorderRef.current.stop();
  };
  const startRecording = async () => {
    if (!activeId || sending || recording) return;
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const preferred = ["audio/webm;codecs=opus", "audio/webm", "audio/ogg;codecs=opus"].find((type) => MediaRecorder.isTypeSupported(type));
      const recorder = new MediaRecorder(stream, preferred ? { mimeType: preferred } : undefined);
      recorderChunks.current = [];
      recordingSecondsRef.current = 0;
      setRecordingSeconds(0);
      recorder.ondataavailable = (event) => { if (event.data.size) recorderChunks.current.push(event.data); };
      recorder.onstop = async () => {
        stream.getTracks().forEach((track) => track.stop());
        if (recordingTimer.current) window.clearInterval(recordingTimer.current);
        setRecording(false);
        const blob = new Blob(recorderChunks.current, { type: recorder.mimeType || "audio/webm" });
        if (!blob.size || !activeId) return;
        const form = new FormData();
        form.append("voice", blob, `nota-voz-${Date.now()}.webm`);
        form.append("durationSeconds", String(recordingSecondsRef.current));
        setSending(true);
        try {
          await chatRequest(`/api/support/chat/conversations/${activeId}/voice`, { method: "POST", body: form });
          await Promise.all([loadMessages(activeId), loadConversations()]);
        } catch (value) { handleError(value); } finally { setSending(false); }
      };
      recorderRef.current = recorder;
      recorder.start(250);
      setRecording(true);
      recordingTimer.current = window.setInterval(() => {
        recordingSecondsRef.current += 1;
        setRecordingSeconds(recordingSecondsRef.current);
      }, 1000);
    } catch { setError("No fue posible usar el micrófono. Revisa el permiso del navegador."); }
  };

  const beginConversation = async (
    department: string,
    category: string | null,
    requestedAgent: boolean,
  ) => {
    setSending(true);
    setError("");
    try {
      const created = await chatRequest<Conversation>(
        "/api/support/chat/conversations",
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ department, category, requestedAgent }),
        },
      );
      setRouteStep(null);
      setContactDirectoryOpen(false);
      setContactQuery("");
      setSelectedDepartment(null);
      await loadConversations();
      setActiveId(created.id);
      await loadMessages(created.id);
    } catch (value) {
      handleError(value);
    } finally {
      setSending(false);
    }
  };
  const beginDirectConversation = async (contact: SupportContact) => {
    if (sending) return;
    setSending(true);
    setError("");
    try {
      const created = await chatRequest<Conversation>(
        "/api/support/chat/direct",
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ targetUserId: contact.id }),
        },
      );
      setRouteStep(null);
      returnToContactsRef.current = true;
      setContactDirectoryOpen(false);
      setContactQuery("");
      await loadConversations();
      setActiveId(created.id);
      await loadMessages(created.id);
      setNotice(`Conversación directa con ${contact.displayName}.`);
    } catch (value) {
      handleError(value);
    } finally {
      setSending(false);
    }
  };
  const sendText = async (text: string, restoreOnError = true) => {
    const message = text.trim();
    if (!activeId || !message || sending) return;
    setSending(true);
    setError("");
    setDraft("");
    try {
      await chatRequest(
        `/api/support/chat/conversations/${activeId}/messages`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ message }),
        },
      );
      await Promise.all([loadMessages(activeId), loadConversations()]);
    } catch (value) {
      if (restoreOnError) setDraft(message);
      handleError(value);
    } finally {
      setSending(false);
    }
  };
  const sendMessage = (event: React.FormEvent) => {
    event.preventDefault();
    const text = replyTo
      ? `↪ ${replyTo.senderName}: ${replyTo.message.slice(0, 120)}\n${draft}`
      : draft;
    setReplyTo(null);
    void sendText(text);
  };
  const copyMessage = async (message: Message) => {
    await navigator.clipboard.writeText(message.message);
    setCopiedMessageId(message.id);
    window.setTimeout(() => setCopiedMessageId(null), 1400);
  };
  const deleteMessage = async (message: Message) => {
    if (sending || message.isDeleted) return;
    if (!window.confirm("¿Eliminar este mensaje? Se mostrará como eliminado y la acción quedará registrada.")) return;
    setSending(true);
    setError("");
    try {
      await chatRequest(`/api/support/chat/messages/${message.id}`, { method: "DELETE" });
      if (activeId) await Promise.all([loadMessages(activeId), loadConversations()]);
      setNotice("Mensaje eliminado correctamente.");
      if (replyTo?.id === message.id) setReplyTo(null);
    } catch (value) {
      handleError(value);
    } finally {
      setSending(false);
    }
  };
  const toggleDesktopNotifications = async () => {
    if (!desktopNotifications && "Notification" in window) {
      const permission = await Notification.requestPermission();
      if (permission !== "granted") { setError("El navegador no autorizó las notificaciones."); return; }
    }
    const next = !desktopNotifications;
    setDesktopNotifications(next);
    localStorage.setItem("supportChatNotifications", next ? "1" : "0");
  };
  const toggleCompactMode = () => {
    const next = !compactMode; setCompactMode(next); localStorage.setItem("supportChatCompact", next ? "1" : "0");
  };
  const toggleEnterToSend = () => {
    const next = !enterToSend; setEnterToSend(next); localStorage.setItem("supportChatEnterToSend", next ? "1" : "0");
  };
  const updateStatus = async (status: string) => {
    if (!activeId) return;
    setSending(true);
    try {
      await chatRequest(`/api/support/chat/conversations/${activeId}/status`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status }),
      });
      await loadConversations();
    } catch (value) {
      handleError(value);
    } finally {
      setSending(false);
    }
  };
  const startNew = () => {
    setActiveId(null);
    setMessages([]);
    setSelectedDepartment(null);
    setRouteStep("departments");
    setError("");
  };
  const selectDepartment = (code: string) => {
    if (routeStep === "agent_departments") {
      void beginConversation(code, "PERSONAL", true);
      return;
    }
    setSelectedDepartment(code);
    const categories = departmentCategories(
      catalog.find((item) => item.code === code),
    ).filter((item) => item.isActive);
    if (categories.length) setRouteStep("categories");
    else void beginConversation(code, "GENERAL", false);
  };
  const saveContactProfile = async () => {
    setSending(true);
    try {
      await chatRequest("/api/account/contact-profile", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ contact: profileContact }),
      });
      setProfile((current) =>
        current
          ? { ...current, contact: profileContact.trim() || null }
          : current,
      );
      setProfileOpen(false);
    } catch (value) {
      handleError(value);
    } finally {
      setSending(false);
    }
  };
  const uploadProfileAvatar = async (file: File) => {
    const form = new FormData();
    form.append("avatar", file);
    setSending(true);
    try {
      const data = await chatRequest<{ avatarUrl: string }>(
        "/api/account/avatar",
        { method: "POST", body: form },
      );
      setProfile((current) =>
        current ? { ...current, avatarUrl: data.avatarUrl } : current,
      );
      await loadConversations();
    } catch (value) {
      handleError(value);
    } finally {
      setSending(false);
    }
  };

  return (
    <>
      <button
        className="support-chat-launcher"
        onClick={() => {
          setActiveId(null);
          setMessages([]);
          setReplyTo(null);
          setRouteStep(null);
          setSelectedDepartment(null);
          setContactDirectoryOpen(false);
          setProfileOpen(false);
          setSidebarMenu(null);
          returnToContactsRef.current = false;
          setOpen(true);
        }}
        aria-label="Abrir chat de soporte"
      >
        <MessageCircle />
        <span>
          <strong>Chat de soporte</strong>
          <small>
            {isSupervisor
              ? "Asistente y personal"
              : "Bandeja compartida"}
          </small>
        </span>
        {canServe && waitingCount > 0 && (
          <em>{waitingCount > 99 ? "99+" : waitingCount}</em>
        )}
      </button>
      {open && (
        <div
          className="support-chat-backdrop"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) closeChat();
          }}
        >
          <section
            className={`support-chat-crm ${compactMode ? "chat-compact" : ""}`}
            role="dialog"
            aria-modal="true"
            aria-label="Chat CRM de soporte"
          >
            <header>
              <div>
                <span>
                  <Headphones /> CENTRO DE ASISTENCIA
                </span>
                <h2>Chat de soporte</h2>
                <p>
                  {isSupervisor
                    ? "Comunícate con cualquier técnico del equipo de soporte."
                    : "Bandeja compartida con todos los supervisores y técnicos autorizados."}
                </p>
              </div>
              <div className="chat-header-actions">
                <button onClick={() => setProfileOpen(true)}>
                  <UserRound />
                  Mi perfil
                </button>
                <button onClick={closeChat} aria-label="Cerrar chat">
                  <X />
                </button>
              </div>
            </header>
            <div className={`support-chat-layout${activeId ? " has-active-chat" : ""}`}>
              <aside>
                <div className="chat-sidebar-heading">
                  <span>
                    <strong>Conversaciones</strong>
                    <small>{scopedConversations.length} registradas</small>
                  </span>
                  <div className="chat-sidebar-actions">
                    <button
                      type="button"
                      onClick={() => {
                        setSidebarMenu(null);
                        setContactDirectoryOpen(true);
                      }}
                      aria-label="Abrir contactos"
                    >
                      <UserRound /> <span>Contactos</span>
                    </button>
                    {isSupervisor && (
                      <button
                        type="button"
                        onClick={startNew}
                        aria-label="Nueva conversación"
                      >
                        <Plus /> <span>Nueva</span>
                      </button>
                    )}
                    <button
                      type="button"
                      className="chat-mobile-profile"
                      onClick={() => setProfileOpen(true)}
                      aria-label="Abrir mi perfil"
                    >
                      <Settings />
                    </button>
                    <button
                      type="button"
                      className="chat-mobile-close"
                      onClick={closeChat}
                      aria-label="Cerrar chat"
                    >
                      <X />
                    </button>
                  </div>
                </div>
                <label className="chat-conversation-search">
                  <Search />
                  <input
                    value={conversationQuery}
                    onChange={(event) =>
                      setConversationQuery(event.target.value)
                    }
                    placeholder="Buscar conversación..."
                  />
                </label>
                <nav
                  className="chat-filter-chips"
                  aria-label="Filtrar conversaciones"
                >
                  {(
                    [
                      ["ALL", "Todos"],
                      ["WAITING_SUPPORT", "Pendientes"],
                      ["IN_PROGRESS", "En curso"],
                      ["FINISHED", "Resueltos"],
                    ] as const
                  ).map(([value, label]) => (
                    <button
                      type="button"
                      key={value}
                      className={conversationViewFilter === value ? "active" : ""}
                      onClick={() => setConversationViewFilter(value)}
                    >
                      {label}
                      <span>{conversationFilterCounts[value]}</span>
                    </button>
                  ))}
                </nav>
                {session.role === "Administrator" &&
                  !!availableGroups.length && (
                    <label className="chat-group-filter">
                      Grupo
                      <select
                        value={groupFilter}
                        onChange={(event) => setGroupFilter(event.target.value)}
                      >
                        <option value="ALL">Todos los grupos</option>
                        {availableGroups.map((group) => (
                          <option key={group}>{group}</option>
                        ))}
                      </select>
                    </label>
                  )}
                <div className="chat-conversation-list">
                  {scopedConversations.map((item) => (
                    <button
                      key={item.id}
                      className={activeId === item.id ? "active" : ""}
                      onPointerDown={() => beginLongPress(item)}
                      onPointerUp={cancelLongPress}
                      onPointerLeave={cancelLongPress}
                      onPointerMove={cancelLongPress}
                      onContextMenu={(event) => { event.preventDefault(); setSidebarMenu(item); }}
                      onClick={() => {
                        returnToContactsRef.current = false;
                        setRouteStep(null);
                        setSelectedDepartment(null);
                        setActiveId(item.id);
                      }}
                    >
                      <span
                        className={`chat-list-icon${item.supervisorAvatarUrl && !isSupervisor ? " has-avatar" : ""}`}
                      >
                        {item.supervisorAvatarUrl && !isSupervisor ? (
                          <img
                            src={item.supervisorAvatarUrl}
                            alt={item.supervisorName}
                          />
                        ) : item.requestedAgent ? (
                          <UserRound />
                        ) : (
                          <MessageCircle />
                        )}
                      </span>
                      <span className="chat-list-copy">
                        <span className="chat-list-title-row">
                          <strong>
                            {item.category?.startsWith("DIRECT:")
                              ? item.supervisorName
                              : isSupervisor
                                ? departmentName(item.assignedDepartment)
                                : item.supervisorName}
                          </strong>
                          <time>{conversationTimeLabel(item.lastMessageAt)}</time>
                        </span>
                        <small>
                          {item.lastMessage || "Conversación iniciada"}
                        </small>
                        <em>
                          {statusLabels[item.status] || item.status} ·{" "}
                          {conversationGroups(item).join(", ") || "Sin grupo"}
                          {item.isMuted ? " · Silenciada" : ""}
                          {item.isBlocked ? " · Bloqueada" : ""}
                        </em>
                      </span>
                    </button>
                  ))}
                  {!scopedConversations.length && !loading && (
                    <div className="chat-list-empty">
                      <MessageCircle />
                      <span>Sin conversaciones</span>
                    </div>
                  )}
                </div>
              </aside>
              <main>
                {loading && !catalog.length ? (
                  <div className="chat-loading">
                    <LoaderCircle className="spin" /> Preparando asistencia...
                  </div>
                ) : routeStep || (!activeId && isSupervisor) ? (
                  <div className="support-bot-flow">
                    <div className="chat-bot-message">
                      <span>
                        <Bot />
                      </span>
                      <div>
                        <strong>Asistente de soporte</strong>
                        <p>
                          {routeStep === "categories"
                            ? `¿Qué necesitas de ${departmentName(selectedDepartment || "")}?`
                            : routeStep === "agent_departments"
                              ? "¿Con qué departamento deseas comunicarte?"
                              : "¡Hola! ¿Qué deseas gestionar hoy? Selecciona una opción para dirigir tu solicitud."}
                        </p>
                      </div>
                    </div>
                    {routeStep === "categories" ? (
                      <>
                        <button
                          className="chat-flow-back"
                          onClick={() => {
                            setSelectedDepartment(null);
                            setRouteStep("departments");
                          }}
                        >
                          <ArrowLeft /> Cambiar departamento
                        </button>
                        <div className="chat-option-grid">
                          {activeCategories.map((category) => (
                            <button
                              key={category.id}
                              onClick={() =>
                                void beginConversation(
                                  selectedDepartment!,
                                  category.code,
                                  false,
                                )
                              }
                              disabled={sending}
                            >
                              <span>
                                <CheckCircle2 />
                              </span>
                              <div>
                                <strong>{category.name}</strong>
                                <small>{category.description}</small>
                              </div>
                            </button>
                          ))}
                          <button
                            className="request-agent-option"
                            onClick={() =>
                              void beginConversation(
                                selectedDepartment!,
                                "PERSONAL",
                                true,
                              )
                            }
                            disabled={sending}
                          >
                            <span>
                              <UserRound />
                            </span>
                            <div>
                              <strong>Comunicarme con una persona</strong>
                              <small>
                                Pasar directamente al personal del departamento.
                              </small>
                            </div>
                          </button>
                        </div>
                      </>
                    ) : (
                      <div className="chat-option-grid department-options">
                        {catalog
                          .filter((item) => item.isActive)
                          .map((department) => (
                            <button
                              key={department.code}
                              style={
                                {
                                  "--chat-accent": department.accentColor,
                                } as React.CSSProperties
                              }
                              onClick={() => selectDepartment(department.code)}
                              disabled={sending}
                            >
                              <span>
                                <Building2 />
                              </span>
                              <div>
                                <strong>{department.name}</strong>
                                <small>{department.description}</small>
                              </div>
                            </button>
                          ))}
                        {routeStep !== "agent_departments" && (
                          <button
                            className="request-agent-option"
                            onClick={() => setRouteStep("agent_departments")}
                          >
                            <span>
                              <UserRound />
                            </span>
                            <div>
                              <strong>Comunicarme con una persona</strong>
                              <small>
                                Elegir el departamento y hablar con su personal.
                              </small>
                            </div>
                          </button>
                        )}
                      </div>
                    )}
                  </div>
                ) : current ? (
                  <>
                    <div className="chat-thread-header">
                      <div>
                        <button
                          type="button"
                          className="chat-mobile-back"
                          onClick={() => {
                            setActiveId(null);
                            setMessages([]);
                            setReplyTo(null);
                            if (returnToContactsRef.current) {
                              setContactDirectoryOpen(true);
                            }
                          }}
                          aria-label={returnToContactsRef.current ? "Volver a contactos" : "Volver a la lista de chats"}
                        >
                          <ArrowLeft />
                          <span>{returnToContactsRef.current ? "Contactos" : "Conversaciones"}</span>
                        </button>
                        <span className="chat-thread-avatar">
                          {!isSupervisor && current.supervisorAvatarUrl ? (
                            <img
                              src={current.supervisorAvatarUrl}
                              alt={current.supervisorName}
                            />
                          ) : isSupervisor ? (
                            <Headphones />
                          ) : (
                            <UserRound />
                          )}
                        </span>
                        <span>
                          <strong>
                            {isSupervisor
                              ? departmentName(current.assignedDepartment)
                              : current.supervisorName}
                          </strong>
                          <small>
                            {current.category?.startsWith("DIRECT:")
                              ? "Conversación directa"
                              : current.category || "Asistencia general"}
                            {current.requestedAgent
                              ? " · Solicitó personal"
                              : ""}
                          </small>
                          {!isSupervisor && (
                            <small className="chat-contact-line">
                              {current.supervisorContact ||
                                "Contacto no registrado"}
                              {conversationGroups(current).length
                                ? ` · ${conversationGroups(current).join(", ")}`
                                : ""}
                            </small>
                          )}
                        </span>
                      </div>
                      <div className="chat-thread-tools">
                        <div className="chat-presence-status">
                          <span
                            className={presence.supportOnline ? "online" : ""}
                          >
                            <Wifi />
                            {presence.supportOnline
                              ? "Soporte en línea"
                              : "Soporte desconectado"}
                          </span>
                          <em
                            className={`chat-status status-${current.status.toLowerCase()}`}
                          >
                            {statusLabels[current.status] || current.status}
                          </em>
                          {(current.isMuted ||
                            current.isRestricted ||
                            current.isBlocked) && (
                            <div className="chat-control-state-chips">
                              {current.isMuted && (
                                <span>
                                  <VolumeX /> Silenciada
                                </span>
                              )}
                              {current.isRestricted && (
                                <span>
                                  <LockKeyhole /> Restringida
                                </span>
                              )}
                              {current.isBlocked && (
                                <span className="danger">
                                  <ShieldBan /> Bloqueada
                                </span>
                              )}
                            </div>
                          )}
                        </div>
                        <button
                          type="button"
                          className="chat-thread-close"
                          onClick={closeChat}
                          aria-label="Salir del chat"
                          title="Salir del chat"
                        >
                          <X />
                        </button>
                      </div>
                    </div>
                    <div className="chat-message-list">
                      {visibleMessages.map((message) => (
                        <article
                          key={message.id}
                          className={`${message.isBot ? "bot" : message.senderUserId === session.id ? "mine" : "theirs"}${message.isDeleted ? " deleted" : ""}`}
                        >
                          <span>
                            {message.isBot ? (
                              <Bot />
                            ) : message.avatarUrl ? (
                              <img
                                src={message.avatarUrl}
                                alt={message.senderName}
                              />
                            ) : (
                              <UserRound />
                            )}
                          </span>
                          <div>
                            <header>
                              <strong>{message.senderName}</strong>
                              <time>
                                {chatDate(message.createdAt).toLocaleString(
                                  "es-DO",
                                )}
                              </time>
                            </header>
                            <p>{message.isDeleted ? "Este mensaje fue eliminado." : message.message}</p>
                            {message.voiceNoteUrl && (
                              <audio className="chat-voice-player" controls preload="metadata" src={message.voiceNoteUrl}>
                                Tu navegador no puede reproducir esta nota de voz.
                              </audio>
                            )}
                            {!message.isBot && !message.isDeleted && (
                              <div className="chat-message-tools">
                                <button type="button" onClick={() => setReplyTo(message)} title="Responder"><Reply /> Responder</button>
                                <button type="button" onClick={() => void copyMessage(message)} title="Copiar mensaje"><Copy /> {copiedMessageId === message.id ? "Copiado" : "Copiar"}</button>
                                {(message.senderUserId === session.id || session.role === "Administrator") && <button className="delete" type="button" onClick={() => void deleteMessage(message)} title="Eliminar mensaje"><Trash2 /> Eliminar</button>}
                              </div>
                            )}
                          </div>
                        </article>
                      ))}
                      {!messages.length && (
                        <div className="chat-loading">
                          <MessageCircle /> No hay mensajes todavía.
                        </div>
                      )}
                      {!!presence.typing.length && (
                        <div className="chat-typing-indicator">
                          <i /> <i /> <i />
                          <span>
                            {presence.typing.join(", ")} está escribiendo...
                          </span>
                        </div>
                      )}
                      <div ref={messageEndRef} className="chat-message-end" />
                    </div>
                    {conversationLocked && (
                      <div className="chat-conversation-locked">
                        <LockKeyhole />
                        <span>
                          <strong>
                            {current.isBlocked
                              ? "Conversación bloqueada"
                              : "Conversación restringida"}
                          </strong>
                          <small>
                            {current.isBlocked
                              ? "No se pueden enviar mensajes hasta que soporte la desbloquee."
                              : "El supervisor no puede responder mientras exista esta restricción."}
                          </small>
                        </span>
                      </div>
                    )}
                    {canServe && !isDirect && !current.isBlocked && (
                      <div className="chat-agent-actions">
                        <button
                          onClick={() => void updateStatus("IN_PROGRESS")}
                          disabled={sending}
                        >
                          Tomar conversación
                        </button>
                        <button
                          onClick={() => void updateStatus("RESOLVED")}
                          disabled={sending}
                        >
                          Marcar resuelta
                        </button>
                        <button
                          onClick={() => void updateStatus("CLOSED")}
                          disabled={sending}
                        >
                          Cerrar
                        </button>
                      </div>
                    )}
                    {canServe &&
                      !current.isBlocked &&
                      !["RESOLVED", "CLOSED"].includes(current.status) && (
                        <div className="chat-quick-replies">
                          <span>RESPUESTAS RÁPIDAS</span>
                          <div>
                            {quickReplies.map((reply) => (
                              <button
                                type="button"
                                key={reply}
                                disabled={sending}
                                onClick={() => void sendText(reply, false)}
                              >
                                {reply}
                              </button>
                            ))}
                          </div>
                        </div>
                      )}{" "}
                    {!conversationLocked &&
                      !["RESOLVED", "CLOSED"].includes(current.status) && (
                        <form className="chat-composer" onSubmit={sendMessage}>
                          {replyTo && (
                            <div className="chat-reply-preview">
                              <Reply />
                              <span><strong>Respondiendo a {replyTo.senderName}</strong><small>{replyTo.message.slice(0, 120)}</small></span>
                              <button type="button" onClick={() => setReplyTo(null)} aria-label="Cancelar respuesta"><X /></button>
                            </div>
                          )}
                          <div className="chat-composer-row">
                            {recording ? (
                              <div className="chat-recording-status"><span /> Grabando {Math.floor(recordingSeconds / 60)}:{String(recordingSeconds % 60).padStart(2, "0")}</div>
                            ) : (
                            <textarea
                              ref={composerTextareaRef}
                              value={draft}
                              onChange={(event) => setDraft(event.target.value)}
                              onKeyDown={(event) => {
                                if (enterToSend && event.key === "Enter" && !event.shiftKey) {
                                  event.preventDefault();
                                  event.currentTarget.form?.requestSubmit();
                                }
                              }}
                              maxLength={2000}
                              placeholder={
                                isSupervisor
                                  ? "Describe la avería o responde al personal..."
                                  : "Escribe una respuesta para el supervisor..."
                              }
                            />
                            )}
                            <button type="button" className={`chat-mic-button ${recording ? "recording" : ""}`} disabled={sending} onClick={() => recording ? stopRecording() : void startRecording()} aria-label={recording ? "Detener y enviar nota de voz" : "Grabar nota de voz"}>
                              {recording ? <Square /> : <Mic />}
                            </button>
                            <button disabled={sending || !draft.trim()}>
                              {sending ? (
                                <LoaderCircle className="spin" />
                              ) : (
                                <Send />
                              )}
                            </button>
                          </div>
                          {!recording && <small className="chat-composer-counter">{draft.length}/2000 · Enter para enviar</small>}
                        </form>
                      )}
                  </>
                ) : (
                  <div className="chat-select-conversation">
                    <MessageCircle />
                    <strong>Selecciona una conversación</strong>
                    <span>
                      Abre un caso de la bandeja para consultar y responder.
                    </span>
                  </div>
                )}
                {notice && (
                  <div className="chat-notice" role="status">
                    <CheckCircle2 />
                    <span>{notice}</span>
                    <button type="button" onClick={() => setNotice("")} aria-label="Cerrar aviso"><X /></button>
                  </div>
                )}
                {error && <div className="chat-error">{error}</div>}
              </main>
            </div>
            {contactDirectoryOpen && (
              <div
                className="chat-contact-modal-backdrop"
                onMouseDown={(event) => {
                  if (event.target === event.currentTarget)
                    setContactDirectoryOpen(false);
                }}
              >
                <section
                  className="chat-contact-modal"
                  role="dialog"
                  aria-modal="true"
                  aria-label="Seleccionar contacto"
                >
                  <header>
                    <div>
                      <UserRound />
                      <span>
                        <strong>Nueva conversación</strong>
                        <small>Supervisores y técnicos disponibles</small>
                      </span>
                    </div>
                    <button
                      type="button"
                      onClick={() => setContactDirectoryOpen(false)}
                      aria-label="Cerrar contactos"
                    >
                      <X />
                    </button>
                  </header>
                  <label>
                    <Search />
                    <input
                      autoFocus
                      value={contactQuery}
                      onChange={(event) => setContactQuery(event.target.value)}
                      placeholder="Buscar por nombre, usuario, grupo o teléfono..."
                    />
                  </label>
                  <div className="chat-contact-modal-list">
                    {visibleContacts.map((contact) => (
                      <button
                        type="button"
                        key={contact.id}
                        disabled={sending}
                        onClick={() => void beginDirectConversation(contact)}
                      >
                        <span className="chat-contact-avatar">
                          {contact.avatarUrl ? (
                            <img
                              src={contact.avatarUrl}
                              alt={contact.displayName}
                            />
                          ) : (
                            <UserRound />
                          )}
                        </span>
                        <span>
                          <strong>{contact.displayName}</strong>
                          <small>
                            {contactRoleLabels[contact.role] || contact.role}
                          </small>
                          <em>
                            {(contact.groups || []).join(", ") || "Sin grupo"}
                          </em>
                        </span>
                        <span className="chat-contact-meta">
                          <small>{contact.contact || contact.username}</small>
                          <MessageCircle />
                        </span>
                      </button>
                    ))}
                    {!visibleContacts.length && (
                      <div className="chat-list-empty">
                        <Search />
                        <span>No encontramos contactos.</span>
                      </div>
                    )}
                  </div>
                </section>
              </div>
            )}
            {profileOpen && profile && (
              <aside className="chat-profile-panel">
                <header>
                  <strong>Perfil de contacto</strong>
                  <button onClick={() => setProfileOpen(false)}>
                    <X />
                  </button>
                </header>
                <div className="chat-profile-avatar">
                  {profile.avatarUrl ? (
                    <img src={profile.avatarUrl} alt={profile.displayName} />
                  ) : (
                    <UserRound />
                  )}
                  <label>
                    Cambiar imagen
                    <input
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      onChange={(event) => {
                        const file = event.target.files?.[0];
                        if (file) void uploadProfileAvatar(file);
                      }}
                    />
                  </label>
                </div>
                <h3>{profile.displayName}</h3>
                <p>{profile.email}</p>
                <span className="chat-profile-role"><i /> Disponible · {contactRoleLabels[profile.role] || profile.role}</span>
                <div className="chat-profile-stats">
                  <span><strong>{conversations.length}</strong><small>Chats</small></span>
                  <span><strong>{messages.length}</strong><small>Mensajes</small></span>
                  <span><strong>{messages.filter((item) => item.voiceNoteUrl).length}</strong><small>Audios</small></span>
                </div>
                <section className="chat-profile-section">
                  <header><Users /><strong>Grupos y equipos</strong></header>
                  <div className="chat-profile-groups">
                    {(Array.isArray(profile.groups) && profile.groups.length ? profile.groups : ["Equipo de soporte"]).map((group) => <span key={group}>{group}</span>)}
                  </div>
                </section>
                <label className="chat-profile-contact">
                  Información de contacto
                  <input
                    maxLength={80}
                    value={profileContact}
                    onChange={(event) => setProfileContact(event.target.value)}
                    placeholder="Teléfono, extensión o WhatsApp"
                  />
                </label>
                <section className="chat-profile-section">
                  <header><Search /><strong>Buscar en esta conversación</strong></header>
                  <label className="chat-profile-search"><Search /><input value={messageQuery} onChange={(event) => setMessageQuery(event.target.value)} placeholder="Palabra, nombre o mensaje..." /></label>
                  {messageQuery && <small className="chat-profile-result">{visibleMessages.length} resultado(s) encontrados</small>}
                </section>
                <section className="chat-profile-section chat-profile-settings">
                  <header><Bell /><strong>Preferencias del chat</strong></header>
                  <button type="button" onClick={() => void toggleDesktopNotifications()}><span><Bell /><i><strong>Notificaciones</strong><small>Avisos nuevos en este dispositivo</small></i></span><em className={desktopNotifications ? "on" : ""}>{desktopNotifications ? "Sí" : "No"}</em></button>
                  <button type="button" onClick={toggleEnterToSend}><span><Keyboard /><i><strong>Enviar con Enter</strong><small>Shift + Enter crea otra línea</small></i></span><em className={enterToSend ? "on" : ""}>{enterToSend ? "Sí" : "No"}</em></button>
                  <button type="button" onClick={toggleCompactMode}><span><FileAudio /><i><strong>Vista compacta</strong><small>Muestra más mensajes en pantalla</small></i></span><em className={compactMode ? "on" : ""}>{compactMode ? "Sí" : "No"}</em></button>
                </section>
                {current && (
                  <section className="chat-profile-section chat-profile-settings">
                    <header><MessageCircle /><strong>Conversación actual</strong></header>
                    <button type="button" onClick={() => void controlConversation(current.isMuted ? "UNMUTE" : "MUTE")}><span>{current.isMuted ? <Volume2 /> : <VolumeX />}<i><strong>{current.isMuted ? "Activar avisos" : "Silenciar chat"}</strong><small>Controla las alertas de esta conversación</small></i></span></button>
                    {(canDeleteConversation || current.category?.startsWith("DIRECT:")) && <button type="button" className="danger" onClick={() => void controlConversation("DELETE")}><span><Trash2 /><i><strong>Eliminar conversación</strong><small>Esta acción requiere confirmación</small></i></span></button>}
                  </section>
                )}
                <button
                  className="chat-profile-save"
                  disabled={sending}
                  onClick={() => void saveContactProfile()}
                >
                  Guardar perfil
                </button>
              </aside>
            )}
            {sidebarMenu && (
              <div className="chat-sidebar-menu-backdrop" onClick={() => setSidebarMenu(null)}>
                <section className="chat-sidebar-menu" onClick={(event) => event.stopPropagation()}>
                  <header><strong>{sidebarMenu.supervisorName}</strong><small>Mantén pulsada cualquier conversación para abrir este menú</small></header>
                  <button onClick={() => { setActiveId(sidebarMenu.id); setSidebarMenu(null); }}><MessageCircle /> Abrir conversación</button>
                  <button onClick={() => void controlConversation(sidebarMenu.isMuted ? "UNMUTE" : "MUTE", sidebarMenu.id)}>{sidebarMenu.isMuted ? <Volume2 /> : <VolumeX />} {sidebarMenu.isMuted ? "Activar notificaciones" : "Silenciar"}</button>
                  {canModerate && <button onClick={() => void controlConversation(sidebarMenu.isRestricted ? "UNRESTRICT" : "RESTRICT", sidebarMenu.id)}><LockKeyhole /> {sidebarMenu.isRestricted ? "Quitar restricción" : "Restringir"}</button>}
                  {canModerate && <button onClick={() => void controlConversation(sidebarMenu.isBlocked ? "UNBLOCK" : "BLOCK", sidebarMenu.id)}><ShieldBan /> {sidebarMenu.isBlocked ? "Desbloquear" : "Bloquear"}</button>}
                  {(canDeleteConversation || sidebarMenu.category?.startsWith("DIRECT:")) && <button className="danger" onClick={() => void controlConversation("DELETE", sidebarMenu.id)}><Trash2 /> Eliminar conversación</button>}
                  <button onClick={() => setSidebarMenu(null)}><X /> Cancelar</button>
                </section>
              </div>
            )}
          </section>
        </div>
      )}
    </>
  );
}
