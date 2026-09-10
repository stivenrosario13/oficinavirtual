import Link from "next/link";
import { auth, signOut } from "@/auth";

export default async function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await auth();
  const email = session?.user?.email;

  return (
    <div className="min-h-full bg-canvas">
      <header className="border-b border-line bg-surface">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 px-4 py-3">
          <div className="flex items-center gap-4">
            <Link href="/admin" className="text-base font-bold text-ink">
              Panel administrativo
            </Link>
            {email && (
              <nav className="flex items-center gap-3 text-sm">
                <Link href="/admin" className="text-brand-600 hover:underline">
                  Dashboard
                </Link>
                <Link
                  href="/admin/records"
                  className="text-brand-600 hover:underline"
                >
                  Registros
                </Link>
              </nav>
            )}
          </div>
          {email && (
            <div className="flex items-center gap-3 text-sm">
              <span className="hidden text-muted sm:inline">{email}</span>
              <form
                action={async () => {
                  "use server";
                  await signOut({ redirectTo: "/admin/login" });
                }}
              >
                <button
                  type="submit"
                  className="rounded-lg border border-line px-3 py-1.5 font-medium text-ink hover:bg-brand-50"
                >
                  Cerrar sesión
                </button>
              </form>
            </div>
          )}
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-6">{children}</main>
    </div>
  );
}
