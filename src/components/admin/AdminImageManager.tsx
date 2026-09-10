"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { validateImageMeta } from "@/lib/image";
import { MAX_IMAGE_SIZE_MB } from "@/lib/constants";

export default function AdminImageManager({
  agencyId,
  hasImage,
}: {
  agencyId: string;
  hasImage: boolean;
}) {
  const router = useRouter();
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [bust, setBust] = useState(0);
  const [present, setPresent] = useState(hasImage);

  const src = `/api/admin/agencies/${agencyId}/image?v=${bust}`;

  async function upload(file: File) {
    setError(null);
    const check = validateImageMeta(file.type, file.size);
    if (!check.ok) {
      setError(check.error ?? "Imagen inválida.");
      return;
    }
    setUploading(true);
    try {
      const form = new FormData();
      form.append("file", file);
      const res = await fetch(`/api/admin/agencies/${agencyId}/image`, {
        method: "POST",
        body: form,
      });
      const json = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(json.error ?? "No se pudo subir la imagen.");
      setPresent(true);
      setBust((b) => b + 1);
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error al subir.");
    } finally {
      setUploading(false);
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {present ? (
        <img
          src={src}
          alt="Imagen de la agencia"
          className="max-h-64 w-full rounded-xl border border-line object-contain"
        />
      ) : (
        <p className="rounded-xl bg-canvas px-4 py-8 text-center text-sm text-muted">
          Sin imagen registrada.
        </p>
      )}

      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        className="hidden"
        onChange={(e) => {
          const f = e.target.files?.[0];
          if (f) void upload(f);
        }}
      />
      <button
        type="button"
        className="btn-secondary"
        disabled={uploading}
        onClick={() => inputRef.current?.click()}
      >
        {uploading
          ? "Subiendo…"
          : present
            ? "Reemplazar imagen"
            : "Subir imagen"}
      </button>
      <p className="text-xs text-muted">
        JPG, JPEG, PNG o WEBP. Máximo {MAX_IMAGE_SIZE_MB} MB.
      </p>
      {error && (
        <p className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
          {error}
        </p>
      )}
    </div>
  );
}
