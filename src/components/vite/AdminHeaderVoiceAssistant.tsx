import { useEffect, useRef, useState } from "react";
import {
  Bot,
  LoaderCircle,
  Mic,
  MicOff,
  RotateCcw,
  Send,
  Sparkles,
  X,
} from "lucide-react";
import { apiFetch as fetch } from "@/lib/api-fetch";
import {
  normalizeVoiceText,
  type VoiceWarehouseBatch,
} from "@/lib/voice-operation-assistant";
import {
  filterAgencyCatalog,
  filterInventoryCatalog,
  resolveAgencyMentions,
  resolveAgencyStrict,
  resolveCategoryStrict,
  resolveDepartmentStrict,
  resolveInventoryStrict,
  searchAgencyOptions,
  searchInventoryOptions,
  selectSpokenOption,
  spokenAgencyLabel,
  spokenNaturalText,
  spokenOptions,
  spokenQuantityValues,
  suggestAgencyOptions,
  suggestInventoryOptions,
  type AssistantAgency,
  type AssistantCategory,
  type AssistantDepartment,
  type AssistantInventory,
} from "@/lib/voice-assistant-validation";
import "./adminHeaderVoiceAssistant.css";

type Intent = "ticket" | "warehouse";
type WarehouseDraft = {
  agency: AssistantAgency;
  lines: { productId: string; name: string; quantity: number }[];
};
type Pending =
  | { kind: "agency"; options: AssistantAgency[] }
  | { kind: "agencyLookup"; options: AssistantAgency[] }
  | { kind: "department"; options: AssistantDepartment[] }
  | { kind: "category"; options: AssistantCategory[] }
  | { kind: "detail"; category: AssistantCategory }
  | { kind: "inventory"; options: AssistantInventory[] }
  | { kind: "inventoryLookup"; options: AssistantInventory[] }
  | { kind: "quantity"; items: AssistantInventory[] }
  | { kind: "warehouseItems" }
  | { kind: "warehouseNext" }
  | null;
type Conversation = {
  intent?: Intent;
  agency?: AssistantAgency;
  department?: AssistantDepartment;
  category?: AssistantCategory;
  detail?: string;
  transcript: string;
  pending: Pending;
  warehouseInput: string;
  warehouseDrafts: WarehouseDraft[];
  plannedAgencies: AssistantAgency[];
};
type ConversationMessage = {
  id: number;
  role: "assistant" | "user";
  text: string;
};
type RecognitionEvent = {
  results: ArrayLike<{ 0: { transcript: string }; isFinal: boolean }>;
};
type RecognitionInstance = {
  lang: string;
  continuous: boolean;
  interimResults: boolean;
  maxAlternatives?: number;
  start: () => void;
  stop: () => void;
  abort?: () => void;
  onresult: ((event: RecognitionEvent) => void) | null;
  onerror: ((event: { error: string }) => void) | null;
  onend: (() => void) | null;
};
type RecognitionConstructor = new () => RecognitionInstance;
const freshConversation = (): Conversation => ({
  transcript: "",
  pending: null,
  warehouseInput: "",
  warehouseDrafts: [],
  plannedAgencies: [],
});
const intentFrom = (text: string): Intent | undefined => {
  const value = normalizeVoiceText(text);
  return /solicitud|almacen|material|articulo|equipo|pedido|requerimiento|inventario|despacho/.test(value) &&
    !/ticket/.test(value)
    ? "warehouse"
    : /ticket|falla|averia|incidencia|problema|danado|no funciona|soporte|reparar/.test(value)
      ? "ticket"
      : undefined;
};
export const wakeCommand = (phrase: string) => {
  const normalized = normalizeVoiceText(phrase).trim();
  const match = normalized.match(/^(?:hola\s+)?oficina\s+virtual(?:\s+(.+))?$/);
  return match ? { command: (match[1] || "").trim() } : null;
};

export default function AdminHeaderVoiceAssistant({
  onWarehouseCommand,
}: {
  onWarehouseCommand: (batch: VoiceWarehouseBatch) => void;
}) {
  const [open, setOpen] = useState(false),
    [armed, setArmed] = useState(false),
    [listening, setListening] = useState(false),
    [speaking, setSpeaking] = useState(false),
    [busy, setBusy] = useState(false),
    [text, setText] = useState(""),
    [status, setStatus] = useState("Micrófono apagado."),
    [prompt, setPrompt] = useState(
      "Pulsa Activar palabra clave y di “Oficina Virtual”.",
    ),
    [messages, setMessages] = useState<ConversationMessage[]>([]),
    [inventoryFilter, setInventoryFilter] = useState(""),
    [agencyFilter, setAgencyFilter] = useState("");
  const [agencies, setAgencies] = useState<AssistantAgency[]>([]),
    [departments, setDepartments] = useState<AssistantDepartment[]>([]),
    [inventory, setInventory] = useState<AssistantInventory[]>([]),
    [catalogReady, setCatalogReady] = useState(false);
  const recognition = useRef<RecognitionInstance | null>(null),
    armedRef = useRef(false),
    openRef = useRef(false),
    speakingRef = useRef(false),
    speechRunRef = useRef(0),
    restartTimer = useRef<number | undefined>(undefined),
    retryCount = useRef(0),
    lastFinal = useRef(""),
    conversation = useRef<Conversation>(freshConversation()),
    startRef = useRef<() => void>(() => undefined),
    catalogReadyRef = useRef(false),
    catalogLoadRef = useRef<Promise<void> | null>(null);
  armedRef.current = armed;
  openRef.current = open;
  speakingRef.current = speaking;

  const loadCatalogs = (force = false) => {
    if (catalogReadyRef.current && !force) return Promise.resolve();
    if (catalogLoadRef.current) return catalogLoadRef.current;
    const task = (async () => {
    setCatalogReady(false);
    catalogReadyRef.current = false;
    try {
      const [a, c, i] = await Promise.all([
        fetch("/api/tickets/agencies", {
          cache: "no-store",
          credentials: "same-origin",
        }),
        fetch("/api/support/catalog", {
          cache: "no-store",
          credentials: "same-origin",
        }),
        fetch("/api/warehouse/fulfillment/inventory", {
          cache: "no-store",
          credentials: "same-origin",
        }),
      ]);
      if (!a.ok || !c.ok || !i.ok)
        throw new Error("No pude consultar los catálogos del sistema.");
      const agencyData = await a.json(),
        catalogData = await c.json(),
        inventoryData = await i.json(),
        agencyRows = Array.isArray(agencyData.agencies)
          ? (agencyData.agencies as AssistantAgency[])
          : [],
        uniqueAgencies = [
          ...new Map(agencyRows.map((item) => [item.id, item])).values(),
        ];
      if (
        agencyData.catalogComplete === false ||
        Number(agencyData.totalActive || 0) !== uniqueAgencies.length
      )
        throw new Error(
          `El catálogo de agencias está incompleto: recibí ${uniqueAgencies.length} de ${Number(agencyData.totalActive || 0)}. Pulsa Actualizar datos.`,
        );
      setAgencies(uniqueAgencies);
      setDepartments(
        (catalogData.departments || []).filter((item: AssistantDepartment) =>
          ["TECHNOLOGY", "GENERAL_SERVICES", "HUMAN_RESOURCES"].includes(
            item.code,
          ),
        ),
      );
      setInventory(Array.isArray(inventoryData) ? inventoryData : []);
      setStatus(
        `Catálogos verificados: ${uniqueAgencies.length} agencias activas cargadas.`,
      );
      setCatalogReady(true);
      catalogReadyRef.current = true;
    } catch (reason) {
      setStatus(
        reason instanceof Error
          ? reason.message
          : "No pude cargar los datos del sistema.",
      );
    } finally {
      catalogLoadRef.current = null;
    }
    })();
    catalogLoadRef.current = task;
    return task;
  };
  const stopRecognition = () => {
    window.clearTimeout(restartTimer.current);
    recognition.current?.abort?.();
    recognition.current?.stop();
    setListening(false);
  };
  const scheduleListening = (delay = 350) => {
    if (!armedRef.current || speakingRef.current || document.hidden) return;
    window.clearTimeout(restartTimer.current);
    restartTimer.current = window.setTimeout(() => startRef.current(), delay);
  };
  const speak = (message: string, resume = true) => {
    setPrompt(message);
    setMessages((current) =>
      [
        ...current,
        {
          id: Date.now() + Math.random(),
          role: "assistant" as const,
          text: message,
        },
      ].slice(-20),
    );
    setStatus("Robot respondiendo…");
    stopRecognition();
    const synthesizer = (
      window as unknown as { speechSynthesis?: SpeechSynthesis }
    ).speechSynthesis;
    if (!synthesizer) {
      if (resume) {
        setStatus("Ahora te escucho…");
        scheduleListening();
      }
      return;
    }
    const speechRun = ++speechRunRef.current;
    synthesizer.cancel();
    setSpeaking(true);
    speakingRef.current = true;
    const utterance = new SpeechSynthesisUtterance(spokenNaturalText(message));
    utterance.lang = "es-DO";
    const voices = synthesizer.getVoices();
    utterance.voice =
      voices.find((voice) => voice.lang.toLowerCase() === "es-do") ||
      voices.find((voice) => /^es-(419|mx|us)$/i.test(voice.lang)) ||
      voices.find((voice) => voice.lang.toLowerCase().startsWith("es")) ||
      null;
    utterance.rate = 0.94;
    utterance.pitch = 1;
    utterance.volume = 1;
    utterance.onend = () => {
      if (speechRun !== speechRunRef.current) return;
      setSpeaking(false);
      speakingRef.current = false;
      if (resume) {
        setStatus("Ahora te escucho…");
        scheduleListening(250);
      }
    };
    utterance.onerror = utterance.onend;
    synthesizer.speak(utterance);
  };
  const resetConversation = (close = false) => {
    conversation.current = freshConversation();
    setText("");
    setMessages([]);
    setInventoryFilter("");
    setAgencyFilter("");
    lastFinal.current = "";
    if (close) setOpen(false);
  };
  const askAgency = (options: AssistantAgency[] = []) => {
    setAgencyFilter("");
    conversation.current.pending = options.length
      ? { kind: "agency", options }
      : null;
    speak(
      options.length
        ? "Encontré agencias relacionadas y te muestro el catálogo completo disponible. Busca o dime el número, el código o el nombre correcto."
        : "¿Para cuál agencia? Dime el código, el nombre y, si lo sabes, el grupo.",
    );
  };
  const showAgencyLookup = (answer: string) => {
    const normalized = normalizeVoiceText(answer);
    if (!/\b(?:muestra|muestrame|busca|encuentra|ensena|ensename|lista)\b.*\bagencias?\b/.test(normalized))
      return false;
    const requested = spokenQuantityValues(normalized)[0] || 10;
    const limit = Math.min(50, Math.max(1, requested));
    const options = searchAgencyOptions(answer, agencies, limit);
    setAgencyFilter("");
    if (!options.length) {
      speak("No encontré agencias relacionadas con esa búsqueda. Dime otro nombre, código o grupo.");
      return true;
    }
    conversation.current.pending = { kind: "agencyLookup", options };
    speak(
      `Encontré ${options.length} ${options.length === 1 ? "agencia relacionada" : "agencias relacionadas"}. Te las muestro en pantalla con su código, nombre y grupo. Puedes pedirme una por su nombre o continuar con un ticket o una solicitud a Almacén.`,
    );
    return true;
  };
  const showInventoryLookup = (answer: string) => {
    const normalized = normalizeVoiceText(answer);
    if (!/\b(?:muestra|muestrame|busca|encuentra|ensena|ensename|lista)\b.*\b(?:articulos?|equipos?|materiales?|inventario)\b/.test(normalized))
      return false;
    const requested = spokenQuantityValues(normalized)[0] || 10;
    const limit = Math.min(50, Math.max(1, requested));
    const options = searchInventoryOptions(answer, inventory, limit);
    setInventoryFilter("");
    if (!options.length) {
      speak("No encontré artículos relacionados con esa búsqueda. Dime otro nombre o código del inventario.");
      return true;
    }
    conversation.current.pending = { kind: "inventoryLookup", options };
    speak(`Encontré ${options.length} ${options.length === 1 ? "artículo relacionado" : "artículos relacionados"}. Te los muestro con su código y existencia. Puedes elegir uno hablando normalmente o pedirme que prepare la solicitud.`);
    return true;
  };
  const showDepartmentLookup = (answer: string) => {
    const normalized = normalizeVoiceText(answer);
    if (!/\b(?:muestra|muestrame|busca|encuentra|ensena|ensename|lista)\b/.test(normalized))
      return false;
    if (/\bdepartamentos?\b/.test(normalized)) {
      conversation.current.pending = { kind: "department", options: departments };
      speak("Estos son los tres departamentos disponibles para tickets: Tecnología, Servicios Generales y Recursos Humanos.");
      return true;
    }
    if (/\b(?:fallas?|averias?|incidencias?)\b/.test(normalized)) {
      const resolution = resolveDepartmentStrict(answer, departments);
      if (resolution.kind !== "unique" || !resolution.value) {
        conversation.current.pending = { kind: "department", options: departments };
        speak("¿De cuál departamento quieres consultar las fallas: Tecnología, Servicios Generales o Recursos Humanos?");
        return true;
      }
      conversation.current.department = resolution.value;
      const categories = resolution.value.categories.filter((item) => item.isActive !== false);
      conversation.current.pending = { kind: "category", options: categories };
      speak(`Te muestro las ${categories.length} fallas activas de ${resolution.value.name}. Puedes decir el nombre de cualquiera para continuar.`);
      return true;
    }
    return false;
  };
  const createTicket = async () => {
    const context = conversation.current;
    if (!context.agency || !context.department || !context.category) return;
    setBusy(true);
    speak(
      `Todos los datos están completos. Estoy creando el ticket de ${context.category.name} para la ${spokenAgencyLabel(context.agency)}.`,
      false,
    );
    try {
      const description = [
        `Creado por Oficina Virtual mediante conversación de voz.`,
        context.transcript,
        context.detail
          ? `${context.category.detailLabel || "Detalle"}: ${context.detail}`
          : "",
      ]
        .filter(Boolean)
        .join("\n");
      const response = await fetch("/api/tickets", {
        method: "POST",
        credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          agencyId: context.agency.id,
          category: context.category.code,
          assignedDepartment: context.department.code,
          assignedTeam:
            context.department.code === "TECHNOLOGY"
              ? "TECHNICAL_FAILURE"
              : null,
          priority: context.category.defaultPriority || "MEDIUM",
          subject: context.category.name,
          description,
          ticketType: "INTERNAL",
        }),
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok)
        throw new Error(
          body.error || body.message || "No pude crear el ticket.",
        );
      resetConversation();
      speak(
        `Listo. Creé el ticket número ${body.ticketNumber}, de ${context.category.name}, para la ${spokenAgencyLabel(context.agency)}.`,
      );
      window.dispatchEvent(new Event("support-live-refresh"));
    } catch (reason) {
      speak(
        reason instanceof Error
          ? reason.message
          : "No pude crear el ticket. No se registró nada.",
      );
    } finally {
      setBusy(false);
    }
  };
  const continueTicket = async (_answer: string) => {
    const context = conversation.current;
    if (!context.department) {
      const resolution = resolveDepartmentStrict(
        context.transcript,
        departments,
      );
      if (resolution.kind === "ambiguous") {
        context.pending = { kind: "department", options: resolution.options };
        speak(
          `Escuché más de un área. ¿Cuál es correcta? ${spokenOptions(resolution.options, (item) => item.name)}.`,
        );
        return;
      }
      context.department = resolution.value;
      if (!context.department) {
        const inferred = departments
          .map((department) => ({
            department,
            category: resolveCategoryStrict(context.transcript, department),
          }))
          .filter((item) => item.category.kind === "unique");
        if (inferred.length === 1) {
          context.department = inferred[0].department;
          context.category = inferred[0].category.value;
        }
      }
      if (!context.department) {
        context.pending = { kind: "department", options: departments };
        speak(
          "¿En cuál departamento quieres crear el ticket: Tecnología, Servicios Generales o Recursos Humanos? Puedes decir el nombre completo o describir la falla.",
        );
        return;
      }
    }
    if (!context.category) {
      const resolution = resolveCategoryStrict(
          context.transcript,
          context.department,
        ),
        available = context.department.categories.filter(
          (item) => item.isActive !== false,
        );
      if (resolution.kind === "ambiguous") {
        context.pending = { kind: "category", options: available };
        speak(
          `Hay varias fallas posibles en ${context.department.name}. Te muestro todas las fallas activas en pantalla para que elijas una.`,
        );
        return;
      }
      if (resolution.kind === "missing") {
        context.pending = { kind: "category", options: available };
        speak(
          `¿Qué falla del área de ${context.department.name} quieres reportar? Te muestro todas las fallas activas en pantalla.`,
        );
        return;
      }
      context.category = resolution.value;
    }
    const category = context.category;
    if (!category) return;
    if (category.requiresDetail && !context.detail) {
      const options = category.options || [];
      const selected = options.find((option) =>
        normalizeVoiceText(context.transcript).includes(
          normalizeVoiceText(option),
        ),
      );
      if (selected) context.detail = selected;
      else {
        context.pending = { kind: "detail", category };
        speak(
          options.length
            ? `${category.detailLabel || "Indica el detalle"}. Las opciones son: ${options.join("; ")}.`
            : `${category.detailLabel || "Dime el detalle específico de la falla"}.`,
        );
        return;
      }
    }
    await createTicket();
  };
  const askWarehouseItems = () => {
    const context = conversation.current;
    if (!context.agency) return;
    context.pending = { kind: "warehouseItems" };
    speak("Ahora dime los artículos y las cantidades para esta agencia.");
  };
  const submitWarehouseBatch = () => {
    const context = conversation.current;
    if (!context.warehouseDrafts.length) {
      speak("Todavía no tengo ninguna agencia con artículos confirmados.");
      return;
    }
    const batch: VoiceWarehouseBatch = {
      transcript: context.transcript,
      targets: context.warehouseDrafts.map((draft) => ({
        agencyId: draft.agency.id,
        agencyCode: draft.agency.codigo,
        agencyName: draft.agency.terminal,
        agencyGroup: draft.agency.grupo,
        lines: draft.lines,
      })),
    };
    setBusy(true);
    speak(
      `Datos completos. Crearé ${batch.targets.length === 1 ? "una solicitud" : `${batch.targets.length} solicitudes, una para cada agencia`}.`,
      false,
    );
    onWarehouseCommand(batch);
    resetConversation(true);
    setBusy(false);
  };
  const continueWarehouse = async (answer: string, showSuggestions = false) => {
    const context = conversation.current;
    if (!context.agency) {
      const used = new Set(
        context.warehouseDrafts.map((item) => item.agency.id),
      );
      const mentions = resolveAgencyMentions(
        context.transcript,
        agencies,
      ).filter((item) => !used.has(item.id));
      if (mentions.length > 1 && !context.plannedAgencies.length)
        context.plannedAgencies = mentions;
      const planned = context.plannedAgencies.find(
        (item) => !used.has(item.id),
      );
      if (planned) context.agency = planned;
      else {
        const resolution = resolveAgencyStrict(
          answer || context.transcript,
          agencies,
        );
        if (resolution.kind === "ambiguous") {
          askAgency(resolution.options);
          return;
        }
        if (resolution.kind === "missing") {
          askAgency(
            suggestAgencyOptions(
              answer || context.transcript,
              agencies,
              agencies.length,
            ),
          );
          return;
        }
        context.agency = resolution.value;
      }
    }
    if (!context.warehouseInput) context.warehouseInput = answer;
    const result = resolveInventoryStrict(context.warehouseInput, inventory);
    if (result.ambiguous.length) {
      setInventoryFilter("");
      context.pending = { kind: "inventory", options: result.ambiguous };
      speak(
        "Encontré varios artículos parecidos para esta agencia. Te los muestro en pantalla; elige con un clic o dime el número, el nombre o el código correcto.",
      );
      return;
    }
    if (result.missingQuantity.length) {
      context.pending = { kind: "quantity", items: result.missingQuantity };
      speak(
        `¿Qué cantidad necesitas de ${result.missingQuantity.map((item) => item.name).join(" y ")}?`,
      );
      return;
    }
    if (!result.lines.length) {
      if (showSuggestions && !inventory.length) {
        speak(
          "No pude cargar el inventario. Pulsa Actualizar datos y vuelve a decirme el artículo.",
        );
        return;
      }
      if (showSuggestions) {
        setInventoryFilter("");
        context.pending = {
          kind: "inventory",
          options: suggestInventoryOptions(
            context.warehouseInput,
            inventory,
            inventory.length,
          ),
        };
        speak(
          "No encuentro el artículo para esta agencia. Te muestro el inventario en pantalla; puedes buscarlo, seleccionarlo con un clic o decirme su nombre o código.",
        );
        return;
      }
      askWarehouseItems();
      return;
    }
    if (
      result.lines.some(
        (line) =>
          !Number.isInteger(line.quantity) ||
          line.quantity < 1 ||
          line.quantity > 10000,
      ) ||
      result.lines.reduce((sum, line) => sum + line.quantity, 0) > 10000
    ) {
      context.pending = { kind: "warehouseItems" };
      speak(
        "Las cantidades no son válidas. Dime cantidades enteras entre 1 y 10 mil, con un máximo total de 10 mil.",
      );
      return;
    }
    const agency = context.agency!;
    const existingDraft = context.warehouseDrafts.find(
      (item) => item.agency.id === agency.id,
    );
    const combined = new Map<
      string,
      { productId: string; name: string; quantity: number }
    >();
    for (const line of existingDraft?.lines || [])
      combined.set(line.productId, { ...line });
    for (const line of result.lines) {
      const current = combined.get(line.item.productId);
      combined.set(line.item.productId, {
        productId: line.item.productId,
        name: line.item.name,
        quantity: (current?.quantity || 0) + line.quantity,
      });
    }
    const combinedLines = [...combined.values()];
    if (
      combinedLines.length > 30 ||
      combinedLines.some((line) => line.quantity > 10000) ||
      combinedLines.reduce((total, line) => total + line.quantity, 0) > 10000
    ) {
      context.warehouseInput = "";
      context.pending = { kind: "warehouseNext" };
      speak(
        "No agregué ese artículo porque una solicitud admite hasta 30 artículos y un máximo total de 10 mil unidades por agencia.",
      );
      return;
    }
    const draft: WarehouseDraft = {
      agency,
      lines: combinedLines,
    };
    context.warehouseDrafts = [
      ...context.warehouseDrafts.filter((item) => item.agency.id !== agency.id),
      draft,
    ];
    context.warehouseInput = "";
    context.pending = { kind: "warehouseNext" };
    speak(
      `Agregué ${result.lines.map((line) => `${line.quantity} ${line.item.name}`).join(", ")}. Esta solicitud ahora tiene ${draft.lines.length} ${draft.lines.length === 1 ? "artículo" : "artículos"}. Puedes decir “agrega otro artículo”, continuar con otra agencia o decir “crear solicitudes”.`,
    );
  };
  const processAnswer = async (raw: string, visibleAnswer = raw) => {
    if (busy) return;
    const answer = raw.trim();
    if (!answer) return;
    setText("");
    setMessages((current) =>
      [
        ...current,
        {
          id: Date.now() + Math.random(),
          role: "user" as const,
          text: visibleAnswer.trim() || answer,
        },
      ].slice(-20),
    );
    const context = conversation.current;
    const pending = context.pending;
    context.pending = null;
    let agencySelectedNow = false;
    if (pending?.kind === "warehouseNext") {
      const normalized = normalizeVoiceText(answer),
        finishAll =
          /crear|creala|crealas|hazla|hazlas|procesar|procesala|enviar|finalizar|confirmo|ya termine|termine con todas|termine todas|eso es todo|no hay otra/.test(
            normalized,
          ) || /^(termine|terminado|listo)$/.test(normalized),
        continueOthers =
          /otra|otras|siguiente|continuemos|primera agencia|segunda agencia|tercera agencia/.test(
            normalized,
          ),
        addMore =
          /agrega|anade|incluye|suma|otro articulo|otros articulos|mas articulo|tambien necesito|me falta/.test(
            normalized,
          );
      context.transcript = `${context.transcript} ${answer}`.trim();
      if (finishAll) {
        submitWarehouseBatch();
        return;
      }
      if (addMore) {
        context.warehouseInput = answer;
        await continueWarehouse(answer, true);
        return;
      }
      if (continueOthers) {
        const used = new Set(
          context.warehouseDrafts.map((item) => item.agency.id),
        );
        context.agency = context.plannedAgencies.find(
          (item) => !used.has(item.id),
        );
        context.warehouseInput = "";
        if (context.agency) askWarehouseItems();
        else askAgency();
        return;
      }
      context.pending = pending;
      speak(
        "Puedes decir “agrega otro artículo”, “continuemos con otra agencia” o “crear solicitudes”.",
      );
      return;
    }
    if (pending?.kind === "warehouseItems") context.warehouseInput = answer;
    else if (pending?.kind === "agencyLookup") {
      const selected = selectSpokenOption(
        answer,
        pending.options,
        (item) => `${item.codigo} ${item.terminal} ${item.grupo}`,
      );
      if (!selected) {
        context.pending = pending;
        speak("Dime el nombre, código o grupo de una de las agencias mostradas, o indícame si quieres crear un ticket o una solicitud.");
        return;
      }
      context.agency = selected;
      context.transcript = `${context.transcript} ${answer}`.trim();
      context.intent ??= intentFrom(context.transcript);
      if (context.intent === "ticket") {
        await continueTicket(answer);
        return;
      }
      if (context.intent === "warehouse") {
        askWarehouseItems();
        return;
      }
      speak(`Seleccioné la ${spokenAgencyLabel(selected, true)}. Ahora dime si quieres crear un ticket o una solicitud a Almacén.`);
      return;
    }
    else if (pending?.kind === "agency") {
      const selected = selectSpokenOption(
        answer,
        pending.options,
        (item) => `${item.codigo} ${item.terminal} ${item.grupo}`,
      );
      if (!selected) {
        askAgency(pending.options);
        return;
      }
      context.agency = selected;
      agencySelectedNow = true;
    } else if (pending?.kind === "department") {
      const selected = selectSpokenOption(
        answer,
        pending.options,
        (item) => item.name,
      );
      if (!selected) {
        context.pending = pending;
        speak(
          `No pude identificar el área. Elige ${spokenOptions(pending.options, (item) => item.name)}.`,
        );
        return;
      }
      context.department = selected;
    } else if (pending?.kind === "category") {
      const selected = selectSpokenOption(
        answer,
        pending.options,
        (item) => item.name,
      );
      if (!selected) {
        context.pending = pending;
        speak(
          `No pude identificar la falla. Elige ${spokenOptions(pending.options, (item) => item.name)}.`,
        );
        return;
      }
      context.category = selected;
    } else if (pending?.kind === "detail") {
      const options = pending.category.options || [];
      if (options.length) {
        const selected = selectSpokenOption(answer, options, (item) => item);
        if (!selected) {
          context.pending = pending;
          speak(
            `Elige una opción válida: ${spokenOptions(options, (item) => item)}.`,
          );
          return;
        }
        context.detail = selected;
      } else if (answer.length < 2) {
        context.pending = pending;
        speak("Dime un poco más de detalle sobre la falla.");
        return;
      } else context.detail = answer;
    } else if (pending?.kind === "inventory") {
      const selected = selectSpokenOption(
        answer,
        pending.options,
        (item) => `${item.name} ${item.productId}`,
      );
      if (!selected) {
        context.pending = pending;
        speak(
          "Todavía no puedo saber cuál artículo elegiste. Selecciónalo directamente en pantalla o dime el número, el nombre o el código exacto.",
        );
        return;
      }
      setInventoryFilter("");
      if (context.intent === "warehouse")
        context.warehouseInput =
          `${context.warehouseInput} código ${selected.productId}`.trim();
      else context.transcript += ` código ${selected.productId}`;
    } else if (pending?.kind === "inventoryLookup") {
      const selected = selectSpokenOption(
        answer,
        pending.options,
        (item) => `${item.name} ${item.productId}`,
      );
      if (!selected) {
        context.pending = pending;
        speak("Dime el nombre o código de uno de los artículos mostrados. También puedes decir para cuál agencia quieres solicitarlo.");
        return;
      }
      context.warehouseInput = `código ${selected.productId}`;
      context.transcript = `${context.transcript} ${answer}`.trim();
      context.intent ??= intentFrom(context.transcript);
      if (context.intent === "warehouse") {
        if (!context.agency) {
          askAgency();
          return;
        }
        await continueWarehouse(context.warehouseInput);
        return;
      }
      context.pending = { kind: "quantity", items: [selected] };
      speak(`Seleccioné ${selected.name}, código ${selected.productId}. ¿Qué cantidad necesitas?`);
      return;
    } else if (pending?.kind === "quantity") {
      const quantities = spokenQuantityValues(answer);
      if (
        !quantities.length ||
        quantities.some((value) => value < 1 || value > 10000)
      ) {
        context.pending = pending;
        speak("Dime una cantidad entera válida entre 1 y 10 mil.");
        return;
      }
      if (
        pending.items.length > 1 &&
        quantities.length !== pending.items.length
      ) {
        context.pending = pending;
        speak(
          `Necesito una cantidad para cada artículo, en este orden: ${pending.items.map((item) => item.name).join("; ")}.`,
        );
        return;
      }
      const quantityText = pending.items
        .map(
          (item, index) => `${quantities[index] || quantities[0]} ${item.name}`,
        )
        .join(" ");
      if (context.intent === "warehouse")
        context.warehouseInput =
          `${context.warehouseInput} ${quantityText}`.trim();
      else context.transcript += ` ${quantityText}`;
    }
    if (answer !== context.transcript)
      context.transcript = `${context.transcript} ${answer}`.trim();
    if (
      !pending &&
      (showAgencyLookup(answer) ||
        showInventoryLookup(answer) ||
        showDepartmentLookup(answer))
    )
      return;
    context.intent ??= intentFrom(context.transcript);
    if (!context.intent) {
      speak("¿Quieres crear un ticket o una solicitud a Almacén?");
      return;
    }
    if (context.intent === "ticket") {
      if (!context.agency) {
        const resolution = resolveAgencyStrict(context.transcript, agencies);
        if (resolution.kind === "ambiguous") {
          askAgency(resolution.options);
          return;
        }
        if (resolution.kind === "missing") {
          askAgency(suggestAgencyOptions(answer, agencies, agencies.length));
          return;
        }
        context.agency = resolution.value;
      }
      await continueTicket(answer);
      return;
    }
    if (agencySelectedNow) {
      if (context.warehouseInput)
        await continueWarehouse(context.warehouseInput, true);
      else askWarehouseItems();
      return;
    }
    await continueWarehouse(
      pending?.kind === "warehouseItems" ||
        pending?.kind === "inventory" ||
        pending?.kind === "quantity"
        ? context.warehouseInput
        : answer,
      pending?.kind === "warehouseItems",
    );
  };
  const start = () => {
    if (busy || speakingRef.current) return;
    // Los catálogos pesados se descargan únicamente cuando el usuario activa
    // el robot; así no bloquean el inicio de sesión ni la navegación móvil.
    void loadCatalogs();
    const Recognition =
      (
        window as unknown as {
          SpeechRecognition?: RecognitionConstructor;
          webkitSpeechRecognition?: RecognitionConstructor;
        }
      ).SpeechRecognition ||
      (
        window as unknown as {
          webkitSpeechRecognition?: RecognitionConstructor;
        }
      ).webkitSpeechRecognition;
    if (!Recognition) {
      setOpen(true);
      setStatus(
        "Este navegador no admite reconocimiento de voz. Puedes escribir el comando y pulsar Procesar.",
      );
      return;
    }
    window.clearTimeout(restartTimer.current);
    recognition.current?.abort?.();
    const instance = new Recognition();
    recognition.current = instance;
    instance.lang = "es-DO";
    instance.continuous = false;
    instance.interimResults = false;
    instance.maxAlternatives = 3;
    instance.onresult = (event) => {
      let phrase = "";
      for (let index = 0; index < event.results.length; index++)
        if (event.results[index].isFinal)
          phrase += `${event.results[index][0].transcript} `;
      phrase = phrase.trim();
      if (!phrase || speakingRef.current) return;
      if (!openRef.current) {
        const wake = wakeCommand(phrase);
        if (!wake) return;
        setOpen(true);
        openRef.current = true;
        resetConversation();
        lastFinal.current = phrase;
        if (wake.command) void processAnswer(wake.command);
        else speak("Oficina Virtual activada. ¿En qué puedo ayudarte?");
        return;
      }
      setText(phrase);
      if (phrase !== lastFinal.current) {
        lastFinal.current = phrase;
        void processAnswer(phrase);
      }
    };
    instance.onerror = (event) => {
      setListening(false);
      if (
        event.error === "not-allowed" ||
        event.error === "service-not-allowed"
      ) {
        setArmed(false);
        armedRef.current = false;
        setOpen(true);
        setStatus(
          "El navegador bloqueó el micrófono. Permite el acceso y pulsa Activar palabra clave.",
        );
        return;
      }
      if (
        ["no-speech", "aborted", "network", "audio-capture"].includes(
          event.error,
        ) &&
        !speakingRef.current
      ) {
        retryCount.current++;
        scheduleListening(Math.min(2500, 400 * retryCount.current));
      }
    };
    instance.onend = () => {
      setListening(false);
      if (armedRef.current && !speakingRef.current) scheduleListening(500);
    };
    try {
      instance.start();
      retryCount.current = 0;
      setArmed(true);
      armedRef.current = true;
      setListening(true);
      setStatus(
        openRef.current
          ? "Ahora te escucho…"
          : "En espera de “Oficina Virtual”…",
      );
    } catch {
      if (!speakingRef.current) scheduleListening(900);
    }
  };
  startRef.current = start;
  const stop = () => {
    armedRef.current = false;
    setArmed(false);
    ++speechRunRef.current;
    stopRecognition();
    window.speechSynthesis?.cancel();
    speakingRef.current = false;
    setSpeaking(false);
  };
  useEffect(() => {
    const resume = () => {
      if (armedRef.current && !speakingRef.current) scheduleListening(200);
    };
    window.addEventListener("focus", resume);
    document.addEventListener("visibilitychange", resume);
    return () => {
      stopRecognition();
      window.removeEventListener("focus", resume);
      document.removeEventListener("visibilitychange", resume);
    };
  }, []);
  const close = () => {
    resetConversation(true);
    setStatus("Di “Oficina Virtual” para activarme.");
    setPrompt("Di “Oficina Virtual” para comenzar una conversación.");
  };
  const pending = conversation.current.pending,
    agencyChoices =
      pending?.kind === "agency" || pending?.kind === "agencyLookup"
        ? pending.options
        : [],
    filteredAgencyChoices = filterAgencyCatalog(agencyFilter, agencyChoices),
    inventoryChoices =
      pending?.kind === "inventory" || pending?.kind === "inventoryLookup"
        ? pending.options
        : [],
    filteredInventoryChoices = filterInventoryCatalog(
      inventoryFilter,
      inventoryChoices,
    ),
    departmentChoices = pending?.kind === "department" ? pending.options : [],
    categoryChoices = pending?.kind === "category" ? pending.options : [],
    detailChoices =
      pending?.kind === "detail" ? pending.category.options || [] : [],
    warehouseDrafts = conversation.current.warehouseDrafts,
    activeAgency = conversation.current.agency;
  return (
    <div className="admin-header-voice">
      <button
        type="button"
        className={`notification-bell admin-voice-button${armed ? " is-armed" : ""}`}
        onClick={() => {
          if (!armed) start();
          else setOpen((value) => !value);
        }}
        title={
          armed
            ? "Oficina Virtual en espera de la palabra clave"
            : "Activar palabra clave Oficina Virtual"
        }
      >
        <Bot />
        {armed && <i />}
      </button>
      {open && (
        <section
          className="admin-voice-popover"
          role="dialog"
          aria-label="Asistente Oficina Virtual"
        >
          <header>
            <span>
              <Sparkles />
              <strong>Oficina Virtual</strong>
              <small>Conversación por turnos con validación del sistema</small>
            </span>
            <button type="button" onClick={close} aria-label="Cerrar">
              <X />
            </button>
          </header>
          <div className="admin-voice-state">
            <button
              type="button"
              className={listening ? "listening" : ""}
              onClick={() => {
                if (armed) stop();
                else start();
              }}
              disabled={busy}
            >
              {busy ? (
                <LoaderCircle className="spin" />
              ) : listening ? (
                <MicOff />
              ) : (
                <Mic />
              )}
              {busy
                ? "Creando…"
                : listening
                  ? speaking
                    ? "En pausa"
                    : "Escuchando"
                  : "Activar palabra clave"}
            </button>
            <p>
              {speaking
                ? "Robot respondiendo; cuando termine podrás contestar."
                : status}
            </p>
          </div>
          <div
            className={`admin-voice-catalog-state${catalogReady ? " ready" : ""}`}
          >
            <span>
              <b>
                {catalogReady
                  ? "Datos del sistema conectados"
                  : "Catálogos no disponibles"}
              </b>
              <small>
                {catalogReady
                  ? `${agencies.length} agencias · ${inventory.length} artículos`
                  : "Actualiza para consultar agencias e inventario"}
              </small>
            </span>
            <button
              type="button"
              onClick={() => void loadCatalogs(true)}
              disabled={busy}
            >
              Actualizar datos
            </button>
          </div>
          <section className="admin-voice-question" aria-live="polite">
            <Sparkles />
            <span>
              <small>Pregunta actual</small>
              <strong>{prompt}</strong>
            </span>
          </section>
          {activeAgency && (
            <section className="admin-voice-confirmed-agency">
              <small>Agencia confirmada</small>
              <strong>
                {activeAgency.codigo} · {activeAgency.terminal}
              </strong>
              <span>{activeAgency.grupo}</span>
            </section>
          )}
          {!!messages.length && (
            <section
              className="admin-voice-conversation"
              aria-label="Conversación con Oficina Virtual"
            >
              <header>
                <strong>Conversación</strong>
                <small>Preguntas del robot y respuestas reconocidas</small>
              </header>
              {messages.map((message) => (
                <article key={message.id} className={message.role}>
                  <b>{message.role === "assistant" ? <Bot /> : <Mic />}</b>
                  <span>
                    <small>
                      {message.role === "assistant"
                        ? "Oficina Virtual"
                        : "Tu respuesta"}
                    </small>
                    <p>{message.text}</p>
                  </span>
                </article>
              ))}
            </section>
          )}
          {!!agencyChoices.length && (
            <section
              className="admin-voice-agency-options admin-voice-agency-picker"
              aria-label="Opciones de agencias"
            >
              <strong>Catálogo de agencias activas</strong>
              <small>
                {agencyChoices.length} agencias disponibles. Busca por código,
                nombre o grupo.
              </small>
              <label>
                <span>Buscar agencia</span>
                <input
                  value={agencyFilter}
                  onChange={(event) => setAgencyFilter(event.target.value)}
                  placeholder="Código, nombre o grupo…"
                />
              </label>
              <div className="admin-voice-result-counter">
                <span>{agencyFilter ? "Coincidencias encontradas" : "Primeras agencias del catálogo"}</span>
                <b>{filteredAgencyChoices.length}</b>
                <small>de {agencyChoices.length}</small>
              </div>
              <div className="admin-voice-agency-results">
                {filteredAgencyChoices.map((item) => {
                  const optionNumber = agencyChoices.indexOf(item) + 1;
                  return (
                    <button
                      type="button"
                      key={item.id}
                      onClick={() =>
                        void processAnswer(
                          `${item.codigo} ${item.terminal} ${item.grupo}`,
                          `Agencia ${item.codigo} · ${item.terminal} · ${item.grupo}`,
                        )
                      }
                    >
                      <b>{optionNumber}</b>
                      <span>
                        <strong>
                          {item.codigo} · {item.terminal}
                        </strong>
                        <small>{item.grupo}</small>
                      </span>
                    </button>
                  );
                })}
                {!filteredAgencyChoices.length && (
                  <p>
                    No hay agencias con ese texto. Prueba otro código, nombre o
                    grupo.
                  </p>
                )}
              </div>
            </section>
          )}
          {!!departmentChoices.length && (
            <section
              className="admin-voice-choice-panel"
              aria-label="Áreas disponibles"
            >
              <strong>Áreas disponibles</strong>
              {departmentChoices.map((item, index) => (
                <button
                  type="button"
                  key={item.code}
                  onClick={() =>
                    void processAnswer(`opción ${index + 1}`, item.name)
                  }
                >
                  <b>{index + 1}</b>
                  <span>
                    <strong>{item.name}</strong>
                  </span>
                </button>
              ))}
            </section>
          )}
          {!!categoryChoices.length && (
            <section
              className="admin-voice-choice-panel"
              aria-label="Fallas activas"
            >
              <strong>Todas las fallas activas</strong>
              <small>Di el número o el nombre de la falla.</small>
              {categoryChoices.map((item, index) => (
                <button
                  type="button"
                  key={item.code}
                  onClick={() =>
                    void processAnswer(`opción ${index + 1}`, item.name)
                  }
                >
                  <b>{index + 1}</b>
                  <span>
                    <strong>{item.name}</strong>
                  </span>
                </button>
              ))}
            </section>
          )}
          {!!detailChoices.length && (
            <section
              className="admin-voice-choice-panel"
              aria-label="Detalles disponibles"
            >
              <strong>Detalles disponibles</strong>
              {detailChoices.map((item, index) => (
                <button
                  type="button"
                  key={item}
                  onClick={() =>
                    void processAnswer(`opción ${index + 1}`, item)
                  }
                >
                  <b>{index + 1}</b>
                  <span>
                    <strong>{item}</strong>
                  </span>
                </button>
              ))}
            </section>
          )}
          {!!inventoryChoices.length && (
            <section
              className="admin-voice-choice-panel admin-voice-inventory-picker"
              aria-label="Opciones de artículos"
            >
              <strong>Selecciona el artículo del inventario</strong>
              <small>
                Busca por nombre o código. También puedes decir el número
                mostrado.
              </small>
              <label>
                <span>Buscar artículo</span>
                <input
                  value={inventoryFilter}
                  onChange={(event) => setInventoryFilter(event.target.value)}
                  placeholder="Escribe el nombre o código…"
                />
              </label>
              <div className="admin-voice-result-counter">
                <span>{inventoryFilter ? "Coincidencias encontradas" : "Primeros artículos del catálogo"}</span>
                <b>{filteredInventoryChoices.length}</b>
                <small>de {inventoryChoices.length}</small>
              </div>
              <div className="admin-voice-inventory-results">
                {filteredInventoryChoices.map((item) => {
                  const optionNumber = inventoryChoices.indexOf(item) + 1;
                  return (
                    <button
                      type="button"
                      key={item.productId}
                      onClick={() =>
                        void processAnswer(
                          `${item.name} ${item.productId}`,
                          `${item.name} · código ${item.productId}`,
                        )
                      }
                    >
                      <b>{optionNumber}</b>
                      <span>
                        <strong>{item.name}</strong>
                        <small>
                          Código {item.productId}
                          {item.group ? ` · ${item.group}` : ""}
                          {Number.isFinite(item.available)
                            ? ` · Disponible: ${item.available}`
                            : ""}
                        </small>
                      </span>
                    </button>
                  );
                })}
                {!filteredInventoryChoices.length && (
                  <p>
                    No hay artículos con ese texto. Prueba otro nombre o código.
                  </p>
                )}
              </div>
            </section>
          )}
          {pending?.kind === "quantity" && (
            <section
              className="admin-voice-quick-actions"
              aria-label="Cantidades sugeridas"
            >
              <strong>Selecciona o di una cantidad</strong>
              {[1, 2, 3, 5, 10].map((quantity) => (
                <button
                  type="button"
                  key={quantity}
                  onClick={() =>
                    void processAnswer(String(quantity), `Cantidad ${quantity}`)
                  }
                >
                  {quantity}
                </button>
              ))}
            </section>
          )}
          {pending?.kind === "warehouseNext" && (
            <section
              className="admin-voice-next-actions"
              aria-label="Continuar solicitud"
            >
              <button
                type="button"
                onClick={() => void processAnswer("agrega otro artículo")}
              >
                Agregar más artículos
              </button>
              <button
                type="button"
                onClick={() =>
                  void processAnswer("continuemos con otra agencia")
                }
              >
                Continuar con otra agencia
              </button>
              <button
                type="button"
                onClick={() => void processAnswer("crear solicitudes")}
              >
                Crear solicitudes
              </button>
            </section>
          )}
          {!!warehouseDrafts.length && (
            <section
              className="admin-voice-warehouse-summary"
              aria-label="Agencias confirmadas"
            >
              <strong>Solicitud por agencias</strong>
              {warehouseDrafts.map((item) => (
                <span key={item.agency.id}>
                  <b>
                    {item.agency.codigo} · {item.agency.terminal}
                  </b>
                  <small>
                    {item.lines
                      .map((line) => `${line.quantity} ${line.name}`)
                      .join(" · ")}
                  </small>
                </span>
              ))}
            </section>
          )}
          <div className="admin-voice-response-label">
            <strong>Tu respuesta</strong>
            <small>Habla o escribe solamente la respuesta actual.</small>
          </div>
          <textarea
            value={text}
            onChange={(event) => setText(event.target.value)}
            placeholder="Responde por voz o escribe aquí…"
          />
          <div className="admin-voice-actions">
            <button
              type="button"
              disabled={busy || !text.trim() || !catalogReady}
              onClick={() => void processAnswer(text)}
            >
              <Send />
              Procesar
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={() => {
                resetConversation();
                speak("Conversación reiniciada. ¿En qué puedo ayudarte?");
              }}
            >
              <RotateCcw />
              Reiniciar
            </button>
          </div>
        </section>
      )}
    </div>
  );
}
