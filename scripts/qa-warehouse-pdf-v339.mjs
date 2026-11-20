import fs from "node:fs";
import { buildReceiptCopy } from "../src/components/vite/institutionalReceiptPdf.ts";
import { buildWarehouseRequestPdf } from "../src/components/vite/warehouseRequestPdf.ts";

const logo = `data:image/png;base64,${fs.readFileSync("public/loto-real-logo-transparent.png").toString("base64")}`;
const original = new Uint8Array(await buildWarehouseRequestPdf({
  number: "SOL-ALM-V339-ENVIO",
  department: "TECHNOLOGY",
  agency: { codigo: "6010183", terminal: "6010183 | 572 - DOGOUT / RIFEROS", grupo: "01 | RIFERO" },
  installer: "Técnico de instalación",
  priority: "HIGH",
  notes: "Activo enviado nuevamente a Almacén desde las acciones del inventario.",
  createdBy: "Administrador principal",
  createdByLogin: "admin@local",
  createdAt: new Date("2026-09-12T09:15:00-04:00"),
  logoData: logo,
  origin: "https://realagencias.runasp.net",
  lines: [
    { kind: "RETURN", name: "Escáner 2Connect", quantity: 1, serial: "7460548000154", note: "Envío a Almacén" },
  ],
}));

const authorized = await buildReceiptCopy(original, {
  id: 339,
  title: "Solicitud a Almacén SOL-ALM-V339-ENVIO",
  originalFileName: "SOL-ALM-V339-ENVIO.pdf",
  contentType: "application/pdf",
  size: original.length,
  ownerUserName: "admin@local",
  ownerDisplayName: "Administrador principal",
  department: "Tecnología",
  area: "communications",
  channel: "requirements",
  recipientDepartment: "Almacén",
  status: "approved",
  createdUtc: "2026-09-12T13:15:00Z",
  sentUtc: "2026-09-12T13:15:00Z",
  receivedUtc: "2026-09-12T13:20:00Z",
  decisionUtc: "2026-09-12T13:25:00Z",
  decisionBy: "Encargado de Almacén",
  receiverDisplayName: "Encargado de Almacén",
  receiverLogin: "almacen@local",
  receiptCode: "REC-V339-001",
  verificationToken: "V339-ENVIO-ALMACEN-QR",
  fileUrl: "/api/institutional-documents/339/file",
  isOwner: false,
  canEdit: false,
  sharedWith: [],
  isRecipient: true,
  canMove: false,
}, logo, "https://realagencias.runasp.net");

fs.mkdirSync("output/pdf", { recursive: true });
fs.writeFileSync("output/pdf/V339-envio-almacen-autorizado.pdf", authorized);
console.log("Generated V339 authorized warehouse return PDF QA sample.");
