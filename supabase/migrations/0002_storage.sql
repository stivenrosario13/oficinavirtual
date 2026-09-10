-- =============================================================================
-- Bucket privado para imágenes de agencias (Supabase Storage).
-- Alternativa a Google Drive: una service account de Google no tiene cuota de
-- almacenamiento en cuentas personales de Gmail.
--
-- Puedes ejecutar este SQL, o usar el script:  npx tsx scripts/setup-storage.ts
-- =============================================================================
insert into storage.buckets (id, name, public)
values ('agency-photos', 'agency-photos', false)
on conflict (id) do nothing;

-- La aplicación accede al Storage con la SERVICE ROLE KEY (omite RLS). No se
-- crean políticas públicas: las imágenes se sirven por una ruta autenticada.
