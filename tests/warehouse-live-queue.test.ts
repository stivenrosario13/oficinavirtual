import {describe,expect,it} from "vitest";
import {warehouseQueueStatus,warehouseRequestResultForOperationalDate,warehouseRequestsInOperationalQueue} from "../src/lib/warehouse-live-queue";

const request=(overrides:Partial<{id:number;status:string;createdUtc:string;decisionUtc:string}>={})=>({
  id:1,status:"pending",createdUtc:"2026-09-22T13:00:00Z",...overrides,
});

describe("cola diaria de Almacén",()=>{
  it("muestra en vivo únicamente las solicitudes que entraron durante la jornada actual",()=>{
    const items=[
      request(),
      request({id:2,status:"approved",createdUtc:"2026-09-22T15:00:00Z",decisionUtc:"2026-09-22T16:00:00Z"}),
      request({id:3,status:"rejected",createdUtc:"2026-09-21T15:00:00Z",decisionUtc:"2026-09-21T16:00:00Z"}),
    ];
    expect(warehouseRequestsInOperationalQueue(items,new Date(2026,8,22,12,0))).toHaveLength(2);
    expect(warehouseRequestsInOperationalQueue(items,new Date(2026,8,22,22,1))).toEqual([]);
  });

  it("separa pendientes, autorizadas y rechazadas",()=>{
    expect(warehouseQueueStatus(request())).toBe("PENDING");
    expect(warehouseQueueStatus(request({status:"received"}))).toBe("PENDING");
    expect(warehouseQueueStatus(request({status:"approved"}))).toBe("AUTHORIZED");
    expect(warehouseQueueStatus(request({status:"rejected"}))).toBe("REJECTED");
  });

  it("deja como pendiente la solicitud que cruza las 10:00 p. m. sin decisión",()=>{
    expect(warehouseRequestResultForOperationalDate(request(),"2026-09-22")?.result).toBe("PENDING");
  });

  it("registra decisiones de la misma jornada y no cambia el cierre por una decisión del día siguiente",()=>{
    const authorized=request({status:"approved",decisionUtc:"2026-09-22T20:00:00Z"});
    const late=request({status:"approved",decisionUtc:"2026-09-23T14:00:00Z"});
    const rejected=request({status:"rejected",decisionUtc:"2026-09-22T21:00:00Z"});
    expect(warehouseRequestResultForOperationalDate(authorized,"2026-09-22")?.result).toBe("AUTHORIZED");
    expect(warehouseRequestResultForOperationalDate(rejected,"2026-09-22")?.result).toBe("REJECTED");
    expect(warehouseRequestResultForOperationalDate(late,"2026-09-22")?.result).toBe("PENDING");
  });
});
