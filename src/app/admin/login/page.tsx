import { signIn, auth } from "@/auth";
import { redirect } from "next/navigation";

type SearchParams = Promise<{ callbackUrl?: string; error?: string }>;

export default async function LoginPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const { callbackUrl, error } = await searchParams;

  // Si ya hay sesión válida, ir directo al panel.
  const session = await auth();
  if (session?.user?.email) {
    redirect(callbackUrl || "/admin");
  }

  return (
    <div className="mx-auto flex min-h-[70vh] max-w-sm flex-col items-center justify-center text-center">
      <div className="card w-full">
        <h1 className="text-lg font-bold text-ink">Acceso administrativo</h1>
        <p className="mt-1 text-sm text-muted">
          Inicia sesión con una cuenta de Google autorizada.
        </p>

        {error && (
          <p className="mt-4 rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {error === "AccessDenied"
              ? "Tu cuenta no está autorizada para el panel administrativo."
              : "No se pudo iniciar sesión. Inténtalo de nuevo."}
          </p>
        )}

        <form
          className="mt-5"
          action={async () => {
            "use server";
            await signIn("google", {
              redirectTo: callbackUrl || "/admin",
            });
          }}
        >
          <button type="submit" className="btn-primary">
            Iniciar sesión con Google
          </button>
        </form>
      </div>
    </div>
  );
}
