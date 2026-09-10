/**
 * Configuración central de políticas de la aplicación.
 *
 * Estos valores son "configurables": este archivo es el único punto de cambio.
 * Se permiten overrides por variable de entorno `NEXT_PUBLIC_*` (no secretas)
 * para poder ajustarlos en Vercel sin recompilar el código.
 */

function num(value: string | undefined, fallback: number): number {
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? n : fallback;
}

/** Precisión GPS máxima aceptada, en metros. */
export const MAX_GPS_ACCURACY_METERS = num(
  import.meta.env.VITE_MAX_GPS_ACCURACY_METERS,
  100,
);

/** Tamaño máximo de imagen, en megabytes. */
export const MAX_IMAGE_SIZE_MB = num(
  import.meta.env.VITE_MAX_IMAGE_SIZE_MB,
  5,
);

export const MAX_IMAGE_SIZE_BYTES = Math.round(MAX_IMAGE_SIZE_MB * 1024 * 1024);

/** Tipos MIME de imagen permitidos. */
export const ALLOWED_IMAGE_MIME = [
  "image/jpeg",
  "image/jpg",
  "image/png",
  "image/webp",
] as const;

export const ALLOWED_IMAGE_EXT = ["jpg", "jpeg", "png", "webp"] as const;

/** Fuente de geolocalización para usuarios normales. */
export const GEO_SOURCE_BROWSER = "browser_geolocation" as const;
export const GEO_SOURCE_ADMIN = "admin_manual" as const;

/** Opciones controladas de tipo de establecimiento. La última debe ser "Otro". */
export const ESTABLISHMENT_TYPES = [
  "Colmado",
  "Supermercado",
  "Farmacia",
  "Banca de lotería",
  "Estación de combustible",
  "Tienda / Variedades",
  "Restaurante / Cafetería",
  "Salón / Barbería",
  "Ferretería",
  "Centro de pagos",
  "Otro",
] as const;

export const ESTABLISHMENT_OTRO = "Otro" as const;

/** Estados de una agencia. */
export const AGENCY_STATUS = {
  PENDING: "PENDING",
  COMPLETED: "COMPLETED",
  REVIEW_REQUIRED: "REVIEW_REQUIRED",
} as const;
export type AgencyStatus = (typeof AGENCY_STATUS)[keyof typeof AGENCY_STATUS];

/** Estados de sincronización con Google Sheets. */
export const SYNC_STATUS = {
  PENDING: "PENDING",
  SYNCED: "SYNCED",
  FAILED: "FAILED",
} as const;
export type SyncStatus = (typeof SYNC_STATUS)[keyof typeof SYNC_STATUS];

/** Límites de longitud de texto (defensa básica). */
export const TEXT_MAX = {
  direccion: 300,
  sector: 120,
  municipio: 120,
  provincia: 120,
  tipoOtro: 120,
} as const;

/** Rate limiting del formulario público. */
export const RATE_LIMIT = {
  windowMs: num(import.meta.env.VITE_RATE_LIMIT_WINDOW_SECONDS, 60) * 1000,
  max: num(import.meta.env.VITE_RATE_LIMIT_MAX, 8),
} as const;

/** Nombre de la pestaña de destino en Google Sheets. */
export const SHEET_TAB_NAME = "Agency Profiles";

/** Encabezados de la hoja, en orden. */
export const SHEET_HEADERS = [
  "response_id",
  "agency_id",
  "codigo",
  "terminal",
  "grupo",
  "direccion",
  "sector",
  "municipio",
  "provincia",
  "tipo_establecimiento",
  "tipo_establecimiento_otro",
  "latitude",
  "longitude",
  "accuracy_meters",
  "location_captured_at",
  "submitted_at",
  "photo_url",
  "sync_status",
] as const;
