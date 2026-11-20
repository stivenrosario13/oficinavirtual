export type LicenseStatus = {
 enabled:boolean; allowed:boolean; status:string; code:string; message:string;
 expiresAt?:string|null; usingGracePeriod:boolean;
};
export async function readLicenseStatus(response:Response):Promise<LicenseStatus>{
 const data:unknown=await response.json();
 if(!response.ok||!data||typeof data!=='object')throw new Error('LICENSE_UNAVAILABLE');
 const value=data as Record<string,unknown>;
 if(typeof value.allowed!=='boolean'||typeof value.status!=='string'||!value.status.trim()||typeof value.code!=='string'||typeof value.message!=='string')throw new Error('LICENSE_INVALID_RESPONSE');
 return {enabled:value.enabled===true,allowed:value.allowed,status:value.status,code:value.code,message:value.message,usingGracePeriod:value.usingGracePeriod===true};
}
