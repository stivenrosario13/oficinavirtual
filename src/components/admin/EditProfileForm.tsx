"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { updateProfileAction, deleteProfileAction } from "@/server/actions";
import { validateGeolocation } from "@/lib/geo";
import {
  ESTABLISHMENT_TYPES,
  ESTABLISHMENT_OTRO,
  GEO_SOURCE_ADMIN,
  GEO_SOURCE_BROWSER,
} from "@/lib/constants";

interface Initial {
  direccion: string;
  sector: string;
  municipio: string;
  provincia: string;
  tipoEstablecimiento: string;
  tipoEstablecimientoOtro: string;
  latitude: number;
  longitude: number;
  accuracyMeters: number;
}

export default function EditProfileForm({
  agencyId,
  profileId,
  initial,
}: {
  agencyId: string;
  profileId: string;
  initial: Initial;
}) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const [form, setForm] = useState({
    direccion: initial.direccion,
    sector: initial.sector,
    municipio: initial.municipio,
    provincia: initial.provincia,
    tipoEstablecimiento: initial.tipoEstablecimiento,
    tipoEstablecimientoOtro: initial.tipoEstablecimientoOtro,
  });

  const [updateLocation, setUpdateLocation] = useState(false);
  const [loc, setLoc] = useState({
    lat: String(initial.latitude),
    lng: String(initial.longitude),
    acc: String(initial.accuracyMeters),
  });
  const [locSource, setLocSource] = useState<string>(GEO_SOURCE_ADMIN);
  const [capturing, setCapturing] = useState(false);

  const isOtro = form.tipoEstablecimiento === ESTABLISHMENT_OTRO;

  function captureBrowser() {
    setError(null);
    if (typeof navigator === "undefined" || !navigator.geolocation) {
      setError("El navegador no soporta geolocalización.");
      return;
    }
    setCapturing(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setLoc({
          lat: String(pos.coords.latitude),
          lng: String(pos.coords.longitude),
          acc: String(Math.round(pos.coords.accuracy)),
        });
        setLocSource(GEO_SOURCE_BROWSER);
        setCapturing(false);
      },
      () => {
        setError("No se pudo obtener la ubicación del dispositivo.");
        setCapturing(false);
      },
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 },
    );
  }

  function save() {
    setError(null);
    setSuccess(false);

    let geolocation:
      | {
          latitude: number;
          longitude: number;
          accuracyMeters: number;
          capturedAt: string;
          source: string;
        }
      | undefined;

    if (updateLocation) {
      const candidate = {
        latitude: Number(loc.lat),
        longitude: Number(loc.lng),
        accuracyMeters: Number(loc.acc),
        capturedAt: new Date().toISOString(),
        source: locSource,
      };
      const check = validateGeolocation(candidate, { checkAge: false });
      if (!check.ok) {
        setError(check.errors.join(" "));
        return;
      }
      geolocation = candidate;
    }

    startTransition(async () => {
      const res = await updateProfileAction({
        profileId,
        agencyId,
        direccion: form.direccion,
        sector: form.sector,
        municipio: form.municipio,
        provincia: form.provincia,
        tipoEstablecimiento: form.tipoEstablecimiento,
        tipoEstablecimientoOtro: isOtro
          ? form.tipoEstablecimientoOtro
          : undefined,
        geolocation,
      });
      if (!res.ok) {
        setError(res.error ?? "No se pudo guardar.");
        return;
      }
      setSuccess(true);
      setUpdateLocation(false);
      router.refresh();
    });
  }

  function remove() {
    if (
      !window.confirm(
        "¿Eliminar el perfil? La agencia volverá a PENDING y podrá recapturarse. Esta acción queda auditada.",
      )
    ) {
      return;
    }
    startTransition(async () => {
      const res = await deleteProfileAction({ profileId, agencyId });
      if (!res.ok) {
        setError(res.error ?? "No se pudo eliminar.");
        return;
      }
      router.push("/admin/records");
    });
  }

  return (
    <div className="flex flex-col gap-4">
      {error && (
        <p className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
          {error}
        </p>
      )}
      {success && (
        <p className="rounded-lg bg-ok-bg px-3 py-2 text-sm text-ok">
          Cambios guardados y resincronizados.
        </p>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field
          label="Dirección"
          value={form.direccion}
          onChange={(v) => setForm({ ...form, direccion: v })}
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
          <label className="field-label">Tipo de establecimiento</label>
          <select
            className="field-input"
            value={form.tipoEstablecimiento}
            onChange={(e) =>
              setForm({ ...form, tipoEstablecimiento: e.target.value })
            }
          >
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
            onChange={(v) => setForm({ ...form, tipoEstablecimientoOtro: v })}
          />
        )}
      </div>

      {/* Actualización de geolocalización con validaciones equivalentes */}
      <div className="rounded-2xl border border-line bg-canvas p-4">
        <label className="flex items-center gap-2 text-sm font-medium text-ink">
          <input
            type="checkbox"
            checked={updateLocation}
            onChange={(e) => setUpdateLocation(e.target.checked)}
          />
          Actualizar geolocalización
        </label>

        {updateLocation && (
          <div className="mt-3 flex flex-col gap-3">
            <div className="grid grid-cols-3 gap-2">
              <NumField
                label="Latitud"
                value={loc.lat}
                onChange={(v) => {
                  setLoc({ ...loc, lat: v });
                  setLocSource(GEO_SOURCE_ADMIN);
                }}
              />
              <NumField
                label="Longitud"
                value={loc.lng}
                onChange={(v) => {
                  setLoc({ ...loc, lng: v });
                  setLocSource(GEO_SOURCE_ADMIN);
                }}
              />
              <NumField
                label="Precisión (m)"
                value={loc.acc}
                onChange={(v) => {
                  setLoc({ ...loc, acc: v });
                  setLocSource(GEO_SOURCE_ADMIN);
                }}
              />
            </div>
            <button
              type="button"
              className="btn-secondary"
              onClick={captureBrowser}
              disabled={capturing}
            >
              {capturing ? "Obteniendo…" : "Usar mi ubicación actual"}
            </button>
            <p className="text-xs text-muted">
              Fuente: {locSource}. Se validará precisión y rango igual que en el
              formulario público.
            </p>
          </div>
        )}
      </div>

      <div className="flex flex-wrap gap-3">
        <button
          type="button"
          className="btn-primary sm:w-auto sm:px-8"
          onClick={save}
          disabled={pending}
        >
          {pending ? "Guardando…" : "Guardar cambios"}
        </button>
        <button
          type="button"
          className="rounded-xl border border-danger/40 px-5 py-3 text-sm font-semibold text-danger hover:bg-danger-bg"
          onClick={remove}
          disabled={pending}
        >
          Eliminar perfil
        </button>
      </div>
    </div>
  );
}

function Field(props: {
  label: string;
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <div>
      <label className="field-label">{props.label}</label>
      <input
        className="field-input"
        value={props.value}
        onChange={(e) => props.onChange(e.target.value)}
      />
    </div>
  );
}

function NumField(props: {
  label: string;
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <div>
      <label className="field-label text-xs">{props.label}</label>
      <input
        className="field-input"
        inputMode="decimal"
        value={props.value}
        onChange={(e) => props.onChange(e.target.value)}
      />
    </div>
  );
}
