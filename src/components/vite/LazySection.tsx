import { Component, Suspense, lazy, useMemo, useState, type ComponentType, type ReactNode } from 'react';

class SectionBoundary extends Component<{ children: ReactNode; retry: () => void }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  render() {
    return this.state.failed ? <div role="alert" className="loading"><p>No se pudo cargar esta sección. Comprueba la conexión e inténtalo de nuevo.</p><button onClick={this.props.retry}>Reintentar sección</button></div> : this.props.children;
  }
}

export function lazySection<P extends object>(loader: () => Promise<{ default: ComponentType<P> }>) {
  return function DeferredSection(props: P) {
    const [attempt, setAttempt] = useState(0);
    const Content = useMemo(() => lazy(() => new Promise<{ default: ComponentType<P> }>((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('La sección tardó demasiado en cargar.')), 20000);
      loader().then(module => { clearTimeout(timer); resolve(module); }, error => { clearTimeout(timer); reject(error); });
    })), [attempt]);
    return <SectionBoundary key={attempt} retry={() => setAttempt(value => value + 1)}><Suspense fallback={<div className="loading" role="status">Cargando sección…</div>}><Content {...props}/></Suspense></SectionBoundary>;
  };
}
