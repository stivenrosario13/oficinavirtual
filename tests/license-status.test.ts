import {it,expect} from 'vitest';
import {readLicenseStatus} from '../src/lib/license-status';
it('rejects the actual forbidden payload without treating it as a license',async()=>{
 await expect(readLicenseStatus(new Response(JSON.stringify({error:'Este recurso no pertenece al perfil administrativo.'}),{status:403}))).rejects.toThrow();
});
it('rejects malformed and missing status without granting access',async()=>{
 for(const data of [null,{}, {allowed:true}, {allowed:true,status:null}, {allowed:true,status:''}])await expect(readLicenseStatus(new Response(JSON.stringify(data)))).rejects.toThrow();
});
it('preserves valid allowed and denied results',async()=>{
 for(const allowed of [true,false])expect(await readLicenseStatus(new Response(JSON.stringify({allowed,status:'ACTIVE',code:'QA',message:'QA'})))).toMatchObject({allowed,status:'ACTIVE'});
});
