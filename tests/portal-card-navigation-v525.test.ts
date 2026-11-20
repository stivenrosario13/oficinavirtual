import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";

const app = readFileSync("src/App.tsx", "utf8");
const css = readFileSync("src/portal-card-navigation-v525.css", "utf8");
const main = readFileSync("src/main.tsx", "utf8");
const home = readFileSync("src/components/vite/PortalHome.tsx", "utf8");

describe("portal card navigation v525", () => {
  it("opens on the permission-aware card launcher", () => {
    expect(app).toContain('return "home";');
    expect(app).toContain("<PortalHome");
    expect(app).toContain("homeModules");
    expect(home).toContain("¿Qué deseas hacer?");
    expect(home).toContain('aria-label="Módulos disponibles"');
  });

  it("uses one clean account menu instead of the broken action row", () => {
    expect(app).toContain('className="header-user-menu"');
    expect(app).toContain('className="header-user-popover"');
    expect(app).toContain("Probar notificaciones");
    expect(app).toContain("Cerrar sesión");
    expect(css).toContain("grid-template-columns: minmax(170px, .7fr) minmax(280px, 620px) auto");
  });

  it("provides a complete modern desktop sidebar and mobile two-column cards with dock", () => {
    expect(css).toContain("width: 286px !important");
    expect(css).toContain("margin-left: 286px !important");
    expect(css).toContain("grid-template-columns: repeat(2, minmax(0, 1fr))");
    expect(app).toContain('className="portal-mobile-dock"');
    expect(main.lastIndexOf('import "./portal-card-navigation-v525.css"')).toBeGreaterThan(
      main.lastIndexOf('import "./professional-system-v521.css"'),
    );
  });

  it("renders real recent activity and regional status in a full-width launcher", () => {
    expect(home).toContain('/api/dashboard/home-summary');
    expect(home).toContain('Actividad reciente');
    expect(home).toContain('Estado por región');
    expect(css).toContain('.portal-launcher { width: 100%');
    expect(css).toContain('grid-template-columns: repeat(3, minmax(0, 1fr))');
  });
});
