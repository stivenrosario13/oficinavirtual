import {readFileSync} from 'node:fs';
import {describe,expect,it} from 'vitest';

describe('carga diferida de estilos pesados V522',()=>{
 it('mantiene fuera del arranque los estilos exclusivos de funciones opcionales',()=>{
  const main=readFileSync('src/main.tsx','utf8');
  for(const sheet of ['geo-atlas-2026.css','chat-professional-v196.css','chat-mobile-v193-restored-v208.css','operations-center-v250.css'])expect(main).not.toContain(sheet);
 });
 it('carga cada estilo únicamente junto con su sección',()=>{
  expect(readFileSync('src/components/vite/EmployeeAuditForm.tsx','utf8')).toContain('../../geo-atlas-2026.css');
  const chat=readFileSync('src/components/vite/SupportChat.tsx','utf8');
  expect(chat).toContain('../../chat-professional-v196.css');
  expect(chat).toContain('../../chat-mobile-v193-restored-v208.css');
  expect(readFileSync('src/components/vite/OperationsCenter.tsx','utf8')).toContain('../../operations-center-v250.css');
 });
 it('mantiene el resultado de auditoría bajo demanda',()=>{
  const app=readFileSync('src/App.tsx','utf8');
  expect(app).toContain('const EvidenceSuccess=lazy(()=>import("@/components/vite/EvidenceSuccess"));');
  expect(app).not.toContain('import EvidenceSuccess from');
 });
 it('descarga la recuperación de contraseña sólo cuando el usuario la solicita',()=>{
  const login=readFileSync('src/components/vite/PortalLogin.tsx','utf8');
  expect(login).toContain("const PasswordRecovery=lazy(()=>import('./PasswordRecovery'));");
  expect(login).not.toContain("import PasswordRecovery, {RecoveryCodeStart}");
 });
});
