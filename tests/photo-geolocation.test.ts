import {afterEach,describe,expect,it,vi} from 'vitest';
import {locatePhoto,type PhotoGeo} from '../src/components/vite/EmployeeAuditForm';

const position=(accuracy=20):GeolocationPosition=>({coords:{latitude:18.5,longitude:-71.1,accuracy,altitude:null,altitudeAccuracy:null,heading:null,speed:null},timestamp:Date.now(),toJSON(){return this}});
const error=(code:number):GeolocationPositionError=>({code,message:'qa',PERMISSION_DENIED:1,POSITION_UNAVAILABLE:2,TIMEOUT:3});
function installGeolocation(geo:Partial<Geolocation>){
 Object.defineProperty(globalThis,'window',{configurable:true,value:{isSecureContext:true,setTimeout,clearTimeout}});
 Object.defineProperty(globalThis,'navigator',{configurable:true,value:{geolocation:{clearWatch:vi.fn(),watchPosition:vi.fn(),getCurrentPosition:vi.fn(),...geo}}});
}
afterEach(()=>{vi.restoreAllMocks();Reflect.deleteProperty(globalThis,'window');Reflect.deleteProperty(globalThis,'navigator')});

describe('GPS de fotografías de supervisores',()=>{
 it('recupera un error temporal de Android con getCurrentPosition',async()=>{
  installGeolocation({watchPosition:((_,failure)=>{failure?.(error(2));return 1}) as Geolocation['watchPosition'],getCurrentPosition:((success)=>success(position(35))) as Geolocation['getCurrentPosition']});
  await expect(locatePhoto()).resolves.toMatchObject({latitude:18.5,longitude:-71.1,accuracy:35});
 });
 it('acepta la precisión oficial de hasta 100 metros',async()=>{
  installGeolocation({watchPosition:((_,failure)=>{failure?.(error(3));return 1}) as Geolocation['watchPosition'],getCurrentPosition:((success)=>success(position(80))) as Geolocation['getCurrentPosition']});
  await expect(locatePhoto()).resolves.toMatchObject({accuracy:80});
 });
 it('conserva la mejor coordenada aproximada en zonas rurales en vez de bloquear la foto',async()=>{
  installGeolocation({watchPosition:((_,failure)=>{failure?.(error(3));return 1}) as Geolocation['watchPosition'],getCurrentPosition:((success)=>success(position(680))) as Geolocation['getCurrentPosition']});
  await expect(locatePhoto()).resolves.toMatchObject({accuracy:680,latitude:18.5,longitude:-71.1});
 });
 it('reutiliza solamente una lectura reciente del mismo levantamiento cuando el sensor queda temporalmente sin señal',async()=>{
  const recent:PhotoGeo={latitude:18.51,longitude:-71.11,accuracy:25,capturedAt:new Date().toISOString()};
  installGeolocation({watchPosition:((_,failure)=>{failure?.(error(2));return 1}) as Geolocation['watchPosition'],getCurrentPosition:((_,failure)=>failure?.(error(2))) as Geolocation['getCurrentPosition']});
  await expect(locatePhoto(recent)).resolves.toMatchObject({...recent,reused:true});
 });
 it('mantiene bloqueado el flujo cuando el sitio no tiene permiso, incluso con una lectura anterior',async()=>{
  const recent:PhotoGeo={latitude:18.51,longitude:-71.11,accuracy:25,capturedAt:new Date().toISOString()};
  installGeolocation({watchPosition:((_,failure)=>{failure?.(error(1));return 1}) as Geolocation['watchPosition']});
  await expect(locatePhoto(recent)).rejects.toThrow('no tiene permiso');
 });
});
