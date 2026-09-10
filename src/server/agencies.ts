import "server-only";
import { supabaseAdmin } from "@/lib/supabase/admin";
import { AGENCY_STATUS, SYNC_STATUS } from "@/lib/constants";
import type { AgencyRow } from "@/lib/types";

export interface PendingGroup {
  grupo: string;
  pending: number;
}

/** Grupos que todavía tienen agencias PENDING (los únicos que se muestran). */
export async function getPendingGroups(): Promise<PendingGroup[]> {
  const { data, error } = await supabaseAdmin()
    .from("agencies")
    .select("grupo")
    .eq("status", AGENCY_STATUS.PENDING);

  if (error) throw error;

  const counts = new Map<string, number>();
  for (const row of data ?? []) {
    const g = (row as { grupo: string }).grupo;
    counts.set(g, (counts.get(g) ?? 0) + 1);
  }
  return [...counts.entries()]
    .map(([grupo, pending]) => ({ grupo, pending }))
    .sort((a, b) => a.grupo.localeCompare(b.grupo, "es"));
}

export interface PendingAgency {
  id: string;
  codigo: string;
  terminal: string;
}

/** Agencias PENDING de un grupo (para el paso 2 del formulario). */
export async function getPendingAgencies(
  grupo: string,
): Promise<PendingAgency[]> {
  const { data, error } = await supabaseAdmin()
    .from("agencies")
    .select("id, codigo, terminal")
    .eq("status", AGENCY_STATUS.PENDING)
    .eq("grupo", grupo)
    .order("terminal", { ascending: true });

  if (error) throw error;
  return (data ?? []) as PendingAgency[];
}

export async function getAgencyById(id: string): Promise<AgencyRow | null> {
  const { data, error } = await supabaseAdmin()
    .from("agencies")
    .select("*")
    .eq("id", id)
    .maybeSingle();
  if (error) throw error;
  return (data as AgencyRow) ?? null;
}

export interface GroupProgress {
  grupo: string;
  esperadas: number;
  completadas: number;
  pendientes: number;
  revision: number;
  porcentaje: number;
}

export interface DashboardStats {
  totalExpected: number;
  completed: number;
  pending: number;
  reviewRequired: number;
  syncErrors: number;
  percent: number;
  groups: GroupProgress[];
}

/** Indicadores globales y por grupo para el dashboard administrativo. */
export async function getDashboardStats(): Promise<DashboardStats> {
  const supabase = supabaseAdmin();

  const [{ data: agencies, error }, syncErrorsResult] = await Promise.all([
    supabase.from("agencies").select("grupo, status"),
    supabase
      .from("agency_profiles")
      .select("id", { count: "exact", head: true })
      .in("google_sheet_sync_status", [SYNC_STATUS.FAILED, SYNC_STATUS.PENDING]),
  ]);

  if (error) throw error;
  if (syncErrorsResult.error) throw syncErrorsResult.error;

  const rows = (agencies ?? []) as { grupo: string; status: string }[];

  const byGroup = new Map<string, GroupProgress>();
  let totalExpected = 0;
  let completed = 0;
  let pending = 0;
  let reviewRequired = 0;

  for (const row of rows) {
    totalExpected += 1;
    const g =
      byGroup.get(row.grupo) ??
      {
        grupo: row.grupo,
        esperadas: 0,
        completadas: 0,
        pendientes: 0,
        revision: 0,
        porcentaje: 0,
      };
    g.esperadas += 1;
    if (row.status === AGENCY_STATUS.COMPLETED) {
      g.completadas += 1;
      completed += 1;
    } else if (row.status === AGENCY_STATUS.PENDING) {
      g.pendientes += 1;
      pending += 1;
    } else if (row.status === AGENCY_STATUS.REVIEW_REQUIRED) {
      g.revision += 1;
      reviewRequired += 1;
    }
    byGroup.set(row.grupo, g);
  }

  const groups = [...byGroup.values()]
    .map((g) => ({
      ...g,
      porcentaje:
        g.esperadas > 0 ? Math.round((g.completadas / g.esperadas) * 100) : 0,
    }))
    .sort((a, b) => a.grupo.localeCompare(b.grupo, "es"));

  return {
    totalExpected,
    completed,
    pending,
    reviewRequired,
    syncErrors: syncErrorsResult.count ?? 0,
    percent: totalExpected > 0 ? Math.round((completed / totalExpected) * 100) : 0,
    groups,
  };
}
