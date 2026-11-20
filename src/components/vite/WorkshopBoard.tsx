import { useEffect, useRef, useState } from "react";
import {
  ArrowLeft,
  ArrowRight,
  Camera,
  CheckCircle2,
  Clock3,
  FileText,
  PackageCheck,
  PenLine,
  QrCode,
  RefreshCw,
  Search,
  Trash2,
  Wrench,
  X,
} from "lucide-react";
import { maintenanceApi } from "./maintenanceApi";

import { departmentLabels, formatDocumentTime } from "./maintenanceDocumentPdf";
import {
  type WorkshopCase,
  type WorkshopStatus,
  workshopLabels,
  workshopCode,
} from "./workshopWorkflow";
import "./workshopProcess.css";
const steps: WorkshopStatus[] = [
  "SENT",
  "RECEIVED",
  "REPAIR",
  "REPLACEMENT",
  "PART_REPLACEMENT",
  "DISCHARGED",
  "INVENTORY",
  "DELIVERED",
];
type MaintenanceTraceCase = {
  requiresInstallation?: boolean;
  ticketNumber?: number | null;
  ticketStatus?: string | null;
  ticketDepartment?: string | null;
  id: string;
  documentNumber: string;
  equipmentType: string;
  serialNumber?: string | null;
  department: string;
  movementType: string;
  operationalArea: string;
  status: string;
  statusText: string;
  sentAt: string;
  senderName: string;
  returnName?: string | null;
  receivedByName?: string | null;
  replacementSerial?: string | null;
  events: {
    id: string;
    action: string;
    status: string;
    note?: string | null;
    actorName: string;
    actorLogin: string;
    occurredAt: string;
    replacementSerial?: string | null;
  }[];
};
type WorkshopMovementFallback = {
  id: string;
  documentNumber: string;
  qrToken: string;
  equipmentType: string;
  serialNumber?: string | null;
  quantity: number;
  department: string;
  movementType: string;
  deliveredByName?: string | null;
  createdByName: string;
  createdAt: string;
  occurredAt?: string | null;
  failureCause: string;
};
function responseRows<T>(payload: unknown): T[] {
  let value = payload;
  for (let attempt = 0; attempt < 3; attempt += 1) {
    if (Array.isArray(value)) return value as T[];
    if (typeof value === "string") {
      try {
        value = JSON.parse(value);
        continue;
      } catch {
        return [];
      }
    }
    if (value && typeof value === "object") {
      const record = value as Record<string, unknown>;
      const nested =
        record.items ??
        record.movements ??
        record.data ??
        record.results ??
        record.value ??
        record.rows;
      if (nested !== undefined) {
        value = nested;
        continue;
      }
      if (typeof record.id === "string") return [record as T];
    }
    return [];
  }
  return Array.isArray(value) ? (value as T[]) : [];
}
export const workshopRows = (payload: unknown) =>
  responseRows<WorkshopCase>(payload);
export function WorkshopTimeline({ item }: { item: WorkshopCase }) {
  const events = item.events || [];
  const choice = events.some((e) => e.action === "PART") ? "PART_REPLACEMENT" : events.some((e) => e.action === "REPLACE")
    ? "REPLACEMENT"
    : "REPAIR";
  const path: WorkshopStatus[] = ["SENT", "RECEIVED", choice, item.status === "DISCHARGED" ? "DISCHARGED" : item.status === "INVENTORY" ? "INVENTORY" : "DELIVERED"];
  return (
    <section className="wt-timeline">
      <header>
        <small>SEGUIMIENTO DEL EQUIPO</small>
        <h3>{item.equipmentType}</h3>
        <p>
          {item.documentNumber} · Serial original:{" "}
          {item.serialNumber || "No registrado"}
        </p>
      </header>
      <strong className={"wt-status " + item.status}>
        {item.pendingWarehouse ? "Almacén · pendiente de recibir" : item.events.some(e=>e.action==='WAREHOUSE_RECEIVE') ? "Recibido en Almacén" : workshopLabels[item.status] || "Pendiente de recibir"}
      </strong>
      <ol className="wt-steps">
        {path.map((status, i) => {
          const complete =
            status === "SENT" || events.some((e) => e.status === status);
          return (
            <li key={status} className={complete ? "complete" : ""}>
              {complete ? <CheckCircle2 /> : <Clock3 />}
              <span>
                {
                  [
                    "Enviado",
                    "Recibido",
                    events.some(
                      (e) => e.action === "REPAIR" || e.action === "REPLACE",
                    )
                      ? choice === "REPAIR"
                        ? "Reparación"
                        : "Reemplazo"
                      : "Reparación / reemplazo",
                    "Enviado a Almacén",
                  ][i]
                }
                <small>{complete ? "Registrado" : "Pendiente"}</small>
              </span>
            </li>
          );
        })}
      </ol>
      <div className="wt-custody">
        <span>
          Entregó originalmente
          <strong>{item.returnName || "No identificado en el envío"}</strong>
        </span>
        <span>
          Recibió en Taller<strong>{item.receivedByName || "Pendiente"}</strong>
        </span>
        <span>
          Destino del reparado<strong>Almacén</strong>
        </span>
        {item.replacementSerial && (
          <span>
            Serial del reemplazo<strong>{item.replacementSerial}</strong>
          </span>
        )}
      </div>
      <h4>Bitácora y trazabilidad</h4>
      <ol className="wt-events">
        <li>
          <span className="wt-event-dot" />
          <div>
            <strong>Enviado a Taller</strong>
            <small>
              {formatDocumentTime(item.sentAt || new Date().toISOString())} ·
              Registrado por {item.senderName || "Usuario no identificado"}
            </small>
            <p>{item.failureCause || "Sin detalle registrado"}</p>
          </div>
        </li>
        {events.map((e) => (
          <li key={e.id}>
            <span className="wt-event-dot" />
            <div>
              <strong>
                {e.action === "WAREHOUSE_RECEIVE" ? "Recibido en Almacén e ingresado a inventario" : e.action === "WORK"
                  ? "Trabajo registrado"
                  : e.action === "DELIVER"
                    ? "Enviado a Almacén"
                    : workshopLabels[e.status] || e.action}
              </strong>
              <small>
                {formatDocumentTime(e.occurredAt)} · {e.actorName} ·{" "}
                {e.actorLogin}
              </small>
              {e.note && <p>{e.note}</p>}
              {e.replacementSerial && (
                <p>Serial sustituto: {e.replacementSerial}</p>
              )}
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
}
function MaintenanceTraceTimeline({ item }: { item: MaintenanceTraceCase }) {
  const events = item.events || [];
  const complete = (action: string) => events.some((e) => e.action === action);
  const delivered = item.status === "DELIVERED" || item.status === "INSTALLED";
  const cancelled = complete("CANCELLED");
  const rejected = complete("REJECTED")||cancelled,
    decisionAction = cancelled ? "CANCELLED" : rejected ? "REJECTED" : "APPROVED";
  const requestSteps = [
    ["RECEIVED_WAREHOUSE", "Almacén recibió la solicitud"],
    [
      decisionAction,
      cancelled ? "Solicitud anulada" : rejected ? "Solicitud rechazada" : "Almacén autorizó la solicitud",
    ],
    ["DISPATCHED", "Equipo y serial asignados"],
    ["WITHDRAWN", "Equipo retirado de Almacén"],
    ...(item.requiresInstallation ? [["INSTALLED", "Instalado y comprobado"]] : []),
  ];
  const regularSteps = [
    ["CREATED", "Formulario creado"],
    ["SIGN_DELIVERY", "Firma de quien entrega"],
    ["SIGN_RECEIPT", "Firma de quien recibe"],
    ["RECEIVE", delivered ? "Sello de entregado" : "Sello de recibido"],
  ];
  const labels: Record<string, string> = {
    CREATED: "Formulario creado",
    SENT_TO_WAREHOUSE: "Entrada registrada en Almacén",
    RECEIVED_WAREHOUSE: "Recibido y sellado",
    APPROVED: "Solicitud autorizada",
    REJECTED: "Solicitud rechazada",
    CANCELLED: "Solicitud anulada",
    WITHDRAWN: "Equipo retirado de Almacén",
    DISPATCHED: "Equipo y serial asignados; pendiente de retiro",
    INSTALLED: "Instalado y comprobado",
    TICKET_CREATED: "Ticket de falla creado",
    DELIVERED: "Entrega confirmada",
    SIGN_DELIVERY: "Firma de quien entrega",
    SIGN_RECEIPT: "Firma de quien recibe",
    RECEIVE: delivered ? "Entregado confirmado" : "Recibido confirmado",
  };
  const ticketStatusLabels: Record<string,string> = {OPEN:"Abierto",IN_PROGRESS:"En proceso",PENDING:"Pendiente",RESOLVED:"Resuelto",CLOSED:"Cerrado",CANCELLED:"Anulado"};
  const flow = item.movementType === "REQUEST" ? requestSteps : regularSteps;
  return (
    <section className="wt-timeline">
      <header>
        <small>FORMULARIO INSTITUCIONAL · ALMACÉN</small>
        <h3>{item.equipmentType}</h3>
        <p>
          {item.documentNumber} · {item.serialNumber || "Serial no registrado"}
        </p>
      </header>
      <strong className={"wt-status " + item.status}>{item.statusText}</strong>
      {item.ticketNumber&&<p className="wt-ticket-link">Ticket #{item.ticketNumber} · {departmentLabels[item.ticketDepartment||""]||item.ticketDepartment||"Soporte"} · {ticketStatusLabels[item.ticketStatus||""]||item.ticketStatus||"Abierto"}</p>}
      <ol
        className={`wt-steps${item.movementType === "REQUEST" ? " request" : ""}`}
      >
        {flow.map(([action, label]) => {
          const done = action === "CREATED" || complete(action);
          const notApplicable=item.movementType==='REQUEST'&&rejected&&['DISPATCHED','WITHDRAWN','INSTALLED'].includes(action);
          return (
            <li key={action} className={done ? "complete" : ""}>
              {done ? <CheckCircle2 /> : <Clock3 />}
              <span>
                {label}
                <small>{done ? "Registrado" : notApplicable ? "No aplica · solicitud rechazada" : "Pendiente"}</small>
              </span>
            </li>
          );
        })}
      </ol>
      <div className="wt-custody">
        <span>
          Departamento
          <strong>
            {departmentLabels[item.department] || item.department}
          </strong>
        </span>
        <span>
          Registrado por<strong>{item.senderName}</strong>
        </span>
        <span>
          {delivered ? "Entregado por" : "Recibido por"}
          <strong>{item.receivedByName || "Pendiente"}</strong>
        </span>
      </div>
      <h4>Bitácora y trazabilidad</h4>
      <ol className="wt-events">
        {events.map((e) => (
          <li key={e.id}>
            <span className="wt-event-dot" />
            <div>
              <strong>{labels[e.action] || e.action}</strong>
              <small>
                {formatDocumentTime(e.occurredAt)}
                {e.actorName ? ` · ${e.actorName}` : ""}
                {e.actorLogin ? ` · ${e.actorLogin}` : ""}
              </small>
              {e.note && <p>{e.note}</p>}
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
}
export function EquipmentTracking({
  initialCode = "",
  onClose,
}: {
  initialCode?: string;
  onClose: () => void;
}) {
  const [code, setCode] = useState(initialCode),
    [item, setItem] = useState<WorkshopCase | null>(null),
    [maintenanceItem, setMaintenanceItem] =
      useState<MaintenanceTraceCase | null>(null),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  const panel = useRef<HTMLElement>(null);
  const video = useRef<HTMLVideoElement>(null),
    cameraStream = useRef<MediaStream | null>(null),
    cameraFrame = useRef(0);
  const [cameraOpen, setCameraOpen] = useState(false);
  const stopCamera = () => {
    cancelAnimationFrame(cameraFrame.current);
    cameraStream.current?.getTracks().forEach((track) => track.stop());
    cameraStream.current = null;
    setCameraOpen(false);
  };
  async function lookup(value: string) {
    if (!value.trim()) return;
    setBusy(true);
    setError("");
    setItem(null);
    setMaintenanceItem(null);
    const normalized = workshopCode(value);
    try {
      const warehouseRows=await maintenanceApi<MaintenanceTraceCase[]>("/api/warehouse/fulfillment/track?code="+encodeURIComponent(value.trim()));
      if(Array.isArray(warehouseRows)&&warehouseRows.length===1){setMaintenanceItem({...warehouseRows[0],events:warehouseRows[0].events||[]});return;}
      try {
        const rows = await maintenanceApi<WorkshopCase[]>(
          "/api/workshop/track?code=" + encodeURIComponent(normalized),
        );
        if (Array.isArray(rows) && rows.length === 1) {
          setItem({
            ...rows[0],
            events: Array.isArray(rows[0].events) ? rows[0].events : [],
          });
          return;
        }
      } catch {
        /* Si no es un QR de Taller, se intenta la trazabilidad institucional. */
      }
      const rows = await maintenanceApi<MaintenanceTraceCase[]>(
        "/api/maintenance/track?code=" + encodeURIComponent(normalized),
      );
      if (!Array.isArray(rows) || rows.length !== 1)
        throw new Error(
          "No se encontró trazabilidad con ese QR, o tu cuenta no tiene acceso.",
        );
      setMaintenanceItem({
        ...rows[0],
        events: Array.isArray(rows[0].events) ? rows[0].events : [],
      });
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function openCamera() {
    type BarcodeResult = { rawValue?: string };
    type Detector = {
      detect: (source: CanvasImageSource) => Promise<BarcodeResult[]>;
    };
    type DetectorConstructor = new (options?: {
      formats?: string[];
    }) => Detector;
    const Detector = (
      window as unknown as { BarcodeDetector?: DetectorConstructor }
    ).BarcodeDetector;
    if (!window.isSecureContext) {
      setError("La cámara requiere abrir la Oficina Virtual mediante HTTPS.");
      return;
    }
    if (!navigator.mediaDevices?.getUserMedia) {
      setError(
        "Este dispositivo no permite usar la cámara desde el navegador.",
      );
      return;
    }
    setError("");
    setCameraOpen(true);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: false,
        video: {
          facingMode: { ideal: "environment" },
          width: { ideal: 3840 },
          height: { ideal: 2160 },
          advanced: [{ focusMode: "continuous" } as MediaTrackConstraintSet],
        },
      });
      cameraStream.current = stream;
      const element = video.current;
      if (!element) {
        stopCamera();
        return;
      }
      element.srcObject = stream;
      await element.play();
      const detector = Detector ? new Detector({ formats: ["qr_code"] }) : null;
      const jsQR = detector ? null : (await import('jsqr')).default;
      if(cameraStream.current!==stream)return;
      const canvas=document.createElement("canvas"),context=canvas.getContext("2d",{willReadFrequently:true});
      let detecting = false;
      const scanFrame = async () => {
        if (!cameraStream.current || !video.current) return;
        if (!detecting && video.current.readyState >= 2) {
          detecting = true;
          try {
            let raw:string|undefined;
            if(detector){const found=await detector.detect(video.current);raw=found.find(result=>result.rawValue)?.rawValue;}
            else if(context&&jsQR){const width=Math.min(1280,video.current.videoWidth||1280),height=Math.max(1,Math.round(width*(video.current.videoHeight||720)/(video.current.videoWidth||1280)));canvas.width=width;canvas.height=height;context.drawImage(video.current,0,0,width,height);raw=jsQR(context.getImageData(0,0,width,height).data,width,height,{inversionAttempts:"attemptBoth"})?.data;}
            if (raw) {
              setCode(raw);
              stopCamera();
              void lookup(raw);
              return;
            }
          } catch {
            /* Mantiene la cámara activa mientras enfoca el siguiente cuadro. */
          } finally {
            detecting = false;
          }
        }
        cameraFrame.current = requestAnimationFrame(scanFrame);
      };
      cameraFrame.current = requestAnimationFrame(scanFrame);
    } catch (cameraError) {
      stopCamera();
      const name = (cameraError as DOMException).name;
      setError(
        name === "NotAllowedError"
          ? "Permite el acceso a la cámara para escanear el comprobante."
          : "No fue posible iniciar la cámara trasera. Verifica que ninguna otra aplicación la esté usando.",
      );
    }
  }
  useEffect(() => {
    if (initialCode) void lookup(initialCode);
  }, [initialCode]);
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    panel.current?.focus();
    return () => {
      stopCamera();
      previous?.focus();
    };
  }, []);
  return (
    <div className="wt-overlay">
      <section
        className="wt-track-dialog"
        ref={panel}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-label="Estado y trazabilidad del equipo"
        onKeyDown={(e) => {
          if (e.key === "Escape") {
            if (cameraOpen) stopCamera();
            else onClose();
          }
          if (e.key === "Tab") {
            const nodes = e.currentTarget.querySelectorAll<HTMLElement>(
              "button:not(:disabled),input:not(:disabled)",
            );
            const first = nodes[0],
              last = nodes[nodes.length - 1];
            if (e.shiftKey && document.activeElement === first) {
              e.preventDefault();
              last?.focus();
            } else if (!e.shiftKey && document.activeElement === last) {
              e.preventDefault();
              first?.focus();
            }
          }
        }}
      >
        <header>
          <div>
            <small>CONSULTA POR QR</small>
            <h2>Estado y trazabilidad</h2>
          </div>
          <button
            aria-label="Cerrar seguimiento"
            onClick={() => {
              stopCamera();
              onClose();
            }}
          >
            <X />
          </button>
        </header>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void lookup(code);
          }}
        >
          <label>
            Escanea el QR con tu lector o pega el código
            <input
              autoFocus
              value={code}
              onChange={(e) => setCode(e.target.value)}
              placeholder="REAL-MNT|… o número del formulario"
            />
          </label>
          <button disabled={busy || !code.trim()}>
            <Search />
            {busy ? "Consultando…" : "Consultar"}
          </button>
          <button
            type="button"
            className="wt-camera-button"
            disabled={busy || cameraOpen}
            onClick={() => void openCamera()}
          >
            <Camera />
            Escanear con cámara
          </button>
        </form>
        {cameraOpen && (
          <section className="wt-camera" aria-label="Escáner QR con cámara">
            <video ref={video} playsInline muted />
            <span className="wt-camera-frame">
              <i />
              <i />
              <i />
              <i />
            </span>
            <div>
              <strong>Apunta al QR del comprobante</strong>
              <small>
                Usando cámara trasera y la mayor resolución disponible
              </small>
              <button type="button" onClick={stopCamera}>
                <X />
                Cancelar escaneo
              </button>
            </div>
          </section>
        )}
        <p className="wt-help">
          En el móvil puedes usar la cámara trasera. El sistema solicita la
          máxima calidad compatible con el dispositivo y consulta el comprobante
          al detectar el QR.
        </p>
        {error && (
          <p role="alert" className="wt-error">
            {error}
          </p>
        )}
        {(item || maintenanceItem) && (
          <button
            className="wt-refresh"
            disabled={busy}
            onClick={() => void lookup(code)}
          >
            <RefreshCw />
            Actualizar estado
          </button>
        )}
        {item && <><WorkshopTimeline item={item} />
          {item.canReceiveWarehouse&&<button disabled={busy} onClick={async()=>{if(!window.confirm('¿Confirmas que Almacén recibió físicamente este equipo? Se registrará la entrada a su inventario y la salida de Taller.'))return;setBusy(true);try{await maintenanceApi(`/api/workshop/${item.id}/warehouse-receipt`,'POST',{});await lookup(code);window.dispatchEvent(new Event('support-live-refresh'));}catch(e){setError((e as Error).message)}finally{setBusy(false)}}}><PackageCheck/>Confirmar recepción en Almacén</button>}
          <button disabled={busy} onClick={async()=>{setBusy(true);try{const [{buildWorkshopFormPdf},{realLogoData}]=await Promise.all([import('./workshopFormPdf'),import('./institutionalLetterPdf')]);(await buildWorkshopFormPdf([item],await realLogoData())).save(item.documentNumber+'.pdf');}catch(e){setError((e as Error).message)}finally{setBusy(false)}}}><FileText/>Descargar formulario y sellos</button>
        </>}{" "}
        {maintenanceItem && <MaintenanceTraceTimeline item={maintenanceItem} />}
      </section>
    </div>
  );
}
export default function WorkshopBoard({
  department,
  onBack,
  onChanged,
  onDocument,
  managed = false,
  canDeleteWorkshop = false,
}: {
  department: string;
  managed?: boolean;
  canDeleteWorkshop?: boolean;
  onBack: () => void;
  onChanged: () => void;
  onDocument: (id: string) => void;
}) {
  const [items, setItems] = useState<WorkshopCase[]>([]),
    [tab, setTab] = useState<WorkshopStatus>("SENT"),
    [query, setQuery] = useState(""),
    [error, setError] = useState(""),
    [loading, setLoading] = useState(true),
    [busy, setBusy] = useState(""),
    [detail, setDetail] = useState<string | null>(null),
    [work, setWork] = useState<string | null>(null),
    [editing, setEditing] = useState<{
      id: string;
      equipment: string;
      serial: string;
      failure: string;
      notes: string;
    } | null>(null),
    [diagnosis, setDiagnosis] = useState(""),
    [tasks, setTasks] = useState(""),
    [result, setResult] = useState(""),
    [serial, setSerial] = useState(""),
    [scan, setScan] = useState(false),
    [notice, setNotice] = useState("");
  async function load() {
    try {
      const data = await maintenanceApi<unknown>(managed?"/api/workshop/manage":"/api/workshop");
      let rows = workshopRows(data);
      if (!rows.length && !managed) {
        const movementData = await maintenanceApi<unknown>(
          "/api/maintenance/movements",
        );
        rows = responseRows<WorkshopMovementFallback>(movementData)
          .filter(
            (item) =>
              item?.movementType === "TRANSFER_TO_WORKSHOP" &&
              (!department || item.department === department),
          )
          .map((item) => ({
            id: item.id,
            documentNumber: item.documentNumber,
            qrToken: item.qrToken,
            equipmentType: item.equipmentType,
            serialNumber: item.serialNumber || "",
            quantity: item.quantity || 1,
            department: item.department,
            deliveredByName: item.deliveredByName || undefined,
            senderName: item.createdByName,
            returnName: item.deliveredByName || item.createdByName,
            sentAt: item.occurredAt || item.createdAt,
            status: "SENT",
            failureCause: item.failureCause,
            canOperate: false,
            events: [],
          }));
      }
      setItems(
        rows
          .filter((item) => item && item.id)
          .map((item) => ({
            ...item,
            status: steps.includes(item.status) ? item.status : "SENT",
            events: Array.isArray(item.events) ? item.events : [],
          })),
      );
      setError("");
    } catch (e) {
      setItems([]);
      setError(
        (e as Error).message || "No fue posible cargar las órdenes de Taller.",
      );
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    void load();
    const refresh = () => {
      void load();
    };
    window.addEventListener("support-live-refresh", refresh);
    return () => window.removeEventListener("support-live-refresh", refresh);
  }, []);
  const scoped = items.filter(
    (i) => !department || i.department === department,
  );
  const visible = scoped.filter(
    (i) =>
      i.status === tab &&
      (!query ||
        [
          i.equipmentType,
          i.serialNumber,
          i.documentNumber,
          i.returnName,
          i.replacementSerial,
        ]
          .map((value) => value || "")
          .join(" ")
          .toLocaleLowerCase("es")
          .includes(query.toLocaleLowerCase("es"))),
  );
  async function act(item: WorkshopCase, action: string, reason?: string) {
    if (busy) return;
    setBusy(item.id);
    setError("");
    setNotice("");
    try {
      const note =
        action === "WORK"
          ? "Diagnóstico: " +
            diagnosis.trim() +
            "\nTrabajo realizado: " +
            tasks.trim() +
            "\nResultado: " +
            result.trim()
          : reason;
      await maintenanceApi("/api/workshop/" + item.id + "/actions", "POST", {
        action,
        expectedStatus: item.status,
        note,
        replacementSerial:
          action === "WORK" && item.status === "REPLACEMENT"
            ? serial.trim()
            : undefined,
      });
      await load();
      onChanged();
      setWork(null);
      const next: WorkshopStatus =
        action === "STOCK" ? "INVENTORY" : action === "PART" ? "PART_REPLACEMENT" : action === "DISCHARGE" ? "DISCHARGED" : action === "RECEIVE"
          ? "RECEIVED"
          : action === "REPAIR"
            ? "REPAIR"
            : action === "REPLACE"
              ? "REPLACEMENT"
              : action === "DELIVER"
                ? "DELIVERED"
                : item.status;
      setTab(next);
      setNotice(
        action === "DELIVER"
          ? "Equipo reparado enviado a Almacén."
          : action === "WORK"
            ? "Trabajo agregado a la bitácora."
            : "Equipo actualizado. Se registró el movimiento automáticamente.",
      );
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy("");
    }
  }
  async function remove(item:WorkshopCase){
    if(busy||(!canDeleteWorkshop&&!item.canDelete))return;
    const reason=window.prompt(`Eliminar de Taller: ${item.equipmentType} · ${item.serialNumber}\n\nIndica el motivo (mínimo 5 caracteres).`);
    if(reason===null)return;
    if(reason.trim().length<5){setError("Indica el motivo de eliminación (mínimo 5 caracteres).");return;}
    if(!window.confirm("¿Confirmas que este equipo fue agregado por error y debe desaparecer de Taller?"))return;
    setBusy(item.id);setError("");setNotice("");
    try{await maintenanceApi(`/api/workshop/${item.id}`,"DELETE",{reason:reason.trim()});await load();onChanged();setNotice("Equipo eliminado de Taller. La corrección quedó registrada en la auditoría.");}
    catch(e){setError((e as Error).message)}finally{setBusy("")}
  }
  async function saveEdit() {
    if (!editing || busy) return;
    setBusy(editing.id);
    setError("");
    setNotice("");
    try {
      await maintenanceApi(`/api/workshop/${editing.id}`, "PUT", {
        equipment: editing.equipment.trim(),
        serial: editing.serial.trim(),
        failure: editing.failure.trim(),
        notes: editing.notes.trim() || null,
      });
      setEditing(null);
      await load();
      onChanged();
      setNotice("Registro de Taller actualizado. Los cambios quedaron guardados en la auditoría.");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy("");
    }
  }
  return (
    <section className="wt-board">
      <button className="wt-back" onClick={onBack}>
        <ArrowLeft />
        Departamentos de Taller
      </button>
      <header className="wt-heading">
        <div>
          <small>TALLER · PROCESO POR EQUIPO</small>
          <h2>{departmentLabels[department] || "Órdenes de Taller"}</h2>
          <p>
            Recibe equipos retirados de agencias, registra la reparación y envía
            los reparados a Almacén.
          </p>
        </div>
        <div>
          <button onClick={() => setScan(true)}>
            <QrCode />
            Consultar QR
          </button>
          <button disabled={loading || !!busy} onClick={() => void load()}>
            <RefreshCw />
            Actualizar
          </button>
        </div>
      </header>
      <nav className="wt-tabs" aria-label="Etapas de Taller">
        {steps.filter(s=>s!=="REPLACEMENT"||scoped.some(i=>i.status==="REPLACEMENT")).map((s) => (
          <button
            key={s}
            aria-pressed={tab === s}
            onClick={() => {
              setTab(s);
              setWork(null);
              setDetail(null);
            }}
          >
            {workshopLabels[s]}
            <b>{scoped.filter((i) => i.status === s).length}</b>
          </button>
        ))}
      </nav>
      <div className="wt-toolbar">
        <strong>{workshopLabels[tab]}</strong>
        <label>
          <Search />
          <input
            aria-label="Buscar equipo de Taller"
            placeholder="Equipo, serial, formulario o remitente"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </label>
      </div>
      {error && (
        <p className="wt-error" role="alert">
          {error}
        </p>
      )}
      {notice && (
        <p className="wt-notice" role="status">
          {notice}
        </p>
      )}
      {loading ? (
        <p role="status">Cargando envíos y seguimiento…</p>
      ) : (
        <div className="wt-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Equipo / formulario</th>
                <th>Serial</th>
                <th>Origen / destino final</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {visible.map((item) => (
                <tr key={item.id}>
                  <td>
                    <strong>{item.equipmentType}</strong>
                    <small>
                      {item.documentNumber} · {item.quantity} ud.
                    </small>
                  </td>
                  <td>
                    {item.serialNumber}
                    {item.replacementSerial && (
                      <small>Reemplazo: {item.replacementSerial}</small>
                    )}
                  </td>
                  <td>
                    <strong>
                      {item.returnName || "Remitente no identificado"}
                    </strong>
                    <small>
                      {item.receivedAt
                        ? "Recibido: " + formatDocumentTime(item.receivedAt)
                        : "Enviado: " + formatDocumentTime(item.sentAt)}
                    </small>
                  </td>
                  <td>
                    <div className="wt-row-actions">
                      {item.canOperate && (
                        <>
                          {tab === "SENT" && (
                            <button
                              disabled={!!busy}
                              onClick={() => void act(item, "RECEIVE")}
                            >
                              <PackageCheck />
                              Recibir
                            </button>
                          )}
                          {tab === "RECEIVED" && (
                            <>
                              <button
                                disabled={!!busy}
                                onClick={() => void act(item, "REPAIR")}
                              >
                                <Wrench />
                                Enviar a reparación
                              </button>

                            </>
                          )}
                          {(tab === "RECEIVED" || tab === "REPAIR") && <button disabled={!!busy} onClick={() => void act(item, "PART")}><Wrench />Enviar a reemplazo de pieza</button>}
                          {["RECEIVED","REPAIR","REPLACEMENT","PART_REPLACEMENT"].includes(tab) && <button disabled={!!busy} onClick={() => {
                            const reason = window.prompt("Marcar equipo sin reparación. Indica el diagnóstico y motivo (mínimo 5 caracteres). Esta acción conserva el historial y no envía el equipo a Almacén.");
                            if (reason === null) return;
                            if (reason.trim().length < 5) { setError("Indica el motivo de descarga (mínimo 5 caracteres)."); return; }
                            if (window.confirm("¿Confirmas que no tiene reparación: " + item.equipmentType + " · " + item.serialNumber + "?")) void act(item, "DISCHARGE", reason.trim());
                          }}>No tiene reparación</button>}
                          {tab==="INVENTORY"&&<button disabled={!!busy} onClick={()=>void act(item,"DELIVER")}>Enviar a Almacén</button>}
                          {(tab === "REPAIR" || tab === "REPLACEMENT" || tab === "PART_REPLACEMENT") && (
                            <>
                              <button
                                disabled={!!busy}
                                onClick={() => {
                                  setWork(item.id);
                                  setDiagnosis("");
                                  setTasks("");
                                  setResult("");
                                  setSerial(item.replacementSerial || "");
                                }}
                              >
                                <PenLine />
                                {tab==="PART_REPLACEMENT"?"Registrar cambio de pieza":"Registrar reparación"}
                              </button>
                              <button
                                disabled={
                                  !!busy ||
                                  !item.events.some(
                                    (e) => e.action === "WORK" && e.status === item.status,
                                  ) ||
                                  (tab === "REPLACEMENT" &&
                                    !item.replacementSerial)
                                }
                                title="Enviar el equipo reparado a Almacén"
                                onClick={() => void act(item, "DELIVER")}
                              >
                                <CheckCircle2 />
                                {tab !== "REPLACEMENT"
                                  ? "Enviar reparado a Almacén"
                                  : "Enviar reemplazo a Almacén"}
                              </button>
                              <button disabled={!!busy||!item.events.some(e=>e.action==="WORK"&&e.status===item.status)} onClick={()=>void act(item,"STOCK")}>Enviar al inventario de Taller</button>
                            </>
                          )}
                        </>
                      )}
                      {(canDeleteWorkshop||item.canDelete)&&<button className="wt-edit" disabled={!!busy} title="Editar los datos de este equipo" onClick={()=>setEditing({id:item.id,equipment:item.equipmentType,serial:item.serialNumber,failure:item.failureCause,notes:item.notes||""})}><PenLine/>Editar</button>}
                      {(canDeleteWorkshop||item.canDelete)&&<button className="wt-delete" disabled={!!busy} title="Eliminar este equipo de Taller con auditoría" onClick={()=>void remove(item)}><Trash2/>Eliminar</button>}
                      <button
                        onClick={() =>
                          setDetail(detail === item.id ? null : item.id)
                        }
                      >
                        <ArrowRight />
                        Trazabilidad
                      </button>
                      <button
                        onClick={() => {
                          if(managed){onDocument(item.id);return;}
                          const delivered = [...item.events]
                            .reverse()
                            .find(
                              (e) => e.action === "DELIVER" && e.movementId,
                            )?.movementId;
                          const latest = [...item.events]
                            .reverse()
                            .find((e) => e.movementId)?.movementId;
                          onDocument(delivered || latest || item.id);
                        }}
                      >
                        <FileText />
                        PDF + QR
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!visible.length && (
            <div className="wt-empty">
              <PackageCheck />
              <h3>
                {query
                  ? "No hay equipos con esa búsqueda"
                  : "No hay equipos en esta etapa"}
              </h3>
              <p>Los equipos aparecerán aquí al completar el paso anterior.</p>
            </div>
          )}
        </div>
      )}
      {editing && (
        <div className="wt-overlay" role="presentation" onMouseDown={(event)=>{if(event.target===event.currentTarget)setEditing(null)}}>
          <form className="wt-edit-dialog" onSubmit={(event)=>{event.preventDefault();void saveEdit()}}>
            <header>
              <div><small>CORRECCIÓN CON AUDITORÍA</small><h3>Editar equipo de Taller</h3></div>
              <button type="button" aria-label="Cerrar edición" onClick={()=>setEditing(null)}><X/></button>
            </header>
            <label>Equipo<input required minLength={2} maxLength={100} value={editing.equipment} onChange={(event)=>setEditing({...editing,equipment:event.target.value})}/></label>
            <label>Serial<input required maxLength={120} value={editing.serial} onChange={(event)=>setEditing({...editing,serial:event.target.value})}/></label>
            <label>Falla o motivo<textarea required minLength={2} maxLength={200} value={editing.failure} onChange={(event)=>setEditing({...editing,failure:event.target.value})}/></label>
            <label>Notas<textarea maxLength={2000} value={editing.notes} onChange={(event)=>setEditing({...editing,notes:event.target.value})}/></label>
            <div className="wt-edit-actions">
              <button type="button" onClick={()=>setEditing(null)}>Cancelar</button>
              <button type="submit" disabled={!!busy}><CheckCircle2/>{busy ? "Guardando…" : "Guardar cambios"}</button>
            </div>
          </form>
        </div>
      )}
      {work &&
        (() => {
          const item = items.find((i) => i.id === work);
          return (
            item && (
              <form
                className="wt-work"
                onSubmit={(e) => {
                  e.preventDefault();
                  void act(item, "WORK");
                }}
              >
                <header>
                  <div>
                    <small>
                      BITÁCORA DE{" "}
                      {tab === "REPAIR" ? "REPARACIÓN" : "REEMPLAZO"}
                    </small>
                    <h3>
                      {item.equipmentType} · {item.serialNumber}
                    </h3>
                  </div>
                  <button
                    type="button"
                    aria-label="Cerrar bitácora"
                    onClick={() => setWork(null)}
                  >
                    <X />
                  </button>
                </header>
                <p>
                  Se conservará el trabajo con tu usuario, fecha y hora. Después
                  se enviará a <strong>Almacén</strong>.
                </p>
                <label>
                  Diagnóstico
                  <textarea
                    required
                    minLength={3}
                    maxLength={450}
                    value={diagnosis}
                    onChange={(e) => setDiagnosis(e.target.value)}
                  />
                </label>
                <label>
                  Trabajo realizado / piezas utilizadas
                  <textarea
                    required
                    minLength={3}
                    maxLength={900}
                    value={tasks}
                    onChange={(e) => setTasks(e.target.value)}
                  />
                </label>
                <label>
                  Resultado y pruebas
                  <textarea
                    required
                    minLength={3}
                    maxLength={450}
                    value={result}
                    onChange={(e) => setResult(e.target.value)}
                  />
                </label>
                {item.status === "REPLACEMENT" && (
                  <label>
                    Serial del equipo sustituto
                    <input
                      required
                      maxLength={120}
                      value={serial}
                      onChange={(e) => setSerial(e.target.value)}
                    />
                  </label>
                )}
                <button disabled={!!busy}>
                  <CheckCircle2 />
                  Guardar trabajo realizado
                </button>
              </form>
            )
          );
        })()}
      {detail && items.find((i) => i.id === detail) && (
        <WorkshopTimeline item={items.find((i) => i.id === detail)!} />
      )}
      {scan && <EquipmentTracking onClose={() => setScan(false)} />}
    </section>
  );
}
