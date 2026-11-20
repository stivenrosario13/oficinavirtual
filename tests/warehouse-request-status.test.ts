import { describe, expect, it } from "vitest";
import { warehouseRequestStage } from "../src/components/vite/WarehousePdfRequests";

const request = (patch: Record<string, unknown> = {}) =>
  ({
    id: 1,
    status: "pending",
    receivedUtc: undefined,
    warehouseAssigned: [],
    warehouseRequest: { lines: [{ kind: "EQUIPMENT" }] },
    ...patch,
  }) as any;

describe("estados consecutivos de solicitudes de Almacén", () => {
  it.each([true,false])('la recepción registrada prevalece sobre un pendiente antiguo (almacén=%s)', warehouse=>{
    expect(warehouseRequestStage(request({receivedUtc:'2026-10-17T12:22:36Z',receiverDisplayName:'Diorly Abreu'}),warehouse)).toBe('authorization');
  });
  it.each([['correction_requested','authorization'],['rejected','rejected'],['cancelled','cancelled'],['approved','approved']])('el sello no revierte una decisión posterior: %s', (status,stage)=>{
    expect(warehouseRequestStage(request({status,receivedUtc:'2026-10-17T12:22:36Z'}),true)).toBe(stage);
  });
  it("mantiene solicitudes históricas recibidas en pendientes de autorizar aunque falte la fecha", () => {
    expect(warehouseRequestStage(request({status:"received",receivedUtc:null}),true)).toBe("authorization");
    expect(warehouseRequestStage(request({status:"received",receivedUtc:null}),false)).toBe("authorization");
  });
  it("mantiene cada solicitud en una sola etapa del flujo de Almacén", () => {
    expect(warehouseRequestStage(request(), true)).toBe("pending");
    expect(
      warehouseRequestStage(
        request({ status: "received", receivedUtc: "2026-09-23T12:00:00Z" }),
        true,
      ),
    ).toBe("authorization");
    expect(
      warehouseRequestStage(
        request({ status: "approved", receivedUtc: "2026-09-23T12:00:00Z" }),
        true,
      ),
    ).toBe("approved");
    expect(
      warehouseRequestStage(
        request({
          status: "approved",
          receivedUtc: "2026-09-23T12:00:00Z",
          warehouseAssigned: [{ serial: "SER-1" }],
        }),
        true,
      ),
    ).toBe("dispatch");
  });

  it("muestra Retirar al departamento y Retirada después de la entrega", () => {
    const ready = request({
      status: "approved",
      receivedUtc: "2026-09-23T12:00:00Z",
      warehouseAssigned: [{ serial: "SER-1" }],
    });
    expect(warehouseRequestStage(ready, false)).toBe("withdraw");
    expect(
      warehouseRequestStage(
        request({ ...ready, warehouseDelivery: { number: "ENT-1" } }),
        false,
      ),
    ).toBe("withdrawn");
  });
});
