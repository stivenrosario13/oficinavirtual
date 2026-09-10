import { describe, it, expect } from "vitest";
import {
  normalizeText,
  normalizeCode,
  foldKey,
  buildSourceKey,
  isBlank,
} from "@/lib/normalize";

describe("normalizeText", () => {
  it("recorta y colapsa espacios internos", () => {
    expect(normalizeText("  hola   mundo  ")).toBe("hola mundo");
  });
  it("devuelve cadena vacía para null/undefined", () => {
    expect(normalizeText(null)).toBe("");
    expect(normalizeText(undefined)).toBe("");
  });
});

describe("normalizeCode", () => {
  it("preserva ceros a la izquierda (no convierte a número)", () => {
    expect(normalizeCode("00123")).toBe("00123");
    expect(normalizeCode("  00700  ")).toBe("00700");
  });
});

describe("foldKey", () => {
  it("es insensible a mayúsculas y acentos", () => {
    expect(foldKey("Ozáma")).toBe(foldKey("OZAMA"));
    expect(foldKey(" metro m ")).toBe("METRO M");
  });
});

describe("buildSourceKey", () => {
  it("es determinista para las mismas entradas", () => {
    expect(buildSourceKey("00123", "T-1")).toBe(buildSourceKey("00123", "T-1"));
  });
  it("normaliza mayúsculas/espacios de forma estable", () => {
    expect(buildSourceKey("00123", "  t-1 ")).toBe(
      buildSourceKey("00123", "T-1"),
    );
  });
  it("distingue códigos/terminales diferentes", () => {
    expect(buildSourceKey("1", "A")).not.toBe(buildSourceKey("1", "B"));
    expect(buildSourceKey("1", "A")).not.toBe(buildSourceKey("2", "A"));
  });
});

describe("isBlank", () => {
  it("detecta valores vacíos tras normalizar", () => {
    expect(isBlank("   ")).toBe(true);
    expect(isBlank("x")).toBe(false);
  });
});
