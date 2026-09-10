"use client";

import { useEffect, useState } from "react";
import Stepper from "./Stepper";
import GeolocationCapture from "./GeolocationCapture";
import ImageUpload from "./ImageUpload";
import { validateGeolocation, type GeoInput } from "@/lib/geo";
import { ESTABLISHMENT_TYPES, ESTABLISHMENT_OTRO } from "@/lib/constants";
import { foldKey } from "@/lib/normalize";

interface Group {
  grupo: string;
  pending: number;
}
interface Agency {
  id: string;
  codigo: string;
  terminal: string;
}
interface SubmitResult {
  profileId: string;
  uploadToken: string;
  syncStatus: string;
}

const STEP_LABELS = ["Grupo", "Agencia", "Datos", "Listo"];
type Step = "group" | "agency" | "form" | "success";

const EMPTY_FORM = {
  direccion: "",
  sector: "",
  municipio: "",
  provincia: "",
  tipoEstablecimiento: "",
  tipoEstablecimientoOtro: "",
  website: "", // honeypot: debe permanecer vacío
};

// Fetchers a nivel de módulo (estables): no dependen de estado/props.
async function fetchGroupsData(): Promise<Group[]> {
  const res = await fetch("/api/public/groups", { cache: "no-store" });
  const json = await res.json();
  if (!res.ok) throw new Error(json.error);
  return json.groups ?? [];
}

async function fetchAgenciesData(grupo: string): Promise<Agency[]> {
  const res = await fetch(
    `/api/public/agencies?grupo=${encodeURIComponent(grupo)}`,
    { cache: "no-store" },
  );
  const json = await res.json();
  if (!res.ok) throw new Error(json.error);
  return json.agencies ?? [];
}

export default function PublicWizard() {
  const [step, setStep] = useState<Step>("group");
  const [groups, setGroups] = useState<Group[]>([]);
  const [loadingGroups, setLoadingGroups] = useState(true);
  const [selectedGroup, setSelectedGroup] = useState<string>("");

  const [agencies, setAgencies] = useState<Agency[]>([]);
  const [loadingAgencies, setLoadingAgencies] = useState(false);
  const [agency, setAgency] = useState<Agency | null>(null);

  // Búsqueda rápida para filtrar grupos y agencias.
  const [groupQuery, setGroupQuery] = useState("");
  const [agencyQuery, setAgencyQuery] = useState("");

  const [form, setForm] = useState({ ...EMPTY_FORM });
  const [geo, setGeo] = useState<GeoInput | null>(null);

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<SubmitResult | null>(null);

  const stepIndex =
    step === "group" ? 0 : step === "agency" ? 1 : step === "form" ? 2 : 3;

  async function loadGroups() {
    setLoadingGroups(true);
    setError(null);
    try {
      setGroups(await fetchGroupsData());
    } catch {
      setError("No se pudieron cargar los grupos. Recarga la página.");
    } finally {
      setLoadingGroups(false);
    }
  }

  // Carga inicial: se espera al fetch antes de tocar el estado (sin cascadas).
  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const g = await fetchGroupsData();
        if (active) setGroups(g);
      } catch {
        if (active)
          setError("No se pudieron cargar los grupos. Recarga la página.");
      } finally {
        if (active) setLoadingGroups(false);
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  async function loadAgencies(grupo: string) {
    setLoadingAgencies(true);
    setError(null);
    try {
      setAgencies(await fetchAgenciesData(grupo));
    } catch {
      setError("No se pudieron cargar las agencias.");
    } finally {
      setLoadingAgencies(false);
    }
  }

  function chooseGroup(grupo: string) {
    setSelectedGroup(grupo);
    setAgency(null);
    setAgencyQuery("");
    setStep("agency");
    void loadAgencies(grupo);
  }

  function chooseAgency(a: Agency) {
    setAgency(a);
    setForm({ ...EMPTY_FORM });
    setGeo(null);
    setError(null);
    setStep("form");
  }

  const isOtro = form.tipoEstablecimiento === ESTABLISHMENT_OTRO;
  const geoValid = validateGeolocation(geo, { checkAge: false }).ok;
  const formValid =
    !!agency &&
    form.direccion.trim() !== "" &&
    form.sector.trim() !== "" &&
    form.municipio.trim() !== "" &&
    form.provincia.trim() !== "" &&
    form.tipoEstablecimiento !== "" &&
    (!isOtro || form.tipoEstablecimientoOtro.trim() !== "") &&
    geoValid;

  async function submit() {
    if (!agency || !geo) return;
    setError(null);

    // Revalida integridad y precisión; la captura no caduca por tiempo.
    const fresh = validateGeolocation(geo, { checkAge: true });
    if (!fresh.ok) {
      setError(fresh.errors.join(" "));
      return;
    }

    setSubmitting(true);
    try {
      const res = await fetch("/api/public/submit", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          agencyId: agency.id,
          direccion: form.direccion,
          sector: form.sector,
          municipio: form.municipio,
          provincia: form.provincia,
          tipoEstablecimiento: form.tipoEstablecimiento,
          tipoEstablecimientoOtro: isOtro
            ? form.tipoEstablecimientoOtro
            : undefined,
          geolocation: geo,
          website: form.website, // honeypot
        }),
      });
      const json = await res.json().catch(() => ({}));

      if (res.status === 409) {
        setError(
          json.error ??
            "Esta agencia ya fue registrada. Actualizamos la lista de pendientes.",
        );
        setStep("agency");
        await loadAgencies(selectedGroup);
        return;
      }
      if (!res.ok) {
        setError(json.error ?? "No se pudo guardar. Inténtalo de nuevo.");
        return;
      }

      setResult({
        profileId: json.profileId,
        uploadToken: json.uploadToken,
        syncStatus: json.syncStatus,
      });
      setStep("success");
    } catch {
      setError("Error de conexión. Inténtalo de nuevo.");
    } finally {
      setSubmitting(false);
    }
  }

  function reset() {
    setResult(null);
    setAgency(null);
    setSelectedGroup("");
    setGroupQuery("");
    setAgencyQuery("");
    setForm({ ...EMPTY_FORM });
    setGeo(null);
    setError(null);
    setStep("group");
    void loadGroups();
  }

  // Filtrado insensible a mayúsculas/acentos.
  const gq = foldKey(groupQuery);
  const filteredGroups = gq
    ? groups.filter((g) => foldKey(g.grupo).includes(gq))
    : groups;
  const aq = foldKey(agencyQuery);
  const filteredAgencies = aq
    ? agencies.filter(
        (a) => foldKey(a.terminal).includes(aq) || foldKey(a.codigo).includes(aq),
      )
    : agencies;

  return (
    <div>
      {step !== "success" && (
        <Stepper steps={STEP_LABELS} current={stepIndex} />
      )}

      {error && (
        <div className="mb-4 rounded-xl bg-danger-bg px-4 py-3 text-sm text-danger">
          {error}
        </div>
      )}

      {/* PASO 1 — GRUPO */}
      {step === "group" && (
        <section className="card">
          <h2 className="mb-1 text-lg font-bold">Selecciona el grupo</h2>
          <p className="mb-4 text-sm text-muted">
            Solo se muestran grupos con agencias pendientes.
          </p>
          {loadingGroups ? (
            <Loading label="Cargando grupos…" />
          ) : groups.length === 0 ? (
            <EmptyState label="No hay grupos con agencias pendientes. ¡Todo está completado!" />
          ) : (
            <>
              <SearchInput
                value={groupQuery}
                onChange={setGroupQuery}
                placeholder="Buscar grupo…"
              />
              {filteredGroups.length === 0 ? (
                <EmptyState label={`Sin resultados para "${groupQuery}".`} />
              ) : (
                <div className="flex flex-col gap-2">
                  {filteredGroups.map((g) => (
                    <button
                      key={g.grupo}
                      type="button"
                      onClick={() => chooseGroup(g.grupo)}
                      className="flex items-center justify-between rounded-xl border border-line bg-surface px-4 py-4 text-left transition hover:border-brand-500 hover:bg-brand-50"
                    >
                      <span className="font-semibold text-ink">{g.grupo}</span>
                      <span className="badge bg-warn-bg text-warn">
                        {g.pending} pendiente{g.pending === 1 ? "" : "s"}
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </>
          )}
        </section>
      )}

      {/* PASO 2 — AGENCIA */}
      {step === "agency" && (
        <section className="card">
          <BackButton onClick={() => setStep("group")} />
          <h2 className="mb-1 text-lg font-bold">Selecciona la agencia</h2>
          <p className="mb-4 text-sm text-muted">
            Grupo: <span className="font-semibold text-ink">{selectedGroup}</span>
          </p>
          {loadingAgencies ? (
            <Loading label="Cargando agencias…" />
          ) : agencies.length === 0 ? (
            <EmptyState label="No quedan agencias pendientes en este grupo." />
          ) : (
            <>
              <SearchInput
                value={agencyQuery}
                onChange={setAgencyQuery}
                placeholder="Buscar por terminal o código…"
              />
              {filteredAgencies.length === 0 ? (
                <EmptyState label={`Sin resultados para "${agencyQuery}".`} />
              ) : (
                <div className="flex flex-col gap-2">
                  {filteredAgencies.map((a) => (
                    <button
                      key={a.id}
                      type="button"
                      onClick={() => chooseAgency(a)}
                      className="rounded-xl border border-line bg-surface px-4 py-3 text-left transition hover:border-brand-500 hover:bg-brand-50"
                    >
                      <span className="block text-sm font-semibold text-ink">
                        {a.terminal}
                      </span>
                      <span className="block text-xs text-muted">
                        Código: {a.codigo}
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </>
          )}
        </section>
      )}

      {/* PASO 3 — FORMULARIO + GEO */}
      {step === "form" && agency && (
        <section className="card">
          <BackButton onClick={() => setStep("agency")} />
          <h2 className="mb-1 text-lg font-bold">Datos de la agencia</h2>
          <div className="mb-4 rounded-xl bg-brand-50 px-4 py-3 text-sm">
            <p className="font-semibold text-ink">{agency.terminal}</p>
            <p className="text-muted">Código: {agency.codigo}</p>
          </div>

          <div className="flex flex-col gap-4">
            {/* Honeypot anti-spam: invisible para humanos, fuera de pantalla. */}
            <div
              aria-hidden="true"
              style={{
                position: "absolute",
                left: "-9999px",
                width: 1,
                height: 1,
                overflow: "hidden",
              }}
            >
              <label>
                No llenar este campo
                <input
                  type="text"
                  name="website"
                  tabIndex={-1}
                  autoComplete="off"
                  value={form.website}
                  onChange={(e) => setForm({ ...form, website: e.target.value })}
                />
              </label>
            </div>

            <Field
              label="Dirección"
              value={form.direccion}
              onChange={(v) => setForm({ ...form, direccion: v })}
              placeholder="Calle, número, referencia"
            />
            <Field
              label="Sector"
              value={form.sector}
              onChange={(v) => setForm({ ...form, sector: v })}
            />
            <Field
              label="Municipio"
              value={form.municipio}
              onChange={(v) => setForm({ ...form, municipio: v })}
            />
            <Field
              label="Provincia"
              value={form.provincia}
              onChange={(v) => setForm({ ...form, provincia: v })}
            />

            <div>
              <label className="field-label" htmlFor="tipo">
                Tipo de establecimiento
              </label>
              <select
                id="tipo"
                className="field-input"
                value={form.tipoEstablecimiento}
                onChange={(e) =>
                  setForm({ ...form, tipoEstablecimiento: e.target.value })
                }
              >
                <option value="">Selecciona…</option>
                {ESTABLISHMENT_TYPES.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </div>

            {isOtro && (
              <Field
                label="Especifica el tipo"
                value={form.tipoEstablecimientoOtro}
                onChange={(v) =>
                  setForm({ ...form, tipoEstablecimientoOtro: v })
                }
                placeholder="Describe el tipo de establecimiento"
              />
            )}

            <GeolocationCapture value={geo} onChange={setGeo} />

            <button
              type="button"
              className="btn-primary"
              disabled={!formValid || submitting}
              onClick={() => void submit()}
            >
              {submitting ? "Guardando…" : "Guardar registro"}
            </button>
            {!formValid && (
              <p className="text-center text-xs text-muted">
                Completa todos los campos y captura una ubicación válida para
                continuar.
              </p>
            )}
          </div>
        </section>
      )}

      {/* PASO 4 — ÉXITO + IMAGEN */}
      {step === "success" && result && (
        <section className="flex flex-col gap-4">
          <div className="card text-center">
            <div className="mx-auto mb-3 flex h-14 w-14 items-center justify-center rounded-full bg-ok-bg text-2xl">
              ✓
            </div>
            <h2 className="text-lg font-bold text-ink">Registro guardado</h2>
            <p className="mt-1 text-sm text-muted">
              Los datos de la agencia se guardaron correctamente.
            </p>
            {result.syncStatus !== "SYNCED" && (
              <p className="mt-3 rounded-lg bg-warn-bg px-3 py-2 text-xs text-warn">
                La sincronización con Google Sheets quedó pendiente. El dato está
                guardado y un administrador podrá reintentarla.
              </p>
            )}
          </div>

          <ImageUpload profileId={result.profileId} token={result.uploadToken} />

          <button type="button" className="btn-secondary" onClick={reset}>
            Registrar otra agencia
          </button>
        </section>
      )}
    </div>
  );
}

/* ---------- Subcomponentes UI ---------- */

function Field(props: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
}) {
  return (
    <div>
      <label className="field-label">{props.label}</label>
      <input
        className="field-input"
        value={props.value}
        placeholder={props.placeholder}
        onChange={(e) => props.onChange(e.target.value)}
        autoComplete="off"
      />
    </div>
  );
}

function SearchInput({
  value,
  onChange,
  placeholder,
}: {
  value: string;
  onChange: (v: string) => void;
  placeholder: string;
}) {
  return (
    <div className="relative mb-3">
      <svg
        className="pointer-events-none absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-slate-400"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        <circle cx="11" cy="11" r="7" />
        <path d="m21 21-4.3-4.3" />
      </svg>
      <input
        type="text"
        inputMode="search"
        autoComplete="off"
        className="field-input pl-11 pr-10"
        value={value}
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
        aria-label={placeholder}
      />
      {value && (
        <button
          type="button"
          onClick={() => onChange("")}
          aria-label="Limpiar búsqueda"
          className="absolute right-2 top-1/2 flex h-8 w-8 -translate-y-1/2 items-center justify-center rounded-full text-slate-400 hover:bg-canvas hover:text-ink"
        >
          ✕
        </button>
      )}
    </div>
  );
}

function BackButton({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="mb-3 text-sm font-medium text-brand-600"
    >
      ← Atrás
    </button>
  );
}

function Loading({ label }: { label: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted">
      <span className="h-4 w-4 animate-spin rounded-full border-2 border-line border-t-brand-500" />
      {label}
    </div>
  );
}

function EmptyState({ label }: { label: string }) {
  return (
    <p className="rounded-xl bg-canvas px-4 py-8 text-center text-sm text-muted">
      {label}
    </p>
  );
}
