"use client";

import { useState, useTransition } from "react";
import { retrySyncAction } from "@/server/actions";

export default function SyncRetryButton({
  profileId,
  compact = false,
}: {
  profileId: string;
  compact?: boolean;
}) {
  const [pending, startTransition] = useTransition();
  const [msg, setMsg] = useState<string | null>(null);

  function retry() {
    setMsg(null);
    startTransition(async () => {
      const res = await retrySyncAction(profileId);
      if (!res.ok) setMsg(res.error ?? "Falló el reintento.");
    });
  }

  return (
    <span className="inline-flex flex-col items-start gap-1">
      <button
        type="button"
        onClick={retry}
        disabled={pending}
        className={
          compact
            ? "rounded-md border border-line px-2 py-1 text-xs font-medium text-brand-600 hover:bg-brand-50 disabled:opacity-50"
            : "btn-secondary"
        }
      >
        {pending ? "Reintentando…" : "Reintentar sync"}
      </button>
      {msg && <span className="text-xs text-danger">{msg}</span>}
    </span>
  );
}
