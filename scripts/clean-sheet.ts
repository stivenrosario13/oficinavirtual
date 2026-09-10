/**
 * Limpia datos de prueba del Google Sheet (mantiene los encabezados).
 *   npx tsx scripts/clean-sheet.ts
 */
import { config as loadEnv } from "dotenv";
import { google } from "googleapis";

loadEnv({ path: ".env.local", quiet: true });
loadEnv({ quiet: true });

async function main() {
  const auth = new google.auth.JWT({
    email: process.env.GOOGLE_SERVICE_ACCOUNT_EMAIL!,
    key: (process.env.GOOGLE_SERVICE_ACCOUNT_PRIVATE_KEY ?? "").replace(/\\n/g, "\n"),
    scopes: ["https://www.googleapis.com/auth/spreadsheets"],
  });
  const sheets = google.sheets({ version: "v4", auth });
  const spreadsheetId = process.env.GOOGLE_SHEET_ID!;

  // Borra filas de datos de "Agency Profiles" (conserva encabezados en fila 1).
  await sheets.spreadsheets.values.clear({
    spreadsheetId,
    range: "'Agency Profiles'!A2:R100000",
  });
  console.log("✅ Limpiadas filas de datos en 'Agency Profiles'.");

  // Borra el valor de prueba en la primera hoja.
  await sheets.spreadsheets.values.clear({
    spreadsheetId,
    range: "'Hoja 1'!A1:Z100",
  });
  console.log("✅ Limpiado el valor de prueba en 'Hoja 1'.");
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
