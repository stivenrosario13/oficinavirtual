import NextAuth from "next-auth";
import Google from "next-auth/providers/google";
import { isAdminEmail } from "@/lib/env";

/**
 * Configuración de Auth.js (NextAuth v5) con Google OAuth.
 *
 * Solo los correos incluidos en ADMIN_EMAILS pueden iniciar sesión: cualquier
 * sesión válida pertenece a un administrador. Aun así, cada acción del servidor
 * vuelve a verificar el correo (defensa en profundidad).
 *
 * Las variables se leen directamente de process.env (sin lanzar) para que
 * `next build` no falle cuando los secretos no están presentes.
 */
export const { handlers, auth, signIn, signOut } = NextAuth({
  trustHost: true,
  secret: process.env.NEXTAUTH_SECRET ?? process.env.AUTH_SECRET,
  session: { strategy: "jwt" },
  providers: [
    Google({
      clientId: process.env.GOOGLE_CLIENT_ID,
      clientSecret: process.env.GOOGLE_CLIENT_SECRET,
      authorization: {
        params: { prompt: "select_account" },
      },
    }),
  ],
  pages: {
    signIn: "/admin/login",
    error: "/admin/login",
  },
  callbacks: {
    // Bloquea el inicio de sesión de correos no autorizados.
    signIn({ user }) {
      return isAdminEmail(user.email);
    },
  },
});
