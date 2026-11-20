import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";

const read=(path:string)=>readFileSync(path,"utf8");

describe("V529 sobre base exacta V520",()=>{
  it("agrega únicamente buscador y detalle visual aislado",()=>{
    const script=read("public/v520-targeted-enhancements.js");
    const pdf=read("public/v520-findings-pdf.js");
    const css=read("public/v520-targeted-enhancements.css");
    expect(script).toContain("Buscar en la Oficina Virtual...");
    expect(script).toContain("HALLAZGOS AUTOMÁTICOS · DETALLE COMPLETO");
    expect(script).toContain("Trabajo y solución aplicada");
    expect(script).toContain("Verificación final");
    expect(css).toContain("Extensiones puntuales sobre V520");
    expect(css).not.toContain(".dashboard-card");
    expect(css).not.toContain(".portal-sidebar");
    expect(css).toContain("position:absolute!important");
    expect(script).toContain('toolbar.insertAdjacentElement("afterend",section)');
    expect(script).toContain("account.parentNode===header");
    expect(script).not.toContain('header.insertBefore(box,account||null)');
    expect(script).toContain("downloadFindingsPdf");
    expect(script).toContain("window.V520FindingsPdf.download");
    expect(pdf).toContain("global.V520FindingsPdf");
    expect(pdf).toContain("Pregunta de auditoría que originó el hallazgo");
    expect(pdf).toContain("pdf.save");
    expect(pdf).toContain("/loto-real-logo-transparent.png");
    expect(pdf).toContain("pdf.addImage");
    expect(pdf).toContain("DAMAGE_CONNECTIVITY");
    expect(pdf).toContain('probe.includes("conectividad")');
    expect(pdf).toContain("enrich:enrich");
    expect(script).toContain("Pregunta de Auditoría de campo");
    expect(script).toContain("Por qué se creó automáticamente");
    expect(script).toContain("Respuesta registrada en Auditoría de campo");
    expect(script).toContain("cloneNode(true)");
  });

  it("tolera esquemas SQL históricos sin cancelar el reporte",()=>{
    const api=read("api/ExecutiveReport.cs");
    expect(api).toContain("if(!richFindingDetails)");
    expect(api).toContain("basicFindingDetailSql");
    expect(api).toContain("catch(Exception exception) when(exception is not OperationCanceledException)");
    expect(api).toContain("private static async Task<bool> ReadOptional");
    expect(api).toContain("FindingSourceQuestion");
    expect(api).toContain("FindingTriggerReason");
    expect(api).toContain("FindingSourceAnswer");
    expect(api).toContain("af.finding_key");
    expect(api).toContain("¿La agencia fue pintada?");
    expect(api).toContain("¿Hubo alguna avería durante el levantamiento?");
    expect(api).toContain("¿Le dieron mantenimiento a la impresora?");
    expect(api).toContain("Sí - Conectividad");
    expect(api).toContain('=>"No"');
  });

  it("mantiene disponible la bandeja aunque falle una fuente de notificaciones",()=>{
    const api=read("api/Program.cs");
    expect(api).toContain("Notificaciones de tickets omitidas");
    expect(api).toContain("Notificaciones de hallazgos omitidas");
    expect(api).toContain("notifications.OrderByDescending");
  });

  it("empaqueta desde la publicación original V520",()=>{
    const pack=read("scripts/package-v529-v520-targeted.ps1");
    expect(pack).toContain("^_publish_v520_revision_integral_");
    expect(pack).toContain("v520-targeted-enhancements.js");
    expect(pack).toContain("v520-findings-pdf.js");
    expect(pack).toContain("v520-jspdf.umd.min.js");
    expect(pack).not.toContain("npm run build");
  });
});
