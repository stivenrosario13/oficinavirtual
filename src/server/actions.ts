"use server";

import { revalidatePath } from "next/cache";
import { requireAdmin, NotAuthorizedError } from "@/server/admin-guard";
import {
  adminUpdateProfile,
  syncProfileSafe,
  deleteProfileAudited,
} from "@/server/profiles";
import { adminProfileUpdateSchema, formatZodError } from "@/lib/validation";
import { validateGeolocation } from "@/lib/geo";
import { GEO_SOURCE_ADMIN, GEO_SOURCE_BROWSER } from "@/lib/constants";

export interface ActionResult {
  ok: boolean;
  error?: string;
  status?: string;
}

function handleError(err: unknown): ActionResult {
  if (err instanceof NotAuthorizedError) {
    return { ok: false, error: err.message };
  }
  console.error("Admin action error:", err);
  return { ok: false, error: "Ocurrió un error. Inténtalo de nuevo." };
}

/** Reintenta la sincronización de un perfil con Google Sheets. */
export async function retrySyncAction(profileId: string): Promise<ActionResult> {
  try {
    const actor = await requireAdmin();
    const status = await syncProfileSafe(profileId, {
      actor,
      auditAsRetry: true,
    });
    revalidatePath("/admin/records");
    revalidatePath(`/admin`);
    return { ok: status === "SYNCED", status, error: status === "SYNCED" ? undefined : "La sincronización falló nuevamente." };
  } catch (err) {
    return handleError(err);
  }
}

/** Edición administrativa del perfil (actualiza el mismo, nunca crea otro). */
export async function updateProfileAction(input: {
  profileId: string;
  agencyId: string;
  direccion: string;
  sector: string;
  municipio: string;
  provincia: string;
  tipoEstablecimiento: string;
  tipoEstablecimientoOtro?: string;
  geolocation?: {
    latitude: number;
    longitude: number;
    accuracyMeters: number;
    capturedAt: string;
    source: string;
  };
}): Promise<ActionResult> {
  try {
    const actor = await requireAdmin();

    const parsed = adminProfileUpdateSchema.safeParse({
      direccion: input.direccion,
      sector: input.sector,
      municipio: input.municipio,
      provincia: input.provincia,
      tipoEstablecimiento: input.tipoEstablecimiento,
      tipoEstablecimientoOtro: input.tipoEstablecimientoOtro,
      geolocation: input.geolocation,
    });
    if (!parsed.success) {
      return { ok: false, error: formatZodError(parsed.error).join(" ") };
    }

    // Validaciones de geolocalización equivalentes a las del formulario público.
    if (parsed.data.geolocation) {
      const src = parsed.data.geolocation.source;
      if (src !== GEO_SOURCE_ADMIN && src !== GEO_SOURCE_BROWSER) {
        return { ok: false, error: "Fuente de ubicación inválida." };
      }
      const geo = validateGeolocation(parsed.data.geolocation, {
        now: Date.now(),
        checkAge: true,
      });
      if (!geo.ok) {
        return { ok: false, error: geo.errors.join(" ") };
      }
    }

    await adminUpdateProfile(
      input.profileId,
      {
        direccion: parsed.data.direccion,
        sector: parsed.data.sector,
        municipio: parsed.data.municipio,
        provincia: parsed.data.provincia,
        tipoEstablecimiento: parsed.data.tipoEstablecimiento,
        tipoEstablecimientoOtro: parsed.data.tipoEstablecimientoOtro,
        geolocation: parsed.data.geolocation,
      },
      actor,
    );

    revalidatePath(`/admin/records/${input.agencyId}`);
    revalidatePath("/admin/records");
    revalidatePath("/admin");
    return { ok: true };
  } catch (err) {
    return handleError(err);
  }
}

/** Eliminación explícita y auditada de un perfil. La agencia vuelve a PENDING. */
export async function deleteProfileAction(input: {
  profileId: string;
  agencyId: string;
}): Promise<ActionResult> {
  try {
    const actor = await requireAdmin();
    await deleteProfileAudited(input.profileId, actor);
    revalidatePath("/admin/records");
    revalidatePath("/admin");
    return { ok: true };
  } catch (err) {
    return handleError(err);
  }
}
