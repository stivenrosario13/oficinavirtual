import { fmtDateTime } from "@/lib/display";
import type { AuditLogRow } from "@/lib/types";

const ACTION_LABELS: Record<string, string> = {
  PROFILE_CREATED: "Perfil creado",
  PROFILE_UPDATED: "Perfil editado",
  LOCATION_UPDATED: "Ubicación actualizada",
  SYNC_RETRY: "Reintento de sincronización",
  PHOTO_UPLOADED: "Imagen cargada",
  PHOTO_REPLACED: "Imagen reemplazada",
  PROFILE_DELETED: "Perfil eliminado",
  STATUS_CHANGED: "Estado cambiado",
};

export default function AuditTrail({ entries }: { entries: AuditLogRow[] }) {
  if (entries.length === 0) {
    return (
      <p className="text-sm text-muted">Sin eventos de auditoría todavía.</p>
    );
  }
  return (
    <ol className="flex flex-col gap-3">
      {entries.map((e) => (
        <li
          key={e.id}
          className="border-l-2 border-line pl-3 text-sm"
        >
          <div className="flex flex-wrap items-center justify-between gap-1">
            <span className="font-semibold text-ink">
              {ACTION_LABELS[e.action] ?? e.action}
            </span>
            <span className="text-xs text-muted">
              {fmtDateTime(e.created_at)}
            </span>
          </div>
          <p className="text-xs text-muted">
            {e.actor_email ? `Por ${e.actor_email}` : "Registro público"}
          </p>
        </li>
      ))}
    </ol>
  );
}
