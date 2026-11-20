export type PhotoGeo = {latitude:number;longitude:number;accuracy:number;capturedAt:string;reused?:boolean};
export const PHOTO_MATCH_RADIUS_METERS=75;
export const PRIMARY_AUDIT_PHOTO_MATCH_RADIUS_METERS=3000;
const RECENT_MS=120_000;

export function photoDistance(a:Pick<PhotoGeo,'latitude'|'longitude'>,b:Pick<PhotoGeo,'latitude'|'longitude'>){
 const p=Math.PI/180,dLat=(b.latitude-a.latitude)*p,dLon=(b.longitude-a.longitude)*p;
 const h=Math.sin(dLat/2)**2+Math.cos(a.latitude*p)*Math.cos(b.latitude*p)*Math.sin(dLon/2)**2;
 return 6_371_000*2*Math.atan2(Math.sqrt(h),Math.sqrt(Math.max(0,1-h)));
}

export function validPhotoGeo(geo:PhotoGeo){
 return Number.isFinite(geo.latitude)&&Math.abs(geo.latitude)<=90
  &&Number.isFinite(geo.longitude)&&Math.abs(geo.longitude)<=180
  &&!(geo.latitude===0&&geo.longitude===0)&&Number.isFinite(geo.accuracy)&&geo.accuracy>0;
}

// Compare the reported uncertainty areas, keeping the original coordinates and accuracy.
export function photoLocationMatches(reference:PhotoGeo,photo:PhotoGeo,minimumRadiusMeters=PHOTO_MATCH_RADIUS_METERS){
 return validPhotoGeo(reference)&&validPhotoGeo(photo)
  &&photoDistance(reference,photo)<=Math.max(minimumRadiusMeters,reference.accuracy+photo.accuracy);
}

export function locatePhoto(recentReading?:PhotoGeo|null):Promise<PhotoGeo>{
 return new Promise((resolve,reject)=>{
  if(!window.isSecureContext||!navigator.geolocation){reject(new Error('La captura necesita HTTPS y un navegador con ubicación disponible.'));return;}
  let best:PhotoGeo|null=null,watchId:number|null=null,done=false,fallbackStarted=false;
  const timers:number[]=[];
  const fresh=(geo:PhotoGeo)=>{const time=Date.parse(geo.capturedAt);return validPhotoGeo(geo)&&Number.isFinite(time)&&Math.abs(Date.now()-time)<=RECENT_MS;};
  const cleanup=()=>{if(watchId!==null)navigator.geolocation.clearWatch(watchId);timers.forEach(timer=>window.clearTimeout(timer));};
  const finish=(geo:PhotoGeo)=>{if(done)return;done=true;cleanup();resolve(geo);};
  const fail=(error:Pick<GeolocationPositionError,'code'>)=>{
   if(done)return;
   if(error.code!==1){
    if(best&&fresh(best)){finish(best);return;}
    if(recentReading&&fresh(recentReading)){finish({...recentReading,reused:true});return;}
   }
   done=true;cleanup();
   reject(new Error(error.code===1
    ?'El sitio no tiene permiso de ubicación. Abre el candado del navegador, permite Ubicación y pulsa Reintentar GPS.'
    :'El dispositivo todavía no entregó una coordenada GPS. Mantén la ubicación activa y pulsa Reintentar GPS; la foto permanece guardada.'));
  };
  const consider=(position:GeolocationPosition)=>{
   if(done||!Number.isFinite(position.timestamp))return;
   const geo:PhotoGeo={latitude:position.coords.latitude,longitude:position.coords.longitude,accuracy:position.coords.accuracy,capturedAt:new Date(position.timestamp).toISOString()};
   if(fresh(geo)&&(!best||!fresh(best)||geo.accuracy<best.accuracy)){
    const first=!best;best=geo;
    if(first)timers.push(window.setTimeout(()=>{if(best)finish(best);},5_000));
   }
  };
  const fallback=()=>{
   if(done||fallbackStarted)return;
   fallbackStarted=true;
   navigator.geolocation.getCurrentPosition(position=>{if(done)return;consider(position);fail({code:3});},fail,{enableHighAccuracy:true,maximumAge:RECENT_MS,timeout:15_000});
  };
  watchId=navigator.geolocation.watchPosition(position=>{
   if(done)return;consider(position);
   if(best&&best.accuracy<=12)finish(best);
  },error=>{
   if(done)return;
   if(error.code===1||best){fail(error);return;}
   fallback();
  },{enableHighAccuracy:true,maximumAge:0,timeout:20_000});
  // Some browser wrappers invoke callbacks synchronously.
  if(done){cleanup();return;}
  timers.push(window.setTimeout(()=>{if(best)fail({code:3});else fallback();},12_000));
  timers.push(window.setTimeout(()=>fail({code:3}),28_000));
 });
}
