export const AGENCY_IMPORT_LIMIT=5000;
export const AGENCY_IMPORT_CHUNK_SIZE=1000;

export function agencyImportChunks<T>(rows:T[]):T[][]{
  if(rows.length>AGENCY_IMPORT_LIMIT)throw new Error("El archivo supera el límite de 5,000 agencias por importación.");
  const chunks:T[][]=[];
  for(let index=0;index<rows.length;index+=AGENCY_IMPORT_CHUNK_SIZE)chunks.push(rows.slice(index,index+AGENCY_IMPORT_CHUNK_SIZE));
  return chunks;
}
