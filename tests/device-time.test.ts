import {describe,expect,it} from "vitest";
import {deviceDate,deviceTimestamp} from "../src/lib/display";

describe("hora local del dispositivo",()=>{
  it("interpreta datetime2 de SQL Server como UTC antes de mostrarlo",()=>{
    expect(deviceDate("2026-09-11T12:34:56").toISOString()).toBe("2026-09-11T12:34:56.000Z");
    expect(deviceTimestamp("2026-09-11 12:34:56")).toBe(Date.parse("2026-09-11T12:34:56Z"));
  });

  it("conserva fechas que ya incluyen su zona horaria",()=>{
    expect(deviceDate("2026-09-11T08:34:56-04:00").toISOString()).toBe("2026-09-11T12:34:56.000Z");
  });
});
