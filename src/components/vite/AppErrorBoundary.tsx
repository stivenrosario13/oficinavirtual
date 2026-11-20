import { Component, type ErrorInfo, type ReactNode } from "react";
import { AlertTriangle, RefreshCw, ShieldCheck } from "lucide-react";
import { reportOperationalError } from "@/lib/operational-error-report";

type Props = { children: ReactNode };
type State = { failed: boolean; reference: string };
const RECOVERY_KEY_PREFIX = "module-recovery";

const applicationRelease = () =>
  document.querySelector<HTMLMetaElement>('meta[name="app-release"]')?.content.trim() ||
  "current";

const recoveryKey = () => `${RECOVERY_KEY_PREFIX}-${applicationRelease()}`;

async function repairApplication() {
  try {
    if ("caches" in window) await Promise.all((await caches.keys()).map((key) => caches.delete(key)));
    if ("serviceWorker" in navigator) await Promise.all((await navigator.serviceWorker.getRegistrations()).map((registration) => registration.unregister()));
  } catch (error) {
    console.warn("Application cache recovery", error);
  }
  const url = new URL(window.location.href);
  url.searchParams.set("_release", applicationRelease());
  url.searchParams.set("recovery", Date.now().toString(36));
  window.location.replace(url.toString());
}

export default class AppErrorBoundary extends Component<Props, State> {
  state: State = { failed: false, reference: "" };

  static getDerivedStateFromError(error: Error): State {
    const fingerprint = String(error?.message || "render-error")
      .replace(/[^a-z0-9]+/gi, "-")
      .slice(0, 32)
      .toUpperCase();
    return { failed: true, reference: fingerprint || "RENDER-ERROR" };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("Portal rendering failure", error, info.componentStack);
    reportOperationalError({action:"INTERFACE_ERROR",reference:"Interfaz",detail:error.message || "No se pudo renderizar un módulo."});
    const chunkFailure = /ChunkLoadError|Loading chunk|dynamically imported module|module script failed|Importing a module script failed|Failed to fetch/i.test(error.message);
    const key = recoveryKey();
    if (chunkFailure && sessionStorage.getItem(key) !== "1") {
      sessionStorage.setItem(key, "1");
      void repairApplication();
    }
  }

  render() {
    if (!this.state.failed) return this.props.children;

    return (
      <main className="app-error-screen">
        <section>
          <span className="app-error-icon"><AlertTriangle /></span>
          <small><ShieldCheck /> Oficina Virtual</small>
          <h1>No pudimos mostrar este módulo</h1>
          <p>
            Tu información permanece segura. Recarga la aplicación para restaurar
            la sesión y volver a intentarlo.
          </p>
          {this.state.reference && <small>Referencia: {this.state.reference}</small>}
          <button type="button" onClick={() => void repairApplication()}>
            <RefreshCw /> Reparar y recargar
          </button>
        </section>
      </main>
    );
  }
}
