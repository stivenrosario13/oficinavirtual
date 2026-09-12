import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath, URL } from "node:url";
import {readFileSync} from "node:fs";

const apiProxyTarget = process.env.VITE_API_PROXY_TARGET || "http://127.0.0.1:8080";
const previewReadOnly = process.env.PREVIEW_READ_ONLY === "true";

export default defineConfig({
  plugins: [react(),{
    name:"local-pdf-preview",
    configureServer(server){
      server.middlewares.use((req,res,next)=>{
        const path=req.url?.split("?")[0];
        const allowsSessionStart = req.method === "POST" && ["/api/admin/login", "/api/admin/session/restore"].includes(path || "");
        if(previewReadOnly && path?.startsWith("/api/") && req.method !== "GET" && req.method !== "HEAD" && !allowsSessionStart){
          res.statusCode=405;
          res.setHeader("Content-Type","application/json");
          res.end(JSON.stringify({error:"La vista previa es solo de consulta; no realiza cambios en producción."}));
          return;
        }
        if(path!=="/vendor/pdfjs/pdf.mjs"&&path!=="/vendor/pdfjs/pdf.worker.mjs")return next();
        res.setHeader("Content-Type","text/javascript");
        res.end(readFileSync(fileURLToPath(new URL(`./public${path}`,import.meta.url))));
      });
    },
  }],
  resolve: { alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) } },
  server: {
    port: 5173,
    strictPort: true,
    watch: {
      ignored: ["**/api/bin/**", "**/api/obj/**", "**/artifacts/**"],
    },
    proxy: { "/api": { target: apiProxyTarget, changeOrigin: true } },
  },
  build: {
    outDir: "api/wwwroot",
    emptyOutDir: true,
    rollupOptions: {
      output: {
        manualChunks: {
          react: ["react", "react-dom"],
          icons: ["lucide-react"],
        },
      },
    },
  },
});
