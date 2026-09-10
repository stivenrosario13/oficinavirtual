import Link from "next/link";
import { notFound } from "next/navigation";
import { getAgencyWithProfile, getAuditTrail } from "@/server/profiles";
import EditProfileForm from "@/components/admin/EditProfileForm";
import AdminImageManager from "@/components/admin/AdminImageManager";
import AuditTrail from "@/components/admin/AuditTrail";
import {
  statusLabel,
  statusClass,
  syncLabel,
  syncClass,
  mapsUrl,
  fmtDateTime,
} from "@/lib/display";

export const dynamic = "force-dynamic";

type Params = Promise<{ id: string }>;

export default async function EditRecordPage({
  params,
}: {
  params: Params;
}) {
  const { id } = await params;
  const agency = await getAgencyWithProfile(id);
  if (!agency) notFound();

  const profile = agency.profile;
  const auditIds = [agency.id, ...(profile ? [profile.id] : [])];
  const audit = await getAuditTrail(auditIds);

  return (
    <div className="flex flex-col gap-5">
      <Link href="/admin/records" className="text-sm text-brand-600">
        ← Volver a registros
      </Link>

      <div className="card">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <h1 className="text-lg font-bold text-ink">{agency.terminal}</h1>
            <p className="text-sm text-muted">
              Código {agency.codigo} · Grupo {agency.grupo}
            </p>
          </div>
          <span className={`badge ${statusClass(agency.status)}`}>
            {statusLabel(agency.status)}
          </span>
        </div>
      </div>

      {profile ? (
        <>
          <div className="card">
            <h2 className="mb-4 font-bold text-ink">Editar información</h2>
            <EditProfileForm
              agencyId={agency.id}
              profileId={profile.id}
              initial={{
                direccion: profile.direccion,
                sector: profile.sector,
                municipio: profile.municipio,
                provincia: profile.provincia,
                tipoEstablecimiento: profile.tipo_establecimiento,
                tipoEstablecimientoOtro:
                  profile.tipo_establecimiento_otro ?? "",
                latitude: profile.latitude,
                longitude: profile.longitude,
                accuracyMeters: profile.accuracy_meters,
              }}
            />
          </div>

          <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
            <div className="card">
              <h2 className="mb-3 font-bold text-ink">Ubicación y sincronización</h2>
              <dl className="grid grid-cols-2 gap-y-2 text-sm">
                <dt className="text-muted">Coordenadas</dt>
                <dd className="text-right">
                  <a
                    className="text-brand-600 hover:underline"
                    href={mapsUrl(profile.latitude, profile.longitude)}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    {profile.latitude.toFixed(6)},{" "}
                    {profile.longitude.toFixed(6)}
                  </a>
                </dd>
                <dt className="text-muted">Precisión</dt>
                <dd className="text-right text-ink">
                  ± {Math.round(profile.accuracy_meters)} m
                </dd>
                <dt className="text-muted">Capturada</dt>
                <dd className="text-right text-ink">
                  {fmtDateTime(profile.location_captured_at)}
                </dd>
                <dt className="text-muted">Fuente</dt>
                <dd className="text-right text-ink">
                  {profile.location_source}
                </dd>
                <dt className="text-muted">Sincronización</dt>
                <dd className="text-right">
                  <span
                    className={`badge ${syncClass(
                      profile.google_sheet_sync_status,
                    )}`}
                  >
                    {syncLabel(profile.google_sheet_sync_status)}
                  </span>
                </dd>
                <dt className="text-muted">Fila en Sheet</dt>
                <dd className="truncate text-right text-xs text-muted">
                  {profile.google_sheet_row_reference ?? "—"}
                </dd>
              </dl>
            </div>

            <div className="card">
              <h2 className="mb-3 font-bold text-ink">Imagen de la agencia</h2>
              <AdminImageManager
                agencyId={agency.id}
                hasImage={Boolean(profile.photo_drive_file_id)}
              />
            </div>
          </div>
        </>
      ) : (
        <div className="card">
          <p className="text-sm text-muted">
            Esta agencia aún no tiene un perfil registrado (estado{" "}
            {statusLabel(agency.status)}).
          </p>
        </div>
      )}

      <div className="card">
        <h2 className="mb-3 font-bold text-ink">Historial de auditoría</h2>
        <AuditTrail entries={audit} />
      </div>
    </div>
  );
}
