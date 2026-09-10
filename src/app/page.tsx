import PublicWizard from "@/components/public/PublicWizard";

export default function Home() {
  return (
    <div className="mx-auto flex min-h-full w-full max-w-md flex-col px-4 pb-16 pt-6">
      <header className="mb-5">
        <h1 className="text-xl font-bold text-ink">Registro de Agencias</h1>
        <p className="text-sm text-muted">
          Completa la información operativa y geográfica de tu agencia.
        </p>
      </header>
      <main className="flex-1">
        <PublicWizard />
      </main>
      <footer className="mt-8 text-center text-xs text-muted">
        Tus datos se guardan de forma segura. Captura la ubicación desde la
        agencia.
      </footer>
    </div>
  );
}
