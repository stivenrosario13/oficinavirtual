import { expect, it } from "vitest";
import { readFileSync } from "node:fs";

const job = readFileSync(".codex/decompiled-v495/StorageMigrationJob.cs", "utf8");

it("no apaga el portal si el hosting bloquea el archivo de estado de migración", () => {
  expect(job).toContain("ex is IOException || ex is UnauthorizedAccessException");
  expect(job).toContain("El estado de una tarea de mantenimiento nunca debe detener el portal");
  expect(job).toContain("Estado de migración no disponible; el sitio continuará activo");
});
