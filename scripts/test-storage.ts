/**
 * Prueba directa de Supabase Storage (subir, URL firmada, descargar, borrar).
 *   npx tsx scripts/test-storage.ts
 */
import { config as loadEnv } from "dotenv";
import { createClient } from "@supabase/supabase-js";

loadEnv({ path: ".env.local", quiet: true });
loadEnv({ quiet: true });

const BUCKET = "agency-photos";

async function main() {
  const supabase = createClient(
    process.env.SUPABASE_URL!,
    process.env.SUPABASE_SERVICE_ROLE_KEY!,
    { auth: { persistSession: false, autoRefreshToken: false } },
  );

  const png = Buffer.from(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
    "base64",
  );
  const path = `profiles/_prueba-${Date.now()}.png`;

  const up = await supabase.storage
    .from(BUCKET)
    .upload(path, png, { contentType: "image/png", upsert: true });
  if (up.error) {
    console.error("❌ Subida:", up.error.message);
    process.exit(1);
  }
  console.log("✅ Storage: subida OK ->", path);

  const signed = await supabase.storage
    .from(BUCKET)
    .createSignedUrl(path, 3600);
  console.log("✅ Storage: URL firmada OK ->", signed.data?.signedUrl ? "generada" : "sin url");

  const dl = await supabase.storage.from(BUCKET).download(path);
  console.log("✅ Storage: descarga OK ->", dl.data ? `${dl.data.size} bytes` : "sin datos");

  await supabase.storage.from(BUCKET).remove([path]);
  console.log("✅ Storage: archivo de prueba eliminado.");
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
