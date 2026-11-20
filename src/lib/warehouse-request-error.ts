export async function warehouseRequestError(response:Response,fallback:string):Promise<Error>{
 const body=await response.json().catch(()=>null);
 const detail=typeof body?.message==='string'?body.message:typeof body?.error==='string'?body.error:fallback;
 const code=typeof body?.code==='string'?` · ${body.code}`:'';
 return new Error(`${detail} (HTTP ${response.status}${code})`);
}
