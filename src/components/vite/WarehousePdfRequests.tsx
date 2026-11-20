import { apiFetch as fetch } from "@/lib/api-fetch";
import { useCallback, useEffect, useMemo, useState } from "react";
import { isWarehouseRequest } from "@/lib/warehouse-live-queue";
import { administrativeDepartmentName } from "@/lib/administrative-requests";
import {requestDepartmentChoices} from '@/lib/warehouse-request-departments';
import {
  CheckCircle2,
  Download,
  Eye,
  FileText,
  FolderOpen,
  Grid2X2,
  Inbox,
  List,
  LoaderCircle,
  PackageSearch,
  PenLine,
  Plus,
  Printer,
  QrCode,
  RefreshCw,
  Search,
  Stamp,
} from "lucide-react";
import type { PortalSession } from "@/lib/session";
import InstitutionalDocumentDetail from "./InstitutionalDocumentDetail";
import type { DocumentDetail, DocumentItem } from "./DocumentSystemModule";
import { downloadDocument, realLogoData } from "./institutionalDocumentAssets";
import { buildReceiptCopy } from "./institutionalReceiptPdf";
import WarehouseRequestPickupButton from "./WarehouseRequestPickupButton";
import WarehouseRequestComposer from "./WarehouseRequestComposer";
import WarehouseRequestCancelButton from "./WarehouseRequestCancelButton";
import { EquipmentTracking } from "./WorkshopBoard";
// This section loads independently; its layout must not depend on visiting another module.
import "./maintenanceWorkflow.css";
import "./maintenanceCommunications.css";
import "./warehouseFulfillment.css";


type PdfRequest = DocumentItem & {
  isRecipient?: boolean;
  warehouseDelivery?: DocumentDetail["warehouseDelivery"];
};
const names: Record<string, string> = {
  pending: "Pendiente",
  received: "Recibida",
  approved: "Autorizada",
  rejected: "Rechazada",
  cancelled: "Anulada",
  correction_requested: "Corrección solicitada",
  authorization: "Pendiente de autorizar",
  dispatch: "Despachar",
  withdraw: "Retirar",
  withdrawn: "Retirada",
};
export const warehouseRequestStage = (item: PdfRequest, warehouse: boolean) => {
  if (item.status === "cancelled") return "cancelled";
  if (item.status === "correction_requested") return item.receivedUtc ? "authorization" : "pending";
  if (item.status === "rejected") return "rejected";
  if (item.warehouseDelivery) return "withdrawn";
  if (item.status === "approved" && item.warehouseAssigned?.length)
    return warehouse ? "dispatch" : "withdraw";
  if (item.status === "approved") return "approved";
  if (item.status === "received" || (item.status === "pending" && item.receivedUtc)) return "authorization";
  if (!item.receivedUtc) return "pending";
  return item.status;
};

const normalizeRequestSearch=(value:string)=>value.normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es").trim();
export function warehouseRequestMatches(item:PdfRequest,query:string){
  const needle=normalizeRequestSearch(query);
  if(!needle)return true;
  const request=item.warehouseRequest;
  const haystack=[item.title,item.description,item.originalFileName,item.department,item.ownerDisplayName,item.ownerUserName,item.status,request?.number,request?.ticketNumber,request?.agencyCode,request?.agencyName,request?.agencyGroup,...(request?.lines.flatMap(line=>[line.productId,line.name,line.note])||[])].filter(Boolean).join(" ");
  return normalizeRequestSearch(haystack).includes(needle);
}

export default function WarehousePdfRequests({
  session,
  department,
  onOpenInventory,
  warehouseAccess,
  embedded = false,
}: {
  session: PortalSession;
  department: string;
  onOpenInventory?: (department: string, requestNumber: string) => void;
  warehouseAccess?: boolean;
  embedded?: boolean;
}) {
  const requesterOnly=Boolean(session.permissions?.['profile:WAREHOUSE_REQUESTER']);
  const warehouse =
    warehouseAccess ??
    (session.role === "Warehouse" || session.supportTeam === "WAREHOUSE" ||
      (session.role === "Administrator" && !department));
  const [items, setItems] = useState<PdfRequest[]>([]),
    [detail, setDetail] = useState<DocumentDetail | null>(null),
    [loading, setLoading] = useState(true),
    [error, setError] = useState("");
  const [traceCode, setTraceCode] = useState<string | null>(null);
  const [editOnOpen,setEditOnOpen]=useState<number|null>(null);
  const departments=useMemo(()=>requestDepartmentChoices(items),[items]);
  const vittini=session.role==='Technology'&&session.supportTeam==='TECHNICAL_FAILURE'&&(session.email||'').split('@')[0].toLowerCase()==='rosmarlin.vittini';
  const [loanMode,setLoanMode]=useState(false),[loanComposer,setLoanComposer]=useState(false);
  const [composerOpen,setComposerOpen]=useState(false);
  const requestDepartment=warehouse?'WAREHOUSE':department;
  const [query, setQuery] = useState(""),
    [departmentFilter, setDepartmentFilter] = useState(""),
    [status, setStatus] = useState(warehouse ? "all" : "pending"),
    [view, setView] = useState<"list" | "cards">("cards");
  const departmentName = administrativeDepartmentName(department);
  const load = useCallback(async () => {
    setLoading(true);
    try {
      const baseUrl = warehouse
        ? "/api/institutional-documents?area=communications&channel=requirements&view=all"
        : `/api/institutional-documents?area=communications&channel=requirements&view=${requesterOnly?'mine':'all'}`;
      const url = departmentName&&!vittini
        ? `${baseUrl}&department=${encodeURIComponent(departmentName)}`
        : baseUrl;
      const response = await fetch(url, {
        credentials: "same-origin",
        cache: "no-store",
      });
      if (!response.ok)
        throw new Error("No se pudieron cargar las solicitudes de equipo.");
      setItems((await response.json() as PdfRequest[]).filter(isWarehouseRequest));
      setError("");
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setLoading(false);
    }
  }, [departmentName, warehouse, requesterOnly,vittini]);
  useEffect(() => {
    void load();
    const refresh = () => void load();
    window.addEventListener("support-live-refresh", refresh);
    return () => window.removeEventListener("support-live-refresh", refresh);
  }, [load]);
  useEffect(() => {
    if (warehouse) setDepartmentFilter("");
  }, [departmentName, warehouse, requesterOnly]);
  const scopedItems = useMemo(
    () => {
      // A department page stays scoped even when Almacén can access every department.
      const selectedDepartment = vittini?"":departmentName || (warehouse ? departmentFilter : "");
      return items.filter(
        (item) => (!loanMode||!!item.warehouseRequest?.weekendLoan)&&(!selectedDepartment || item.department === selectedDepartment),
      );
    },
    [items, departmentName, departmentFilter, warehouse,loanMode,vittini],
  );
  const visible = useMemo(
    () =>
      scopedItems.filter((item) => {
        const matchesStatus = status === "all" || warehouseRequestStage(item, warehouse) === status;
        return matchesStatus && warehouseRequestMatches(item,query);
      }),
    [scopedItems, status, query, warehouse],
  );
  const count = (value: string) =>
    value === "all"
      ? scopedItems.length
      : scopedItems.filter((item) => warehouseRequestStage(item, warehouse) === value).length;
  const statusOptions = warehouse
    ? [
        ["all", "Todas"],
        ["pending", "Pendientes de recibir"],
        ["authorization", "Pendientes de autorizar"],
        ["approved", "Autorizadas"],
        ["dispatch", "Despachar"],
        ["withdrawn", "Retiradas"],
        ["rejected", "Rechazadas"],
        ["cancelled", "Anuladas"],
      ]
    : [
        ["all", "Todas"],
        ["pending", "Pendientes de recibir"],
        ["authorization", "Pendientes de autorizar"],
        ["approved", "Autorizadas"],
        ["withdraw", "Retirar"],
        ["withdrawn", "Retiradas"],
        ["rejected", "Rechazadas"],
        ["cancelled", "Anuladas"],
      ];
  async function open(item: PdfRequest,edit=false) {
    const response = await fetch(`/api/institutional-documents/${item.id}`, {
      credentials: "same-origin",
      cache: "no-store",
    });
    if (!response.ok) {
      setError("No se pudo abrir la solicitud PDF.");
      return;
    }
    const document=await response.json() as DocumentDetail;
    setEditOnOpen(edit&&document.canEditWarehouseRequest?document.id:null);
    setDetail(document);
  }
  async function refreshDetail(updated?: DocumentDetail) {
    if (!detail) return;
    if (updated) {
      setDetail(updated);
      return;
    }
    const response = await fetch(`/api/institutional-documents/${detail.id}?v=${Date.now()}`, {
      credentials: "same-origin",
      cache: "no-store",
    });
    const contentType = response.headers.get("content-type") || "";
    if (!response.ok || !contentType.toLowerCase().includes("json"))
      throw new Error("No se pudo actualizar la solicitud. Vuelve a abrirla.");
    setDetail(await response.json());
    await load();
  }
  async function preparedPdf(item: PdfRequest) {
    const detailResponse = await fetch(
      `/api/institutional-documents/${item.id}`,
      { credentials: "same-origin", cache: "no-store" },
    );
    if (!detailResponse.ok)
      throw new Error("No se pudo preparar el formulario.");
    const documentDetail = (await detailResponse.json()) as DocumentDetail;
    const fileResponse = await fetch(documentDetail.fileUrl, {
      credentials: "same-origin",
      cache: "no-store",
    });
    if (!fileResponse.ok) throw new Error("No se pudo cargar el PDF.");
    const original = new Uint8Array(await fileResponse.arrayBuffer());
    return {
      detail: documentDetail,
      bytes: await buildReceiptCopy(
        original,
        documentDetail,
        await realLogoData(),
        window.location.origin,
      ),
    };
  }
  async function downloadPdf(item: PdfRequest) {
    try {
      const { detail: documentDetail, bytes } = await preparedPdf(item);
      downloadDocument(bytes, documentDetail.originalFileName);
      setError("");
    } catch (reason) {
      setError((reason as Error).message);
    }
  }
  async function printPdf(item: PdfRequest) {
    try {
      const { bytes } = await preparedPdf(item),
        url = URL.createObjectURL(
          new Blob([bytes.slice().buffer], { type: "application/pdf" }),
        ),
        popup = window.open(url, "_blank", "noopener,noreferrer");
      if (popup)
        popup.addEventListener("load", () => popup.print(), { once: true });
      window.setTimeout(() => URL.revokeObjectURL(url), 60000);
      setError("");
    } catch (reason) {
      setError((reason as Error).message);
    }
  }
  if (detail)
    return (
      <InstitutionalDocumentDetail
        item={detail}
        onBack={() => { setDetail(null); void load(); }}
        onChanged={refreshDetail}
        warehouseAccess={warehouse}
        warehouseAuthorizeAccess={
          session.permissions.canAuthorizeWarehouseRequests
        }
        initialWarehouseEdit={editOnOpen===detail.id}
      />
    );
  return (
    <section
      className={`warehouse-pdf-management${embedded ? " embedded" : ""}`}
      aria-labelledby="warehouse-pdf-title"
    >
      <header className="mc-page-heading">
        <div>
          <small>
            {warehouse
              ? "ALMACÉN · RECEPCIÓN DOCUMENTAL"
              : "FORMULARIOS · SOLICITUDES A ALMACÉN"}
          </small>
          <h3 id="warehouse-pdf-title">Solicitudes de equipo</h3>
          <p>
            {warehouse
              ? "Abre una solicitud pendiente y pulsa Recibir y sellar. Después pasará a Pendientes de autorizar; al autorizarla podrás asignar equipo y serial para enviarla a Despachar."
              : `Historial de solicitudes de equipo enviadas por ${departmentName || "tu departamento"}.`}
          </p>
        </div>
        <div>
          {requestDepartment&&<button type="button" className="primary" onClick={()=>setComposerOpen(true)}><Plus/>Nueva solicitud</button>}
          <button type="button" onClick={() => setTraceCode("")}>
            <QrCode />
            Escanear QR y consultar etapa
          </button>
          <button type="button" disabled={loading} onClick={() => void load()}>
            {loading ? <LoaderCircle className="spin" /> : <RefreshCw />}
            Actualizar
          </button>
        </div>
      </header>
      {(vittini||warehouse)&&<section className="weekend-loan-section"><h3>Préstamos de fin de semana · Avería Técnica</h3><p>Equipos entregados a Vittini, uso por agencia y devolución de sobrantes.</p><button onClick={()=>{setLoanMode(!loanMode);setStatus('all')}}>{loanMode?'Ver todas las solicitudes':'Ver préstamos de fin de semana'}</button>{vittini&&<button className="primary" onClick={()=>setLoanComposer(true)}>Solicitar préstamo de fin de semana</button>}</section>}
      <div className="mc-summary">
        <article>
          <FileText />
          <span>
            <strong>{scopedItems.length}</strong>
            <small>Solicitudes registradas</small>
          </span>
        </article>
        <article>
          <Inbox />
          <span>
            <strong>{count("pending")}</strong>
            <small>{warehouse ? "Pendientes de recibir" : "Pendientes"}</small>
          </span>
        </article>
        <article>
          <CheckCircle2 />
          <span>
            <strong>{count(warehouse ? "authorization" : "withdraw")}</strong>
            <small>
              {warehouse ? "Pendientes de autorizar" : "Listas para retirar"}
            </small>
          </span>
        </article>
      </div>
      {warehouse && !embedded && !departmentName && (
        <section className="mc-department-folders">
          <header>
            <div>
              <small>ORGANIZACIÓN DE ALMACÉN</small>
              <h4>Solicitudes por departamento</h4>
              <p>
                Abre una carpeta para revisar únicamente los formularios
                enviados por esa área.
              </p>
            </div>
            <button
              className={!departmentFilter ? "active" : ""}
              onClick={() => setDepartmentFilter("")}
            >
              <Grid2X2 />
              Ver todos
            </button>
          </header>
          <div>
            {departments.map((name) => {
              const scoped = items.filter((item) => item.department === name);
              return (
                <button
                  key={name}
                  className={departmentFilter === name ? "active" : ""}
                  onClick={() => setDepartmentFilter(name)}
                >
                  <span>
                    <FolderOpen />
                  </span>
                  <small>DEPARTAMENTO</small>
                  <strong>{name}</strong>
                  <em>
                    {scoped.length} solicitudes ·{" "}
                    {
                      scoped.filter((item) =>
                        ["pending", "correction_requested"].includes(
                          item.status,
                        ),
                      ).length
                    }{" "}
                    pendientes
                  </em>
                  <b>Abrir carpeta</b>
                </button>
              );
            })}
          </div>
        </section>
      )}
      <div className="mc-searchbar">
        <label>
          <Search />
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Buscar formulario, agencia o remitente…"
          />
        </label>
        {warehouse && !embedded && !departmentName && (
          <select
            value={departmentFilter}
            onChange={(event) => setDepartmentFilter(event.target.value)}
          >
            <option value="">Todos los departamentos</option>
            {departments.map((name) => (
              <option key={name}>{name}</option>
            ))}
          </select>
        )}
        <div className="mc-view-switch">
          <button
            aria-label="Vista de lista"
            aria-pressed={view === "list"}
            onClick={() => setView("list")}
          >
            <List />
          </button>
          <button
            aria-label="Vista de tarjetas"
            aria-pressed={view === "cards"}
            onClick={() => setView("cards")}
          >
            <Grid2X2 />
          </button>
        </div>
      </div>
      {error && (
        <p className="mw-error" role="alert">
          {error}
        </p>
      )}
      <div className="mc-catalog-layout">
        <main>
          <div className="mc-results">
            <strong>
              {status === "all"
                ? "Todas las solicitudes"
                : names[status] || status}
            </strong>
            <span>{visible.length} formularios</span>
          </div>
          <div
            className={`mw-request-grid mc-request-collection warehouse-pdf-list ${view}`}
          >
            {visible.map((item) => {
              const shownStatus = warehouseRequestStage(item, warehouse);
              return (
                <article key={item.id}>
                  <div className="mc-request-title">
                    <span className={`mw-status ${shownStatus}`}>
                      {names[shownStatus] || shownStatus}
                    </span>
                    <small>
                      {item.department}
                      {item.warehouseRequest?.ticketNumber
                        ? ` · Ticket #${item.warehouseRequest.ticketNumber}`
                        : ""}
                    </small>
                    <h3>{item.title}</h3>{item.warehouseRequest?.weekendLoan&&<p><strong>Préstamo de fin de semana</strong> · {item.warehouseRequest.weekendLoan.from} al {item.warehouseRequest.weekendLoan.to} · Devolver: {item.warehouseRequest.weekendLoan.returnBy}</p>}
                    <p>
                      {item.originalFileName} · {(item.size / 1024).toFixed(1)}{" "}
                      KB
                    </p>
                  </div>
                  {item.receivedUtc && warehouse && (
                    <div className={`mw-document-stamp ${item.status}`}>
                      <Stamp />
                      <span>
                        <strong>
                          {shownStatus === "cancelled"
                            ? "ANULADO"
                            : shownStatus === "withdrawn"
                              ? "ENTREGADO Y RECIBIDO"
                            : item.status === "correction_requested"
                              ? "CORRECCIÓN SOLICITADA"
                            : shownStatus === "dispatch"
                            ? "LISTO PARA DESPACHAR"
                            : item.status === "approved"
                              ? "AUTORIZADO"
                            : item.status === "rejected"
                              ? "RECHAZADO"
                              : "RECIBIDO · PENDIENTE DE AUTORIZAR"}
                        </strong>
                        <small>
                          {item.status === "received"
                              ? item.receiverDisplayName
                              : item.decisionBy || item.receiverDisplayName}
                        </small>
                      </span>
                    </div>
                  )}
                  <p className="mc-excerpt">
                    {item.description || "Solicitud de equipo institucional"}
                  </p>
                  <footer>
                    <strong>
                      {item.ownerDisplayName || item.ownerUserName}
                    </strong>
                    <time>
                      {new Date(item.createdUtc).toLocaleString("es-DO", {
                        timeZone: "America/Santo_Domingo",
                      })}
                    </time>
                  </footer>
                  <div className="mw-toolbar mc-row-actions">
                    <button
                      className={
                        warehouse &&
                        ["pending", "authorization", "approved", "dispatch"].includes(shownStatus)
                          ? "primary"
                          : ""
                      }
                      onClick={() => void (requesterOnly?printPdf(item):open(item))}
                    >
                      <Eye />
                      {warehouse && shownStatus === "pending"
                        ? "Recibir"
                        : warehouse && shownStatus === "authorization"
                          ? "Autorizar"
                          : warehouse && shownStatus === "approved"
                            ? "Asignar equipo y serial"
                            : warehouse && shownStatus === "dispatch"
                              ? "Despachar"
                              : !warehouse && shownStatus === "withdraw"
                                ? "Ver equipos para retirar"
                                : "Ver formulario"}
                    </button>
                    {item.canEditWarehouseRequest&&<button className="primary" onClick={()=>void open(item,true)}><PenLine/>Editar formulario</button>}
                    <button
                      onClick={() =>
                        setTraceCode(
                          item.originalFileName.replace(/\.pdf$/i, ""),
                        )
                      }
                    >
                      <QrCode />
                      Consultar etapa
                    </button>
                    {warehouse && ["dispatch", "withdrawn"].includes(shownStatus) && (
                      <WarehouseRequestPickupButton
                        item={item}
                        onCompleted={async (updated) => {
                          await load();
                          setDetail(updated);
                        }}
                      />
                    )}
                    <button onClick={() => void downloadPdf(item)}>
                      <Download />
                      Descargar comprobante PDF
                    </button>
                    <button onClick={() => void printPdf(item)}>
                      <Printer />
                      Imprimir
                    </button>
                    {!requesterOnly&&<WarehouseRequestCancelButton item={item} canManage={warehouse||session.role==="Administrator"||!!item.isRecipient} onCancelled={load}/>}
                    {warehouse &&
                      item.status === "approved" &&
                      onOpenInventory && (
                        <button
                          className="primary"
                          onClick={() =>
                            onOpenInventory(
                              item.department,
                              item.originalFileName.replace(/\.pdf$/i, ""),
                            )
                          }
                        >
                          <PackageSearch />
                          Abrir inventario
                        </button>
                      )}
                  </div>
                </article>
              );
            })}
            {!loading && !visible.length && (
              <div className="mc-empty">
                <Inbox />
                <h4>No hay solicitudes de equipo con estos filtros</h4>
                <p>Los formularios enviados a Almacén aparecerán aquí.</p>
              </div>
            )}
          </div>
        </main>
        <aside>
          <section className="mc-side-panel">
            <h4>Estado de la solicitud</h4>
            {statusOptions.map(([value, label]) => (
              <button
                key={value}
                className={status === value ? "active" : ""}
                onClick={() => setStatus(value)}
              >
                <Inbox />
                {label}
                <b>{count(value)}</b>
              </button>
            ))}
          </section>
          {warehouse && (
            <section className="mc-help-card">
              <Stamp />
              <h4>Despacho interno</h4>
              <p>
                Después de recibir, la encargada autoriza la solicitud. Luego
                Almacén asigna los equipos y seriales y la envía a Despachar.
              </p>
            </section>
          )}
        </aside>
      </div>
      {traceCode !== null && (
        <EquipmentTracking
          initialCode={traceCode}
          onClose={() => setTraceCode(null)}
        />
      )}
      {loanComposer&&<WarehouseRequestComposer session={session} department={requestDepartment||"TECHNOLOGY"} agencies={[]} weekendLoan onClose={()=>setLoanComposer(false)} onSaved={async()=>{setLoanComposer(false);setLoanMode(true);setStatus('all');await load();window.dispatchEvent(new Event('support-live-refresh'));}}/>}
      {composerOpen&&<WarehouseRequestComposer session={session} department={requestDepartment} agencies={[]} onClose={()=>setComposerOpen(false)} onSaved={async()=>{setComposerOpen(false);setStatus('all');if(warehouse)setDepartmentFilter('Almacén');await load();window.dispatchEvent(new Event('support-live-refresh'));}}/>}
    </section>
  );
}
