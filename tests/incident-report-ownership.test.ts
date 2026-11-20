import {describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {administrativeDefaults} from '../src/lib/administrative-form-defaults';
import {assignedAdministrativeForms,isIncidentOperatorAccount} from '../src/lib/administrative-profiles';

describe('informe de incidentes de Juegos tradicionales',()=>{
 it('solo se asigna a Yesica/Yessica y Lucianny, nunca a Sulay',()=>{
  expect(assignedAdministrativeForms('yessica.mercedes')).toEqual(['incident_report']);
  expect(assignedAdministrativeForms('yesica.mercedes@grupotejeda.local')).toEqual(['incident_report']);
  expect(assignedAdministrativeForms('lucianny.gonzalez')).toEqual(['incident_report']);
  expect(assignedAdministrativeForms('lucianny.gonzales')).toEqual(['incident_report']);
  expect(assignedAdministrativeForms('DOMINIO\\lucianny.gonzalez@empresa.local')).toEqual(['incident_report']);
  expect(assignedAdministrativeForms('whilmaly.sulay')).not.toContain('incident_report');
 });

 it('reconoce a las dos operadoras por el nombre del perfil aunque el correo sea diferente',()=>{
  expect(isIncidentOperatorAccount('cuenta.legada@empresa.local','Lucianny González')).toBe(true);
  expect(isIncidentOperatorAccount('cuenta.legada@empresa.local','Yessica Mercedes')).toBe(true);
  expect(isIncidentOperatorAccount('supervisor@empresa.local','Steven Supervisor')).toBe(false);
 });

 it('elige como responsable a la gestora autenticada',()=>{
  expect(administrativeDefaults('incident_report',{displayName:'Yesica Mercedes',email:'yesica.mercedes'}).values.responsible).toBe('YESICA MERCEDES');
  expect(administrativeDefaults('incident_report',{displayName:'Lucianny González',email:'lucianny.gonzalez'}).values.responsible).toBe('LUCIANNY GONZÁLEZ');
  expect(administrativeDefaults('incident_report',{displayName:'Lucianny González',email:'lucianny.gonzales'}).values.responsible).toBe('LUCIANNY GONZÁLEZ');
 });

 it('conserva importación y entrega QR protegida en cliente y servidor',()=>{
  const composer=readFileSync('src/components/vite/AdministrativeRequestComposer.tsx','utf8');
  const requests=readFileSync('src/components/vite/AdministrativeRequests.tsx','utf8');
  const endpoint=readFileSync('api/AdministrativeAssetEndpoints.cs','utf8');
  expect(composer).toContain('Importar masivo desde Excel');
  expect(requests).toContain('Entregar por QR');
  expect(requests).toContain('data-administrative-scanner="true"');
  expect(endpoint).toContain('El QR escaneado no corresponde a esta solicitud');
  expect(endpoint).toContain('IsolationLevel.Serializable');
 });
});
