import { NextResponse } from "next/server";
import { rateLimit, clientIp } from "@/lib/rate-limit";
import { verifyUploadToken } from "@/lib/upload-token";
import { validateImageMeta, extForMime } from "@/lib/image";
import { uploadAgencyPhoto } from "@/lib/supabase/storage";
import { attachProfilePhoto } from "@/server/profiles";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  const ip = clientIp(request.headers);
  const limit = rateLimit(`image:${ip}`, { max: 12 });
  if (!limit.allowed) {
    return NextResponse.json(
      { error: `Demasiados intentos. Espera ${limit.retryAfterSeconds}s.` },
      { status: 429 },
    );
  }

  let form: FormData;
  try {
    form = await request.formData();
  } catch {
    return NextResponse.json({ error: "Solicitud inválida." }, { status: 400 });
  }

  const file = form.get("file");
  const profileId = String(form.get("profileId") ?? "");
  const token = String(form.get("token") ?? "");

  if (!profileId || !verifyUploadToken(token, profileId)) {
    return NextResponse.json(
      { error: "No se pudo validar la autorización segura de subida." },
      { status: 401 },
    );
  }

  if (!(file instanceof File)) {
    return NextResponse.json(
      { error: "No se recibió ninguna imagen." },
      { status: 400 },
    );
  }

  const check = validateImageMeta(file.type, file.size);
  if (!check.ok) {
    return NextResponse.json({ error: check.error }, { status: 422 });
  }

  try {
    const buffer = Buffer.from(await file.arrayBuffer());
    const ext = extForMime(file.type);

    const uploaded = await uploadAgencyPhoto({
      profileId,
      buffer,
      mimeType: file.type,
      ext,
    });

    await attachProfilePhoto(
      profileId,
      { fileId: uploaded.path, url: uploaded.signedUrl },
      { actor: null, replaced: false },
    );

    return NextResponse.json({ ok: true });
  } catch (err) {
    console.error("POST /api/public/image", err);
    return NextResponse.json(
      {
        error:
          "No se pudo subir la imagen. El registro sigue guardado; puedes reintentar.",
      },
      { status: 502 },
    );
  }
}
