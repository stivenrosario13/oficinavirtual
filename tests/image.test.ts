import { describe, it, expect } from "vitest";
import { validateImageMeta, extForMime } from "@/lib/image";
import { MAX_IMAGE_SIZE_BYTES } from "@/lib/constants";

describe("validateImageMeta", () => {
  it("acepta JPG/PNG/WEBP dentro del límite", () => {
    expect(validateImageMeta("image/jpeg", 1000).ok).toBe(true);
    expect(validateImageMeta("image/png", 1000).ok).toBe(true);
    expect(validateImageMeta("image/webp", 1000).ok).toBe(true);
  });

  it("rechaza formatos no permitidos", () => {
    expect(validateImageMeta("image/gif", 1000).ok).toBe(false);
    expect(validateImageMeta("application/pdf", 1000).ok).toBe(false);
  });

  it("rechaza archivos vacíos", () => {
    expect(validateImageMeta("image/jpeg", 0).ok).toBe(false);
  });

  it("rechaza imágenes por encima del tamaño máximo", () => {
    expect(validateImageMeta("image/jpeg", MAX_IMAGE_SIZE_BYTES + 1).ok).toBe(
      false,
    );
  });
});

describe("extForMime", () => {
  it("mapea tipos a extensiones seguras", () => {
    expect(extForMime("image/jpeg")).toBe("jpg");
    expect(extForMime("image/png")).toBe("png");
    expect(extForMime("image/webp")).toBe("webp");
    expect(extForMime("text/plain")).toBe("bin");
  });
});
