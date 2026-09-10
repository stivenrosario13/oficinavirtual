/**
 * Crea el bucket privado de Supabase Storage para las imágenes de agencias.
 * Idempotente. Ejecutar una sola vez:
 *
 *   npx tsx scripts/setup-storage.ts
 */
import { config as loadEnv } from "dotenv";
import { createClient } from "@supabase/supabase-js";

loadEnv({ path: ".env.local", quiet: true });
loadEnv({ quiet: true });

const BUCKET = "agency-photos";

async function main() {
  const url = process.env.SUPABASE_URL;
  const key = process.env.SUPABASE_SERVICE_ROLE_KEY;
  if (!url || !key) {
    console.error("Faltan SUPABASE_URL / SUPABASE_SERVICE_ROLE_KEY en .env.local");
    process.exit(1);
  }
  const supabase = createClient(url, key, {
    auth: { persistSession: false, autoRefreshToken: false },
  });

  const { data: existing } = await supabase.storage.getBucket(BUCKET);
  if (existing) {
    console.log(`✅ El bucket "${BUCKET}" ya existe (privado=${!existing.public}).`);
    return;
  }

  const { error } = await supabase.storage.createBucket(BUCKET, {
    public: false,
    fileSizeLimit: "6MB",
    allowedMimeTypes: ["image/jpeg", "image/png", "image/webp"],
  });
  if (error) {
    console.error("Error al crear el bucket:", error.message);
    process.exit(1);
  }
  console.log(`✅ Bucket "${BUCKET}" creado (privado).`);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
