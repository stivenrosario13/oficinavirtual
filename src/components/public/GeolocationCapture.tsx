"use client";

import { useState } from "react";
import { validateGeolocation, type GeoInput } from "@/lib/geo";
import { GEO_SOURCE_BROWSER, MAX_GPS_ACCURACY_METERS } from "@/lib/constants";

interface Props {
  value: GeoInput | null;
  onChange: (geo: GeoInput | null) => void;
}

type Status = "idle" | "capturing" | "captured" | "error";

/** Captura de geolocalización con validación en vivo. */
export default function GeolocationCapture({ value, onChange }: Props) {
  const [status, setStatus] = useState<Status>(value ? "captured" : "idle");
  const [message, setMessage] = useState<string | null>(null);

  function capture() {
    setMessage(null);

    if (typeof navigator === "undefined" || !navigator.geolocation) {
      setStatus("error");
      setMessage(
        "Tu navegador no soporta geolocalización. Usa un navegador móvil actualizado.",
      );
      onChange(null);
      return;
    }

    setStatus("capturing");
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const geo: GeoInput = {
          latitude: pos.coords.latitude,
          longitude: pos.coords.longitude,
          accuracyMeters: pos.coords.accuracy,
          capturedAt: new Date(pos.timestamp || Date.now()).toISOString(),
          source: GEO_SOURCE_BROWSER,
        };
        // Validación inmediata (sin comprobar antigüedad todavía).
        const result = validateGeolocation(geo, { checkAge: false });
        if (!result.ok) {
          setStatus("error");
          setMessage(result.errors.join(" "));
          onChange(null);
          return;
        }
        setStatus("captured");
        onChange(geo);
      },
      (err) => {
        setStatus("error");
        onChange(null);
        switch (err.code) {
          case err.PERMISSION_DENIED:
            setMessage(
              "Permiso de ubicación denegado. Actívalo en los ajustes del navegador y vuelve a intentar.",
            );
            break;
          case err.POSITION_UNAVAILABLE:
            setMessage(
              "Ubicación no disponible. Verifica que el GPS esté activado y estés al aire libre.",
            );
            break;
          case err.TIMEOUT:
            setMessage("Se agotó el tiempo de espera. Intenta de nuevo.");
            break;
          default:
            setMessage("No se pudo obtener la ubicación. Intenta de nuevo.");
        }
      },
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 },
    );
  }

  const isValid = status === "captured" && !!value;
  const capturedTime = value
    ? new Date(value.capturedAt).toLocaleTimeString("es-DO")
    : null;

  return (
    <div className="rounded-2xl border border-line bg-brand-50/40 p-4">
      <p className="mb-1 text-sm font-semibold text-ink">
        Geolocalización <span className="text-danger">*</span>
      </p>
      <p className="mb-3 text-xs text-muted">
        Ubícate físicamente en la agencia y captura tu ubicación actual. Se
        requiere una precisión de {MAX_GPS_ACCURACY_METERS} m o mejor.
      </p>

      <button
        type="button"
        onClick={capture}
        disabled={status === "capturing"}
        className={isValid ? "btn-secondary" : "btn-primary"}
      >
        {status === "capturing"
          ? "Obteniendo ubicación…"
          : isValid
            ? "Volver a capturar ubicación"
            : "Capturar mi ubicación actual"}
      </button>

      {message && (
        <p className="mt-3 rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
          {message}
        </p>
      )}

      {isValid && value && (
        <dl className="mt-4 grid grid-cols-2 gap-x-3 gap-y-2 text-sm">
          <dt className="text-muted">Latitud</dt>
          <dd className="text-right font-mono text-ink">
            {value.latitude.toFixed(6)}
          </dd>
          <dt className="text-muted">Longitud</dt>
          <dd className="text-right font-mono text-ink">
            {value.longitude.toFixed(6)}
          </dd>
          <dt className="text-muted">Precisión</dt>
          <dd className="text-right font-mono text-ink">
            ± {Math.round(value.accuracyMeters)} m
          </dd>
          <dt className="text-muted">Hora de captura</dt>
          <dd className="text-right text-ink">{capturedTime}</dd>
          <dt className="text-muted">Estado</dt>
          <dd className="text-right">
            <span className="badge bg-ok-bg text-ok">✓ Válida</span>
          </dd>
        </dl>
      )}
    </div>
  );
}
