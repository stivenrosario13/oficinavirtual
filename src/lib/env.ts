/**
 * Acceso a variables de entorno del SERVIDOR.
 *
 * Importante: la lectura es perezosa (funciones), nunca a nivel de módulo,
 * para que `next build` no falle cuando los secretos no están presentes en el
 * entorno de compilación. Estas funciones se llaman en tiempo de solicitud.
 *
 * Este archivo NUNCA debe importarse desde componentes de cliente.
 */

function required(name: string): string {
  const value = process.env[name];
  if (!value || value.trim() === "") {
    throw new Error(
      `Falta la variable de entorno requerida: ${name}. ` +
        `Revísala en tu archivo .env.local o en la configuración de Vercel.`,
    );
  }
  return value;
}

function optional(name: string): string | undefined {
  const value = process.env[name];
  return value && value.trim() !== "" ? value : undefined;
}

/** Configuración de Supabase (fuente de verdad). */
export const supabaseEnv = {
  url: () => required("SUPABASE_URL"),
  anonKey: () => required("SUPABASE_ANON_KEY"),
  serviceRoleKey: () => required("SUPABASE_SERVICE_ROLE_KEY"),
};

/** Credenciales del service account de Google (Sheets + Drive). */
export const googleEnv = {
  clientEmail: () => required("GOOGLE_SERVICE_ACCOUNT_EMAIL"),
  privateKey: () =>
    // En Vercel la clave se guarda en una línea con "\n" escapados.
    required("GOOGLE_SERVICE_ACCOUNT_PRIVATE_KEY").replace(/\\n/g, "\n"),
  sheetId: () => required("GOOGLE_SHEET_ID"),
  driveFolderId: () => required("GOOGLE_DRIVE_FOLDER_ID"),
};

/** OAuth de Google para el panel administrativo (Auth.js). */
export const authEnv = {
  googleClientId: () => required("GOOGLE_CLIENT_ID"),
  googleClientSecret: () => required("GOOGLE_CLIENT_SECRET"),
  secret: () => optional("NEXTAUTH_SECRET") ?? optional("AUTH_SECRET"),
  adminEmailsRaw: () => optional("ADMIN_EMAILS") ?? "",
};

/**
 * Lista de correos administrativos permitidos (minúsculas, sin espacios).
 */
export function getAdminEmails(): string[] {
  return authEnv
    .adminEmailsRaw()
    .split(/[,;\s]+/)
    .map((e) => e.trim().toLowerCase())
    .filter((e) => e.length > 0);
}

/** true si el correo está en la lista de administradores. */
export function isAdminEmail(email: string | null | undefined): boolean {
  if (!email) return false;
  return getAdminEmails().includes(email.trim().toLowerCase());
}
