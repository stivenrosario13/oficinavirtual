import { describe, expect, it } from "vitest";
import type { PortalSession } from "../src/lib/session";
import { canAccessTicketSection } from "../src/lib/support-navigation";

const supervisorSession = {
  id: "supervisor-test",
  role: "GroupAdministrator",
  permissions: {
    canCreateTickets: false,
    canViewSupportDashboard: false,
    canUseSupportChat: false,
  },
} as PortalSession;

describe("navegación de tickets", () => {
  it("muestra la sección Tickets a todo supervisor de agencia", () => {
    expect(canAccessTicketSection(supervisorSession)).toBe(true);
  });

  it("no habilita la sección a una cuenta sin rol ni permisos de soporte", () => {
    expect(
      canAccessTicketSection({
        ...supervisorSession,
        role: "Viewer",
      } as PortalSession),
    ).toBe(false);
  });
});
