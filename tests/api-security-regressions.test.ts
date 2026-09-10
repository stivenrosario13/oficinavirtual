import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const program = readFileSync(resolve(process.cwd(), "api/Program.cs"), "utf8");
const licenseServer = readFileSync(resolve(process.cwd(), "license-server/Program.cs"), "utf8");

describe("regresiones de seguridad y evidencia de campo", () => {
  it("captura los errores SQL antes de validar la sesión", () => {
    const errorHandler=program.indexOf("try { await next(); }");
    expect(errorHandler).toBeGreaterThan(-1);
    expect(errorHandler).toBeLessThan(program.indexOf("app.UseAuthentication();"));
    expect(errorHandler).toBeLessThan(program.indexOf(".Account(userId,context.RequestAborted)"));
    expect(program).toContain('code="SESSION_REQUIRED"');
    expect(program).toContain('context.Response.StatusCode=503');
  });
  it("protege el geocodificador y no crea credenciales predeterminadas", () => {
    expect(program).toContain('}).RequireAuthorization("FieldAudit");');
    expect(program).not.toContain('??"admin@local"');
    expect(program).not.toContain('??"admin123"');
  });

  it("solo acepta geolocalización real y no futura, sin caducarla por tiempo", () => {
    expect(program).toContain('if(g.Source!="browser_geolocation")');
    expect(program).not.toContain('g.Source!="demo_geolocation"');
    expect(program).not.toContain('MAX_LOCATION_AGE_SECONDS');
    expect(program).not.toContain('demasiado antigua');
    expect(program).toContain('if(ageSeconds< -120)');
    expect(program).toContain('photoAgeSeconds< -120');
    expect(program).not.toContain('photoAgeSeconds>');
  });

  it("mantiene la firma de fotos sin hacer expirar el permiso", () => {
    expect(program).toContain('public string Create(string id)=>$"{id}.{Sign(id)}"');
    expect(program).not.toContain('DateTimeOffset.UtcNow.AddHours(1)');
    expect(program).not.toContain('exp>=DateTimeOffset.UtcNow');
  });

  it("comprueba la firma real de las fotografías antes de guardarlas", () => {
    expect(program).toContain('if(!ValidEvidenceImage(imageData,file.ContentType))');
    expect(program).toContain('Encoding.ASCII.GetString(data,4,4)=="ftyp"');
  });

  it("limita fuerza bruta y escrituras sensibles", () => {
    expect(program).toContain('options.AddPolicy("login"');
    expect(program).toContain('.RequireRateLimiting("login")');
    expect(program).toContain('options.AddPolicy("sensitive-write"');
    expect(licenseServer).toContain('options.AddPolicy("license-check"');
    expect(licenseServer).toContain('options.AddPolicy("license-admin"');
  });

  it("bloquea solicitudes cruzadas y agrega cabeceras defensivas", () => {
    expect(program).toContain("IsCrossSiteRequest(context.Request)");
    expect(program).toContain('context.Response.Headers["Content-Security-Policy"]');
    expect(program).toContain('context.Response.Headers["Permissions-Policy"]');
    expect(program).toContain('context.Response.Headers["X-Frame-Options"]="DENY"');
    expect(licenseServer).toContain('context.Response.Headers["Content-Security-Policy"]');
  });

  it("no expone diagnósticos y valida archivos por su firma", () => {
    expect(program).not.toContain("diagnostic=guard.LastDiagnostic");
    expect(licenseServer).not.toContain("diagnostic = error.GetType()");
    expect(program).toContain("ValidAudio(audioData,contentType)");
    expect(program).toContain("ValidEvidenceImage(avatarData,file.ContentType)");
  });
});
