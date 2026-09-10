"use client";

import { useRouter, usePathname, useSearchParams } from "next/navigation";
import { useState } from "react";
import { AGENCY_STATUS, SYNC_STATUS } from "@/lib/constants";

export default function RecordFilters({ groups }: { groups: string[] }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("search") ?? "");

  function update(next: Record<string, string>) {
    const qs = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(next)) {
      if (v) qs.set(k, v);
      else qs.delete(k);
    }
    qs.delete("page"); // volver a la primera página al filtrar
    router.push(`${pathname}?${qs.toString()}`);
  }

  return (
    <div className="mb-4 grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-4">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          update({ search });
        }}
      >
        <input
          className="field-input"
          placeholder="Buscar código, terminal o grupo…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </form>

      <select
        className="field-input"
        defaultValue={params.get("grupo") ?? ""}
        onChange={(e) => update({ grupo: e.target.value })}
      >
        <option value="">Todos los grupos</option>
        {groups.map((g) => (
          <option key={g} value={g}>
            {g}
          </option>
        ))}
      </select>

      <select
        className="field-input"
        defaultValue={params.get("status") ?? ""}
        onChange={(e) => update({ status: e.target.value })}
      >
        <option value="">Todos los estados</option>
        <option value={AGENCY_STATUS.PENDING}>Pendiente</option>
        <option value={AGENCY_STATUS.COMPLETED}>Completada</option>
        <option value={AGENCY_STATUS.REVIEW_REQUIRED}>En revisión</option>
      </select>

      <select
        className="field-input"
        defaultValue={params.get("sync") ?? ""}
        onChange={(e) => update({ sync: e.target.value })}
      >
        <option value="">Toda sincronización</option>
        <option value={SYNC_STATUS.SYNCED}>Sincronizado</option>
        <option value={SYNC_STATUS.PENDING}>Sync pendiente</option>
        <option value={SYNC_STATUS.FAILED}>Sync fallida</option>
      </select>
    </div>
  );
}
