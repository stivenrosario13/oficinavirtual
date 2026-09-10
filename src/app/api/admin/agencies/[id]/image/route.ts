import { NextResponse } from "next/server";
import { requireAdmin, NotAuthorizedError } from "@/server/admin-guard";
import { getAgencyWithProfile, attachProfilePhoto } from "@/server/profiles";
import { getAgencyPhoto, uploadAgencyPhoto } from "@/lib/supabase/storage";
import { validateImageMeta, extForMime } from "@/lib/image";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

type Ctx = { params: Promise<{ id: string }> };

/** GET: sirve la imagen del perfil mediante una ruta autenticada. */
export async function GET(_request: Request, ctx: Ctx) {
  try {
    await requireAdmin();
    const { id } = await ctx.params;
    const agency = await getAgencyWithProfile(id);
    const path = agency?.profile?.photo_drive_file_id;
    if (!path) {
      return NextResponse.json({ error: "Sin imagen." }, { status: 404 });
    }
    const { data, mimeType } = await getAgencyPhoto(path);
    return new NextResponse(new Uint8Array(data), {
      status: 200,
      headers: {
        "Content-Type": mimeType,
        "Cache-Control": "private, no-store",
      },
    });
  } catch (err) {
    if (err instanceof NotAuthorizedError) {
      return NextResponse.json({ error: err.message }, { status: 401 });
    }
    console.error("GET admin image", err);
    return NextResponse.json(
      { error: "No se pudo cargar la imagen." },
      { status: 500 },
    );
  }
}

/** POST: reemplaza (o carga) la imagen del perfil desde administración. */
export async function POST(request: Request, ctx: Ctx) {
  try {
    const actor = await requireAdmin();
    const { id } = await ctx.params;
    const agency = await getAgencyWithProfile(id);
    const profile = agency?.profile;
    if (!profile) {
      return NextResponse.json(
        { error: "La agencia no tiene un perfil registrado." },
        { status: 404 },
      );
    }

    const form = await request.formData();
    const file = form.get("file");
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

    const buffer = Buffer.from(await file.arrayBuffer());
    const uploaded = await uploadAgencyPhoto({
      profileId: profile.id,
      buffer,
      mimeType: file.type,
      ext: extForMime(file.type),
    });

    await attachProfilePhoto(
      profile.id,
      { fileId: uploaded.path, url: uploaded.signedUrl },
      { actor, replaced: Boolean(profile.photo_drive_file_id) },
    );

    return NextResponse.json({ ok: true });
  } catch (err) {
    if (err instanceof NotAuthorizedError) {
      return NextResponse.json({ error: err.message }, { status: 401 });
    }
    console.error("POST admin image", err);
    return NextResponse.json(
      { error: "No se pudo subir la imagen." },
      { status: 500 },
    );
  }
}
