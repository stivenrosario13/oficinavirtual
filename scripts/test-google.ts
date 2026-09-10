/**
 * Prueba directa de las credenciales de Google (Sheets + Drive) con la service
 * account de .env.local. No necesita el servidor Next ni geolocalización.
 *
 *   npx tsx scripts/test-google.ts
 */
import { config as loadEnv } from "dotenv";
import { Readable } from "node:stream";
import { google } from "googleapis";

loadEnv({ path: ".env.local", quiet: true });
loadEnv({ quiet: true });

const email = process.env.GOOGLE_SERVICE_ACCOUNT_EMAIL!;
const key = (process.env.GOOGLE_SERVICE_ACCOUNT_PRIVATE_KEY ?? "").replace(/\\n/g, "\n");
const sheetId = process.env.GOOGLE_SHEET_ID!;
const folderId = process.env.GOOGLE_DRIVE_FOLDER_ID!;

async function main() {
  console.log("SA:", email);
  console.log("Sheet:", sheetId, "| Folder:", folderId, "\n");

  // ---- Google Sheets ----
  try {
    const sheetsAuth = new google.auth.JWT({
      email,
      key,
      scopes: ["https://www.googleapis.com/auth/spreadsheets"],
    });
    const sheets = google.sheets({ version: "v4", auth: sheetsAuth });
    const meta = await sheets.spreadsheets.get({ spreadsheetId: sheetId });
    console.log(
      "✅ Sheets: acceso OK. Pestañas:",
      meta.data.sheets?.map((s) => s.properties?.title).join(", "),
    );
    await sheets.spreadsheets.values.update({
      spreadsheetId: sheetId,
      range: "A1",
      valueInputOption: "RAW",
      requestBody: { values: [["prueba-conexion"]] },
    });
    console.log("✅ Sheets: escritura OK (celda A1).");
  } catch (e) {
    console.error("❌ Sheets:", (e as Error).message);
  }

  console.log("");

  // ---- Google Drive ----
  try {
    const driveAuth = new google.auth.JWT({
      email,
      key,
      scopes: ["https://www.googleapis.com/auth/drive"],
    });
    const drive = google.drive({ version: "v3", auth: driveAuth });
    // PNG 1x1 transparente
    const png = Buffer.from(
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
      "base64",
    );
    const res = await drive.files.create({
      requestBody: { name: `prueba-${Date.now()}.png`, parents: [folderId] },
      media: { mimeType: "image/png", body: Readable.from(png) },
      fields: "id, webViewLink",
      supportsAllDrives: true,
    });
    console.log("✅ Drive: subida OK. fileId:", res.data.id);
    await drive.files.delete({ fileId: res.data.id!, supportsAllDrives: true });
    console.log("✅ Drive: archivo de prueba eliminado.");
  } catch (e) {
    console.error("❌ Drive:", (e as Error).message);
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
