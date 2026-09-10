/**
 * Validación de geolocalización. Función pura reutilizada por el cliente
 * (feedback en vivo) y por el servidor (revalidación al enviar).
 */
import { MAX_GPS_ACCURACY_METERS } from "./constants";

export interface GeoInput {
  latitude: number;
  longitude: number;
  accuracyMeters: number;
  /** ISO 8601 con zona horaria, p.ej. new Date().toISOString(). */
  capturedAt: string;
  source: string;
}

export interface GeoValidationResult {
  ok: boolean;
  errors: string[];
}

const EPSILON = 1e-9;

/**
 * Valida una captura de geolocalización.
 * @param input datos capturados
 * @param opts.now momento de referencia (ms). Por defecto Date.now().
 *        Se usa únicamente para detectar fechas incorrectas en el futuro.
 * @param opts.checkAge nombre conservado por compatibilidad. Si es false,
 *        omite la comprobación del reloj; nunca limita la antigüedad.
 */
export function validateGeolocation(
  input: Partial<GeoInput> | null | undefined,
  opts: { now?: number; checkAge?: boolean } = {},
): GeoValidationResult {
  const errors: string[] = [];
  const now = opts.now ?? Date.now();
  const checkAge = opts.checkAge ?? true;

  if (!input) {
    return { ok: false, errors: ["No se ha capturado la ubicación."] };
  }

  const { latitude, longitude, accuracyMeters, capturedAt } = input;

  // Latitud / longitud numéricas y finitas.
  const latOk = typeof latitude === "number" && Number.isFinite(latitude);
  const lngOk = typeof longitude === "number" && Number.isFinite(longitude);

  if (!latOk || !lngOk) {
    errors.push("Latitud y longitud deben ser números válidos.");
  } else {
    if (latitude < -90 || latitude > 90) {
      errors.push("La latitud debe estar entre -90 y 90.");
    }
    if (longitude < -180 || longitude > 180) {
      errors.push("La longitud debe estar entre -180 y 180.");
    }
    if (Math.abs(latitude) < EPSILON && Math.abs(longitude) < EPSILON) {
      errors.push("Las coordenadas 0,0 no son válidas.");
    }
  }

  // Precisión.
  if (
    typeof accuracyMeters !== "number" ||
    !Number.isFinite(accuracyMeters) ||
    accuracyMeters <= 0
  ) {
    errors.push("La precisión GPS no es válida.");
  } else if (accuracyMeters > MAX_GPS_ACCURACY_METERS) {
    errors.push(
      `La precisión es de ${Math.round(
        accuracyMeters,
      )} m. Se requiere ${MAX_GPS_ACCURACY_METERS} m o menos. Intenta de nuevo al aire libre.`,
    );
  }

  // La marca de tiempo debe ser válida, pero nunca caduca por antigüedad.
  const capturedMs = capturedAt ? Date.parse(capturedAt) : NaN;
  if (!capturedAt || Number.isNaN(capturedMs)) {
    errors.push("La fecha y hora de captura no es válida.");
  } else if (checkAge) {
    const ageSeconds = (now - capturedMs) / 1000;
    // Tolerancia de reloj de 2 minutos hacia el futuro.
    if (ageSeconds < -120) {
      errors.push("La fecha de captura está en el futuro. Vuelve a capturarla.");
    }
  }

  return { ok: errors.length === 0, errors };
}
