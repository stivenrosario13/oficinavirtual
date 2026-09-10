import { AGENCY_STATUS, SYNC_STATUS } from "./constants";

export function statusLabel(status: string): string {
  switch (status) {
    case AGENCY_STATUS.COMPLETED:
      return "Completada";
    case AGENCY_STATUS.PENDING:
      return "Pendiente";
    case AGENCY_STATUS.REVIEW_REQUIRED:
      return "En revisión";
    default:
      return status;
  }
}

export function statusClass(status: string): string {
  switch (status) {
    case AGENCY_STATUS.COMPLETED:
      return "bg-ok-bg text-ok";
    case AGENCY_STATUS.PENDING:
      return "bg-warn-bg text-warn";
    default:
      return "bg-danger-bg text-danger";
  }
}

export function syncLabel(status: string): string {
  switch (status) {
    case SYNC_STATUS.SYNCED:
      return "Sincronizado";
    case SYNC_STATUS.PENDING:
      return "Pendiente";
    case SYNC_STATUS.FAILED:
      return "Fallida";
    default:
      return status;
  }
}

export function syncClass(status: string): string {
  switch (status) {
    case SYNC_STATUS.SYNCED:
      return "bg-ok-bg text-ok";
    case SYNC_STATUS.PENDING:
      return "bg-warn-bg text-warn";
    default:
      return "bg-danger-bg text-danger";
  }
}

export function mapsUrl(lat: number, lng: number): string {
  return `https://www.google.com/maps?q=${lat},${lng}`;
}

export function fmtDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleString("es-DO", {
    dateStyle: "short",
    timeStyle: "short",
  });
}
