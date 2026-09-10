/**
 * Verifica el estado del CRUD tras el intento de borrar el registro de prueba.
 *   npx tsx scripts/verify-crud.ts
 */
import { config as loadEnv } from "dotenv";
import { createClient } from "@supabase/supabase-js";

loadEnv({ path: ".env.local", quiet: true });
loadEnv({ quiet: true });

async function main() {
  const supabase = createClient(
    process.env.SUPABASE_URL!,
    process.env.SUPABASE_SERVICE_ROLE_KEY!,
    { auth: { persistSession: false, autoRefreshToken: false } },
  );

  const counts: Record<string, number | null> = {};
  for (const status of ["PENDING", "COMPLETED", "REVIEW_REQUIRED"]) {
    const { count } = await supabase
      .from("agencies")
      .select("id", { count: "exact", head: true })
      .eq("status", status);
    counts[status] = count ?? 0;
  }
  const { count: profileCount } = await supabase
    .from("agency_profiles")
    .select("id", { count: "exact", head: true });

  console.log("=== Estado global ===");
  console.log("agencies PENDING:        ", counts.PENDING);
  console.log("agencies COMPLETED:      ", counts.COMPLETED);
  console.log("agencies REVIEW_REQUIRED:", counts.REVIEW_REQUIRED);
  console.log("agency_profiles (total): ", profileCount);

  // Agencia de prueba (BOMBA CABRAL, código 6040281)
  const { data: ag } = await supabase
    .from("agencies")
    .select("id, codigo, terminal, status")
    .eq("codigo", "6040281")
    .maybeSingle();

  console.log("\n=== Agencia de prueba (código 6040281) ===");
  if (!ag) {
    console.log("No encontrada.");
  } else {
    console.log("status:", ag.status);
    const { count: pCount } = await supabase
      .from("agency_profiles")
      .select("id", { count: "exact", head: true })
      .eq("agency_id", ag.id);
    console.log("perfiles asociados:", pCount);

    const { data: audit } = await supabase
      .from("audit_log")
      .select("action, actor_email, created_at")
      .eq("entity_id", ag.id)
      .order("created_at", { ascending: false })
      .limit(10);
    console.log("auditoría de la agencia:", JSON.stringify(audit, null, 2));
  }

  // Últimos eventos de auditoría (todos)
  const { data: lastAudit } = await supabase
    .from("audit_log")
    .select("entity_type, action, actor_email, created_at")
    .order("created_at", { ascending: false })
    .limit(10);
  console.log("\n=== Últimos eventos de auditoría ===");
  console.log(JSON.stringify(lastAudit, null, 2));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
