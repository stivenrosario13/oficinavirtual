import "server-only";
import { google } from "googleapis";
import { googleEnv } from "@/lib/env";

export const SHEETS_SCOPE = "https://www.googleapis.com/auth/spreadsheets";
export const DRIVE_SCOPE = "https://www.googleapis.com/auth/drive";

/**
 * Autenticación con service account (JWT). Las credenciales viven solo en
 * variables de entorno del servidor y nunca llegan al navegador.
 */
export function googleAuth(scopes: string[]) {
  return new google.auth.JWT({
    email: googleEnv.clientEmail(),
    key: googleEnv.privateKey(),
    scopes,
  });
}
