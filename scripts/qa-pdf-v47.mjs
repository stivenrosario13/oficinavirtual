import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { jsPDF } from "jspdf";

mkdirSync("tmp/pdfs", { recursive: true });
const pdf = new jsPDF({ unit: "mm", format: "a4", compress: true });
pdf.setFillColor(5, 29, 54);
pdf.rect(0, 0, 210, 28, "F");
pdf.setTextColor(255, 255, 255);
pdf.setFont("helvetica", "bold");
const logo = `data:image/png;base64,${readFileSync("public/loto-real-logo-transparent.png").toString("base64")}`;
pdf.addImage(logo, "PNG", 14, 5, 26, 16);
pdf.setTextColor(105, 216, 255);
pdf.setFontSize(17);
pdf.text("GRUPO TEJEDA", 46, 12);
pdf.setTextColor(255, 255, 255);
pdf.setFontSize(9);
pdf.text("AUDITORIA COMPLETADA | GRUPO DE PRUEBA | L-1025", 46, 20);
pdf.setTextColor(20, 45, 68);
pdf.setFontSize(13);
pdf.text("REAL SOÑADORA Bella Vista", 14, 38);
pdf.setFont("helvetica", "normal");
pdf.setFontSize(9);
pdf.text([
  "Empleado: Empleado de prueba (EMP-001)",
  "Dirección: Calle de prueba número 10",
  "Sector / Municipio / Provincia: Bella Vista / Santo Domingo / Distrito Nacional",
  "Coordenadas: 18.448500, -69.943100 | Precisión: ±3 m",
  "Fecha: 05/08/2026 03:15 PM",
], 14, 48);
pdf.setFillColor(232, 245, 252);
pdf.roundedRect(12, 76, 186, 8, 2, 2, "F");
pdf.setFont("helvetica", "bold");
pdf.text("RESPUESTAS DEL FORMULARIO", 16, 81.5);
const questions = [
  "¿La agencia fue pintada?", "¿Fue colocado el sticker de raza?", "¿Fue colocado el sticker de Real?",
  "¿Fue retirada toda la publicidad de Loteka?", "¿Hubo alguna avería durante el levantamiento?",
  "¿Le dieron mantenimiento a la impresora?", "¿Tiene inversor?", "¿Tiene batería?",
];
let y = 92;
pdf.setFontSize(8.5);
questions.forEach((question, index) => {
  pdf.setFont("helvetica", "bold");
  pdf.text(`${index + 1}.`, 14, y);
  pdf.setFont("helvetica", "normal");
  pdf.text(question, 21, y);
  pdf.setFont("helvetica", "bold");
  pdf.text(index === 4 ? "No" : "Sí", 178, y, { align: "right" });
  y += 7;
});
pdf.setFont("helvetica", "bold");
pdf.text("EVIDENCIA FOTOGRÁFICA", 14, 158);
pdf.setFillColor(225, 239, 248);
pdf.roundedRect(14, 164, 182, 102, 3, 3, "F");
pdf.setTextColor(65, 92, 112);
pdf.text("Fotografía georreferenciada conservando su proporción", 105, 215, { align: "center" });
pdf.setFont("helvetica", "normal");
pdf.setFontSize(8);
pdf.text("18.448500, -69.943100 | Precisión: ±3 m | 05/08/2026 03:14 PM", 105, 273, { align: "center" });
pdf.text("Grupo Tejeda | GRUPO DE PRUEBA | Página 1 de 1", 105, 291, { align: "center" });
writeFileSync("tmp/pdfs/exportacion-auditoria-muestra.pdf", Buffer.from(pdf.output("arraybuffer")));
