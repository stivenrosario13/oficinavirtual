import { useEffect, useState } from "react";
import type { LucideIcon } from "lucide-react";
import { ArrowRight, Bell, Clock3, MapPin } from "lucide-react";
import { apiFetch } from "@/lib/api-fetch";

export type PortalHomeModule = {
  id: string;
  title: string;
  description: string;
  icon: LucideIcon;
  accent?: "cyan" | "violet";
  onSelect: () => void;
};

type HomeSummary = {
  regions: { region: string; tickets: number; findings: number }[];
  recent: { ticketNumber: number; subject: string; department: string; status: string; agency: string; updatedAt: string }[];
};

const regionOrder = ["Gran Santo Domingo", "Norte", "Este", "Sur"];
const relativeTime = (value: string) => {
  const minutes = Math.max(0, Math.round((Date.now() - Date.parse(value)) / 60000));
  if (minutes < 1) return "Ahora";
  if (minutes < 60) return `Hace ${minutes} min`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `Hace ${hours} h`;
  return `Hace ${Math.round(hours / 24)} d`;
};

export default function PortalHome({
  displayName,
  modules,
  unreadTickets,
  unreadMessages,
}: {
  displayName: string;
  modules: PortalHomeModule[];
  unreadTickets: number;
  unreadMessages: number;
}) {
  const [summary, setSummary] = useState<HomeSummary>({ regions: [], recent: [] });
  useEffect(() => {
    const controller = new AbortController();
    apiFetch("/api/dashboard/home-summary", { signal: controller.signal, credentials: "same-origin" })
      .then(async (response) => response.ok ? response.json() : Promise.reject(new Error("Resumen no disponible")))
      .then((data: HomeSummary) => setSummary(data))
      .catch((error) => { if (error?.name !== "AbortError") setSummary({ regions: [], recent: [] }); });
    return () => controller.abort();
  }, []);
  const regions = regionOrder.map((region) => summary.regions.find((item) => item.region === region) || { region, tickets: 0, findings: 0 });
  return (
    <section className="portal-launcher" aria-labelledby="portal-launcher-title">
      <header className="portal-launcher-hero">
        <span>OFICINA VIRTUAL · ACCESO RÁPIDO</span>
        <h1 id="portal-launcher-title">¿Qué deseas hacer?</h1>
        <p>
          Hola, {displayName}. Accede a los servicios y procesos disponibles para tu perfil.
        </p>
      </header>

      <nav className="portal-launcher-grid" aria-label="Módulos disponibles">
        {modules.map(({ id, title, description, icon: Icon, accent, onSelect }) => (
          <button
            type="button"
            key={id}
            className={accent === "violet" ? "violet" : undefined}
            onClick={onSelect}
          >
            <span className="portal-launcher-icon"><Icon /></span>
            <span className="portal-launcher-copy">
              <strong>{title}</strong>
              <small>{description}</small>
            </span>
            <ArrowRight className="portal-launcher-arrow" />
          </button>
        ))}
      </nav>

      <div className="portal-launcher-summary">
        <section>
          <header><Clock3 /><strong>Actividad reciente</strong><small>{unreadTickets + unreadMessages} avisos nuevos</small></header>
          {summary.recent.map((item) => <div className="portal-launcher-activity-row" key={item.ticketNumber}>
            <i /><span><strong>Ticket #{item.ticketNumber} · {item.subject}</strong><small>{item.department} · {item.agency}</small></span><time>{relativeTime(item.updatedAt)}</time>
          </div>)}
          {!summary.recent.length && <div className="portal-launcher-empty"><Bell/><span><strong>Actividad al día</strong><small>Las actualizaciones recientes aparecerán aquí.</small></span></div>}
        </section>
        <section>
          <header><MapPin /><strong>Estado por región</strong><small>Actualización automática</small></header>
          {regions.map((item) => <div className="portal-launcher-region-row" key={item.region}>
            <MapPin/><strong>{item.region}</strong><em className={item.findings ? "attention" : "operational"}>{item.findings ? "Atención" : "Operativa"}</em><span>{item.tickets} tickets · {item.findings} hallazgos</span><ArrowRight/>
          </div>)}
        </section>
      </div>
    </section>
  );
}
