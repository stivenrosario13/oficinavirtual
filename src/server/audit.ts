import "server-only";
import { supabaseAdmin } from "@/lib/supabase/admin";

export type AuditAction =
  | "PROFILE_CREATED"
  | "PROFILE_UPDATED"
  | "LOCATION_UPDATED"
  | "SYNC_RETRY"
  | "PHOTO_UPLOADED"
  | "PHOTO_REPLACED"
  | "PROFILE_DELETED"
  | "STATUS_CHANGED";

export interface AuditInput {
  entityType: "agency" | "agency_profile";
  entityId: string;
  action: AuditAction;
  before?: unknown;
  after?: unknown;
  actorEmail?: string | null;
}

/** Inserta una entrada en audit_log. No lanza si falla (no debe bloquear). */
export async function writeAudit(input: AuditInput): Promise<void> {
  try {
    await supabaseAdmin()
      .from("audit_log")
      .insert({
        entity_type: input.entityType,
        entity_id: input.entityId,
        action: input.action,
        before_data: input.before ?? null,
        after_data: input.after ?? null,
        actor_email: input.actorEmail ?? null,
      });
  } catch (err) {
    console.error("No se pudo escribir en audit_log:", err);
  }
}
