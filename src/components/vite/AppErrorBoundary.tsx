import { Component, type ErrorInfo, type ReactNode } from "react";
import { AlertTriangle, RefreshCw, ShieldCheck } from "lucide-react";

type Props = { children: ReactNode };
type State = { failed: boolean };
const RECOVERY_KEY = "module-recovery-v304";

async function repairApplication() {
  try {
    if ("caches" in window) await Promise.all((await caches.keys()).map((key) => caches.delete(key)));
    if ("serviceWorker" in navigator) await Promise.all((await navigator.serviceWorker.getRegistrations()).map((registration) => registration.unregister()));
  } catch (error) {
    console.warn("Application cache recovery", error);
  }
  const url = new URL(window.location.href);
    url.searchParams.set("recovery", "V317");
  window.location.replace(url.toString());
}

export default class AppErrorBoundary extends Component<Props, State> {
  state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("Portal rendering failure", error, info.componentStack);
    const chunkFailure = /ChunkLoadError|Loading chunk|dynamically imported module|module script failed|Failed to fetch/i.test(error.message);
    if (chunkFailure && sessionStorage.getItem(RECOVERY_KEY) !== "1") {
      sessionStorage.setItem(RECOVERY_KEY, "1");
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
          <button type="button" onClick={() => void repairApplication()}>
            <RefreshCw /> Reparar y recargar
          </button>
        </section>
      </main>
    );
  }
}
