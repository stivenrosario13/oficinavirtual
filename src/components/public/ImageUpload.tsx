"use client";

import { useRef, useState } from "react";
import { validateImageMeta } from "@/lib/image";
import { MAX_IMAGE_SIZE_MB } from "@/lib/constants";

interface Props {
  profileId: string;
  token: string;
}

type State = "idle" | "uploading" | "done" | "error";

/**
 * Redimensiona la imagen en el cliente para reducir peso y evitar límites de
 * subida. Si falla (formato no dibujable), devuelve el archivo original.
 */
async function downscale(file: File): Promise<Blob> {
  const MAX_DIM = 1600;
  try {
    const bitmap = await createImageBitmap(file);
    const scale = Math.min(1, MAX_DIM / Math.max(bitmap.width, bitmap.height));
    if (scale >= 1) return file; // ya es pequeña
    const canvas = document.createElement("canvas");
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    const ctx = canvas.getContext("2d");
    if (!ctx) return file;
    ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    const blob: Blob | null = await new Promise((resolve) =>
      canvas.toBlob((b) => resolve(b), "image/jpeg", 0.85),
    );
    return blob ?? file;
  } catch {
    return file;
  }
}

export default function ImageUpload({ profileId, token }: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [state, setState] = useState<State>("idle");
  const [error, setError] = useState<string | null>(null);
  const [fileName, setFileName] = useState<string | null>(null);

  async function handleFile(file: File) {
    setError(null);
    const check = validateImageMeta(file.type, file.size);
    if (!check.ok) {
      setState("error");
      setError(check.error ?? "Imagen inválida.");
      return;
    }

    setFileName(file.name);
    setState("uploading");

    try {
      const blob = await downscale(file);
      // Si el redimensionado produjo JPEG, súbelo como .jpg.
      const isJpeg = blob.type === "image/jpeg";
      const uploadFile = new File(
        [blob],
        isJpeg ? "foto.jpg" : file.name,
        { type: blob.type || file.type },
      );

      const form = new FormData();
      form.append("file", uploadFile);
      form.append("profileId", profileId);
      form.append("token", token);

      const res = await fetch("/api/public/image", {
        method: "POST",
        body: form,
      });
      const json = await res.json().catch(() => ({}));
      if (!res.ok) {
        throw new Error(json.error ?? "No se pudo subir la imagen.");
      }
      setState("done");
    } catch (err) {
      setState("error");
      setError(
        err instanceof Error ? err.message : "No se pudo subir la imagen.",
      );
    }
  }

  if (state === "done") {
    return (
      <div className="rounded-2xl border border-line bg-ok-bg/60 p-4 text-center">
        <p className="text-sm font-semibold text-ok">
          ✓ Imagen subida correctamente
        </p>
      </div>
    );
  }

  return (
    <div className="rounded-2xl border border-line bg-surface p-4">
      <p className="mb-1 text-sm font-semibold text-ink">
        Subir imagen de la agencia
        <span className="ml-1 font-normal text-muted">— Opcional</span>
      </p>
      <p className="mb-3 text-xs text-muted">
        JPG, JPEG, PNG o WEBP. Máximo {MAX_IMAGE_SIZE_MB} MB. La imagen no será
        pública.
      </p>

      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        capture="environment"
        className="hidden"
        onChange={(e) => {
          const f = e.target.files?.[0];
          if (f) void handleFile(f);
        }}
      />

      <button
        type="button"
        className="btn-secondary"
        disabled={state === "uploading"}
        onClick={() => inputRef.current?.click()}
      >
        {state === "uploading"
          ? "Subiendo…"
          : state === "error"
            ? "Reintentar carga de imagen"
            : "Seleccionar imagen"}
      </button>

      {fileName && state !== "error" && (
        <p className="mt-2 truncate text-xs text-muted">{fileName}</p>
      )}
      {error && (
        <p className="mt-3 rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
          {error}
        </p>
      )}
    </div>
  );
}
