import { createReadStream, existsSync, statSync } from "node:fs";
import { createServer as createHttpServer, request as httpRequest } from "node:http";
import { request as httpsRequest } from "node:https";
import { extname, normalize, resolve, sep } from "node:path";

const port = 5173;
const root = resolve("api/wwwroot");
const apiOrigin = new URL("https://realagencias.runasp.net");
const loginRoutes = new Set(["/api/admin/login", "/api/admin/session/restore"]);
const types = {
  ".css": "text/css; charset=utf-8", ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8", ".json": "application/json; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8", ".png": "image/png", ".svg": "image/svg+xml",
  ".webmanifest": "application/manifest+json", ".woff2": "font/woff2",
};

function sendJson(response, status, value) {
  response.writeHead(status, { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" });
  response.end(JSON.stringify(value));
}

function serveFile(request, response) {
  const requestPath = decodeURIComponent(new URL(request.url || "/", "http://localhost").pathname);
  const relative = requestPath === "/" ? "index.html" : requestPath.replace(/^\/+/, "");
  const candidate = resolve(root, normalize(relative));
  const permitted = candidate === root || candidate.startsWith(`${root}${sep}`);
  const file = permitted && existsSync(candidate) && statSync(candidate).isFile()
    ? candidate
    : resolve(root, "index.html");
  if (!existsSync(file)) return sendJson(response, 503, { error: "No se encontró el build V342. Ejecuta el build antes de abrir el preview." });
  const extension = extname(file).toLowerCase();
  response.writeHead(200, {
    "Content-Type": types[extension] || "application/octet-stream",
    "Cache-Control": ["index.html", "release-guard.js", "version-web.json"].includes(relative) ? "no-store" : "public, max-age=300",
  });
  createReadStream(file).pipe(response);
}

function proxyApi(request, response) {
  const path = new URL(request.url || "/", "http://localhost").pathname;
  const method = request.method || "GET";
  if (!["GET", "HEAD"].includes(method) && !loginRoutes.has(path)) {
    return sendJson(response, 405, { error: "La vista previa es solo de consulta; no realiza cambios en producción." });
  }
  const headers = { ...request.headers, host: apiOrigin.host };
  delete headers.connection;
  const proxy = httpsRequest({
    protocol: apiOrigin.protocol,
    hostname: apiOrigin.hostname,
    port: 443,
    method,
    path: request.url,
    headers,
  }, upstream => {
    response.writeHead(upstream.statusCode || 502, upstream.headers);
    upstream.pipe(response);
  });
  proxy.once("error", () => response.headersSent ? response.destroy() : sendJson(response, 502, { error: "No se pudo consultar la API de producción desde el preview." }));
  request.pipe(proxy);
}

createHttpServer((request, response) => {
  const path = new URL(request.url || "/", "http://localhost").pathname;
  if (path.startsWith("/api/")) return proxyApi(request, response);
  return serveFile(request, response);
}).listen(port, "127.0.0.1", () => {
  console.log(`Preview V342 listo en http://127.0.0.1:${port}/?_release=V342`);
});
