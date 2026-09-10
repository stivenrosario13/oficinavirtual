import "server-only";
import { supabaseAdmin } from "@/lib/supabase/admin";
import { syncProfileToSheet } from "@/lib/google/sheets";
import { writeAudit } from "@/server/audit";
import { AGENCY_STATUS, SYNC_STATUS, type SyncStatus } from "@/lib/constants";
import type {
  AgencyProfileRow,
  AgencyRow,
  AgencyWithProfile,
  AuditLogRow,
} from "@/lib/types";

/** La agencia ya no está PENDING (otra persona la completó). -> 409 */
export class ConflictError extends Error {
  constructor(msg = "Esta agencia ya fue registrada por otra persona.") {
    super(msg);
    this.name = "ConflictError";
  }
}
/** La agencia no existe. -> 404 */
export class AgencyNotFoundError extends Error {
  constructor(msg = "La agencia ya no está disponible.") {
    super(msg);
    this.name = "AgencyNotFoundError";
  }
}

export interface SubmitProfileInput {
  agencyId: string;
  direccion: string;
  sector: string;
  municipio: string;
  provincia: string;
  tipoEstablecimiento: string;
  tipoEstablecimientoOtro?: string | null;
  latitude: number;
  longitude: number;
  accuracyMeters: number;
  locationCapturedAt: string;
  locationSource: string;
  context: Record<string, unknown>;
  actor?: string | null;
}

/**
 * Crea el perfil y marca la agencia como COMPLETED de forma ATÓMICA mediante la
 * función transaccional `submit_agency_profile`. Mapea los conflictos a errores
 * tipados para responder 409/404.
 */
export async function submitProfile(
  input: SubmitProfileInput,
): Promise<AgencyProfileRow> {
  const { data, error } = await supabaseAdmin().rpc("submit_agency_profile", {
    p_agency_id: input.agencyId,
    p_direccion: input.direccion,
    p_sector: input.sector,
    p_municipio: input.municipio,
    p_provincia: input.provincia,
    p_tipo_establecimiento: input.tipoEstablecimiento,
    p_tipo_establecimiento_otro: input.tipoEstablecimientoOtro ?? null,
    p_latitude: input.latitude,
    p_longitude: input.longitude,
    p_accuracy_meters: input.accuracyMeters,
    p_location_captured_at: input.locationCapturedAt,
    p_location_source: input.locationSource,
    p_context: input.context,
    p_actor: input.actor ?? null,
  });

  if (error) {
    const msg = error.message ?? "";
    if (msg.includes("AGENCY_NOT_PENDING") || error.code === "23505") {
      throw new ConflictError();
    }
    if (msg.includes("AGENCY_NOT_FOUND")) {
      throw new AgencyNotFoundError();
    }
    throw error;
  }

  // Una función que devuelve una fila compuesta llega como objeto (o [objeto]).
  const profile = (Array.isArray(data) ? data[0] : data) as AgencyProfileRow;
  return profile;
}

async function loadProfileAndAgency(
  profileId: string,
): Promise<{ profile: AgencyProfileRow; agency: AgencyRow }> {
  const supabase = supabaseAdmin();
  const { data: profile, error: pErr } = await supabase
    .from("agency_profiles")
    .select("*")
    .eq("id", profileId)
    .single();
  if (pErr) throw pErr;

  const { data: agency, error: aErr } = await supabase
    .from("agencies")
    .select("*")
    .eq("id", (profile as AgencyProfileRow).agency_id)
    .single();
  if (aErr) throw aErr;

  return { profile: profile as AgencyProfileRow, agency: agency as AgencyRow };
}

/**
 * Sincroniza (o resincroniza) un perfil con Google Sheets. Nunca lanza: si
 * falla, marca FAILED y conserva el dato en Supabase.
 */
export async function syncProfileSafe(
  profileId: string,
  opts: { actor?: string | null; auditAsRetry?: boolean } = {},
): Promise<SyncStatus> {
  const supabase = supabaseAdmin();
  try {
    const { profile, agency } = await loadProfileAndAgency(profileId);
    const { rowReference } = await syncProfileToSheet({
      profile,
      agency: {
        codigo: agency.codigo,
        terminal: agency.terminal,
        grupo: agency.grupo,
      },
    });
    await supabase
      .from("agency_profiles")
      .update({
        google_sheet_sync_status: SYNC_STATUS.SYNCED,
        google_sheet_row_reference: rowReference,
      })
      .eq("id", profileId);

    if (opts.auditAsRetry) {
      await writeAudit({
        entityType: "agency_profile",
        entityId: profileId,
        action: "SYNC_RETRY",
        after: { google_sheet_sync_status: SYNC_STATUS.SYNCED, rowReference },
        actorEmail: opts.actor ?? null,
      });
    }
    return SYNC_STATUS.SYNCED;
  } catch (err) {
    console.error(`Fallo al sincronizar perfil ${profileId} con Sheets:`, err);
    await supabase
      .from("agency_profiles")
      .update({ google_sheet_sync_status: SYNC_STATUS.FAILED })
      .eq("id", profileId);

    if (opts.auditAsRetry) {
      await writeAudit({
        entityType: "agency_profile",
        entityId: profileId,
        action: "SYNC_RETRY",
        after: {
          google_sheet_sync_status: SYNC_STATUS.FAILED,
          error: String((err as Error)?.message ?? err),
        },
        actorEmail: opts.actor ?? null,
      });
    }
    return SYNC_STATUS.FAILED;
  }
}

export interface AdminUpdateInput {
  direccion: string;
  sector: string;
  municipio: string;
  provincia: string;
  tipoEstablecimiento: string;
  tipoEstablecimientoOtro?: string | null;
  geolocation?: {
    latitude: number;
    longitude: number;
    accuracyMeters: number;
    capturedAt: string;
    source: string;
  };
}

/**
 * Edición administrativa: actualiza el MISMO perfil (nunca crea uno nuevo),
 * registra auditoría con before/after y resincroniza con Sheets.
 */
export async function adminUpdateProfile(
  profileId: string,
  input: AdminUpdateInput,
  actor: string,
): Promise<AgencyProfileRow> {
  const supabase = supabaseAdmin();

  const { data: before, error: bErr } = await supabase
    .from("agency_profiles")
    .select("*")
    .eq("id", profileId)
    .single();
  if (bErr) throw bErr;

  const patch: Record<string, unknown> = {
    direccion: input.direccion,
    sector: input.sector,
    municipio: input.municipio,
    provincia: input.provincia,
    tipo_establecimiento: input.tipoEstablecimiento,
    tipo_establecimiento_otro: input.tipoEstablecimientoOtro?.trim()
      ? input.tipoEstablecimientoOtro.trim()
      : null,
  };

  const locationChanged = Boolean(input.geolocation);
  if (input.geolocation) {
    patch.latitude = input.geolocation.latitude;
    patch.longitude = input.geolocation.longitude;
    patch.accuracy_meters = input.geolocation.accuracyMeters;
    patch.location_captured_at = input.geolocation.capturedAt;
    patch.location_source = input.geolocation.source;
  }

  const { data: after, error: uErr } = await supabase
    .from("agency_profiles")
    .update(patch)
    .eq("id", profileId)
    .select("*")
    .single();
  if (uErr) throw uErr;

  await writeAudit({
    entityType: "agency_profile",
    entityId: profileId,
    action: locationChanged ? "LOCATION_UPDATED" : "PROFILE_UPDATED",
    before: before as AgencyProfileRow,
    after: after as AgencyProfileRow,
    actorEmail: actor,
  });

  // Resincroniza la misma fila del Sheet.
  await syncProfileSafe(profileId);

  return after as AgencyProfileRow;
}

/** Asocia (o reemplaza) la imagen del perfil y resincroniza. */
export async function attachProfilePhoto(
  profileId: string,
  photo: { fileId: string; url: string | null },
  opts: { actor?: string | null; replaced?: boolean } = {},
): Promise<void> {
  const supabase = supabaseAdmin();

  const { data: before } = await supabase
    .from("agency_profiles")
    .select("photo_drive_file_id, photo_url")
    .eq("id", profileId)
    .single();

  const { error } = await supabase
    .from("agency_profiles")
    .update({
      photo_drive_file_id: photo.fileId,
      photo_url: photo.url,
    })
    .eq("id", profileId);
  if (error) throw error;

  await writeAudit({
    entityType: "agency_profile",
    entityId: profileId,
    action: opts.replaced ? "PHOTO_REPLACED" : "PHOTO_UPLOADED",
    before: (before as Record<string, unknown>) ?? null,
    after: { photo_drive_file_id: photo.fileId, photo_url: photo.url },
    actorEmail: opts.actor ?? null,
  });

  await syncProfileSafe(profileId);
}

export interface AdminListFilters {
  search?: string;
  grupo?: string;
  status?: string;
  sync?: string;
  limit?: number;
  offset?: number;
}

export interface AdminListResult {
  rows: AgencyWithProfile[];
  total: number;
}

function pickProfile(raw: unknown): AgencyProfileRow | null {
  const p = (raw as { agency_profiles?: unknown }).agency_profiles;
  if (!p) return null;
  if (Array.isArray(p)) return (p[0] as AgencyProfileRow) ?? null;
  return p as AgencyProfileRow;
}

/** Listado administrativo con búsqueda y filtros (código, terminal, grupo…). */
export async function listAgenciesAdmin(
  filters: AdminListFilters,
): Promise<AdminListResult> {
  const supabase = supabaseAdmin();
  const limit = Math.min(Math.max(filters.limit ?? 50, 1), 200);
  const offset = Math.max(filters.offset ?? 0, 0);

  const useInner = Boolean(filters.sync);
  const embed = useInner ? "agency_profiles!inner(*)" : "agency_profiles(*)";

  let query = supabase
    .from("agencies")
    .select(`*, ${embed}`, { count: "exact" });

  if (filters.grupo) query = query.eq("grupo", filters.grupo);
  if (filters.status) query = query.eq("status", filters.status);
  if (filters.sync) {
    query = query.eq("agency_profiles.google_sheet_sync_status", filters.sync);
  }
  if (filters.search) {
    const s = filters.search.replace(/[%,]/g, "").trim();
    if (s) {
      query = query.or(
        `codigo.ilike.%${s}%,terminal.ilike.%${s}%,grupo.ilike.%${s}%`,
      );
    }
  }

  query = query
    .order("grupo", { ascending: true })
    .order("terminal", { ascending: true })
    .range(offset, offset + limit - 1);

  const { data, count, error } = await query;
  if (error) throw error;

  const rows: AgencyWithProfile[] = (data ?? []).map((raw) => {
    const r = raw as AgencyRow;
    return { ...r, profile: pickProfile(raw) };
  });

  return { rows, total: count ?? rows.length };
}

/** Lista de grupos existentes (para el filtro del panel). */
export async function getAllGroups(): Promise<string[]> {
  const { data, error } = await supabaseAdmin()
    .from("agencies")
    .select("grupo");
  if (error) throw error;
  const set = new Set<string>();
  for (const r of data ?? []) set.add((r as { grupo: string }).grupo);
  return [...set].sort((a, b) => a.localeCompare(b, "es"));
}

/** Agencia + perfil por id de agencia (para la página de edición). */
export async function getAgencyWithProfile(
  agencyId: string,
): Promise<AgencyWithProfile | null> {
  const { data, error } = await supabaseAdmin()
    .from("agencies")
    .select("*, agency_profiles(*)")
    .eq("id", agencyId)
    .maybeSingle();
  if (error) throw error;
  if (!data) return null;
  const r = data as AgencyRow;
  return { ...r, profile: pickProfile(data) };
}

/** Historial de auditoría de una agencia y su perfil. */
export async function getAuditTrail(
  entityIds: string[],
): Promise<AuditLogRow[]> {
  const { data, error } = await supabaseAdmin()
    .from("audit_log")
    .select("*")
    .in("entity_id", entityIds)
    .order("created_at", { ascending: false })
    .limit(200);
  if (error) throw error;
  return (data ?? []) as AuditLogRow[];
}

/**
 * Eliminación explícita y auditada de un perfil. La agencia vuelve a PENDING
 * para poder recapturarse. No se elimina automáticamente sin esta acción.
 */
export async function deleteProfileAudited(
  profileId: string,
  actor: string,
): Promise<void> {
  const supabase = supabaseAdmin();

  const { data: before, error: bErr } = await supabase
    .from("agency_profiles")
    .select("*")
    .eq("id", profileId)
    .single();
  if (bErr) throw bErr;
  const profile = before as AgencyProfileRow;

  const { error: dErr } = await supabase
    .from("agency_profiles")
    .delete()
    .eq("id", profileId);
  if (dErr) throw dErr;

  await supabase
    .from("agencies")
    .update({ status: AGENCY_STATUS.PENDING })
    .eq("id", profile.agency_id);

  await writeAudit({
    entityType: "agency_profile",
    entityId: profileId,
    action: "PROFILE_DELETED",
    before: profile,
    after: null,
    actorEmail: actor,
  });
  await writeAudit({
    entityType: "agency",
    entityId: profile.agency_id,
    action: "STATUS_CHANGED",
    after: { status: AGENCY_STATUS.PENDING, reason: "profile_deleted" },
    actorEmail: actor,
  });
}
