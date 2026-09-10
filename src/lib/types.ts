import type { AgencyStatus, SyncStatus } from "./constants";

/** Fila de la tabla `agencies`. */
export interface AgencyRow {
  id: string;
  source_key: string;
  codigo: string;
  terminal: string;
  grupo: string;
  status: AgencyStatus;
  created_at: string;
  updated_at: string;
}

/** Fila de la tabla `agency_profiles`. */
export interface AgencyProfileRow {
  id: string;
  agency_id: string;
  direccion: string;
  sector: string;
  municipio: string;
  provincia: string;
  tipo_establecimiento: string;
  tipo_establecimiento_otro: string | null;
  latitude: number;
  longitude: number;
  accuracy_meters: number;
  location_captured_at: string;
  location_source: string;
  photo_drive_file_id: string | null;
  photo_url: string | null;
  submitted_at: string;
  updated_at: string;
  submitted_by_context: Record<string, unknown> | null;
  google_sheet_sync_status: SyncStatus;
  google_sheet_row_reference: string | null;
}

/** Fila de la tabla `audit_log`. */
export interface AuditLogRow {
  id: string;
  entity_type: string;
  entity_id: string;
  action: string;
  before_data: Record<string, unknown> | null;
  after_data: Record<string, unknown> | null;
  actor_email: string | null;
  created_at: string;
}

/** Vista combinada agencia + perfil (join) usada en el panel admin. */
export interface AgencyWithProfile extends AgencyRow {
  profile: AgencyProfileRow | null;
}

/** Datos que el cliente envía en el formulario público. */
export interface ProfileSubmitPayload {
  agencyId: string;
  direccion: string;
  sector: string;
  municipio: string;
  provincia: string;
  tipoEstablecimiento: string;
  tipoEstablecimientoOtro?: string;
  geolocation: {
    latitude: number;
    longitude: number;
    accuracyMeters: number;
    capturedAt: string;
    source: string;
  };
  /** Campo honeypot: debe permanecer vacío. */
  website?: string;
}
