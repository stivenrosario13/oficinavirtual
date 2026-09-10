import "server-only";
import { createClient, type SupabaseClient } from "@supabase/supabase-js";
import { supabaseEnv } from "@/lib/env";

/**
 * Cliente de Supabase con la SERVICE ROLE KEY.
 * Solo debe usarse en el servidor. La clave nunca llega al navegador.
 */
let cached: SupabaseClient | null = null;

export function supabaseAdmin(): SupabaseClient {
  if (cached) return cached;
  cached = createClient(supabaseEnv.url(), supabaseEnv.serviceRoleKey(), {
    auth: { persistSession: false, autoRefreshToken: false },
  });
  return cached;
}
