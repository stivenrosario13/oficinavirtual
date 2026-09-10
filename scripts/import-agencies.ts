/**
 * Script de importación de agencias desde Excel.
 *
 * Uso:
 *   npm run import:agencies                 # lee ./Agencias.xlsx
 *   npm run import:agencies -- ruta/al.xlsx # ruta personalizada
 *
 * Reglas (ver README §"Cómo importar el Excel"):
 *  - El Excel es solo fuente de importación; NUNCA se lee en producción.
 *  - Requiere las columnas Codigo, Terminal y Grupo.
 *  - Codigo y Terminal se mantienen como TEXTO (no se pierden ceros a la izq.).
 *  - Normaliza espacios y colapsa duplicados por source_key.
 *  - Se detiene con error claro si el Excel está vacío, sin encabezados válidos
 *    o con filas inválidas. No inventa registros.
 */
import path from "node:path";
import process from "node:process";
import { config as loadEnv } from "dotenv";
import ExcelJS from "exceljs";
import { createClient } from "@supabase/supabase-js";
import {
  normalizeCode,
  normalizeText,
  buildSourceKey,
  foldKey,
  isBlank,
} from "../src/lib/normalize";

// Carga variables de entorno: .env.local tiene prioridad sobre .env
loadEnv({ path: ".env.local", quiet: true });
loadEnv({ quiet: true });

const REQUIRED_HEADERS = ["CODIGO", "TERMINAL", "GRUPO"] as const;

function fail(message: string): never {
  console.error(`\n❌ ${message}\n`);
  process.exit(1);
}

interface ParsedRow {
  excelRow: number;
  codigo: string;
  terminal: string;
  grupo: string;
  sourceKey: string;
}

interface HeaderMap {
  codigo: number;
  terminal: number;
  grupo: number;
}

/** Busca en el libro la primera hoja que contenga los 3 encabezados. */
function findHeaderSheet(
  wb: ExcelJS.Workbook,
): { sheet: ExcelJS.Worksheet; headers: HeaderMap } | null {
  for (const sheet of wb.worksheets) {
    const headerRow = sheet.getRow(1);
    const found: Partial<Record<(typeof REQUIRED_HEADERS)[number], number>> = {};
    headerRow.eachCell({ includeEmpty: false }, (cell, colNumber) => {
      const key = foldKey(cell.text);
      if ((REQUIRED_HEADERS as readonly string[]).includes(key)) {
        found[key as (typeof REQUIRED_HEADERS)[number]] = colNumber;
      }
    });
    if (found.CODIGO && found.TERMINAL && found.GRUPO) {
      return {
        sheet,
        headers: {
          codigo: found.CODIGO,
          terminal: found.TERMINAL,
          grupo: found.GRUPO,
        },
      };
    }
  }
  return null;
}

async function main(): Promise<void> {
  const args = process.argv.slice(2);
  const dryRun = args.includes("--dry-run");
  const fileArg = args.find((a) => !a.startsWith("--"));
  const filePath = path.resolve(fileArg ?? "Agencias.xlsx");

  console.log(`\n📄 Leyendo Excel: ${filePath}`);
  if (dryRun) console.log("🧪 Modo dry-run: no se escribirá en la base de datos.");

  const wb = new ExcelJS.Workbook();
  try {
    await wb.xlsx.readFile(filePath);
  } catch (err) {
    fail(
      `No se pudo abrir el archivo. ¿Existe y está guardado (no solo abierto ` +
        `en Excel)? Detalle: ${(err as Error).message}`,
    );
  }

  const headerSheet = findHeaderSheet(wb);
  if (!headerSheet) {
    fail(
      "No se encontraron los encabezados requeridos (Codigo, Terminal, Grupo) " +
        "en ninguna hoja. Verifica que la primera fila los contenga.",
    );
  }
  const { sheet, headers } = headerSheet;
  console.log(`✅ Encabezados válidos en la hoja "${sheet.name}".`);

  const valid: ParsedRow[] = [];
  const invalid: { row: number; reason: string }[] = [];
  let totalDataRows = 0;

  const lastRow = sheet.actualRowCount || sheet.rowCount;
  for (let r = 2; r <= lastRow; r++) {
    const row = sheet.getRow(r);
    const codigo = normalizeCode(row.getCell(headers.codigo).text);
    const terminal = normalizeCode(row.getCell(headers.terminal).text);
    const grupo = normalizeText(row.getCell(headers.grupo).text);

    const allBlank = isBlank(codigo) && isBlank(terminal) && isBlank(grupo);
    if (allBlank) continue; // fila totalmente vacía: se ignora

    totalDataRows += 1;

    const missing: string[] = [];
    if (isBlank(codigo)) missing.push("Codigo");
    if (isBlank(terminal)) missing.push("Terminal");
    if (isBlank(grupo)) missing.push("Grupo");

    if (missing.length > 0) {
      invalid.push({ row: r, reason: `faltan campos: ${missing.join(", ")}` });
      continue;
    }

    valid.push({
      excelRow: r,
      codigo,
      terminal,
      grupo,
      sourceKey: buildSourceKey(codigo, terminal),
    });
  }

  if (totalDataRows === 0) {
    fail("El Excel no contiene registros de agencias. No se importó nada.");
  }

  if (invalid.length > 0) {
    console.error(`\n❌ Se encontraron ${invalid.length} fila(s) inválida(s):`);
    for (const inv of invalid.slice(0, 25)) {
      console.error(`   • Fila ${inv.row}: ${inv.reason}`);
    }
    if (invalid.length > 25) {
      console.error(`   • ... y ${invalid.length - 25} más.`);
    }
    fail(
      "Corrige las filas inválidas en el Excel y vuelve a ejecutar. " +
        "No se importó nada para evitar datos incompletos.",
    );
  }

  // Colapsa duplicados por source_key (se conserva la primera aparición).
  const unique = new Map<string, ParsedRow>();
  let duplicates = 0;
  for (const row of valid) {
    if (unique.has(row.sourceKey)) {
      duplicates += 1;
    } else {
      unique.set(row.sourceKey, row);
    }
  }
  const uniqueRows = [...unique.values()];

  if (dryRun) {
    console.log(`\n===================== RESUMEN (dry-run) =====================`);
    console.log(`  Total leído (filas con datos):   ${totalDataRows}`);
    console.log(`  Registros únicos:                ${uniqueRows.length}`);
    console.log(`  Duplicados detectados:           ${duplicates}`);
    console.log(`  Filas inválidas:                 ${invalid.length}`);
    console.log(`=============================================================\n`);
    console.log("✅ Validación completada (no se escribió en la base de datos).\n");
    console.log("   Ejemplo de source_key generados:");
    for (const r of uniqueRows.slice(0, 3)) {
      console.log(`     ${r.codigo} | ${r.terminal.slice(0, 40)}… -> ${r.sourceKey.slice(0, 60)}`);
    }
    console.log("");
    return;
  }

  // El Excel es válido: recién ahora se exigen credenciales de base de datos.
  const SUPABASE_URL = process.env.SUPABASE_URL;
  const SERVICE_ROLE = process.env.SUPABASE_SERVICE_ROLE_KEY;
  if (!SUPABASE_URL || !SERVICE_ROLE) {
    fail(
      "Faltan SUPABASE_URL y/o SUPABASE_SERVICE_ROLE_KEY. " +
        "Configúralas en .env.local antes de importar.",
    );
  }

  const supabase = createClient(SUPABASE_URL, SERVICE_ROLE, {
    auth: { persistSession: false, autoRefreshToken: false },
  });

  // Clasifica en nuevas vs existentes para el resumen (imported vs updated).
  // Se leen todas las source_key existentes con paginación; NO se filtra por
  // `in(...)` con muchas claves largas (generaría una URL demasiado grande).
  const existing = new Set<string>();
  const PAGE = 1000;
  for (let from = 0; ; from += PAGE) {
    const { data, error } = await supabase
      .from("agencies")
      .select("source_key")
      .range(from, from + PAGE - 1);
    if (error) fail(`Error consultando agencias existentes: ${error.message}`);
    const rows = (data ?? []) as { source_key: string }[];
    for (const row of rows) existing.add(row.source_key);
    if (rows.length < PAGE) break;
  }

  const toImport = uniqueRows.filter((r) => !existing.has(r.sourceKey)).length;
  const toUpdate = uniqueRows.length - toImport;

  const CHUNK = 500;

  // Upsert por source_key. NO se envía `status`: en conflicto, el estado
  // (p.ej. COMPLETED) se preserva; en inserción, usa el default PENDING.
  const payload = uniqueRows.map((r) => ({
    source_key: r.sourceKey,
    codigo: r.codigo,
    terminal: r.terminal,
    grupo: r.grupo,
  }));

  for (let i = 0; i < payload.length; i += CHUNK) {
    const slice = payload.slice(i, i + CHUNK);
    const { error } = await supabase
      .from("agencies")
      .upsert(slice, { onConflict: "source_key" });
    if (error) fail(`Error al insertar/actualizar agencias: ${error.message}`);
    console.log(
      `   … procesadas ${Math.min(i + slice.length, payload.length)}/${payload.length}`,
    );
  }

  console.log(`\n===================== RESUMEN =====================`);
  console.log(`  Total leído (filas con datos):   ${totalDataRows}`);
  console.log(`  Registros únicos:                ${uniqueRows.length}`);
  console.log(`  Importados (nuevos):             ${toImport}`);
  console.log(`  Actualizados (existentes):       ${toUpdate}`);
  console.log(`  Duplicados detectados:           ${duplicates}`);
  console.log(`  Filas inválidas:                 ${invalid.length}`);
  console.log(`===================================================\n`);
  console.log("✅ Importación completada.\n");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
