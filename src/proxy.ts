import { NextResponse } from "next/server";
import { auth } from "@/auth";
import { isAdminEmail } from "@/lib/env";

/**
 * Protege el panel administrativo y sus APIs (convención `proxy` de Next 16,
 * antes `middleware`). No se basa en ocultar botones: las rutas /admin y
 * /api/admin exigen una sesión con correo autorizado.
 */
export default auth((req) => {
  const { pathname, origin } = req.nextUrl;

  // La página de inicio de sesión debe ser accesible sin sesión.
  if (pathname === "/admin/login") return NextResponse.next();

  const email = req.auth?.user?.email;
  if (isAdminEmail(email)) return NextResponse.next();

  if (pathname.startsWith("/api/admin")) {
    return NextResponse.json(
      { error: "No autorizado. Inicia sesión como administrador." },
      { status: 401 },
    );
  }

  const url = new URL("/admin/login", origin);
  url.searchParams.set("callbackUrl", pathname);
  return NextResponse.redirect(url);
});

export const config = {
  // Solo intercepta el área administrativa.
  matcher: ["/admin/:path*", "/api/admin/:path*"],
};
