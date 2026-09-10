import { useEffect, useMemo, useRef, useState } from "react";
import {
  AlertTriangle,
  Building2,
  Camera,
  Check,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
  Clock3,
  ExternalLink,
  LoaderCircle,
  MapPin,
  Navigation,
  Satellite,
  ShieldCheck,
  Sparkles,
  UserRound,
} from "lucide-react";
import { GEO_SOURCE_BROWSER } from "@/lib/constants";

async function fetchWithTimeout(input: RequestInfo | URL, init: RequestInit, timeoutMs: number) {
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), timeoutMs);
  try { return await fetch(input, { ...init, signal: controller.signal }); }
  finally { window.clearTimeout(timeout); }
}

type Agency = {
  id: string;
  codigo: string;
  terminal: string;
  expectedLatitude?: number | null;
  expectedLongitude?: number | null;
};

type Geo = {
  latitude: number;
  longitude: number;
  accuracyMeters: number;
  capturedAt: string;
  source: string;
};

type PhotoGeo = { latitude: number; longitude: number; accuracy: number; capturedAt: string };
type PhotoItem = { file: File | null; geo: PhotoGeo | null; preview: string };
type Answers = { painted: string; razaSticker: string; realSticker: string; lotekaRemoved: string; damageFound: string; printerMaintained: string; inverterPresent: string; batteryPresent: string };
const damageOptions = [
  { value: "WIRING", label: "Fallas de cableado", priority: "Alta" },
  { value: "SCREENS", label: "Pantallas o monitores", priority: "Alta" },
  { value: "CONNECTIVITY", label: "Conectividad", priority: "Crítica" },
  { value: "GENERAL_PAINTING", label: "Pintura", priority: "Baja" },
  { value: "GENERAL_INVERTER", label: "Inversor", priority: "Alta" },
  { value: "GENERAL_BATTERIES", label: "Baterías", priority: "Alta" },
  { value: "GENERAL_RESTRUCTURING", label: "Reestructuración física / Sheetrock", priority: "Media" },
  { value: "GENERAL_LIGHTING", label: "Iluminación", priority: "Media" },
  { value: "GENERAL_METALWORK", label: "Herrería", priority: "Media" },
  { value: "GENERAL_GENERATOR_REQUEST", label: "Requisición planta", priority: "Alta" },
  { value: "GENERAL_ELECTRICAL", label: "Avería eléctrica", priority: "Crítica" },
  { value: "GENERAL_SHUTTER", label: "Falla en el shutter", priority: "Alta" },
  { value: "GENERAL_OTHER", label: "Otro servicio general", priority: "Media" },
];
type Address = { direccion: string; sector: string; municipio: string; provincia: string; displayName?: string };
type AddressResponse = Partial<Address> & { complete?: boolean; source?: string };
type Result = {
  profileId: string;
  uploadToken: string;
  geolocation?: Geo;
  address?: Address;
  distanceMeters?: number | null;
  photosUploaded?: number;
  submittedAt?: string;
  locationSpreadMeters?: number;
  bestPhotoTitle?: string;
};

const PHOTO_MATCH_RADIUS_METERS = 75;
const PHOTO_TARGET_ACCURACY_METERS = 12;
const PHOTO_MAX_ACCURACY_METERS = 50;
const PHOTO_MIN_SAMPLES = 3;

const evidence = [
  { key: "frontal", action: "Acción 1", title: "Foto frontal de la banca", help: "Incluye toldo, mostrador y estructura física completa." },
  { key: "operativa", action: "Acción 2", title: "Foto pantalla de Raza", help: "El monitor operativo debe verse claro e íntegro." },
  { key: "entorno", action: "Acción 3", title: "Foto Rapidita Extraordinaria", help: "Certifica su estado físico y visual en el lugar." },
] as const;

const questions: Array<{ key: keyof Answers; title: string; help: string; allowNotApplicable?: boolean }> = [
  { key: "painted", title: "¿La agencia fue pintada?", help: "Valida paredes internas y externas con los colores oficiales." },
  { key: "razaSticker", title: "¿Fue colocado el sticker de raza?", help: "Inspecciona el mostrador y los puntos de contacto visual." },
  { key: "realSticker", title: "¿Fue colocado el sticker de Real?", help: "Comprueba colocación, visibilidad y limpieza del vinil." },
  { key: "lotekaRemoved", title: "¿Fue retirada toda la publicidad de Loteka?", help: "Confirma que no exista material antiguo o no autorizado." },
  { key: "damageFound", title: "¿Hubo alguna avería durante el levantamiento?", help: "Reporta y clasifica fallas de Tecnología o Servicios Generales." },
  { key: "printerMaintained", title: "¿Le dieron mantenimiento a la impresora?", help: "Confirma si la impresora recibió limpieza, revisión y mantenimiento preventivo.", allowNotApplicable: true },
];

const equipmentQuestions = [
  { key: "inverterPresent", photoKey: "inversor", title: "¿Tiene inversor?", photoTitle: "Fotografía del inversor", help: "Selecciona Sí para abrir la cámara y registrar el inversor con GPS, precisión y hora." },
  { key: "batteryPresent", photoKey: "bateria", title: "¿Tiene batería?", photoTitle: "Fotografía de la batería", help: "Selecciona Sí para abrir la cámara y registrar la batería con GPS, precisión y hora." },
] as const;

function haversine(lat1: number, lon1: number, lat2: number, lon2: number) {
  const radius = 6_371_000;
  const p = Math.PI / 180;
  const dLat = (lat2 - lat1) * p;
  const dLon = (lon2 - lon1) * p;
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(lat1 * p) * Math.cos(lat2 * p) * Math.sin(dLon / 2) ** 2;
  return radius * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

function locatePhoto(): Promise<PhotoGeo> {
  return new Promise((resolve, reject) => {
    if (!window.isSecureContext || !navigator.geolocation) {
      reject(new Error("La captura necesita HTTPS y un navegador con ubicación disponible."));
      return;
    }
    let best: GeolocationPosition | null = null;
    let watchId = 0;
    let done = false;
    let samples = 0;
    const startedAt = Date.now();
    let timer = 0;
    const finish = (force = false) => {
      if (done) return;
      if (!force && (samples < PHOTO_MIN_SAMPLES || Date.now() - startedAt < 3_500)) return;
      done = true;
      navigator.geolocation.clearWatch(watchId);
      window.clearTimeout(timer);
      if (!best) return reject(new Error("No se obtuvo una coordenada para esta fotografía."));
      if (best.coords.accuracy > PHOTO_MAX_ACCURACY_METERS) {
        return reject(new Error(`La señal GPS todavía es imprecisa (±${Math.round(best.coords.accuracy)} m). Acércate a una ventana o sal al exterior y vuelve a tomar la foto.`));
      }
      resolve({
        latitude: best.coords.latitude,
        longitude: best.coords.longitude,
        accuracy: best.coords.accuracy,
        capturedAt: new Date(best.timestamp || Date.now()).toISOString(),
      });
    };
    watchId = navigator.geolocation.watchPosition(
      (position) => {
        const isFresh = Date.now() - position.timestamp <= 15_000;
        const isValid = Number.isFinite(position.coords.latitude) && Number.isFinite(position.coords.longitude)
          && Number.isFinite(position.coords.accuracy) && position.coords.accuracy > 0
          && !(position.coords.latitude === 0 && position.coords.longitude === 0);
        if (!isFresh || !isValid) return;
        samples += 1;
        if (!best || position.coords.accuracy < best.coords.accuracy) best = position;
        if (best.coords.accuracy <= PHOTO_TARGET_ACCURACY_METERS) finish();
      },
      (error) => {
        navigator.geolocation.clearWatch(watchId);
        window.clearTimeout(timer);
        done = true;
        reject(new Error(error.code === 1
          ? "El sitio no tiene permiso de ubicación. Abre el candado del navegador, permite Ubicación y vuelve a tomar la foto."
          : "No se obtuvo señal GPS. Activa Ubicación y Wi‑Fi y vuelve a intentarlo desde la banca."));
      },
      { enableHighAccuracy: true, maximumAge: 0, timeout: 20_000 },
    );
    timer = window.setTimeout(() => finish(true), 15_000);
  });
}

async function optimizeCameraPhoto(original: File): Promise<{ file: File; optimized: boolean }> {
  const extensionLooksLikeImage = /\.(jpe?g|png|webp|heic|heif)$/i.test(original.name);
  if ((!original.type.startsWith("image/") && !extensionLooksLikeImage) || original.size > 25 * 1024 * 1024) {
    throw new Error("Selecciona una fotografía válida de hasta 25 MB.");
  }
  const sourceUrl = URL.createObjectURL(original);
  try {
    const image = await new Promise<HTMLImageElement>((resolve, reject) => {
      const element = new Image();
      element.onload = () => resolve(element);
      element.onerror = () => reject(new Error("Este formato no puede reducirse en el navegador."));
      element.src = sourceUrl;
    });
    const maxSide = 1920;
    const scale = Math.min(1, maxSide / Math.max(image.naturalWidth, image.naturalHeight));
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
    canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
    const context = canvas.getContext("2d");
    if (!context) throw new Error("No se pudo preparar la fotografía.");
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    let quality = .84;
    let blob: Blob | null = null;
    do {
      blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", quality));
      quality -= .1;
    } while (blob && blob.size > 4 * 1024 * 1024 && quality >= .54);
    if (!blob) throw new Error("No se pudo convertir la fotografía.");
    const baseName = original.name.replace(/\.[^.]+$/, "") || `foto-${Date.now()}`;
    return { file: new File([blob], `${baseName}.jpg`, { type: "image/jpeg", lastModified: Date.now() }), optimized: true };
  } catch {
    if (original.size <= 20 * 1024 * 1024) return { file: original, optimized: false };
    throw new Error("La foto es demasiado grande y el teléfono no permitió reducirla. Cambia la cámara a formato compatible o usa una imagen menor de 20 MB.");
  } finally {
    URL.revokeObjectURL(sourceUrl);
  }
}

export default function EmployeeAuditForm({ agency, group, onBack, onComplete }: {
  agency: Agency;
  group: string;
  onBack: () => void;
  onComplete: (result: Result) => void;
}) {
  const pendingSubmission = useRef<{ profileId: string; uploadToken: string } | null>(null);
  const [tab, setTab] = useState<"photos" | "employee" | "check">("photos");
  const [busy, setBusy] = useState(false);
  const [locating, setLocating] = useState("");
  const [geocoding, setGeocoding] = useState(false);
  const [error, setError] = useState("");
  const [status, setStatus] = useState("Toma la primera fotografía para activar el atlas y completar la dirección.");
  const [observation, setObservation] = useState("");
  const [form, setForm] = useState({ employeeName: "", employeeCode: "", direccion: "", sector: "", municipio: "", provincia: "", website: "" });
  const [answers, setAnswers] = useState<Answers>({ painted: "", razaSticker: "", realSticker: "", lotekaRemoved: "", damageFound: "", printerMaintained: "", inverterPresent: "", batteryPresent: "" });
  const [damageTypes, setDamageTypes] = useState<string[]>([]);
  useEffect(() => { if (answers.damageFound !== "yes" && damageTypes.length) setDamageTypes([]); }, [answers.damageFound, damageTypes.length]);
  const [items, setItems] = useState<Record<string, PhotoItem>>({
    frontal: { file: null, geo: null, preview: "" },
    operativa: { file: null, geo: null, preview: "" },
    entorno: { file: null, geo: null, preview: "" },
    inversor: { file: null, geo: null, preview: "" },
    bateria: { file: null, geo: null, preview: "" },
  });

  const readyPhotos = evidence.filter((item) => items[item.key].file && items[item.key].geo).length;
  const photoReadings = evidence.flatMap((meta) => items[meta.key].geo ? [{ meta, geo: items[meta.key].geo! }] : []);
  const bestReading = photoReadings.reduce<(typeof photoReadings)[number] | null>((best, current) => !best || current.geo.accuracy < best.geo.accuracy ? current : best, null);
  const bestGeo = bestReading?.geo ?? null;
  const locationSpreadMeters = bestGeo ? Math.max(0, ...photoReadings.map((reading) => haversine(bestGeo.latitude, bestGeo.longitude, reading.geo.latitude, reading.geo.longitude))) : 0;
  const locationVerified = readyPhotos === 3 && locationSpreadMeters <= PHOTO_MATCH_RADIUS_METERS;
  const answerCount = Object.values(answers).filter(Boolean).length;
  const equipmentEvidenceReady = equipmentQuestions.every((question) => answers[question.key] !== "yes" || Boolean(items[question.photoKey].file && items[question.photoKey].geo));
  const damageClassificationReady = answers.damageFound !== "yes" || damageTypes.length > 0;
  const verificationReady = answerCount === 8 && equipmentEvidenceReady && damageClassificationReady;
  const addressOk = Boolean(form.direccion.trim() && form.sector.trim() && form.municipio.trim() && form.provincia.trim());
  const employeeOk = Boolean(form.employeeName.trim() && addressOk);
  const distanceMeters = useMemo(() => bestGeo && agency.expectedLatitude != null && agency.expectedLongitude != null
    ? haversine(bestGeo.latitude, bestGeo.longitude, agency.expectedLatitude, agency.expectedLongitude)
    : null, [bestGeo, agency]);
  const percent = Math.round(((readyPhotos / 3) + (employeeOk ? 1 : 0) + ((answerCount / 7) * (equipmentEvidenceReady ? 1 : .85))) / 3 * 100);

  const formatTime = (value: string) => new Intl.DateTimeFormat("es-DO", { dateStyle: "medium", timeStyle: "medium" }).format(new Date(value));
  const mapUrl = bestGeo ? `https://www.openstreetmap.org/export/embed.html?bbox=${bestGeo.longitude - .0018}%2C${bestGeo.latitude - .0012}%2C${bestGeo.longitude + .0018}%2C${bestGeo.latitude + .0012}&layer=mapnik&marker=${bestGeo.latitude}%2C${bestGeo.longitude}` : "";
  const mapLink = bestGeo ? `https://www.openstreetmap.org/?mlat=${bestGeo.latitude}&mlon=${bestGeo.longitude}#map=20/${bestGeo.latitude}/${bestGeo.longitude}` : "#";

  const field = (label: string, key: keyof typeof form, placeholder = "") => (
    <label className="atlas-field"><span>{label}</span><input value={form[key]} placeholder={placeholder} onChange={(event) => setForm({ ...form, [key]: event.target.value })} /></label>
  );

  async function reverseGeocode(geo: PhotoGeo) {
    setGeocoding(true);
    const address: AddressResponse = {};
    const merge = (candidate: AddressResponse) => {
      address.direccion ||= candidate.direccion?.trim();
      address.sector ||= candidate.sector?.trim();
      address.municipio ||= candidate.municipio?.trim();
      address.provincia ||= candidate.provincia?.trim();
      address.displayName ||= candidate.displayName?.trim();
    };
    const isComplete = () => Boolean(address.direccion && address.sector && address.municipio && address.provincia);
    try {
      try {
        const response = await fetch(`/api/geocode/reverse?lat=${geo.latitude}&lng=${geo.longitude}`, { cache: "no-store" });
        const data = await response.json().catch(() => ({}));
        if (response.ok) merge(data);
      } catch { /* El navegador probará los proveedores directos. */ }

      if (!isComplete()) try {
        const location = encodeURIComponent(`${geo.longitude},${geo.latitude}`);
        const response = await fetch(`https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/reverseGeocode?location=${location}&f=json&langCode=es&featureTypes=PointAddress%2CStreetAddress%2CStreetName`);
        const data = await response.json();
        const item = data?.address || {};
        merge({
          direccion: item.Address || item.ShortLabel || item.Match_addr,
          sector: item.Sector || item.Neighborhood || item.District,
          municipio: item.City || item.Subregion,
          provincia: item.Region,
          displayName: item.LongLabel || item.Match_addr,
        });
      } catch { /* Continúa con el siguiente respaldo. */ }

      if (!address.sector) try {
        const response = await fetch(`https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${geo.latitude}&lon=${geo.longitude}&zoom=15&addressdetails=1&accept-language=es`);
        const data = await response.json();
        const item = data?.address || {};
        const mappedArea = item.suburb || item.neighbourhood || item.quarter || item.city_district || item.village || item.hamlet || item.town || item.municipality;
        merge({ sector: mappedArea, municipio: item.city || item.town || item.municipality || item.county, provincia: item.state || item.region, displayName: data.display_name });
      } catch { /* El backend y ArcGIS conservan los demás campos encontrados. */ }

      if (!address.municipio || !address.provincia) try {
        const response = await fetch(`https://api.bigdatacloud.net/data/reverse-geocode-client?latitude=${geo.latitude}&longitude=${geo.longitude}&localityLanguage=es`);
        const data = await response.json();
        merge({ municipio: data.city || data.locality, provincia: data.principalSubdivision });
      } catch { /* Se mostrará un error verificable debajo. */ }

      if (!address.direccion && address.displayName) address.direccion = address.displayName.split(",")[0]?.trim();
      setForm((current) => ({
        ...current,
        direccion: address.direccion || current.direccion,
        sector: address.sector || current.sector,
        municipio: address.municipio || current.municipio,
        provincia: address.provincia || current.provincia,
      }));
      if (!isComplete()) {
        const missing = [["dirección", address.direccion], ["sector", address.sector], ["municipio", address.municipio], ["provincia", address.provincia]].filter(([, value]) => !value).map(([label]) => label);
        throw new Error(`La cartografía no tiene registrado: ${missing.join(", ")}. Los demás datos GPS sí fueron completados; puedes corregir únicamente el campo pendiente.`);
      }
      return address as Address;
    } finally {
      setGeocoding(false);
    }
  }

  async function refreshAddress(geo = bestGeo) {
    if (!geo) return false;
    setError("");
    setStatus("Convirtiendo la mejor coordenada GPS en dirección, sector, municipio y provincia...");
    try {
      await reverseGeocode(geo);
      setStatus("Dirección GPS completada correctamente. Revisa los datos antes de continuar.");
      return true;
    } catch (geocodeError) {
      setError((geocodeError as Error).message);
      setStatus("La coordenada permanece guardada. Vuelve a intentar la conversión de dirección.");
      return false;
    }
  }

  async function openEmployeeData() {
    if (!locationVerified || !bestGeo) return;
    await refreshAddress(bestGeo);
    setTab("employee");
  }

  function setEquipmentAnswer(key: "inverterPresent" | "batteryPresent", photoKey: "inversor" | "bateria", value: "yes" | "no" | "not_applicable") {
    setAnswers((current) => ({ ...current, [key]: value }));
    if (value === "yes") return;
    setItems((current) => {
      if (current[photoKey].preview) URL.revokeObjectURL(current[photoKey].preview);
      return { ...current, [photoKey]: { file: null, geo: null, preview: "" } };
    });
  }

  async function selected(key: string, file: File | null) {
    if (!file) return;
    setError("");
    setLocating(key);
    setStatus("Preparando la fotografía de la cámara y capturando su ubicación...");
    try {
      const prepared = await optimizeCameraPhoto(file);
      const geo = await locatePhoto();
      const equipment = equipmentQuestions.find((question) => question.photoKey === key);
      if (equipment && bestGeo) {
        const distance = haversine(bestGeo.latitude, bestGeo.longitude, geo.latitude, geo.longitude);
        if (distance > PHOTO_MATCH_RADIUS_METERS) throw new Error(`La foto de ${equipment.photoTitle.toLowerCase()} está a ${Math.round(distance)} m del punto verificado. Debes tomarla dentro de la misma banca.`);
      }
      const preview = /^image\/(jpeg|png|webp)$/i.test(prepared.file.type) ? URL.createObjectURL(prepared.file) : "";
      setItems((current) => {
        if (current[key].preview) URL.revokeObjectURL(current[key].preview);
        return { ...current, [key]: { file: prepared.file, geo, preview } };
      });
      if (equipment) {
        setStatus(`${equipment.photoTitle} georreferenciada en ${geo.latitude.toFixed(6)}, ${geo.longitude.toFixed(6)} con precisión ±${Math.round(geo.accuracy)} m.`);
        return;
      }
      const nextReadings = evidence.flatMap((meta) => {
        const reading = meta.key === key ? geo : items[meta.key].geo;
        return reading ? [{ meta, geo: reading }] : [];
      });
      const nextBest = nextReadings.reduce<(typeof nextReadings)[number] | null>((best, current) => !best || current.geo.accuracy < best.geo.accuracy ? current : best, null)!;
      const nextSpread = Math.max(0, ...nextReadings.map((reading) => haversine(nextBest.geo.latitude, nextBest.geo.longitude, reading.geo.latitude, reading.geo.longitude)));
      setStatus(`${prepared.optimized ? "Foto optimizada y " : "Foto "}georreferenciada con precisión ±${Math.round(geo.accuracy)} m. La mejor lectura actual es ${nextBest.meta.action.toLowerCase()} con ±${Math.round(nextBest.geo.accuracy)} m.`);
      try {
        await reverseGeocode(nextBest.geo);
        if (nextReadings.length === 3 && nextSpread > PHOTO_MATCH_RADIUS_METERS) {
          setError(`Las tres fotos no coinciden: existe una separación de ${Math.round(nextSpread)} m. Vuelve a tomar la fotografía que esté fuera de la banca.`);
          setStatus("Ubicación sin verificar. Las tres fotografías deben tomarse en la misma banca.");
        } else if (nextReadings.length === 3) {
          setStatus(`Ubicación verificada con 3 fotografías. Punto elegido: ${nextBest.geo.latitude.toFixed(6)}, ${nextBest.geo.longitude.toFixed(6)} · precisión ±${Math.round(nextBest.geo.accuracy)} m.`);
        }
      } catch (geocodeError) {
        setStatus("La foto quedó georreferenciada. Completa o corrige manualmente la dirección antes de continuar.");
        setError((geocodeError as Error).message);
      }
    } catch (captureError) {
      setError((captureError as Error).message);
      setStatus("No se vinculó la fotografía. Revisa el permiso y vuelve a tomarla.");
    } finally {
      setLocating("");
    }
  }

  async function uploadPhotos(profileId: string, token: string) {
    const selectedEvidence = [
      ...evidence,
      ...equipmentQuestions.filter((question) => answers[question.key] === "yes").map((question) => ({ key: question.photoKey, title: question.photoTitle })),
    ];
    for (const [index, meta] of selectedEvidence.entries()) {
      setStatus(`Subiendo fotografía ${index + 1} de ${selectedEvidence.length}: ${meta.title}...`);
      const item = items[meta.key];
      const data = new FormData();
      data.append("image", item.file!);
      data.append("profileId", profileId);
      data.append("token", token);
      data.append("photoType", meta.key);
      data.append("photoLatitude", String(item.geo!.latitude));
      data.append("photoLongitude", String(item.geo!.longitude));
      data.append("photoAccuracy", String(item.geo!.accuracy));
      data.append("photoCapturedAt", item.geo!.capturedAt);
      const response = await fetchWithTimeout("/api/image", { method: "POST", body: data }, 90_000);
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(body.error || `No se guardó ${meta.title.toLowerCase()}.`);
    }
    return selectedEvidence.length;
  }

  async function submit() {
    if (!bestGeo || !locationVerified || !employeeOk || !verificationReady) return;
    setBusy(true);
    setError("");
    setStatus("Creando auditoría y preparando las evidencias...");
    const geolocation: Geo = { ...bestGeo, accuracyMeters: bestGeo.accuracy, source: GEO_SOURCE_BROWSER };
    const equipmentPhotoCount = equipmentQuestions.filter((question) => answers[question.key] === "yes").length;
    const summary = { geolocation, address: { direccion: form.direccion, sector: form.sector, municipio: form.municipio, provincia: form.provincia }, distanceMeters, photosUploaded: 3 + equipmentPhotoCount, submittedAt: new Date().toISOString(), locationSpreadMeters, bestPhotoTitle: bestReading?.meta.title };
    try {
      let body = pendingSubmission.current;
      if (!body) {
        const response = await fetchWithTimeout("/api/submit", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            agencyId: agency.id,
            ...form,
            tipoEstablecimiento: "Banca de lotería",
            tipoEstablecimientoOtro: "",
            geolocation,
            answers: { ...answers, damageTypes },
            observations: observation.trim(),
          }),
        }, 45_000);
        const responseBody = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(responseBody.error || "No se pudo guardar la auditoría.");
        body = { profileId: responseBody.profileId, uploadToken: responseBody.uploadToken };
        pendingSubmission.current = body;
      }
      setStatus(`Auditoría creada. Subiendo ${3 + equipmentPhotoCount} fotografías con sus coordenadas...`);
      await uploadPhotos(body.profileId, body.uploadToken);
      pendingSubmission.current = null;
      onComplete({ ...body, ...summary });
    } catch (submitError) {
      const timedOut = (submitError as Error).name === "AbortError";
      setError(timedOut
        ? "El servidor tardó demasiado en responder. Pulsa Guardar nuevamente para reanudar sin duplicar la auditoría."
        : (submitError as Error).message);
      setStatus(pendingSubmission.current
        ? "La auditoría principal fue creada. Pulsa Guardar nuevamente para continuar las fotografías pendientes."
        : "La información permanece en pantalla. Corrige el problema y vuelve a enviar.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="atlas-form">
      <header className="atlas-form-head">
        <button className="atlas-back" onClick={onBack}><ChevronLeft /> Volver</button>
        <div className="atlas-agency">
          <span className="atlas-agency-icon"><Building2 /></span>
          <div><small>Levantamiento seleccionado</small><h2>{agency.terminal}</h2><p>{group} · {agency.codigo} · Banca de lotería</p></div>
        </div>
        <div className="atlas-completion"><span style={{ "--atlas-progress": `${percent}%` } as React.CSSProperties}><strong>{percent}%</strong></span><small>Avance del formulario</small></div>
      </header>

      <nav className="atlas-tabs" aria-label="Etapas del levantamiento">
        <button className={tab === "photos" ? "active" : ""} onClick={() => setTab("photos")}><em>01</em><Camera /><span><strong>Fotografías y ubicación</strong><small>{locationVerified ? "3 puntos verificados" : `${readyPhotos}/3 georreferenciadas`}</small></span>{locationVerified && <Check className="tab-check" />}</button>
        <button className={tab === "employee" ? "active" : ""} onClick={() => setTab("employee")} disabled={!locationVerified}><em>02</em><UserRound /><span><strong>Datos del empleado</strong><small>Dirección autocompletada</small></span>{employeeOk && <Check className="tab-check" />}</button>
        <button className={tab === "check" ? "active" : ""} onClick={() => setTab("check")} disabled={!employeeOk}><em>03</em><ClipboardCheck /><span><strong>Verificación</strong><small>7 preguntas y evidencias</small></span>{verificationReady && <Check className="tab-check" />}</button>
      </nav>

      {error && <div className="alert atlas-alert" role="alert">{error}</div>}

      {tab === "photos" && <div className="atlas-pane">
        <div className="atlas-pane-title"><div><span><Sparkles /> Validación GPS de tres puntos</span><h3>La mejor coordenada entre las tres fotografías</h3><p>Comparamos las tres lecturas, elegimos la de menor margen de error y verificamos que todas hayan sido tomadas en la misma banca.</p></div><strong>{readyPhotos}/3</strong></div>
        <div className="geo-atlas-layout">
          <section className={`atlas-map ${bestGeo ? "active" : ""}`}>
            <div className="atlas-map-top"><span><i /> SENSOR DE PRECISIÓN · {bestReading?.meta.action ?? "EN ESPERA"}</span><Satellite /></div>
            {bestGeo ? <iframe title="Punto más preciso de las fotografías" src={mapUrl} loading="lazy" /> : <div className="atlas-map-empty"><Navigation /><strong>Sensor en espera</strong><span>La primera fotografía marcará el punto en el mapa.</span></div>}
            {bestGeo && <a href={mapLink} target="_blank" rel="noreferrer"><ExternalLink /> Abrir punto exacto</a>}
            <div className="atlas-coordinate-strip">
              <article><small>Latitud elegida</small><strong>{bestGeo ? bestGeo.latitude.toFixed(6) : "--.------"}</strong></article>
              <article><small>Longitud elegida</small><strong>{bestGeo ? bestGeo.longitude.toFixed(6) : "--.------"}</strong></article>
              <article><small>Mejor precisión</small><strong>{bestGeo ? `±${Math.round(bestGeo.accuracy)} m` : "Sin lectura"}</strong></article>
              <article><small>Separación máxima</small><strong>{readyPhotos ? `${Math.round(locationSpreadMeters)} m` : "Sin comparar"}</strong></article>
            </div>
            {bestGeo && <div className="atlas-time"><Clock3 /><span><small>Fotografía con mayor precisión</small><strong>{bestReading?.meta.title} · {formatTime(bestGeo.capturedAt)}</strong></span></div>}
            <div className="atlas-address-preview"><MapPin /><div><small>Dirección detectada</small><strong>{form.direccion || "Esperando fotografía"}</strong><span>{[form.sector, form.municipio, form.provincia].filter(Boolean).join(" · ") || "La dirección aparecerá aquí"}</span></div></div>
          </section>

          <div className="atlas-photo-stack">
            {evidence.map((meta) => {
              const item = items[meta.key];
              const pointLink = item.geo ? `https://www.openstreetmap.org/?mlat=${item.geo.latitude}&mlon=${item.geo.longitude}#map=19/${item.geo.latitude}/${item.geo.longitude}` : "#";
              const isBest = bestReading?.meta.key === meta.key;
              return <article className={`atlas-photo-card ${item.geo ? "ready" : ""} ${isBest ? "best-reading" : ""}`} key={meta.key}>
                <label>
                  {item.preview ? <img src={item.preview} alt={meta.title} /> : <span className="atlas-photo-placeholder"><Camera /><small>{meta.action}</small></span>}
                  <input data-photo={meta.key} type="file" accept="image/*,.heic,.heif" capture="environment" onChange={(event) => void selected(meta.key, event.target.files?.[0] || null)} />
                  {locating === meta.key && <span className="atlas-photo-loading"><LoaderCircle className="spin" /> Vinculando GPS...</span>}
                </label>
                <div>{isBest && <span className="best-reading-badge"><Satellite/> Mayor precisión</span>}<small>{meta.action}</small><h4>{meta.title}</h4><p>{meta.help}</p>{item.geo ? <div className="atlas-photo-meta"><span><Check /> {item.geo.latitude.toFixed(6)}, {item.geo.longitude.toFixed(6)} · ±{Math.round(item.geo.accuracy)} m</span><a href={pointLink} target="_blank" rel="noreferrer"><MapPin/> Ver punto exacto</a></div> : <button onClick={() => (document.querySelector(`input[data-photo='${meta.key}']`) as HTMLInputElement)?.click()} style={{ display: "none" }}>Tomar foto</button>}</div>
              </article>;
            })}
          </div>
        </div>
        <div className={`photo-location-verification ${locationVerified ? "verified" : readyPhotos === 3 ? "mismatch" : "pending"}`}><ShieldCheck/><div><strong>{locationVerified ? "Ubicación confirmada por tres fotografías" : readyPhotos === 3 ? "Las coordenadas no coinciden" : `Faltan ${3-readyPhotos} fotografías para verificar`}</strong><span>{locationVerified ? `Los tres puntos están dentro de ${Math.round(locationSpreadMeters)} m. Se usará la coordenada de ${bestReading?.meta.title}.` : readyPhotos === 3 ? `La separación máxima es ${Math.round(locationSpreadMeters)} m; debe ser de ${PHOTO_MATCH_RADIUS_METERS} m o menos.` : "Toma las tres fotos físicamente dentro de la misma banca."}</span></div></div>
        <div className="atlas-live-status"><span className={locating ? "pulse" : ""} /><p>{status}</p></div>
        <div className="atlas-actions"><button className="primary" disabled={!locationVerified || geocoding} onClick={() => void openEmployeeData()}>{geocoding ? <LoaderCircle className="spin" /> : null} Continuar a datos del empleado <ChevronRight /></button></div>
      </div>}

      {tab === "employee" && <div className="atlas-pane">
        <div className="atlas-pane-title"><div><span><MapPin /> Dirección obtenida desde evidencia</span><h3>Datos que completa el empleado</h3><p>Revisa la dirección detectada y corrige cualquier detalle antes de continuar.</p></div><span className="fixed-type">Banca de lotería</span></div>
        <div className="atlas-address-banner"><Navigation /><div><small>Fuente de la ubicación verificada</small><strong>{bestReading?.meta.title} — lectura de mayor precisión</strong><span>{bestGeo?.latitude.toFixed(6)}, {bestGeo?.longitude.toFixed(6)} · {bestGeo ? `±${Math.round(bestGeo.accuracy)} metros` : ""}</span></div><button className="address-refresh" disabled={geocoding || !bestGeo} onClick={() => void refreshAddress()}>{geocoding ? <LoaderCircle className="spin" /> : <MapPin />} {geocoding ? "Buscando..." : "Actualizar dirección"}</button></div>
        <div className="atlas-fields-grid">
          {field("Nombre completo del empleado", "employeeName", "Nombre y apellido")}
          {field("Código del empleado", "employeeCode", "Opcional")}
          {field("Calle y dirección", "direccion", "Autocompletada desde la foto")}
          {field("Sector", "sector", "Autocompletado desde la foto")}
          {field("Municipio", "municipio", "Autocompletado desde la foto")}
          {field("Provincia", "provincia", "Autocompletada desde la foto")}
          <label className="honey" aria-hidden="true">Website<input tabIndex={-1} autoComplete="off" value={form.website} onChange={(event) => setForm({ ...form, website: event.target.value })} /></label>
        </div>
        <div className="atlas-actions split"><button className="secondary" onClick={() => setTab("photos")}>Anterior</button><button className="primary" disabled={!employeeOk} onClick={() => setTab("check")}>Continuar a verificación <ChevronRight /></button></div>
      </div>}

      {tab === "check" && <div className="atlas-pane">
        <div className="atlas-pane-title"><div><span><ClipboardCheck /> Verificación estructural y energética</span><h3>Condiciones de la banca</h3><p>Responde las ocho validaciones. Inversor y batería solicitarán una foto georreferenciada cuando selecciones Sí.</p></div><strong>{answerCount}/8</strong></div>
        <div className="atlas-question-progress"><i style={{ width: `${Math.round(answerCount * 100 / 8)}%` }} /><span>{Math.round(answerCount * 100 / 8)}% respondido</span></div>
        <div className="atlas-questions">{questions.map((question, index) => <article className={answers[question.key] ? "answered" : ""} key={question.key}>
          <em>{String(index + 1).padStart(2, "0")}</em><div><strong>{question.title}</strong><small>{question.help}</small></div><div className="answer-segment"><button type="button" className={answers[question.key] === "yes" ? "active yes" : ""} onClick={() => setAnswers({ ...answers, [question.key]: "yes" })}>Sí</button><button type="button" className={answers[question.key] === "no" ? "active no" : ""} onClick={() => setAnswers({ ...answers, [question.key]: "no" })}>No</button>{question.allowNotApplicable && <button type="button" className={answers[question.key] === "not_applicable" ? "active na" : ""} onClick={() => setAnswers({ ...answers, [question.key]: "not_applicable" })}>No aplica</button>}</div>
        </article>)}
        {answers.damageFound === "yes" && <div className="damage-classification">
          <div><AlertTriangle/><span><strong>¿Qué tipo de avería encontraste?</strong><small>Selecciona una o varias. Esta clasificación determina la prioridad del hallazgo.</small></span></div>
          <div>{damageOptions.map(option => <label className={damageTypes.includes(option.value) ? "active" : ""} key={option.value}>
            <input type="checkbox" checked={damageTypes.includes(option.value)} onChange={() => setDamageTypes(current => current.includes(option.value) ? current.filter(value => value !== option.value) : [...current, option.value])}/>
            <span><strong>{option.label}</strong><small>Prioridad {option.priority}</small></span>
          </label>)}</div>
          {!damageTypes.length && <p>Debes identificar al menos un tipo de avería para continuar.</p>}
        </div>}
        {equipmentQuestions.map((question, index) => {
          const item = items[question.photoKey];
          const pointLink = item.geo ? `https://www.openstreetmap.org/?mlat=${item.geo.latitude}&mlon=${item.geo.longitude}#map=20/${item.geo.latitude}/${item.geo.longitude}` : "#";
          return <article className={`equipment-question ${answers[question.key] ? "answered" : ""} ${answers[question.key] === "yes" && item.geo ? "evidence-ready" : ""}`} key={question.key}>
            <em>{String(index + 7).padStart(2, "0")}</em><div><strong>{question.title}</strong><small>{question.help}</small></div>
            <div className="answer-segment equipment-answers">
              <label className={answers[question.key] === "yes" ? "active yes" : ""} onClick={() => setEquipmentAnswer(question.key, question.photoKey, "yes")}><Camera/> Sí<input type="file" accept="image/*,.heic,.heif" capture="environment" onChange={(event) => void selected(question.photoKey, event.target.files?.[0] || null)}/></label>
              <button type="button" className={answers[question.key] === "no" ? "active no" : ""} onClick={() => setEquipmentAnswer(question.key, question.photoKey, "no")}>No</button>
              <button type="button" className={answers[question.key] === "not_applicable" ? "active na" : ""} onClick={() => setEquipmentAnswer(question.key, question.photoKey, "not_applicable")}>No aplica</button>
            </div>
            {answers[question.key] === "yes" && <div className={`equipment-photo-capture ${item.geo ? "ready" : ""}`}>
              <label>{item.preview ? <img src={item.preview} alt={question.photoTitle}/> : <span><Camera/><strong>{question.photoTitle}</strong><small>Toca para abrir la cámara</small></span>}<input type="file" accept="image/*,.heic,.heif" capture="environment" onChange={(event) => void selected(question.photoKey, event.target.files?.[0] || null)}/>{locating === question.photoKey && <em><LoaderCircle className="spin"/> Capturando GPS...</em>}</label>
              <div>{item.geo ? <><span><MapPin/> {item.geo.latitude.toFixed(6)}, {item.geo.longitude.toFixed(6)}</span><small>Precisión ±{Math.round(item.geo.accuracy)} m · {formatTime(item.geo.capturedAt)}</small><a href={pointLink} target="_blank" rel="noreferrer"><ExternalLink/> Ver punto exacto</a></> : <><strong>Fotografía obligatoria</strong><small>La respuesta Sí necesita evidencia tomada dentro de esta banca.</small></>}</div>
            </div>}
          </article>;
        })}</div>
        <label className="atlas-observation"><span>Observación del levantamiento <small>Opcional · máximo 1,000 caracteres</small></span><textarea maxLength={1000} value={observation} onChange={(event) => setObservation(event.target.value)} placeholder="Describe averías, materiales pendientes, condiciones especiales o cualquier detalle que deba revisar el equipo administrativo." /><em>{observation.length}/1000</em></label>
        <div className="atlas-submit-summary"><span className="ok"><Check /> 3 fotos principales</span><span className={employeeOk ? "ok" : ""}><Check /> Datos y dirección</span><span className={verificationReady ? "ok" : ""}><Check /> Verificación {answerCount}/7 · {equipmentEvidenceReady ? "evidencias listas" : "faltan fotos"}</span></div>
        <div className="atlas-actions split"><button className="secondary" onClick={() => setTab("employee")}>Anterior</button><button className="primary" disabled={busy || !locationVerified || !employeeOk || !verificationReady} onClick={() => void submit()}>{busy ? <LoaderCircle className="spin" /> : <Check />} Guardar auditoría completa</button></div>
        {busy && <div className="atlas-saving"><span /><p>{status}</p></div>}
      </div>}
    </section>
  );
}
