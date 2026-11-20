/** Reserved box recorded by the PDF generator. Coordinates are millimetres from the top. */
export function administrativeSealLayout(subject:string,pageSizes:{width:number;height:number}[]){
  const marker=subject.match(/\|ADMIN_SEAL=(\d+),([\d.]+),([\d.]+),([\d.]+),([\d.]+)/);
  if(!marker)return null;
  const [page,x,top,width,height]=marker.slice(1).map(Number),size=pageSizes[page-1],mm=72/25.4;
  if(!size||![x,top,width,height].every(Number.isFinite)||x<0||top<0||width!==87||height!==33||
    (x+width)*mm>size.width||(top+height)*mm>size.height)return null;
  return {page:page-1,x:x*mm,y:size.height-(top+height)*mm,width:width*mm,height:height*mm};
}

/** All three spaces are on page one, above the form footer. */
export const administrativeFormSealSpaces={status:{x:61.5,top:186,width:87,height:33},delivery:{x:12,top:230,width:90,height:36},receipt:{x:108,top:230,width:90,height:36}};
export function administrativeCustodyLayout(subject:string,pageSizes:{width:number;height:number}[]){
 const box=(key:string)=>{
  const marker=subject.match(new RegExp(`\\|${key}=(\\d+),([\\d.]+),([\\d.]+),([\\d.]+),([\\d.]+)`));
  if(!marker)return null;
  const [page,x,top,width,height]=marker.slice(1).map(Number),size=pageSizes[0],mm=72/25.4;
  if(page!==1||!size||![x,top,width,height].every(Number.isFinite)||x<0||top<0||width!==90||height!==36||(x+width)*mm>size.width||(top+height)*mm>size.height-15*mm)return null;
  return {page:0,x:x*mm,y:size.height-(top+height)*mm,width:width*mm,height:height*mm};
 };
 const delivery=box('ADMIN_DELIVERY'),receipt=box('ADMIN_RECEIPT');
 return delivery&&receipt?{delivery,receipt}:null;
}
