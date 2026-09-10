import "server-only";
import { auth } from "@/auth";
import { isAdminEmail } from "@/lib/env";

/** Error lanzado cuando una operación admin se invoca sin autorización. */
export class NotAuthorizedError extends Error {
  constructor() {
    super("No autorizado. Inicia sesión como administrador.");
    this.name = "NotAuthorizedError";
  }
}

/**
 * Verifica que exista una sesión con correo administrativo.
 * Devuelve el correo del administrador (para registrar en auditoría).
 * Úsese al inicio de toda acción/ruta administrativa.
 */
export async function requireAdmin(): Promise<string> {
  const session = await auth();
  const email = session?.user?.email ?? null;
  if (!isAdminEmail(email)) {
    throw new NotAuthorizedError();
  }
  return email as string;
}
