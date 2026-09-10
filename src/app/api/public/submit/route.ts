import { NextResponse } from "next/server";
import { profileSubmitSchema, formatZodError } from "@/lib/validation";
import { validateGeolocation } from "@/lib/geo";
import { rateLimit, clientIp } from "@/lib/rate-limit";
import { createUploadToken } from "@/lib/upload-token";
import {
  submitProfile,
  syncProfileSafe,
  ConflictError,
  AgencyNotFoundError,
} from "@/server/profiles";
import { GEO_SOURCE_BROWSER } from "@/lib/constants";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  const ip = clientIp(request.headers);
  const limit = rateLimit(`submit:${ip}`);
  if (!limit.allowed) {
    return NextResponse.json(
      {
        error: `Demasiados intentos. Espera ${limit.retryAfterSeconds} segundos e inténtalo de nuevo.`,
      },
      { status: 429, headers: { "Retry-After": String(limit.retryAfterSeconds) } },
    );
  }

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ error: "Solicitud inválida." }, { status: 400 });
  }

  // Validación estructural con Zod (incluye honeypot).
  const parsed = profileSubmitSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json(
      { error: formatZodError(parsed.error).join(" ") },
      { status: 422 },
    );
  }
  const data = parsed.data;

  // Honeypot: si viene con contenido, se trata como spam.
  if (data.website && data.website.length > 0) {
    return NextResponse.json({ error: "Solicitud rechazada." }, { status: 400 });
  }

  // Fuente de geolocalización obligatoria para usuarios normales.
  if (data.geolocation.source !== GEO_SOURCE_BROWSER) {
    return NextResponse.json(
      { error: "La ubicación debe capturarse desde el dispositivo." },
      { status: 422 },
    );
  }

  // Revalidación autoritativa de la geolocalización en el servidor.
  const geo = validateGeolocation(data.geolocation, {
    now: Date.now(),
    checkAge: true,
  });
  if (!geo.ok) {
    return NextResponse.json({ error: geo.errors.join(" ") }, { status: 422 });
  }

  const context = {
    ip,
    userAgent: request.headers.get("user-agent") ?? null,
    submittedAt: new Date().toISOString(),
  };

  try {
    const profile = await submitProfile({
      agencyId: data.agencyId,
      direccion: data.direccion,
      sector: data.sector,
      municipio: data.municipio,
      provincia: data.provincia,
      tipoEstablecimiento: data.tipoEstablecimiento,
      tipoEstablecimientoOtro: data.tipoEstablecimientoOtro ?? null,
      latitude: data.geolocation.latitude,
      longitude: data.geolocation.longitude,
      accuracyMeters: data.geolocation.accuracyMeters,
      locationCapturedAt: data.geolocation.capturedAt,
      locationSource: data.geolocation.source,
      context,
      actor: null,
    });

    // Sincroniza con Google Sheets. Si falla, el perfil NO se pierde.
    const syncStatus = await syncProfileSafe(profile.id);

    return NextResponse.json({
      ok: true,
      profileId: profile.id,
      uploadToken: createUploadToken(profile.id),
      syncStatus,
    });
  } catch (err) {
    if (err instanceof ConflictError) {
      return NextResponse.json(
        { error: err.message, code: "CONFLICT" },
        { status: 409 },
      );
    }
    if (err instanceof AgencyNotFoundError) {
      return NextResponse.json(
        { error: err.message, code: "NOT_FOUND" },
        { status: 409 },
      );
    }
    console.error("POST /api/public/submit", err);
    return NextResponse.json(
      { error: "No se pudo guardar el registro. Inténtalo de nuevo." },
      { status: 500 },
    );
  }
}
