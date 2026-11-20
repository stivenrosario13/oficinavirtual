import { expect, it } from "vitest";
import { readFileSync } from "node:fs";

const endpoints = readFileSync(".codex/decompiled-v495/WorkshopEndpoints.cs", "utf8");
const corrections = readFileSync(".codex/decompiled-v495/WorkshopCorrections.cs", "utf8");
const actor = readFileSync(".codex/decompiled-v495/WorkshopActor.cs", "utf8");
const board = readFileSync("src/components/vite/WorkshopBoard.tsx", "utf8");

it("incluye las rutas reales compiladas para editar y eliminar en Taller", () => {
  expect(endpoints).toContain('MapPut("/api/workshop/{id:guid}"');
  expect(endpoints).toContain('MapDelete("/api/workshop/{id:guid}"');
  expect(endpoints).toContain("[Microsoft.AspNetCore.Mvc.FromBody] WorkshopIntakeDeletion body");
  expect(endpoints).toContain("db.UpdateWorkshopIntake");
  expect(endpoints).toContain("db.DeleteWorkshopIntake");
});

it("protege y audita las correcciones de Taller", () => {
  expect(actor).toContain("public bool CanDeleteWorkshop");
  expect(corrections).toContain("workshop_intake_deletions");
  expect(corrections).toContain("workshop_intake_edits");
  expect(corrections).toContain("previous_data");
  expect(corrections).toContain("deleted.Contains(id)");
});

it("muestra Editar junto a Eliminar y guarda los cuatro campos", () => {
  expect(board).toContain('className="wt-edit"');
  expect(board).toContain("<PenLine/>Editar");
  expect(board).toContain('maintenanceApi(`/api/workshop/${editing.id}`, "PUT"');
  expect(board).toContain("equipment: editing.equipment.trim()");
  expect(board).toContain("serial: editing.serial.trim()");
  expect(board).toContain("failure: editing.failure.trim()");
});
