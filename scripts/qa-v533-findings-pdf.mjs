import { mkdir, readFile, writeFile } from "node:fs/promises";
import { jsPDF } from "jspdf";

globalThis.jspdf={jsPDF};
await import("../public/v520-findings-pdf.js");

const rows=[{
  agencyCode:"6011759",
  agency:"9321-30 DE MAYO/JAVIER",
  group:"Publicidad institucional",
  title:"Falla de conectividad",
  detail:"La conectividad impide o compromete la operación.",
  diagnosis:"Se confirmó la presencia de material publicitario anterior en el área de venta.",
  solution:"Se coordinó el retiro del material y la limpieza de la superficie.",
  verification:"Pendiente de evidencia fotográfica final.",
  recommendations:"Validar la imagen institucional durante la próxima visita.",
  supervisor:"Wileilin Caraballo",
  assignedTechnician:"Sin asignar",
  resolvedBy:"No registrado",
  evidenceCount:2
}];

const logo=await readFile("public/loto-real-logo-transparent.png");
const logoData="data:image/png;base64,"+logo.toString("base64");
const pdf=globalThis.V520FindingsPdf.build(rows,"Período consultado: 1/11/2026 - 16/11/2026",logoData);
await mkdir("tmp/pdfs",{recursive:true});
await writeFile("tmp/pdfs/v533-hallazgos-muestra.pdf",Buffer.from(pdf.output("arraybuffer")));
