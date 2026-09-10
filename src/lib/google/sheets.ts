import "server-only";
import { google } from "googleapis";
import { googleEnv } from "@/lib/env";
import { googleAuth, SHEETS_SCOPE } from "./client";
import { SHEET_HEADERS, SHEET_TAB_NAME } from "@/lib/constants";
import type { AgencyProfileRow } from "@/lib/types";

export interface SheetSyncInput {
  profile: AgencyProfileRow;
  agency: { codigo: string; terminal: string; grupo: string };
}

export interface SheetSyncResult {
  rowReference: string;
}

function sheetsClient() {
  const auth = googleAuth([SHEETS_SCOPE]);
  return google.sheets({ version: "v4", auth });
}

/** Letra de columna de Excel para un índice 1-based (1 -> A, 27 -> AA). */
function columnLetter(index: number): string {
  let n = index;
  let letter = "";
  while (n > 0) {
    const rem = (n - 1) % 26;
    letter = String.fromCharCode(65 + rem) + letter;
    n = Math.floor((n - 1) / 26);
  }
  return letter;
}

const LAST_COL = columnLetter(SHEET_HEADERS.length);

function buildRow(input: SheetSyncInput): string[] {
  const { profile: p, agency: a } = input;
  return [
    p.id, // response_id
    p.agency_id,
    a.codigo,
    a.terminal,
    a.grupo,
    p.direccion,
    p.sector,
    p.municipio,
    p.provincia,
    p.tipo_establecimiento,
    p.tipo_establecimiento_otro ?? "",
    String(p.latitude),
    String(p.longitude),
    String(p.accuracy_meters),
    p.location_captured_at ?? "",
    p.submitted_at ?? "",
    p.photo_url ?? "",
    "SYNCED",
  ];
}

/**
 * Asegura que la pestaña de destino exista y tenga la fila de encabezados.
 * Idempotente.
 */
async function ensureSheet(
  sheets: ReturnType<typeof sheetsClient>,
  spreadsheetId: string,
): Promise<void> {
  const meta = await sheets.spreadsheets.get({ spreadsheetId });
  const exists = meta.data.sheets?.some(
    (s) => s.properties?.title === SHEET_TAB_NAME,
  );

  if (!exists) {
    await sheets.spreadsheets.batchUpdate({
      spreadsheetId,
      requestBody: {
        requests: [
          { addSheet: { properties: { title: SHEET_TAB_NAME } } },
        ],
      },
    });
  }

  // Escribe/normaliza la fila de encabezados.
  await sheets.spreadsheets.values.update({
    spreadsheetId,
    range: `${SHEET_TAB_NAME}!A1:${LAST_COL}1`,
    valueInputOption: "RAW",
    requestBody: { values: [SHEET_HEADERS as unknown as string[]] },
  });
}

/**
 * Inserta o actualiza la fila del perfil por `response_id` (columna A).
 * Una edición administrativa actualiza la MISMA fila, no crea otra.
 */
export async function syncProfileToSheet(
  input: SheetSyncInput,
): Promise<SheetSyncResult> {
  const spreadsheetId = googleEnv.sheetId();
  const sheets = sheetsClient();

  await ensureSheet(sheets, spreadsheetId);

  // Busca el response_id en la columna A (desde la fila 2).
  const idColumn = await sheets.spreadsheets.values.get({
    spreadsheetId,
    range: `${SHEET_TAB_NAME}!A2:A`,
  });
  const ids = idColumn.data.values ?? [];
  const foundIndex = ids.findIndex((r) => (r?.[0] ?? "") === input.profile.id);

  const row = buildRow(input);

  if (foundIndex >= 0) {
    const rowNumber = foundIndex + 2; // +2: encabezado + base 1
    const range = `${SHEET_TAB_NAME}!A${rowNumber}:${LAST_COL}${rowNumber}`;
    await sheets.spreadsheets.values.update({
      spreadsheetId,
      range,
      valueInputOption: "RAW",
      requestBody: { values: [row] },
    });
    return { rowReference: range };
  }

  const appended = await sheets.spreadsheets.values.append({
    spreadsheetId,
    range: `${SHEET_TAB_NAME}!A1`,
    valueInputOption: "RAW",
    insertDataOption: "INSERT_ROWS",
    requestBody: { values: [row] },
  });

  return {
    rowReference:
      appended.data.updates?.updatedRange ?? `${SHEET_TAB_NAME}!append`,
  };
}
