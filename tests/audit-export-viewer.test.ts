import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const panel = readFileSync(resolve(process.cwd(), "src/components/vite/AdminPanel.tsx"), "utf8");
const styles = readFileSync(resolve(process.cwd(), "src/geo-atlas-2026.css"), "utf8");

describe("exportación y visor de auditorías", () => {
  it("filtra y exporta un PDF por grupo con respuestas y evidencias", () => {
    expect(panel).toContain("const visibleAudits");
    expect(panel).toContain("const exportAudits");
    expect(panel).toContain("Descargar PDF del grupo");
    expect(panel).toContain('import("jspdf")');
    expect(panel).toContain('link.download = `${safeFileName(targetGroup)}-auditorias-');
    expect(panel).toContain("prepareEvidence(photo.url)");
    expect(panel).toContain("Filtrar y exportar grupo");
    expect(panel).toContain("const selectedExportGroup = auditGroups.includes(exportGroup)");
    expect(panel).toContain("grupo: audit.grupo");
    expect(panel).toContain('assetAsDataUrl("/loto-real-logo-transparent.png")');
    expect(panel).toContain('pdf.addImage(pdfLogo, "PNG"');
    expect(panel).toContain("const ratio = jpeg.width / jpeg.height");
    expect(panel).toContain("const displayedAudits = selectedExportGroup");
    expect(panel).toContain("displayedAudits.map");
    expect(panel).toContain("mapWithConcurrency(auditsToExport, 4");
    expect(panel).toContain("mapWithConcurrency(audit.photos || [], 3");
  });

  it("abre las fotografías en un visor de pantalla completa", () => {
    expect(panel).toContain("evidence-lightbox");
    expect(panel).toContain("audit-photo-preview");
    expect(panel).toContain("onPhotoClick");
    expect(styles).toContain(".evidence-lightbox");
    expect(panel).toContain("Clic para ampliar");
    expect(panel).toContain("openEvidencePhoto");
  });
});
