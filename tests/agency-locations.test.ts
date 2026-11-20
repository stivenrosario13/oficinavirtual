import {describe,it,expect} from 'vitest';
import {locationUrl,locationText,searchLocation,type AgencyLocation} from '../src/lib/agency-locations';
const agency:AgencyLocation={id:'legacy-id',codigo:'001',terminal:'Agencia Central',grupo:'Grupo Norte',direccion:'Calle Sánchez 12',latitude:18.4861,longitude:-69.9312};
describe('agency location sharing',()=>{
  it('shares the precise recorded coordinate with its agency identity',()=>{
    const url=new URL(locationUrl(agency)!);
    expect(url.searchParams.get('query')).toBe('18.4861,-69.9312');
    expect(url.searchParams.get('api')).toBe('1');
    expect(locationText(agency)).toContain('001 · Agencia Central');
    expect(locationText(agency)).toContain('Grupo Norte');
    expect(locationText(agency)).toContain(url.href);
  });
  it('never invents a map pin for missing or invalid coordinates',()=>{
    for(const [latitude,longitude] of [[null,null],[18,null],[0,0],[91,-69],[18,181],[NaN,-69],[18,Infinity]]){
      const row={...agency,latitude,longitude};
      expect(locationUrl(row)).toBeNull();
      expect(locationText(row)).toContain('Sin coordenadas registradas');
      expect(locationText(row)).not.toContain('google.com');
    }
  });
  it('finds agencies by code, group and address without accent sensitivity',()=>{
    for(const query of ['001','central','NORTE','sanchez'])expect(searchLocation(agency,query)).toBe(true);
    expect(searchLocation(agency,'sur')).toBe(false);
  });
});
