import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import MaintenanceCommunications from "../src/components/vite/MaintenanceCommunications";
import MaintenanceTemplates from "../src/components/vite/MaintenanceTemplates";
import type { PortalSession } from "../src/lib/session";

const session = { role: "Technology", supportTeam: "WAREHOUSE", supportDepartment: "TECHNOLOGY" } as PortalSession;

describe("biblioteca y comunicaciones de mantenimiento", () => {
  it("deja únicamente Solicitudes PDF en el centro de trabajo de Almacén", () => {
    const html = renderToStaticMarkup(<MaintenanceCommunications view="requirements" onNavigate={() => {}} onBack={() => {}} onChanged={() => {}} session={session} department="" />);
    for (const label of ["Solicitudes PDF", "Solicitudes de equipo", "Grupo Tejeda", "Pendientes de recibir", "Recibidas para decisión"]) expect(html).toContain(label);
    for (const removed of ["Comunicaciones de departamentos", "Mis plantillas", "Plantillas disponibles", "Gestión de compras"]) expect(html).not.toContain(removed);
    expect(html).not.toContain('role="tablist"');
  });

  it("limita publicación al almacén o administrador también en la interfaz", () => {
    const html = renderToStaticMarkup(<MaintenanceTemplates session={{ ...session, supportTeam: null }} />);
    expect(html).not.toContain("Publicar PDF");
    expect(html).toContain("Conduce institucional - dos firmas");
  });

  it("muestra el flujo PDF por estados, primero pendiente y luego recibido", () => {
    const html = renderToStaticMarkup(<MaintenanceCommunications view="requirements" onNavigate={() => {}} onBack={() => {}} onChanged={() => {}} session={session} department="" />);
    for (const label of ["Solicitudes PDF", "Pendientes de recibir", "Recibidas para decisión", "Solicitudes por departamento", "Estado de la solicitud", "firma de quien recibe es opcional"]) expect(html).toContain(label);
    expect(html).toContain("Autorizar y Rechazar");
  });

  it("elimina del Almacén los canales documentales duplicados", () => {
    const html = renderToStaticMarkup(<MaintenanceCommunications view="requests" onNavigate={() => {}} onBack={() => {}} onChanged={() => {}} session={session} department="" />);
    expect(html).toContain("Solicitudes PDF");
    for (const removed of ["Comunicaciones de departamentos", "Mis plantillas", "Plantillas disponibles", "Gestión de compras"]) expect(html).not.toContain(removed);
  });

  it("organiza requerimientos por carpetas y valida la importación Excel", () => {
    const requests = readFileSync("src/components/vite/WarehouseRequests.tsx", "utf8");
    const center = readFileSync("src/components/vite/TicketCenter.tsx", "utf8");
    const endpoint = readFileSync("api/InstitutionalDocumentEndpoints.cs", "utf8");
    expect(requests).toContain("Requerimientos por departamento");
    expect(requests).toContain("Abrir carpeta");
    expect(center).toContain("Importar inventario desde Excel");
    expect(center).toContain("maintenanceInventoryImport.errors.length");
    expect(center).toContain("seriales duplicados fueron omitidos");
    expect(endpoint).toContain('"received"=>docs.Where(d=>d.ReceivedUtc is not null&&(d.Status is "received" or "approved" or "rejected"))');
  });

  it("registra una solicitud multiequipo una sola vez en Solicitud de equipo", () => {
    const composer = readFileSync("src/components/vite/WarehouseRequestComposer.tsx", "utf8");
    const pdf = readFileSync("src/components/vite/warehouseRequestPdf.ts", "utf8");
    const center = readFileSync("src/components/vite/TicketCenter.tsx", "utf8");
    const endpoint = readFileSync("api/InstitutionalDocumentEndpoints.cs", "utf8");
    const store = readFileSync("api/InstitutionalDocumentStore.cs", "utf8");
    const detail = readFileSync("src/components/vite/InstitutionalDocumentDetail.tsx", "utf8");
    const management = readFileSync("src/components/vite/WarehousePdfRequests.tsx", "utf8");
    for (const marker of ["Equipos y materiales disponibles en Almacén", "Solo se muestran artículos con existencia disponible", 'fetch("/api/warehouse/fulfillment/inventory"', 'item.available>0', 'form.append("channel","requirements")', 'form.append("recipientDepartment","Almacén")', 'form.append("sourceDepartment"', 'fetch("/api/maintenance/movements"', 'movementType:"REQUEST"']) expect(composer).toContain(marker);
    for (const removed of ["Técnico que instalará o recibirá", "Devolución a Almacén", 'data-warehouse-scanner="true"', "Componente requerido"]) expect(composer).not.toContain(removed);
    for (const marker of ['WarehouseRequestLineKind="EQUIPMENT"|"COMPONENT"|"RETURN"', "REAL-WAREHOUSE-REQUEST-V319", "SOLICITUD A ALMACÉN", "FIRMA DE QUIEN RECIBE", "FIRMA DE QUIEN SOLICITA", '?equipment=']) expect(pdf).toContain(marker);
    for (const removed of ["RECEPCIÓN PENDIENTE", "Firma pendiente", "Pendiente de confirmar", "Pendiente - usuario de Almacén"]) expect(pdf).not.toContain(removed);
    expect(composer).not.toContain("Departamento o destino");
    expect(center).not.toContain("<SentWarehouseRequestHistory");
    expect(center).toContain("maintenance-form-card");
    expect(center).not.toContain("Preparar entrega");
    expect(center).toContain("Solicitud origen:");
    expect(management).toContain("Abrir inventario");
    expect(management).toContain("Abrir y recibir");
    expect(management).toContain('warehouseAccess?:boolean;embedded?:boolean');
    expect(management).toContain('useState<"list"|"cards">("cards")');
    expect(management).toContain("warehouse&&!embedded");
    expect(endpoint).toContain('sourceDepartment is "Tecnología" or "Servicios Generales"');
    expect(store).not.toContain("La persona que recibe debe firmar");
    expect(store).toContain("d.ReceiptSignature=b.SignatureData");
    expect(store).toContain('IsRecipient=Recipient(a)||(a.Manager&&RecipientDepartment=="Almacén")');
    expect(detail).toContain("Recibir y sellar");
    expect(detail).toContain("Recibir con firma");
    expect(detail).toContain("warehouseAccess");
    expect(detail).not.toContain("id-current-stamp");
    expect(detail).toContain("fixedWarehouseStamp");
    expect(management).toContain('value==="all"?items.length');
    expect(management).toContain("mw-document-stamp");
    expect(management).toContain('requesterStatus');
    expect(management).toContain('[["pending","Pendientes"],["received","Recibidas"],["approved","Autorizadas"],["rejected","Rechazadas"]]');
    expect(management).toContain("Consultar trazabilidad");
    const receipt = readFileSync("src/components/vite/institutionalReceiptPdf.ts", "utf8");
    for (const marker of ["sealLabel", "AUTORIZADO", "RECHAZADO", "Sello actual:", "FIRMA DE QUIEN SOLICITA", "Recompone la zona de recepción y firmas"]) expect(receipt).toContain(marker);
    expect(pdf).not.toContain("ESPACIO PARA SELLO INSTITUCIONAL");
    expect(detail).toContain("await buildReceiptCopy(original, item");
    const trace = readFileSync("api/DatabaseMaintenanceDocuments.cs", "utf8");
    for (const marker of ["SENT_TO_WAREHOUSE", "RECEIVED_WAREHOUSE", "DISPATCHED", "institutional_files"]) expect(trace).toContain(marker);
    expect(center).toContain("warehouseDispatchRequest");
    expect(center).toContain("Solicitud origen:");
  });

  it("muestra la solicitud PDF a ancho completo con zoom centrado y sin Seguimiento lateral", () => {
    const detail = readFileSync("src/components/vite/InstitutionalDocumentDetail.tsx", "utf8");
    const canvas = readFileSync("src/components/vite/InstitutionalPdfCanvas.tsx", "utf8");
    const styles = readFileSync("src/components/vite/institutionalBrandOverrides.css", "utf8");
    expect(detail).toContain('aria-label="Controles de zoom del documento"');
    expect(detail).toContain("zoom={zoom}");
    expect(detail).not.toContain("<h3>Seguimiento</h3>");
    expect(detail).not.toContain("id-trace-steps");
    expect(canvas).toContain("element.scrollLeft");
    expect(canvas).toContain("Math.max(fitWidth,paperWidth)");
    expect(styles).toContain("justify-content:center");
    expect(styles).toContain("V337: visor PDF profesional");
    expect(styles).toContain(".id-document-workspace .native-doc-detail-body{display:block");
  });

  it("comprueba límites y autorización del servidor para publicar o retirar", () => {
    const code = readFileSync("api/MaintenanceTemplateEndpoints.cs", "utf8");
    expect(code).toContain('a.Role!="Administrator"&&a.Team!="WAREHOUSE"');
    expect(code).toContain("10485760");
    expect(code).toContain('SequenceEqual("%PDF-"u8)');
    expect(code).toContain('RequireAuthorization("PortalUser")');
    expect(readFileSync("api/DatabaseMaintenanceTemplates.cs", "utf8")).toContain("is_archived=@archived");
  });
});
