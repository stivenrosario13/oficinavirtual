import { RATE_LIMIT } from "./constants";

/**
 * Rate limiting básico en memoria (ventana deslizante por clave, normalmente IP).
 *
 * Limitación conocida: en Vercel serverless el estado no se comparte entre
 * instancias, por lo que esto es una barrera anti-abuso "best-effort". Para
 * límites estrictos a gran escala, usar un almacén compartido (p.ej. Upstash
 * Redis). Es suficiente como protección básica del formulario público.
 */
const hits = new Map<string, number[]>();

export interface RateLimitResult {
  allowed: boolean;
  remaining: number;
  retryAfterSeconds: number;
}

export function rateLimit(
  key: string,
  opts: { windowMs?: number; max?: number } = {},
): RateLimitResult {
  const windowMs = opts.windowMs ?? RATE_LIMIT.windowMs;
  const max = opts.max ?? RATE_LIMIT.max;
  const now = Date.now();
  const cutoff = now - windowMs;

  const recent = (hits.get(key) ?? []).filter((t) => t > cutoff);

  if (recent.length >= max) {
    const oldest = recent[0];
    const retryAfterSeconds = Math.max(
      1,
      Math.ceil((oldest + windowMs - now) / 1000),
    );
    hits.set(key, recent);
    return { allowed: false, remaining: 0, retryAfterSeconds };
  }

  recent.push(now);
  hits.set(key, recent);

  // Limpieza oportunista para evitar crecimiento no acotado del Map.
  if (hits.size > 5000) {
    for (const [k, v] of hits) {
      if (v.every((t) => t <= cutoff)) hits.delete(k);
    }
  }

  return { allowed: true, remaining: max - recent.length, retryAfterSeconds: 0 };
}

/** Extrae una IP de cliente razonable desde los headers de la solicitud. */
export function clientIp(headers: Headers): string {
  const fwd = headers.get("x-forwarded-for");
  if (fwd) return fwd.split(",")[0]!.trim();
  return headers.get("x-real-ip") ?? "unknown";
}
