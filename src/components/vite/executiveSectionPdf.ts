import { jsPDF } from "jspdf";

export type ExecutivePdfSection = {
  title: string;
  subtitle: string;
  period: string;
  metrics?: Array<{ label: string; value: number }>;
  columns?: string[];
  rows?: string[][];
};

export async function buildExecutiveSectionPdf(section: ExecutivePdfSection, logo: string) {
  const pdf = new jsPDF({ unit: "mm", format: "a4", compress: true });
  const width = 210;
  const margin = 14;
  let y = 42;
  let page = 1;

  const header = () => {
    pdf.setFillColor(4, 24, 42);
    pdf.rect(0, 0, width, 30, "F");
    pdf.addImage(logo, "PNG", margin, 6, 27, 16, undefined, "FAST");
    pdf.setTextColor(255, 255, 255);
    pdf.setFont("helvetica", "bold");
    pdf.setFontSize(13);
    pdf.text(section.title, 47, 12);
    pdf.setTextColor(111, 218, 237);
    pdf.setFontSize(7);
    pdf.text(section.subtitle.toUpperCase(), 47, 18);
    pdf.setTextColor(97, 125, 142);
    pdf.text(section.period, 47, 23);
  };
  const footer = () => {
    pdf.setDrawColor(35, 83, 108);
    pdf.line(margin, 282, width - margin, 282);
    pdf.setFontSize(7);
    pdf.setTextColor(90, 116, 132);
    pdf.text("Oficina Virtual · Grupo Tejeda", margin, 288);
    pdf.text(`Página ${page}`, width - margin, 288, { align: "right" });
  };
  const nextPage = () => {
    footer();
    pdf.addPage();
    page += 1;
    header();
    y = 42;
  };

  header();
  if (section.metrics?.length) {
    const columns = Math.min(4, section.metrics.length);
    const gap = 4;
    const cardWidth = (width - margin * 2 - gap * (columns - 1)) / columns;
    section.metrics.forEach((metric, index) => {
      if (index > 0 && index % columns === 0) y += 27;
      const x = margin + (index % columns) * (cardWidth + gap);
      pdf.setFillColor(240, 247, 250);
      pdf.setDrawColor(34, 137, 164);
      pdf.roundedRect(x, y, cardWidth, 22, 2, 2, "FD");
      pdf.setTextColor(9, 96, 124);
      pdf.setFont("helvetica", "bold");
      pdf.setFontSize(16);
      pdf.text(String(metric.value), x + 4, y + 9);
      pdf.setTextColor(57, 82, 98);
      pdf.setFontSize(6.5);
      pdf.text(pdf.splitTextToSize(metric.label.toUpperCase(), cardWidth - 8), x + 4, y + 15);
    });
    y += 32;
  }

  if (section.columns?.length && section.rows) {
    const available = width - margin * 2;
    const columnWidth = available / section.columns.length;
    const drawColumns = () => {
      pdf.setFillColor(9, 65, 91);
      pdf.rect(margin, y, available, 8, "F");
      pdf.setTextColor(255, 255, 255);
      pdf.setFont("helvetica", "bold");
      pdf.setFontSize(7);
      section.columns!.forEach((column, index) => pdf.text(column, margin + index * columnWidth + 2, y + 5));
      y += 8;
    };
    drawColumns();
    section.rows.forEach((row, rowIndex) => {
      const lines = row.map(cell => pdf.splitTextToSize(cell || "—", columnWidth - 4));
      const height = Math.max(7, ...lines.map(cell => cell.length * 4 + 2));
      if (y + height > 278) {
        nextPage();
        drawColumns();
      }
      pdf.setFillColor(rowIndex % 2 ? 248 : 238, rowIndex % 2 ? 251 : 246, rowIndex % 2 ? 252 : 249);
      pdf.rect(margin, y, available, height, "F");
      pdf.setTextColor(30, 55, 70);
      pdf.setFont("helvetica", "normal");
      pdf.setFontSize(7);
      lines.forEach((cell, index) => pdf.text(cell, margin + index * columnWidth + 2, y + 4));
      y += height;
    });
  }
  footer();
  return new Uint8Array(pdf.output("arraybuffer"));
}
