import { z } from "zod";
import {
  ESTABLISHMENT_TYPES,
  ESTABLISHMENT_OTRO,
  TEXT_MAX,
  GEO_SOURCE_BROWSER,
  GEO_SOURCE_ADMIN,
} from "./constants";

/** Cadena obligatoria, recortada y acotada. */
function requiredText(max: number, label: string) {
  return z
    .string({ message: `${label} es obligatorio.` })
    .trim()
    .min(1, `${label} es obligatorio.`)
    .max(max, `${label} es demasiado largo.`);
}

const establishmentValues = ESTABLISHMENT_TYPES as readonly string[];

const tipoEstablecimientoSchema = z
  .string()
  .trim()
  .refine((v) => establishmentValues.includes(v), {
    message: "Tipo de establecimiento no válido.",
  });

/** Estructura de una captura de geolocalización (política aparte, ver geo.ts). */
export const geoInputSchema = z.object({
  latitude: z.number({ message: "Latitud inválida." }),
  longitude: z.number({ message: "Longitud inválida." }),
  accuracyMeters: z.number({ message: "Precisión inválida." }),
  capturedAt: z
    .string()
    .min(1, "Falta la hora de captura.")
    .refine((v) => !Number.isNaN(Date.parse(v)), {
      message: "Hora de captura inválida.",
    }),
  source: z
    .string()
    .refine((v) => v === GEO_SOURCE_BROWSER || v === GEO_SOURCE_ADMIN, {
      message: "Fuente de ubicación inválida.",
    }),
});

/** Campos de perfil comunes (sin geolocalización ni agencyId). */
const profileFields = {
  direccion: requiredText(TEXT_MAX.direccion, "La dirección"),
  sector: requiredText(TEXT_MAX.sector, "El sector"),
  municipio: requiredText(TEXT_MAX.municipio, "El municipio"),
  provincia: requiredText(TEXT_MAX.provincia, "La provincia"),
  tipoEstablecimiento: tipoEstablecimientoSchema,
  tipoEstablecimientoOtro: z
    .string()
    .trim()
    .max(TEXT_MAX.tipoOtro, "El detalle es demasiado largo.")
    .optional(),
};

/** Regla: si el tipo es "Otro", el detalle es obligatorio. */
function requireOtro<T extends z.ZodTypeAny>(schema: T) {
  return schema.superRefine((val: unknown, ctx: z.RefinementCtx) => {
    const v = val as {
      tipoEstablecimiento?: string;
      tipoEstablecimientoOtro?: string;
    };
    if (
      v.tipoEstablecimiento === ESTABLISHMENT_OTRO &&
      (!v.tipoEstablecimientoOtro || v.tipoEstablecimientoOtro.trim() === "")
    ) {
      ctx.addIssue({
        code: "custom",
        path: ["tipoEstablecimientoOtro"],
        message: 'Indica el tipo de establecimiento cuando seleccionas "Otro".',
      });
    }
  });
}

/** Payload del formulario público. */
export const profileSubmitSchema = requireOtro(
  z.object({
    agencyId: z.string().uuid("Agencia inválida."),
    ...profileFields,
    geolocation: geoInputSchema,
    // Honeypot anti-spam: debe permanecer vacío.
    website: z.string().max(0, "Solicitud rechazada.").optional(),
  }),
);

export type ProfileSubmitInput = z.infer<typeof profileSubmitSchema>;

/** Payload de edición administrativa (geolocalización opcional). */
export const adminProfileUpdateSchema = requireOtro(
  z.object({
    ...profileFields,
    geolocation: geoInputSchema.optional(),
  }),
);

export type AdminProfileUpdateInput = z.infer<typeof adminProfileUpdateSchema>;

/** Convierte un error de Zod en una lista de mensajes en español. */
export function formatZodError(error: z.ZodError): string[] {
  return error.issues.map((i) => i.message);
}
