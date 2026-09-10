import { describe, it, expect, vi } from "vitest";
import crypto from "node:crypto";

// El token se firma con este secreto (leído de forma perezosa).
process.env.NEXTAUTH_SECRET = "test-secret-para-tokens-1234567890";

import { createUploadToken, verifyUploadToken } from "@/lib/upload-token";

describe("upload-token", () => {
  it("valida un token para el mismo profileId (roundtrip)", () => {
    const token = createUploadToken("perfil-123");
    expect(verifyUploadToken(token, "perfil-123")).toBe(true);
  });

  it("rechaza el token para un profileId distinto", () => {
    const token = createUploadToken("perfil-123");
    expect(verifyUploadToken(token, "otro-perfil")).toBe(false);
  });

  it("rechaza un token manipulado", () => {
    const token = createUploadToken("perfil-123");
    const tampered = token.slice(0, -2) + "xx";
    expect(verifyUploadToken(tampered, "perfil-123")).toBe(false);
  });

  it("rechaza tokens mal formados o vacíos", () => {
    expect(verifyUploadToken("", "perfil-123")).toBe(false);
    expect(verifyUploadToken("a.b", "perfil-123")).toBe(false);
    expect(verifyUploadToken(null, "perfil-123")).toBe(false);
  });

  it("mantiene el permiso válido sin importar el tiempo transcurrido", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-01T00:00:00Z"));
    const token = createUploadToken("perfil-123");
    vi.setSystemTime(new Date("2036-01-01T00:00:00Z"));
    expect(verifyUploadToken(token, "perfil-123")).toBe(true);
    vi.useRealTimers();
  });

  it("acepta un permiso firmado por la versión anterior aunque su fecha haya pasado", () => {
    const payload = "perfil-123.1";
    const signature = crypto
      .createHmac("sha256", process.env.NEXTAUTH_SECRET!)
      .update(payload)
      .digest("base64url");
    expect(verifyUploadToken(`${payload}.${signature}`, "perfil-123")).toBe(true);
  });
});
