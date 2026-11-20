import { normalizeVoiceText } from "./voice-operation-assistant";

export type AssistantAgency = {
  id: string;
  codigo: string;
  terminal: string;
  grupo: string;
};
export type AssistantCategory = {
  code: string;
  name: string;
  defaultPriority: string;
  isActive?: boolean;
  requiresDetail?: boolean;
  detailLabel?: string;
  options?: string[];
};
export type AssistantDepartment = {
  code: string;
  name: string;
  categories: AssistantCategory[];
};
export type AssistantInventory = {
  productId: string;
  name: string;
  available?: number;
  group?: string;
  division?: string;
  warehouse?: string;
  unitId?: string;
};
export type Resolution<T> = {
  kind: "missing" | "unique" | "ambiguous";
  value?: T;
  options: T[];
};

const units = [
  "cero",
  "uno",
  "dos",
  "tres",
  "cuatro",
  "cinco",
  "seis",
  "siete",
  "ocho",
  "nueve",
];
const small = [
  "cero",
  "uno",
  "dos",
  "tres",
  "cuatro",
  "cinco",
  "seis",
  "siete",
  "ocho",
  "nueve",
  "diez",
  "once",
  "doce",
  "trece",
  "catorce",
  "quince",
  "dieciséis",
  "diecisiete",
  "dieciocho",
  "diecinueve",
  "veinte",
  "veintiuno",
  "veintidós",
  "veintitrés",
  "veinticuatro",
  "veinticinco",
  "veintiséis",
  "veintisiete",
  "veintiocho",
  "veintinueve",
];
const tens = [
  "",
  "",
  "veinte",
  "treinta",
  "cuarenta",
  "cincuenta",
  "sesenta",
  "setenta",
  "ochenta",
  "noventa",
];
const spokenPair = (pair: string) => {
  if (pair.length === 1) return units[Number(pair)] || pair;
  if (pair.startsWith("0")) return `cero ${units[Number(pair[1])]}`;
  const value = Number(pair);
  if (value < 30) return small[value];
  const unit = value % 10;
  return unit
    ? `${tens[Math.floor(value / 10)]} y ${units[unit]}`
    : tens[Math.floor(value / 10)];
};
export function spokenAgencyCode(code: string) {
  const digits = code.replace(/\D/g, "");
  return (
    digits
      .match(/.{1,2}/g)
      ?.map(spokenPair)
      .join(", ") || code
  );
}
const voiceTerms: Record<string, string> = {
  SIM: "tarjeta sim",
  CPU: "procesador",
  UPS: "respaldo de energía",
  USB: "memoria externa",
  GPS: "geolocalización",
  WIFI: "wifi",
  PDF: "documento digital",
  QR: "código de respuesta rápida",
  IP: "dirección de red",
  PC: "computadora",
  TV: "televisión",
  LTK: "loteka",
  SDE: "santo domingo este",
  MÑ: "mañana",
};
const spokenLetters: Record<string, string> = {
  A: "a",
  B: "be",
  C: "ce",
  D: "de",
  E: "e",
  F: "efe",
  G: "ge",
  H: "hache",
  I: "i",
  J: "jota",
  K: "ka",
  L: "ele",
  M: "eme",
  N: "ene",
  Ñ: "eñe",
  O: "o",
  P: "pe",
  Q: "cu",
  R: "erre",
  S: "ese",
  T: "te",
  U: "u",
  V: "uve",
  W: "doble u",
  X: "equis",
  Y: "ye",
  Z: "zeta",
};
export function spokenReferenceCode(code: string) {
  const chunks =
    code
      .trim()
      .toLocaleUpperCase("es-DO")
      .match(/[A-ZÁÉÍÓÚÑ]+|\d+/g) || [];
  return chunks
    .map((chunk) =>
      /^\d+$/.test(chunk)
        ? spokenAgencyCode(chunk)
        : voiceTerms[chunk] ||
          [...chunk]
            .map(
              (letter) =>
                spokenLetters[letter] || letter.toLocaleLowerCase("es-DO"),
            )
            .join(", "),
    )
    .join(", ");
}
export function spokenNaturalText(value: string) {
  let text = value
    .replace(/\bav\.?\s*/gi, "avenida ")
    .replace(/\bc\/?\s*(?=\d)/gi, "calle ")
    .replace(/\besq\.?\s*/gi, "esquina ")
    .replace(/\bsto\.?\s*/gi, "santo ")
    .replace(/\bsta\.?\s*/gi, "santa ")
    .replace(/\bstgo\.?\s*/gi, "santiago ")
    .replace(/\bs\.?\s*d\.?\b/gi, "santo domingo")
    .replace(/\bkm\.?\s*/gi, "kilómetro ");
  for (const [term, replacement] of Object.entries(voiceTerms))
    text = text.replace(new RegExp(`\\b${term}\\b`, "gi"), replacement);
  text = text.replace(
    /\b(?=[a-záéíóúñ0-9_-]*[a-záéíóúñ])(?=[a-záéíóúñ0-9_-]*\d)[a-záéíóúñ0-9_-]{2,}\b/gi,
    (token) => {
      const digits = token.replace(/\D/g, ""),
        letters = token.replace(/[^a-záéíóúñ]/gi, "");
      if (/^\d/.test(token) && /[aeiouáéíóú]/i.test(letters)) return letters;
      return [
        /[aeiouáéíóú]/i.test(letters) ? letters : "",
        digits ? spokenAgencyCode(digits) : "",
      ]
        .filter(Boolean)
        .join(" ");
    },
  );
  return text
    .replace(/[|/]+/g, ", ")
    .replace(/[_]+/g, " ")
    .replace(/\s*,\s*/g, ", ")
    .replace(/\s+/g, " ")
    .trim()
    .toLocaleLowerCase("es-DO");
}
export function spokenSiteName(value: string) {
  const text = value
    .replace(/\(\s*\d+\s*\)/g, " ")
    .replace(/^(?:\s*\d+\s*(?:\||-)\s*)+/, "")
    .replace(/(?:\b[A-Z]\.){1,}/g, " ")
    .replace(/\b(?!A\b|E\b|Y\b)[A-Z]\b/g, " ");
  return spokenNaturalText(text);
}
export function spokenAgencyLabel(
  agency: Pick<AssistantAgency, "codigo" | "terminal" | "grupo">,
  includeGroup = false,
) {
  return `agencia ${spokenAgencyCode(agency.codigo)}, ${spokenSiteName(agency.terminal)}${includeGroup ? `, grupo ${spokenSiteName(agency.grupo)}` : ""}`;
}

const significant = (value: string) =>
  normalizeVoiceText(value)
    .split(" ")
    .filter(
      (word) =>
        word.length > 2 &&
        ![
          "para",
          "grupo",
          "agencia",
          "banca",
          "ticket",
          "solicitud",
          "quiero",
          "crear",
          "creame",
          "necesito",
          "dame",
          "ponme",
          "selecciona",
          "seleccionar",
          "escojo",
          "elijo",
          "prefiero",
          "busca",
          "buscar",
          "encuentra",
          "encontrar",
          "reportar",
          "reporta",
          "registrar",
          "registra",
          "problema",
          "caso",
          "sobre",
          "donde",
          "tengo",
          "tiene",
          "seria",
          "puede",
          "puedes",
          "esto",
          "algo",
          "solo",
          "cual",
          "como",
          "pero",
          "entonces",
          "tambien",
          "del",
          "con",
          "que",
          "una",
          "uno",
          "los",
          "las",
          "muestra",
          "muestrame",
          "mostrar",
          "ensena",
          "ensename",
          "opcion",
          "numero",
          "articulo",
          "equipo",
          "falla",
          "averia",
          "departamento",
          "correcto",
          "correcta",
          "favor",
          "dice",
          "seria",
          "sera",
          "este",
          "esta",
          "ese",
          "esa",
          "aquel",
          "aquella",
        ].includes(word),
    );
const ordinalWords: Record<string, number> = {
  primera: 0,
  primero: 0,
  uno: 0,
  segunda: 1,
  segundo: 1,
  dos: 1,
  tercera: 2,
  tercero: 2,
  tres: 2,
  cuarta: 3,
  cuarto: 3,
  cuatro: 3,
  quinta: 4,
  quinto: 4,
  cinco: 4,
  sexta: 5,
  sexto: 5,
  seis: 5,
  septima: 6,
  septimo: 6,
  siete: 6,
  octava: 7,
  octavo: 7,
  ocho: 7,
  novena: 8,
  noveno: 8,
  nueve: 8,
  decima: 9,
  decimo: 9,
  diez: 9,
  once: 10,
  doce: 11,
  trece: 12,
  catorce: 13,
  quince: 14,
  dieciseis: 15,
  diecisiete: 16,
  dieciocho: 17,
  diecinueve: 18,
  veinte: 19,
};
const spokenNumberValues: Record<string, string> = {
  cero: "0",
  un: "1",
  uno: "1",
  una: "1",
  dos: "2",
  tres: "3",
  cuatro: "4",
  cinco: "5",
  seis: "6",
  siete: "7",
  ocho: "8",
  nueve: "9",
  diez: "10",
  once: "11",
  doce: "12",
  trece: "13",
  catorce: "14",
  quince: "15",
  dieciseis: "16",
  diecisiete: "17",
  dieciocho: "18",
  diecinueve: "19",
  veinte: "20",
  treinta: "30",
  cuarenta: "40",
  cincuenta: "50",
  sesenta: "60",
  setenta: "70",
  ochenta: "80",
  noventa: "90",
};
const comparableWord = (word: string) =>
  word.length > 5 && word.endsWith("es")
    ? word.slice(0, -2)
    : word.length > 4 && word.endsWith("s")
      ? word.slice(0, -1)
      : word;
const comparableWords = (value: string) =>
  significant(value).map(comparableWord);
const inventoryWordAliases: Record<string, string> = {
  moden: "modem",
  modem: "modem",
  ruter: "router",
  route: "router",
  tonel: "toner",
  tinta: "toner",
  computador: "computadora",
  computadora: "computadora",
  laptop: "portatil",
  portatil: "portatil",
};
const inventoryWords = (value: string) =>
  comparableWords(value).map((word) => inventoryWordAliases[word] || word);

export function spokenQuantityValues(text: string) {
  const tokens = normalizeVoiceText(text).split(" "),
    values: number[] = [];
  for (let index = 0; index < tokens.length; index++) {
    if (/^\d+$/.test(tokens[index])) {
      values.push(Number(tokens[index]));
      continue;
    }
    const value = spokenNumberValues[tokens[index]];
    if (!value) continue;
    if (
      value.endsWith("0") &&
      value !== "0" &&
      tokens[index + 1] === "y" &&
      spokenNumberValues[tokens[index + 2]] &&
      Number(spokenNumberValues[tokens[index + 2]]) < 10
    ) {
      values.push(
        Number(value) + Number(spokenNumberValues[tokens[index + 2]]),
      );
      index += 2;
    } else values.push(Number(value));
  }
  return values;
}

export function spokenAgencyDigitCandidates(text: string) {
  const normalized = normalizeVoiceText(text),
    numeric = normalized.match(/\d+/g) || [],
    candidates = new Set<string>();
  numeric.forEach((value) => {
    if (value.length >= 3) candidates.add(value);
  });
  if (numeric.length > 1) candidates.add(numeric.join(""));
  const spokenSegment =
    normalized.match(
      /(?:agencia|banca|codigo)\s+(.+?)(?=\s+(?:quiero|necesito|con|para|articulo|articulos|equipo|equipos|solicitud|almacen)\b|$)/,
    )?.[1] || normalized;
  const tokens = spokenSegment.split(" "),
    spoken: string[] = [];
  for (let index = 0; index < tokens.length; index++) {
    const value = spokenNumberValues[tokens[index]];
    if (!value) continue;
    if (
      value.endsWith("0") &&
      value !== "0" &&
      tokens[index + 1] === "y" &&
      spokenNumberValues[tokens[index + 2]] &&
      Number(spokenNumberValues[tokens[index + 2]]) < 10
    ) {
      spoken.push(
        String(Number(value) + Number(spokenNumberValues[tokens[index + 2]])),
      );
      index += 2;
    } else spoken.push(value);
  }
  if (spoken.length >= 2) candidates.add(spoken.join(""));
  return [...candidates].filter((value) => value.length >= 3);
}

export function selectSpokenOption<T>(
  answer: string,
  options: T[],
  label: (item: T) => string,
) {
  const text = normalizeVoiceText(answer);
  if (
    options.length === 1 &&
    /\b(?:opcion|articulo|ese|esa|este|esta|correcto|correcta)\b/.test(text)
  )
    return options[0];
  const numeric =
    text.match(/\bopcion\s*(?:numero\s*)?(\d+)\b/)?.[1] ||
    (/^\d+$/.test(text) ? text : undefined);
  const index = numeric
    ? Number(numeric) - 1
    : Object.entries(ordinalWords).find(([word]) =>
        text.split(" ").includes(word),
      )?.[1];
  if (index !== undefined && options[index]) return options[index];
  const matched = options.filter((item) => {
    const target = normalizeVoiceText(label(item));
    return (
      target.includes(text) ||
      (significant(text).length > 0 &&
        significant(text).every((word) => target.includes(word)))
    );
  });
  if (matched.length === 1) return matched[0];
  const queryWords = inventoryWords(text);
  if (!queryWords.length) return undefined;
  const ranked = options
    .map((item) => {
      const targetWords = inventoryWords(label(item));
      const wordScores = queryWords.map((query) => {
        if (targetWords.includes(query)) return 4;
        if (
          targetWords.some(
            (target) => target.startsWith(query) || query.startsWith(target),
          )
        )
          return 3;
        if (
          targetWords.some(
            (target) =>
              query.length >= 4 &&
              target.length >= 4 &&
              editDistance(query, target) <= (query.length > 6 ? 2 : 1),
          )
        )
          return 2;
        return 0;
      });
      const hits = wordScores.filter(Boolean).length;
      return {
        item,
        score: wordScores.reduce<number>((total, value) => total + value, 0),
        coverage: hits / queryWords.length,
      };
    })
    .sort((left, right) =>
      right.coverage !== left.coverage
        ? right.coverage - left.coverage
        : right.score - left.score,
    );
  const best = ranked[0],
    second = ranked[1];
  if (!best || best.coverage < 0.6 || best.score < 2) return undefined;
  if (
    second &&
    second.coverage === best.coverage &&
    second.score >= best.score - 1
  )
    return undefined;
  return best.item;
}

export function resolveAgencyStrict(
  text: string,
  agencies: AssistantAgency[],
): Resolution<AssistantAgency> {
  const normalized = normalizeVoiceText(text);
  const codes = spokenAgencyDigitCandidates(text),
    code = codes.find((candidate) =>
      agencies.some(
        (item) =>
          normalizeVoiceText(item.codigo) === candidate ||
          normalizeVoiceText(item.codigo).endsWith(candidate) ||
          normalizeVoiceText(item.codigo).includes(candidate),
      ),
    );
  if (codes.length && !code && /\b(?:agencia|banca|codigo)\b/.test(normalized))
    return { kind: "missing", options: [] };
  const groupMatch = normalized
    .match(
      /grupo\s+(.+?)(?=\s+(?:con|de|para|por|que|y|falla|ticket|solicitud|necesito|quiero)\b|$)/,
    )?.[1]
    ?.trim();
  let candidates = agencies;
  if (code)
    candidates = candidates.filter(
      (item) =>
        normalizeVoiceText(item.codigo) === code ||
        normalizeVoiceText(item.codigo).endsWith(code) ||
        normalizeVoiceText(item.codigo).includes(code),
    );
  if (groupMatch) {
    const groupWords = significant(groupMatch);
    candidates = candidates.filter((item) =>
      groupWords.every((word) => normalizeVoiceText(item.grupo).includes(word)),
    );
  }
  if (!code && !groupMatch) {
    const direct = agencies.filter((item) => {
      const terminal = significant(item.terminal),
        group = significant(item.grupo);
      return (
        (terminal.length > 0 &&
          terminal.every((word) => normalized.includes(word))) ||
        (group.length > 0 && group.every((word) => normalized.includes(word)))
      );
    });
    candidates = direct;
  }
  return candidates.length === 1
    ? { kind: "unique", value: candidates[0], options: candidates }
    : candidates.length > 1
      ? { kind: "ambiguous", options: candidates }
      : { kind: "missing", options: [] };
}

export function resolveAgencyMentions(
  text: string,
  agencies: AssistantAgency[],
) {
  const normalized = normalizeVoiceText(text),
    codes = spokenAgencyDigitCandidates(text);
  const matches = agencies.filter(
    (item) =>
      codes.some(
        (code) =>
          normalizeVoiceText(item.codigo) === code ||
          normalizeVoiceText(item.codigo).endsWith(code),
      ) ||
      (() => {
        const terminal = significant(item.terminal);
        return (
          terminal.length > 0 &&
          terminal.every((word) => normalized.includes(word))
        );
      })(),
  );
  return [...new Map(matches.map((item) => [item.id, item])).values()];
}

export function suggestAgencyOptions(
  text: string,
  agencies: AssistantAgency[],
  limit = 8,
) {
  const query = comparableWords(text),
    codes = spokenAgencyDigitCandidates(text);
  return agencies
    .map((item) => {
      const target = normalizeVoiceText(
          `${item.codigo} ${item.terminal} ${item.grupo}`,
        ),
        targetWords = comparableWords(`${item.terminal} ${item.grupo}`);
      let score = codes.reduce(
        (total, code) =>
          total + (normalizeVoiceText(item.codigo).includes(code) ? 60 : 0),
        0,
      );
      score += query.reduce(
        (total, word) =>
          total +
          (target.includes(word)
            ? 10 + word.length
            : targetWords.some(
                  (candidate) =>
                    candidate.startsWith(word) || word.startsWith(candidate),
                )
              ? 6
              : 0),
        0,
      );
      return { item, score };
    })
    .sort(
      (left, right) =>
        right.score - left.score ||
        left.item.codigo.localeCompare(right.item.codigo, "es"),
    )
    .filter((entry, index) => entry.score > 0 || index < limit)
    .slice(0, limit)
    .map((entry) => entry.item);
}

export function searchAgencyOptions(
  text: string,
  agencies: AssistantAgency[],
  limit = 10,
) {
  const query = comparableWords(text);
  if (!query.length) return agencies.slice(0, limit);
  return agencies
    .map((item) => {
      const target = normalizeVoiceText(
          `${item.codigo} ${item.terminal} ${item.grupo}`,
        ),
        targetWords = comparableWords(`${item.terminal} ${item.grupo}`);
      const score = query.reduce(
        (total, word) =>
          total +
          (target.includes(word)
            ? 12 + word.length
            : targetWords.some(
                  (candidate) =>
                    candidate.startsWith(word) || word.startsWith(candidate),
                )
              ? 8
              : targetWords.some(
                    (candidate) =>
                      word.length > 3 &&
                      candidate.length > 3 &&
                      editDistance(word, candidate) <= 2,
                  )
                ? 5
                : 0),
        0,
      );
      return { item, score };
    })
    .filter((entry) => entry.score > 0)
    .sort(
      (left, right) =>
        right.score - left.score ||
        left.item.codigo.localeCompare(right.item.codigo, "es"),
    )
    .slice(0, limit)
    .map((entry) => entry.item);
}

export function resolveDepartmentStrict(
  text: string,
  departments: AssistantDepartment[],
): Resolution<AssistantDepartment> {
  const normalized = normalizeVoiceText(text);
  const aliases: Record<string, string[]> = {
    TECHNOLOGY: [
      "tecnologia",
      "sistemas",
      "informatica",
      "soporte tecnico",
      "internet",
      "modem",
      "computadora",
      "cpu",
      "sistema",
      "impresora",
    ],
    GENERAL_SERVICES: [
      "servicios generales",
      "mantenimiento",
      "infraestructura",
      "electricidad",
      "plomeria",
      "aire acondicionado",
      "mobiliario",
      "pintura",
    ],
    HUMAN_RESOURCES: ["recursos humanos", "recursos humano", "rrhh", "nomina", "empleado", "personal"],
  };
  const matches = departments.filter((item) =>
    [normalizeVoiceText(item.name), ...(aliases[item.code] || [])].some(
      (alias) => normalized.includes(alias),
    ),
  );
  return matches.length === 1
    ? { kind: "unique", value: matches[0], options: matches }
    : matches.length > 1
      ? { kind: "ambiguous", options: matches }
      : { kind: "missing", options: [] };
}

export function resolveCategoryStrict(
  text: string,
  department: AssistantDepartment,
): Resolution<AssistantCategory> {
  const normalized = normalizeVoiceText(text);
  const active = department.categories.filter(
    (item) => item.isActive !== false,
  );
  const exact = active.filter((item) =>
    normalized.includes(normalizeVoiceText(item.name)),
  );
  const natural = selectSpokenOption(text, active, (item) => item.name);
  if (natural) return { kind: "unique", value: natural, options: [natural] };
  const candidates = exact.length
    ? exact
    : active.filter((item) => {
        const words = significant(item.name);
        return (
          words.length > 0 && words.every((word) => normalized.includes(word))
        );
      });
  return candidates.length === 1
    ? { kind: "unique", value: candidates[0], options: candidates }
    : candidates.length > 1
      ? { kind: "ambiguous", options: candidates.slice(0, 8) }
      : { kind: "missing", options: [] };
}

export type InventoryLine = { item: AssistantInventory; quantity: number };
export function resolveInventoryStrict(
  text: string,
  inventory: AssistantInventory[],
): {
  lines: InventoryLine[];
  missingQuantity: AssistantInventory[];
  ambiguous: AssistantInventory[];
} {
  const normalized = normalizeVoiceText(text),
    lines: InventoryLine[] = [],
    missingQuantity: AssistantInventory[] = [],
    ambiguous: AssistantInventory[] = [];
  const inputWords = new Set(inventoryWords(text));
  const selectedByCode = inventory.filter((item) => {
    const code = normalizeVoiceText(item.productId);
    return (
      code.length > 0 &&
      new RegExp(
        `(?:^|\\s)${code.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}(?:$|\\s)`,
      ).test(normalized)
    );
  });
  let matched = selectedByCode;
  if (!matched.length) {
    const exact = inventory.filter((item) =>
      normalized.includes(normalizeVoiceText(item.name)),
    );
    if (exact.length) matched = exact;
    else {
      const ranked = inventory
        .map((item) => {
          const words = inventoryWords(item.name);
          const hits = words.reduce(
            (total, word) =>
              total +
              (inputWords.has(word)
                ? 4
                : inputWords.size &&
                    [...inputWords].some(
                      (input) =>
                        input.length > 3 &&
                        word.length > 3 &&
                        (input.startsWith(word) ||
                          word.startsWith(input) ||
                          editDistance(input, word) <= 2),
                    )
                  ? 2
                  : 0),
            0,
          );
          return { item, score: words.length ? hits / words.length : 0, hits };
        })
        .filter((entry) => entry.hits > 0)
        .sort(
          (left, right) =>
            right.score - left.score ||
            right.hits - left.hits ||
            left.item.name.localeCompare(right.item.name, "es"),
        );
      if (ranked.length && ranked[0].score >= 1.5) {
        const close = ranked
          .filter(
            (entry) =>
              entry.score >= ranked[0].score - 0.2 &&
              entry.hits >= ranked[0].hits - 1,
          )
          .slice(0, 8);
        if (close.length > 1)
          return {
            lines,
            missingQuantity,
            ambiguous: close.map((entry) => entry.item),
          };
        matched = [ranked[0].item];
      }
    }
  }
  const byName = new Map<string, AssistantInventory[]>();
  for (const item of matched) {
    const key = normalizeVoiceText(item.name);
    byName.set(key, [...(byName.get(key) || []), item]);
  }
  for (const options of byName.values()) {
    const explicitlySelected = options.find((item) => {
      const code = normalizeVoiceText(item.productId);
      return (
        code &&
        new RegExp(
          `(?:^|\\s)${code.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}(?:$|\\s)`,
        ).test(normalized)
      );
    });
    if (options.length > 1 && !explicitlySelected) {
      ambiguous.push(...options);
      continue;
    }
    const item = explicitlySelected || options[0],
      firstWord = comparableWords(item.name)[0],
      namePosition = normalized.lastIndexOf(firstWord),
      codePosition = normalized.lastIndexOf(normalizeVoiceText(item.productId)),
      position = namePosition >= 0 ? namePosition : codePosition,
      prefix = normalized.slice(Math.max(0, position - 48), position);
    const quantityText =
      prefix.match(/(\d+)\s*(?:[a-z]+\s*){0,2}$/)?.[1] ||
      Object.entries(spokenNumberValues)
        .reverse()
        .find(([word]) =>
          new RegExp(`(?:^|\\s)${word}(?:\\s|$)`).test(prefix),
        )?.[1];
    if (!quantityText) {
      missingQuantity.push(item);
      continue;
    }
    lines.push({ item, quantity: Number(quantityText) });
  }
  return { lines, missingQuantity, ambiguous };
}

function editDistance(left: string, right: string) {
  const rows = Array.from({ length: left.length + 1 }, (_, index) => index);
  for (let column = 1; column <= right.length; column++) {
    let diagonal = rows[0];
    rows[0] = column;
    for (let row = 1; row <= left.length; row++) {
      const previous = rows[row];
      rows[row] = Math.min(
        rows[row] + 1,
        rows[row - 1] + 1,
        diagonal + (left[row - 1] === right[column - 1] ? 0 : 1),
      );
      diagonal = previous;
    }
  }
  return rows[left.length];
}
export function suggestInventoryOptions(
  text: string,
  inventory: AssistantInventory[],
  limit = 8,
) {
  const query = inventoryWords(text).filter(
    (word) => !["almacen", "articulo", "cantidad"].includes(word),
  );
  return inventory
    .map((item) => {
      const normalized = normalizeVoiceText(`${item.name} ${item.productId}`),
        itemWords = inventoryWords(item.name);
      let score = query.reduce(
        (total, word) =>
          total +
          (normalized.includes(word)
            ? 12 + word.length
            : itemWords.some(
                  (candidate) =>
                    candidate.startsWith(word) || word.startsWith(candidate),
                )
              ? 8
              : itemWords.some(
                    (candidate) =>
                      word.length > 3 &&
                      candidate.length > 3 &&
                      editDistance(word, candidate) <= 2,
                  )
                ? 5
                : 0),
        0,
      );
      if (
        normalizeVoiceText(item.productId) &&
        normalizeVoiceText(text).includes(normalizeVoiceText(item.productId))
      )
        score += 50;
      return { item, score };
    })
    .sort(
      (left, right) =>
        right.score - left.score ||
        left.item.name.localeCompare(right.item.name, "es"),
    )
    .filter((entry, index) => entry.score > 0 || index < limit)
    .slice(0, limit)
    .map((entry) => entry.item);
}

export function searchInventoryOptions(
  text: string,
  inventory: AssistantInventory[],
  limit = 10,
) {
  const query = inventoryWords(text).filter(
    (word) =>
      !["almacen", "articulo", "cantidad", "inventario", "material"].includes(
        word,
      ),
  );
  if (!query.length) return inventory.slice(0, limit);
  return inventory
    .map((item) => {
      const target = normalizeVoiceText(
          `${item.name} ${item.productId} ${item.group || ""} ${item.division || ""}`,
        ),
        words = inventoryWords(item.name);
      let score = query.reduce(
        (total, word) =>
          total +
          (target.includes(word)
            ? 12 + word.length
            : words.some(
                  (candidate) =>
                    candidate.startsWith(word) || word.startsWith(candidate),
                )
              ? 8
              : words.some(
                    (candidate) =>
                      word.length > 3 &&
                      candidate.length > 3 &&
                      editDistance(word, candidate) <= 2,
                  )
                ? 5
                : 0),
        0,
      );
      if (
        normalizeVoiceText(item.productId) &&
        normalizeVoiceText(text).includes(normalizeVoiceText(item.productId))
      )
        score += 50;
      return { item, score };
    })
    .filter((entry) => entry.score > 0)
    .sort(
      (left, right) =>
        right.score - left.score ||
        left.item.name.localeCompare(right.item.name, "es"),
    )
    .slice(0, limit)
    .map((entry) => entry.item);
}

export function filterInventoryCatalog(
  text: string,
  inventory: AssistantInventory[],
  limit = 60,
) {
  if (!normalizeVoiceText(text)) return inventory.slice(0, limit);
  return suggestInventoryOptions(text, inventory, limit);
}

export function filterAgencyCatalog(
  text: string,
  agencies: AssistantAgency[],
  limit = 60,
) {
  if (!normalizeVoiceText(text)) return agencies.slice(0, limit);
  const direct = agencies.filter((item) =>
    normalizeVoiceText(
      `${item.codigo} ${item.terminal} ${item.grupo}`,
    ).includes(normalizeVoiceText(text)),
  );
  return (
    direct.length ? direct : suggestAgencyOptions(text, agencies, limit)
  ).slice(0, limit);
}

export function spokenOptions<T>(options: T[], label: (item: T) => string) {
  return options
    .slice(0, 5)
    .map((item, index) => `opción ${index + 1}, ${label(item)}`)
    .join("; ");
}
