import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const api=readFileSync(resolve("api/Program.cs"),"utf8");
const db=readFileSync(resolve("api/DatabaseSqlServerScoped.cs"),"utf8");
const ui=readFileSync(resolve("src/components/vite/TicketCenter.tsx"),"utf8");

describe("anulación de hallazgos automáticos",()=>{
  it("valida por separado los permisos de motivo e imágenes",()=>{
    expect(api).toContain('"/api/tickets/findings/{id:guid}/cancel"');
    expect(api).toContain('canCancelFindingWithReason');
    expect(api).toContain('canCancelFindingWithoutReason');
    expect(api).toContain('canCancelFindingWithEvidence');
    expect(api).toContain('canCancelFindingWithoutEvidence');
    expect(api).toContain('files.Count>6');
    expect(api).toContain('ValidEvidenceImage(data,file.ContentType)');
    expect(api).toContain('RequireAuthorization("SupportTechnician")');
  });
  it("cambia estado y guarda evidencia junto con la auditoría en una transacción",()=>{
    expect(db).toContain("status='CANCELLED',resolution=@reason");
    expect(db).toContain("af.status IN('OPEN','IN_PROGRESS')");
    expect(db).toContain("INSERT INTO dbo.finding_resolution_evidence");
    expect(db).toContain("'FINDING_CANCELLED'");
    expect(db).toContain("await transaction.CommitAsync(ct);return new(true,\"OK\",null)");
    expect(db).toContain("'RESOLVED','CLOSED','CANCELLED'");
  });
  it("ofrece botón en tarjeta, permite quitar una imagen y muestra el motivo archivado",()=>{
    expect(ui).toContain('className="finding-cancel-button"');
    expect(ui).toContain('findingActionMode==="cancel"');
    expect(ui).toContain('Confirmar anulación');
    expect(ui).toContain('aria-label={`Eliminar ${photo.name}`}');
    expect(ui).toContain('Motivo de anulación:');
  });
});
