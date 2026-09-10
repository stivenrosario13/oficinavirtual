/**
 * Utilidades de normalización puras (sin dependencias de Node ni del DOM).
 * Se usan tanto en el script de importación como en el servidor, de forma que
 * el `source_key` se calcule siempre igual.
 */

/** Recorta y colapsa espacios internos. Devuelve "" para null/undefined. */
export function normalizeText(value: unknown): string {
  if (value === null || value === undefined) return "";
  return String(value).replace(/\s+/g, " ").trim();
}

/**
 * Normaliza un código o terminal preservando ceros a la izquierda.
 * NUNCA convertir a número: "00123" debe permanecer "00123".
 */
export function normalizeCode(value: unknown): string {
  // Colapsa espacios pero conserva el resto de caracteres tal cual.
  return normalizeText(value);
}

/** Clave de comparación insensible a mayúsculas/acentos para agrupar. */
export function foldKey(value: unknown): string {
  return normalizeText(value)
    .toUpperCase()
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "");
}

/**
 * Construye un identificador estable por agencia a partir de código y terminal.
 * Determinista: mismas entradas -> misma clave. Usado como `source_key` único.
 */
export function buildSourceKey(codigo: unknown, terminal: unknown): string {
  const c = foldKey(codigo);
  const t = foldKey(terminal);
  return `${c}::${t}`;
}

/** true si el valor de texto está vacío tras normalizar. */
export function isBlank(value: unknown): boolean {
  return normalizeText(value).length === 0;
}
