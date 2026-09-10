import Link from "next/link";
import { listAgenciesAdmin, getAllGroups } from "@/server/profiles";
import RecordFilters from "@/components/admin/RecordFilters";
import SyncRetryButton from "@/components/admin/SyncRetryButton";
import { SYNC_STATUS } from "@/lib/constants";
import {
  statusLabel,
  statusClass,
  syncLabel,
  syncClass,
  mapsUrl,
  fmtDateTime,
} from "@/lib/display";

export const dynamic = "force-dynamic";

const PAGE_SIZE = 50;

type SearchParams = Promise<{
  search?: string;
  grupo?: string;
  status?: string;
  sync?: string;
  page?: string;
}>;

export default async function RecordsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const sp = await searchParams;
  const page = Math.max(1, Number(sp.page ?? "1") || 1);

  let groups: string[] = [];
  let rows: Awaited<ReturnType<typeof listAgenciesAdmin>>["rows"] = [];
  let total = 0;
  let loadError = false;

  try {
    [groups, { rows, total }] = await Promise.all([
      getAllGroups(),
      listAgenciesAdmin({
        search: sp.search,
        grupo: sp.grupo,
        status: sp.status,
        sync: sp.sync,
        limit: PAGE_SIZE,
        offset: (page - 1) * PAGE_SIZE,
      }),
    ]);
  } catch (err) {
    console.error(err);
    loadError = true;
  }

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  function pageHref(p: number): string {
    const qs = new URLSearchParams();
    if (sp.search) qs.set("search", sp.search);
    if (sp.grupo) qs.set("grupo", sp.grupo);
    if (sp.status) qs.set("status", sp.status);
    if (sp.sync) qs.set("sync", sp.sync);
    qs.set("page", String(p));
    return `/admin/records?${qs.toString()}`;
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-end justify-between">
        <div>
          <h1 className="text-xl font-bold text-ink">Registros</h1>
          <p className="text-sm text-muted">
            {total} agencia{total === 1 ? "" : "s"} en el resultado.
          </p>
        </div>
      </div>

      <RecordFilters groups={groups} />

      {loadError ? (
        <div className="card">
          <p className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            No se pudieron cargar los registros. Verifica Supabase y las
            migraciones.
          </p>
        </div>
      ) : (
        <div className="card overflow-hidden p-0">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[1100px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-xs uppercase text-muted">
                  <th className="px-3 py-2 font-medium">Código</th>
                  <th className="px-3 py-2 font-medium">Terminal</th>
                  <th className="px-3 py-2 font-medium">Grupo</th>
                  <th className="px-3 py-2 font-medium">Estado</th>
                  <th className="px-3 py-2 font-medium">Municipio</th>
                  <th className="px-3 py-2 font-medium">Provincia</th>
                  <th className="px-3 py-2 font-medium">Tipo</th>
                  <th className="px-3 py-2 font-medium">Coordenadas</th>
                  <th className="px-3 py-2 font-medium">Precisión</th>
                  <th className="px-3 py-2 font-medium">Captura</th>
                  <th className="px-3 py-2 font-medium">Imagen</th>
                  <th className="px-3 py-2 font-medium">Sync</th>
                  <th className="px-3 py-2 font-medium"></th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => {
                  const p = r.profile;
                  return (
                    <tr
                      key={r.id}
                      className="border-b border-line align-top last:border-0 hover:bg-brand-50/40"
                    >
                      <td className="px-3 py-2.5 font-mono text-xs text-ink">
                        {r.codigo}
                      </td>
                      <td className="max-w-[220px] px-3 py-2.5 text-ink">
                        <span className="line-clamp-2">{r.terminal}</span>
                      </td>
                      <td className="px-3 py-2.5 text-muted">{r.grupo}</td>
                      <td className="px-3 py-2.5">
                        <span className={`badge ${statusClass(r.status)}`}>
                          {statusLabel(r.status)}
                        </span>
                      </td>
                      <td className="px-3 py-2.5 text-muted">
                        {p?.municipio ?? "—"}
                      </td>
                      <td className="px-3 py-2.5 text-muted">
                        {p?.provincia ?? "—"}
                      </td>
                      <td className="px-3 py-2.5 text-muted">
                        {p
                          ? p.tipo_establecimiento === "Otro"
                            ? `Otro: ${p.tipo_establecimiento_otro ?? ""}`
                            : p.tipo_establecimiento
                          : "—"}
                      </td>
                      <td className="px-3 py-2.5">
                        {p ? (
                          <a
                            className="text-brand-600 hover:underline"
                            href={mapsUrl(p.latitude, p.longitude)}
                            target="_blank"
                            rel="noopener noreferrer"
                          >
                            {p.latitude.toFixed(5)}, {p.longitude.toFixed(5)}
                          </a>
                        ) : (
                          "—"
                        )}
                      </td>
                      <td className="px-3 py-2.5 text-muted">
                        {p ? `± ${Math.round(p.accuracy_meters)} m` : "—"}
                      </td>
                      <td className="px-3 py-2.5 text-muted">
                        {fmtDateTime(p?.location_captured_at)}
                      </td>
                      <td className="px-3 py-2.5">
                        {p?.photo_drive_file_id ? (
                          <a
                            className="text-brand-600 hover:underline"
                            href={`/api/admin/agencies/${r.id}/image`}
                            target="_blank"
                            rel="noopener noreferrer"
                          >
                            Ver
                          </a>
                        ) : (
                          "—"
                        )}
                      </td>
                      <td className="px-3 py-2.5">
                        {p ? (
                          <div className="flex flex-col items-start gap-1">
                            <span
                              className={`badge ${syncClass(
                                p.google_sheet_sync_status,
                              )}`}
                            >
                              {syncLabel(p.google_sheet_sync_status)}
                            </span>
                            {p.google_sheet_sync_status !==
                              SYNC_STATUS.SYNCED && (
                              <SyncRetryButton profileId={p.id} compact />
                            )}
                          </div>
                        ) : (
                          "—"
                        )}
                      </td>
                      <td className="px-3 py-2.5">
                        <Link
                          href={`/admin/records/${r.id}`}
                          className="rounded-md border border-line px-2 py-1 text-xs font-medium text-brand-600 hover:bg-brand-50"
                        >
                          {p ? "Editar" : "Ver"}
                        </Link>
                      </td>
                    </tr>
                  );
                })}
                {rows.length === 0 && (
                  <tr>
                    <td
                      colSpan={13}
                      className="px-3 py-10 text-center text-muted"
                    >
                      No se encontraron registros con esos filtros.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-3 text-sm">
          {page > 1 ? (
            <Link href={pageHref(page - 1)} className="text-brand-600">
              ← Anterior
            </Link>
          ) : (
            <span className="text-muted">← Anterior</span>
          )}
          <span className="text-muted">
            Página {page} de {totalPages}
          </span>
          {page < totalPages ? (
            <Link href={pageHref(page + 1)} className="text-brand-600">
              Siguiente →
            </Link>
          ) : (
            <span className="text-muted">Siguiente →</span>
          )}
        </div>
      )}
    </div>
  );
}
