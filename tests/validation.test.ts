import { describe, it, expect } from "vitest";
import { profileSubmitSchema } from "@/lib/validation";
import { GEO_SOURCE_BROWSER } from "@/lib/constants";

// UUID v4 válido (variante RFC correcta), como los que genera gen_random_uuid().
const AGENCY_ID = "11111111-1111-4111-8111-111111111111";

function validPayload(overrides: Record<string, unknown> = {}) {
  return {
    agencyId: AGENCY_ID,
    direccion: "Calle Principal 10",
    sector: "Centro",
    municipio: "Santo Domingo Este",
    provincia: "Santo Domingo",
    tipoEstablecimiento: "Colmado",
    geolocation: {
      latitude: 18.48,
      longitude: -69.93,
      accuracyMeters: 15,
      capturedAt: new Date().toISOString(),
      source: GEO_SOURCE_BROWSER,
    },
    website: "",
    ...overrides,
  };
}

describe("profileSubmitSchema", () => {
  it("acepta un payload válido", () => {
    expect(profileSubmitSchema.safeParse(validPayload()).success).toBe(true);
  });

  it("rechaza un agencyId no-UUID", () => {
    expect(
      profileSubmitSchema.safeParse(validPayload({ agencyId: "abc" })).success,
    ).toBe(false);
  });

  it("rechaza campos obligatorios vacíos", () => {
    expect(
      profileSubmitSchema.safeParse(validPayload({ direccion: "  " })).success,
    ).toBe(false);
  });

  it("exige el detalle cuando el tipo es 'Otro'", () => {
    const r = profileSubmitSchema.safeParse(
      validPayload({ tipoEstablecimiento: "Otro" }),
    );
    expect(r.success).toBe(false);
  });

  it("acepta 'Otro' con detalle presente", () => {
    const r = profileSubmitSchema.safeParse(
      validPayload({
        tipoEstablecimiento: "Otro",
        tipoEstablecimientoOtro: "Kiosco",
      }),
    );
    expect(r.success).toBe(true);
  });

  it("rechaza cuando el honeypot está lleno (spam)", () => {
    expect(
      profileSubmitSchema.safeParse(validPayload({ website: "http://spam" }))
        .success,
    ).toBe(false);
  });

  it("rechaza un tipo de establecimiento no permitido", () => {
    expect(
      profileSubmitSchema.safeParse(
        validPayload({ tipoEstablecimiento: "Nave espacial" }),
      ).success,
    ).toBe(false);
  });
});
