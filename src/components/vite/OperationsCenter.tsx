import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import {
  Activity,
  AlertTriangle,
  ArrowRight,
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
  type OperationsAuditEvent,
  type OperationsTicket,
} from "@/lib/operations-center";
import {deviceDate,deviceTimestamp} from "@/lib/display";

type OperationsCenterProps = {
  mode: "audit" | "queue";
  onSessionExpired: () => void;
  onOpenTicket: (ticket: OperationsTicket) => void;
  teamScope?: "CALL_CENTER" | "TECHNICAL_FAILURE" | "TECHNICIANS" | null;
};

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
};

const statusLabels: Record<string, string> = {
  OPEN: "En espera",
  IN_PROGRESS: "En atención",
  PENDING: "Pendiente",
  RESOLVED: "Resuelto",
  CLOSED: "Cerrado",
  CANCELLED: "Anulado",
};

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

const csvCell = (value: unknown) => `"${String(value ?? "").replaceAll('"', '""')}"`;

const downloadAuditCsv = (events: OperationsAuditEvent[]) => {
  const header = ["Fecha", "Ticket", "Acción", "Actor", "Departamento", "Agencia", "Grupo", "Prioridad", "Estado", "Comentario"];
  const rows = events.map((event) => [
    deviceDate(event.createdAt).toLocaleString("es-DO"),
    `#${event.ticketNumber}`,
    actionLabels[event.action] || event.action,
    event.actorName,
    departmentLabels[event.department] || event.department,
    event.agency,
    event.group,
    priorityLabels[event.priority] || event.priority,
    statusLabels[event.status] || event.status,
    event.comment || "",
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

export default function OperationsCenter({ mode, onSessionExpired, onOpenTicket, teamScope = null }: OperationsCenterProps) {
  const [tickets, setTickets] = useState<OperationsTicket[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null);
  const [query, setQuery] = useState("");
  const [department, setDepartment] = useState(teamScope || "ALL");
  const [action, setAction] = useState("ALL");
  const [period, setPeriod] = useState("7");
  const [selectedEvent, setSelectedEvent] = useState<string | null>(null);
  const [now, setNow] = useState(Date.now());
  const [isFullscreen, setIsFullscreen] = useState(Boolean(document.fullscreenElement));
  const [queueZoom, setQueueZoom] = useState<number | null>(null);
  const [viewportWidth, setViewportWidth] = useState(window.innerWidth);
  const loadInFlightRef = useRef(false);
  const hasLoadedRef = useRef(false);

  const load = useCallback(async (silent = false) => {
    if (loadInFlightRef.current) return;
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
      const response = await fetch("/api/tickets", { cache: "no-store" });
      if (response.status === 401) {
        onSessionExpired();
        return;
      }
      const data = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(data.error || "No fue posible cargar la operación de tickets.");
      setTickets(Array.isArray(data.tickets) ? data.tickets : []);
      setUpdatedAt(new Date());
      setError("");
      hasLoadedRef.current = true;
    } catch (loadError) {
      if (!silent || !hasLoadedRef.current)
        setError(loadError instanceof Error ? loadError.message : "No fue posible sincronizar la información.");
    } finally {
      loadInFlightRef.current = false;
      setLoading(false);
      setRefreshing(false);
    }
  }, [onSessionExpired]);

  useEffect(() => {
    void load();
    const refresh = () => {
      if (!document.hidden) void load(true);
    };
    window.addEventListener("support-live-refresh", refresh);
    return () => window.removeEventListener("support-live-refresh", refresh);
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

  useEffect(() => setDepartment(mode === "queue" && teamScope ? teamScope : "ALL"), [mode, teamScope]);

  const auditEvents = useMemo(() => buildTicketAuditEvents(tickets), [tickets]);
  const availableActions = useMemo(
    () => Array.from(new Set(auditEvents.map((event) => event.action))).sort(),
    [auditEvents],
  );
  const filteredEvents = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase("es");
    const cutoff = period === "ALL" ? 0 : Date.now() - Number(period) * 86400000;
    return auditEvents.filter((event) => {
      if (department !== "ALL" && event.department !== department) return false;
      if (action !== "ALL" && event.action !== action) return false;
      if (cutoff && Date.parse(event.createdAt) < cutoff) return false;
      if (!normalizedQuery) return true;
      return `${event.ticketNumber} ${event.subject} ${event.agency} ${event.group} ${event.actorName} ${event.comment || ""}`
        .toLocaleLowerCase("es")
        .includes(normalizedQuery);
    });
  }, [action, auditEvents, department, period, query]);

  const departmentQueues = useMemo(() => activeTicketsByDepartment(tickets), [tickets]);
  const queues = useMemo(() => activeTicketsByTechnologyArea(tickets), [tickets]);
  const queueDepartments = ["CALL_CENTER", "TECHNICAL_FAILURE", "TECHNICIANS"].filter(
    (code) => department === "ALL" || department === code,
  );
  const totalActive = Array.from(queues.values()).reduce((total, items) => total + items.length, 0);
  const totalWaiting = Array.from(queues.values()).flat().filter((ticket) => ticket.status !== "IN_PROGRESS").length;
  const totalInProgress = Array.from(queues.values()).flat().filter((ticket) => ticket.status === "IN_PROGRESS").length;
  const averageMinutes = averageClosedMinutes(tickets);
  const auditedActive = Array.from(departmentQueues.values()).reduce((total, items) => total + items.length, 0);
  const areaMetrics = useMemo(() => ["CALL_CENTER", "TECHNICAL_FAILURE", "TECHNICIANS"].map((code) => {
    const areaTickets = tickets.filter((ticket) => ticket.assignedDepartment === "TECHNOLOGY" && (ticket.assignedTeam || (ticket.assignedTechnicianName ? "TECHNICIANS" : "TECHNICAL_FAILURE")) === code);
    const pending = areaTickets.filter((ticket) => ticket.status === "OPEN" || ticket.status === "PENDING").length;
    const inProgress = areaTickets.filter((ticket) => ticket.status === "IN_PROGRESS").length;
    const completed = areaTickets.filter((ticket) => ticket.status === "RESOLVED" || ticket.status === "CLOSED").length;
    const total = pending + inProgress + completed;
    const percent = (value: number) => total ? Math.round(value * 100 / total) : 0;
    return { code, total, pending, inProgress, completed, pendingPercent: percent(pending), inProgressPercent: percent(inProgress), completedPercent: percent(completed) };
  }), [tickets]);
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
          <h1>{mode === "audit" ? "Auditoría completa de tickets" : "Pantalla de turnos y cola"}</h1>
          <p>{mode === "audit" ? "Consulta quién creó, asignó, transfirió, puso pendiente o resolvió cada caso." : "Visualiza los tickets que van entrando, su posición y el estado de atención por departamento."}</p>
        </div>
        <div className="operations-live">
          <i />
          <span><strong>Actualización automática</strong><small>{updatedAt ? `Última sincronización ${updatedAt.toLocaleTimeString("es-DO")}` : "Conectando..."}</small></span>
          <button type="button" onClick={() => void load(true)} disabled={refreshing} title="Actualizar ahora"><RefreshCw className={refreshing ? "spin" : ""} /></button>
          {mode === "queue" && <button type="button" onClick={() => void toggleFullscreen()} title="Pantalla completa">{isFullscreen ? <Minimize2 /> : <Maximize2 />}</button>}
        </div>
      </header>

      {error && <div className="operations-error"><AlertTriangle /><span><strong>No fue posible sincronizar</strong><small>{error}</small></span><button onClick={() => void load(true)}>Reintentar</button></div>}

      {mode === "audit" ? (
        <>
          <div className="operations-kpis">
            <article><History /><span>Eventos auditados<small>Según los filtros actuales</small></span><strong>{filteredEvents.length}</strong></article>
            <article><Activity /><span>Tickets activos<small>Abiertos, en proceso o pendientes</small></span><strong>{auditedActive}</strong></article>
            <article><CheckCircle2 /><span>Tickets finalizados<small>Resueltos o cerrados</small></span><strong>{tickets.filter((ticket) => ["RESOLVED", "CLOSED"].includes(ticket.status)).length}</strong></article>
            <article><Clock3 /><span>Tiempo medio<small>Hasta la última actualización final</small></span><strong>{formatDuration(averageMinutes)}</strong></article>
          </div>

          <div className="operations-toolbar">
            <label className="operations-search"><Search /><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Buscar ticket, agencia, actor o comentario..." /></label>
            <label><Filter /><select value={department} onChange={(event) => setDepartment(event.target.value)}><option value="ALL">Todas las áreas</option>{Object.entries(technologyAreaLabels).map(([code, label]) => <option key={code} value={code}>{label}</option>)}</select></label>
            <label><History /><select value={action} onChange={(event) => setAction(event.target.value)}><option value="ALL">Todas las acciones</option>{availableActions.map((code) => <option key={code} value={code}>{actionLabels[code] || code}</option>)}</select></label>
            <label><Clock3 /><select value={period} onChange={(event) => setPeriod(event.target.value)}><option value="1">Últimas 24 horas</option><option value="7">Últimos 7 días</option><option value="30">Últimos 30 días</option><option value="ALL">Todo el historial</option></select></label>
            <button type="button" className="operations-export" onClick={() => downloadAuditCsv(filteredEvents)} disabled={!filteredEvents.length}><Download /> Exportar CSV</button>
          </div>

          <div className="audit-ledger">
            <div className="audit-ledger-head"><span>Fecha y hora</span><span>Evento</span><span>Ticket / agencia</span><span>Responsable</span><span>Departamento</span><span>Detalle</span></div>
            {filteredEvents.map((event) => {
              const expanded = selectedEvent === event.id;
              return <article key={event.id} className={`audit-ledger-row action-${event.action.toLowerCase()}${expanded ? " expanded" : ""}`}>
                <time>{deviceDate(event.createdAt).toLocaleDateString("es-DO")}<small>{deviceDate(event.createdAt).toLocaleTimeString("es-DO")}</small></time>
                <span className="audit-action"><i>{event.action === "RESOLVED" || event.action === "CLOSED" ? <CheckCircle2 /> : event.action === "CREATED" ? <TicketCheck /> : <Activity />}</i><strong>{actionLabels[event.action] || event.action}</strong><small>{statusLabels[event.status] || event.status}</small></span>
                <span><strong>#{event.ticketNumber} · {event.subject}</strong><small>{event.agency} · {event.group}</small></span>
                <span><strong>{event.actorName}</strong><small>{event.ticketType === "INTERNAL" ? "Ticket interno" : "Soporte"}</small></span>
                <span><strong>{departmentLabels[event.department] || event.department}</strong><small>{priorityLabels[event.priority] || event.priority}</small></span>
                <button type="button" onClick={() => setSelectedEvent(expanded ? null : event.id)}>Ver detalle <ArrowRight /></button>
                {expanded && <div className="audit-ledger-detail"><div><small>Comentario o evidencia textual</small><p>{event.comment || "Esta acción no incluyó comentarios adicionales."}</p></div><div><small>Ruta del caso</small><p>{event.fromDepartment ? departmentLabels[event.fromDepartment] || event.fromDepartment : "Origen"} <ArrowRight /> {event.toDepartment ? departmentLabels[event.toDepartment] || event.toDepartment : departmentLabels[event.department] || event.department}</p></div><button type="button" onClick={() => onOpenTicket(tickets.find((ticket) => ticket.id === event.ticketId)!)}><ExternalLink /> Abrir expediente completo</button></div>}
              </article>;
            })}
            {!filteredEvents.length && <div className="operations-empty"><History /><strong>No hay movimientos con estos filtros</strong><span>Ajusta el periodo o los criterios de búsqueda.</span></div>}
          </div>
        </>
      ) : (
        <>
          <div className="queue-summary">
            <article><TicketCheck /><span><small>Tickets activos</small><strong>{totalActive}</strong></span></article>
            <article><UsersRound /><span><small>Esperando turno</small><strong>{totalWaiting}</strong></span></article>
            <article><Activity /><span><small>En atención</small><strong>{totalInProgress}</strong></span></article>
            <label><Filter /><select value={department} disabled={!!teamScope} onChange={(event) => setDepartment(event.target.value)}>{!teamScope&&<option value="ALL">Todas las áreas de Tecnología</option>}{Object.entries(technologyAreaLabels).filter(([code])=>!teamScope||code===teamScope).map(([code, label]) => <option key={code} value={code}>{teamScope?`Mi cola · ${label}`:label}</option>)}</select></label>
            <label className="queue-search"><Search /><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Buscar turno o agencia" /></label>
            <div className="queue-zoom-controls" aria-label="Tamaño de la cola en vivo">
              <button type="button" onClick={() => adjustQueueZoom(-0.1)} title="Alejar: reducir tamaño" aria-label="Alejar, reducir tamaño"><ZoomOut /><span>−</span></button>
              <button type="button" className={queueZoom===null?"auto active":"auto"} onClick={() => setQueueZoom(null)} title="Ajustar automáticamente" aria-label="Ajustar tamaño automáticamente"><small>VISTA</small><strong>{Math.round(effectiveQueueZoom*100)}%</strong></button>
              <button type="button" onClick={() => adjustQueueZoom(0.1)} title="Acercar: aumentar tamaño" aria-label="Acercar, aumentar tamaño"><ZoomIn /><span>+</span></button>
            </div>
            <button type="button" onClick={() => void toggleFullscreen()}>{isFullscreen ? <Minimize2 /> : <Fullscreen />} {isFullscreen ? "Salir" : "Pantalla completa"}</button>
          </div>

          <div className="queue-zoom-stage" style={{zoom:effectiveQueueZoom,width:`${100/effectiveQueueZoom}%`} as CSSProperties}>
          <section className="queue-area-metrics" aria-label="Porcentajes de tickets por área de Tecnología">
            {areaMetrics.filter((metric) => department === "ALL" || metric.code === department).map((metric) => <article key={metric.code} className={`area-${metric.code.toLowerCase()}`}>
              <header><span><i />{technologyAreaLabels[metric.code]}</span><strong>{metric.total}<small>tickets</small></strong></header>
              <div className="queue-area-stat pending"><span><b>Pendientes</b><small>{metric.pending} tickets</small></span><strong>{metric.pendingPercent}%</strong><i><em style={{width:`${metric.pendingPercent}%`}} /></i></div>
              <div className="queue-area-stat progress"><span><b>En progreso</b><small>{metric.inProgress} tickets</small></span><strong>{metric.inProgressPercent}%</strong><i><em style={{width:`${metric.inProgressPercent}%`}} /></i></div>
              <div className="queue-area-stat completed"><span><b>Completados</b><small>{metric.completed} tickets</small></span><strong>{metric.completedPercent}%</strong><i><em style={{width:`${metric.completedPercent}%`}} /></i></div>
            </article>)}
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
                <header><span><i />{technologyAreaLabels[code]}</span><strong>{departmentTickets.length}<small>en cola</small></strong></header>
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
          <footer className="queue-footer"><span><i /> EN VIVO</span><p>Call Center, Avería Técnica y Técnicos separados y ordenados por prioridad y fecha de creación.</p><time>{new Date(now).toLocaleString("es-DO", { dateStyle: "full", timeStyle: "medium" })}</time></footer>
          </div>
        </>
      )}
    </section>
  );
}
