import "server-only";
import { supabaseAdmin } from "./admin";

/**
 * Almacenamiento de imágenes de agencias en Supabase Storage (bucket privado).
 *
 * Se usa en lugar de Google Drive porque una service account de Google no tiene
 * cuota de almacenamiento en cuentas personales de Gmail (solo funcionaría con
 * Google Workspace + Unidades compartidas). Supabase Storage cumple el mismo
 * objetivo: imagen privada, vinculada al perfil, servida por ruta autenticada.
 */
export const PHOTO_BUCKET = "agency-photos";

const ONE_YEAR_SECONDS = 60 * 60 * 24 * 365;

export interface StoredPhoto {
  /** Ruta del objeto dentro del bucket (se guarda en photo_drive_file_id). */
  path: string;
  /** URL firmada de larga duración para consulta externa (Google Sheet). */
  signedUrl: string | null;
}

export async function uploadAgencyPhoto(params: {
  profileId: string;
  buffer: Buffer;
  mimeType: string;
  ext: string;
}): Promise<StoredPhoto> {
  const supabase = supabaseAdmin();
  const path = `profiles/${params.profileId}.${params.ext}`;

  const { error } = await supabase.storage
    .from(PHOTO_BUCKET)
    .upload(path, params.buffer, {
      contentType: params.mimeType,
      upsert: true,
    });
  if (error) throw error;

  const { data } = await supabase.storage
    .from(PHOTO_BUCKET)
    .createSignedUrl(path, ONE_YEAR_SECONDS);

  return { path, signedUrl: data?.signedUrl ?? null };
}

export interface PhotoContent {
  data: Buffer;
  mimeType: string;
}

/** Descarga el contenido de una imagen para servirla por ruta autenticada. */
export async function getAgencyPhoto(path: string): Promise<PhotoContent> {
  const supabase = supabaseAdmin();
  const { data, error } = await supabase.storage
    .from(PHOTO_BUCKET)
    .download(path);
  if (error || !data) {
    throw error ?? new Error("No se encontró la imagen.");
  }
  const buffer = Buffer.from(await data.arrayBuffer());
  return { data: buffer, mimeType: data.type || "application/octet-stream" };
}

export async function deleteAgencyPhoto(path: string): Promise<void> {
  const supabase = supabaseAdmin();
  await supabase.storage.from(PHOTO_BUCKET).remove([path]);
}
