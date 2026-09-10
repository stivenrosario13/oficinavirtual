import Link from "next/link";
import { getDashboardStats } from "@/server/agencies";
import ProgressBar from "@/components/admin/ProgressBar";

export const dynamic = "force-dynamic";

function StatCard({
  label,
  value,
  tone = "default",
}: {
  label: string;
  value: number | string;
  tone?: "default" | "ok" | "warn" | "danger";
}) {
  const toneClass =
    tone === "ok"
      ? "text-ok"
      : tone === "warn"
        ? "text-warn"
        : tone === "danger"
          ? "text-danger"
          : "text-ink";
  return (
    <div className="card">
      <p className="text-xs font-medium uppercase tracking-wide text-muted">
        {label}
      </p>
      <p className={`mt-1 text-2xl font-bold ${toneClass}`}>{value}</p>
    </div>
  );
}

export default async function DashboardPage() {
  let stats;
  try {
    stats = await getDashboardStats();
  } catch (err) {
    console.error(err);
    return (
      <div className="card">
        <h1 className="text-lg font-bold text-ink">Dashboard</h1>
        <p className="mt-2 rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
          No se pudo cargar la información. Verifica la configuración de Supabase
          y que las migraciones estén aplicadas.
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-bold text-ink">Dashboard</h1>
        <p className="text-sm text-muted">
          Progreso global del levantamiento de agencias.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Agencias esperadas" value={stats.totalExpected} />
        <StatCard label="Completadas" value={stats.completed} tone="ok" />
        <StatCard label="Pendientes" value={stats.pending} tone="warn" />
        <StatCard label="% avance global" value={`${stats.percent}%`} />
        <StatCard
          label="Errores de sincronización"
          value={stats.syncErrors}
          tone={stats.syncErrors > 0 ? "danger" : "default"}
        />
        <StatCard
          label="Ubicación en revisión"
          value={stats.reviewRequired}
          tone={stats.reviewRequired > 0 ? "warn" : "default"}
        />
      </div>

      <div className="card overflow-hidden p-0">
        <div className="flex items-center justify-between border-b border-line px-4 py-3">
          <h2 className="font-bold text-ink">Avance por grupos</h2>
          <Link
            href="/admin/records"
            className="text-sm text-brand-600 hover:underline"
          >
            Ver registros →
          </Link>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[560px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-xs uppercase text-muted">
                <th className="px-4 py-2 font-medium">Grupo</th>
                <th className="px-4 py-2 text-right font-medium">Esperadas</th>
                <th className="px-4 py-2 text-right font-medium">Completadas</th>
                <th className="px-4 py-2 text-right font-medium">Pendientes</th>
                <th className="px-4 py-2 font-medium">% avance</th>
              </tr>
            </thead>
            <tbody>
              {stats.groups.map((g) => (
                <tr key={g.grupo} className="border-b border-line last:border-0">
                  <td className="px-4 py-2.5 font-medium text-ink">
                    {g.grupo}
                  </td>
                  <td className="px-4 py-2.5 text-right text-muted">
                    {g.esperadas}
                  </td>
                  <td className="px-4 py-2.5 text-right text-ok">
                    {g.completadas}
                  </td>
                  <td className="px-4 py-2.5 text-right text-warn">
                    {g.pendientes}
                  </td>
                  <td className="px-4 py-2.5">
                    <div className="w-40">
                      <ProgressBar percent={g.porcentaje} />
                    </div>
                  </td>
                </tr>
              ))}
              {stats.groups.length === 0 && (
                <tr>
                  <td
                    colSpan={5}
                    className="px-4 py-8 text-center text-muted"
                  >
                    No hay agencias importadas todavía. Ejecuta el script de
                    importación.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
