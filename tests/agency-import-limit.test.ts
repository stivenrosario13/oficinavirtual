import {describe,expect,it} from "vitest";
import {AGENCY_IMPORT_CHUNK_SIZE,AGENCY_IMPORT_LIMIT,agencyImportChunks} from "../src/lib/agency-import";

describe("importación masiva de agencias",()=>{
  it("procesa exactamente 5,000 agencias en cinco bloques seguros",()=>{
    const rows=Array.from({length:5000},(_,index)=>({codigo:String(index+1)}));
    const chunks=agencyImportChunks(rows);
    expect(AGENCY_IMPORT_LIMIT).toBe(5000);
    expect(AGENCY_IMPORT_CHUNK_SIZE).toBe(1000);
    expect(chunks).toHaveLength(5);
    expect(chunks.every(chunk=>chunk.length===1000)).toBe(true);
    expect(chunks.flat()).toEqual(rows);
  });

  it("acepta el último bloque parcial y rechaza más de 5,000",()=>{
    expect(agencyImportChunks(Array.from({length:4501}))).toHaveLength(5);
    expect(agencyImportChunks(Array.from({length:4501}))[4]).toHaveLength(501);
    expect(()=>agencyImportChunks(Array.from({length:5001}))).toThrow("5,000");
  });
});
