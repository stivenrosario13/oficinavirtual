import {
  ALLOWED_IMAGE_MIME,
  ALLOWED_IMAGE_EXT,
  MAX_IMAGE_SIZE_BYTES,
  MAX_IMAGE_SIZE_MB,
} from "./constants";

export interface ImageValidation {
  ok: boolean;
  error?: string;
}

/** Valida tipo MIME y tamaño de una imagen. Pura (cliente y servidor). */
export function validateImageMeta(
  mimeType: string,
  sizeBytes: number,
): ImageValidation {
  const mime = (mimeType || "").toLowerCase();
  if (!(ALLOWED_IMAGE_MIME as readonly string[]).includes(mime)) {
    return {
      ok: false,
      error: "Formato no permitido. Usa JPG, JPEG, PNG o WEBP.",
    };
  }
  if (!Number.isFinite(sizeBytes) || sizeBytes <= 0) {
    return { ok: false, error: "El archivo está vacío o es inválido." };
  }
  if (sizeBytes > MAX_IMAGE_SIZE_BYTES) {
    return {
      ok: false,
      error: `La imagen supera el máximo de ${MAX_IMAGE_SIZE_MB} MB.`,
    };
  }
  return { ok: true };
}

const MIME_TO_EXT: Record<string, string> = {
  "image/jpeg": "jpg",
  "image/jpg": "jpg",
  "image/png": "png",
  "image/webp": "webp",
};

/** Extensión segura a partir del tipo MIME. */
export function extForMime(mimeType: string): string {
  return MIME_TO_EXT[(mimeType || "").toLowerCase()] ?? "bin";
}

export function isAllowedExt(ext: string): boolean {
  return (ALLOWED_IMAGE_EXT as readonly string[]).includes(ext.toLowerCase());
}
