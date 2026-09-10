import "server-only";
import crypto from "node:crypto";
import { authEnv } from "./env";

/**
 * Token firmado (HMAC) que autoriza subir/relanzar la imagen de un perfil
 * recién creado, sin exigir autenticación administrativa.
 *
 * Se entrega al cliente tras un envío exitoso y permanece válido para que una
 * auditoría de campo pueda completarse sin depender del tiempo transcurrido.
 * La firma sigue evitando subidas a perfiles arbitrarios.
 */

function secret(): string {
  // Reutiliza el secreto de Auth.js; si no está, falla de forma explícita.
  const s = authEnv.secret();
  if (!s) {
    throw new Error(
      "Falta NEXTAUTH_SECRET/AUTH_SECRET para firmar tokens de subida.",
    );
  }
  return s;
}

function sign(data: string): string {
  return crypto
    .createHmac("sha256", secret())
    .update(data)
    .digest("base64url");
}

/** Genera un token para autorizar la imagen del perfil `profileId`. */
export function createUploadToken(profileId: string): string {
  return `${profileId}.${sign(profileId)}`;
}

/** Verifica el token contra un profileId sin aplicar caducidad temporal. */
export function verifyUploadToken(
  token: string | null | undefined,
  profileId: string,
): boolean {
  if (!token) return false;
  const parts = token.split(".");
  if (parts.length !== 2 && parts.length !== 3) return false;
  const [id] = parts;
  if (id !== profileId) return false;

  // También admite los tokens de tres segmentos creados por versiones
  // anteriores, pero ya no descarta una auditoría por el reloj del servidor.
  const payload = parts.length === 3 ? `${id}.${parts[1]}` : id;
  const sig = parts.at(-1)!;
  const expected = sign(payload);
  // Comparación en tiempo constante.
  const a = Buffer.from(sig);
  const b = Buffer.from(expected);
  if (a.length !== b.length) return false;
  return crypto.timingSafeEqual(a, b);
}
