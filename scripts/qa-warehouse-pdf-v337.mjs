import fs from "node:fs";
import { buildReceiptCopy } from "../src/components/vite/institutionalReceiptPdf.ts";
import { buildWarehouseRequestPdf } from "../src/components/vite/warehouseRequestPdf.ts";

const logo = `data:image/png;base64,${fs.readFileSync("public/loto-real-logo-transparent.png").toString("base64")}`;
const original = new Uint8Array(await buildWarehouseRequestPdf({
  number: "SOL-ALM-V338-PREVIEW",
  department: "TECHNOLOGY",
  agency: { codigo: "6010183", terminal: "6010183 | 572 - DOGOUT / RIFEROS", grupo: "01 | RIFERO" },
  installer: "Técnico de instalación",
  priority: "HIGH",
  notes: "Solicitud institucional de muestra para validar espacios limpios de firma y sello físico.",
  createdBy: "Administrador principal",
  createdByLogin: "admin@local",
  createdAt: new Date("2026-09-12T14:30:00-04:00"),
  logoData: logo,
  origin: "https://realagencias.runasp.net",
  lines: [
    { kind: "EQUIPMENT", name: "Escáner 2Connect", quantity: 1, serial: "7460548000154", note: "Equipo para instalación" },
    { kind: "COMPONENT", name: "Cable de datos", quantity: 2, serial: "No aplica", note: "Accesorios de conexión" },
  ],
}));
const cleanForm = await buildReceiptCopy(original, {
  title: "Solicitud a Almacén SOL-ALM-V338-PREVIEW",
  ownerDisplayName: "Administrador principal",
  ownerUserName: "admin@local",
  status: "pending",
} , logo, "https://realagencias.runasp.net");

fs.mkdirSync("output/pdf", { recursive: true });
fs.writeFileSync("output/pdf/V338-solicitud-almacen-formulario-limpio.pdf", cleanForm);
console.log("Generated V338 clean warehouse request PDF QA sample.");
