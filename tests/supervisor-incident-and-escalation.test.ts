import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import {prepareAdministrativeForm,validateAdministrativeForm} from '../src/lib/administrative-form-data';

describe("incidentes de supervisores y escalamiento de Call Center", () => {
  const form = readFileSync("src/components/vite/AdministrativeRequestComposer.tsx", "utf8");
  const validation = readFileSync("src/lib/administrative-form-data.ts", "utf8");
  const ticketCenter = readFileSync("src/components/vite/TicketCenter.tsx", "utf8");
  const program = readFileSync("api/Program.cs", "utf8");

  it("elimina la incorporación de tickets y usa grupos y agencias reales", () => {
    expect(form).not.toContain("Incorporar un ticket existente");
    expect(form).not.toContain("Seleccionar ticket de esta modalidad");
    expect(form).toContain("/api/tickets/agencies");
    expect(form).toContain("Grupo del supervisor (automático)");
    expect(form).toContain('label="Agencia *"');
    expect(form).toContain("Buscar por código o nombre de agencia");
  });

  it("usa el informe simplificado y permite agregar varios tickets", () => {
    expect(form).toContain('label="Departamento *"');
    expect(form).toContain('label="Asunto *"');
    expect(form).not.toContain('label="Observación general"');
    expect(form).toContain('Grupo del supervisor (automático)');
    expect(form).toContain('Número de ticket *');
    expect(form).toContain('Fecha del ticket *');
    expect(form).toContain('Contacto de la agente de venta *');
    expect(form).toContain('Agregar otro ticket');
    expect(form).not.toContain('Tipo de incidente *');
    expect(form).not.toContain('Fecha del incidente *');
    expect(validation).toContain('Cada ticket requiere fecha válida, número y contacto de la agente de venta.');
    expect(form).toContain("if(editing||onDraftSaved||!openedFromCatalog)onClose()");
  });

  it("genera y valida varios tickets con la misma agencia", () => {
    const values={reportDate:'2026-11-12',preparedBy:'Supervisora QA',incidentDepartment:'Supervisión',subject:'Tickets pendientes',groupName:'Grupo QA',incidentAgencyId:'agency-1',incidentAgencyName:'001 · Agencia QA',generalObservations:'Revisar los casos indicados.'};
    const rows=[{agencyId:'001',ticketDate:'2026-11-11',ticketCode:'TK-100',agentPhone:'Ana · 809-555-0100'},{agencyId:'001',ticketDate:'2026-11-12',ticketCode:'TK-101',agentPhone:'Luisa · 809-555-0101'}];
    const prepared=prepareAdministrativeForm('incident_report',values,rows,[],'Supervisión',{displayName:'Supervisora QA'});
    expect(validateAdministrativeForm('incident_report',values,rows,[],prepared)).toBe('');
    expect(prepared.tables[0].rows).toHaveLength(2);
    expect(prepared.tables[0].headers).toEqual(['N.º','Fecha','Número de ticket','Contacto de la agente de venta']);
    expect(prepared.fields.map(field=>field.label)).toEqual(['Fecha de elaboración','Elaborado por','Departamento','Asunto','Grupo del supervisor','Agencia']);
  });

  it("da acceso de agencias a Vittini y Jordaniel", () => {
    expect(program).toContain('"rosmarlin.vittini@grupotejeda.local"');
    expect(program).toContain('"jordaniel.ortega@grupotejeda.local"');
    expect(program).toContain('dictionary["canManageAgencies"] = true');
  });

  it("agrupa y permite buscar supervisores al escalar desde Call Center", () => {
    expect(ticketCenter).toContain("callCenterSupervisorGroup");
    expect(ticketCenter).toContain('label="Grupo del supervisor *"');
    expect(ticketCenter).toContain('label="Supervisor *"');
    expect(ticketCenter).toContain("supervisorsForCallCenterGroup");
    expect(ticketCenter).toContain("label:person.name");
    expect(ticketCenter).not.toContain("callCenterSupervisorSearch");
  });
});
