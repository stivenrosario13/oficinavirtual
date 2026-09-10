import { describe, it, expect } from "vitest";
import { validateGeolocation, type GeoInput } from "@/lib/geo";
import { GEO_SOURCE_BROWSER } from "@/lib/constants";

function base(overrides: Partial<GeoInput> = {}): GeoInput {
  return {
    latitude: 18.4861,
    longitude: -69.9312,
    accuracyMeters: 20,
    capturedAt: new Date().toISOString(),
    source: GEO_SOURCE_BROWSER,
    ...overrides,
  };
}

describe("validateGeolocation", () => {
  it("acepta una ubicación válida", () => {
    expect(validateGeolocation(base()).ok).toBe(true);
  });

  it("rechaza null / sin captura", () => {
    expect(validateGeolocation(null).ok).toBe(false);
  });

  it("rechaza coordenadas 0,0", () => {
    const r = validateGeolocation(base({ latitude: 0, longitude: 0 }));
    expect(r.ok).toBe(false);
    expect(r.errors.join(" ")).toMatch(/0,0/);
  });

  it("rechaza latitud fuera de rango", () => {
    expect(validateGeolocation(base({ latitude: 91 })).ok).toBe(false);
    expect(validateGeolocation(base({ latitude: -91 })).ok).toBe(false);
  });

  it("rechaza longitud fuera de rango", () => {
    expect(validateGeolocation(base({ longitude: 181 })).ok).toBe(false);
  });

  it("rechaza precisión mayor a 100 metros", () => {
    const r = validateGeolocation(base({ accuracyMeters: 150 }));
    expect(r.ok).toBe(false);
    expect(r.errors.join(" ")).toMatch(/precisi/i);
  });

  it("rechaza precisión inválida (0 o negativa)", () => {
    expect(validateGeolocation(base({ accuracyMeters: 0 })).ok).toBe(false);
    expect(validateGeolocation(base({ accuracyMeters: -5 })).ok).toBe(false);
  });

  it("acepta ubicaciones antiguas sin hacer expirar la auditoría", () => {
    const old = new Date(Date.now() - 10 * 365 * 24 * 60 * 60 * 1000).toISOString();
    const r = validateGeolocation(base({ capturedAt: old }));
    expect(r.ok).toBe(true);
  });

  it("también acepta capturas antiguas cuando checkAge=false", () => {
    const old = new Date(Date.now() - 60 * 60 * 1000).toISOString();
    expect(validateGeolocation(base({ capturedAt: old }), { checkAge: false }).ok).toBe(
      true,
    );
  });

  it("rechaza una fecha de captura inválida", () => {
    expect(validateGeolocation(base({ capturedAt: "no-es-fecha" })).ok).toBe(
      false,
    );
  });

  it("rechaza una fecha de captura en el futuro", () => {
    const future = new Date(Date.now() + 3 * 60 * 1000).toISOString();
    expect(validateGeolocation(base({ capturedAt: future })).ok).toBe(false);
  });
});
