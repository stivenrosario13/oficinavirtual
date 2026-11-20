import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(__dirname, "..");
const read = (path: string) => readFileSync(resolve(root, path), "utf8");

describe("V528 · restauración integral de la apariencia V520", () => {
  it("retira la capa global V521 y carga la restauración después de la navegación", () => {
    const main = read("src/main.tsx");
    expect(main).not.toContain('import "./professional-system-v521.css"');
    expect(main).toContain('import "./v520-integral-restoration-v528.css"');
    expect(main.indexOf("portal-card-navigation-v525.css")).toBeLessThan(
      main.indexOf("v520-integral-restoration-v528.css"),
    );
  });

  it("monta el centro de notificaciones en body para mantenerlo centrado", () => {
    const app = read("src/App.tsx");
    expect(app).toContain('import { createPortal } from "react-dom"');
    expect(app).toContain("notificationCenterOpen && createPortal((");
    expect(app).toContain("), document.body)}");
  });

  it("restaura modal, botones y tarjetas 3D sin redefinir todos los formularios", () => {
    const css = read("src/v520-integral-restoration-v528.css");
    expect(css).toContain(".notification-center-backdrop");
    expect(css).toContain("position: fixed !important");
    expect(css).toContain("z-index: 10000 !important");
    expect(css).toContain(".ticket-hero-actions > button");
    expect(css).toContain(".ticket-center .support-primary-three > button");
    expect(css).toContain("perspective: 1200px");
    expect(css).not.toContain(":where(input,select,textarea)");
  });
});
