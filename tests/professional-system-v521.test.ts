import {readFileSync} from 'node:fs';
import {describe,expect,it} from 'vitest';

describe('sistema visual profesional V521',()=>{
 it('carga la capa consolidada después de todos los estilos heredados',()=>{
  const main=readFileSync('src/main.tsx','utf8');
  expect(main.lastIndexOf('professional-system-v521.css')).toBeGreaterThan(main.lastIndexOf('form-theme-v501.css'));
 });
 it('mantiene el tema institucional, legibilidad y diseño adaptable',()=>{
  const css=readFileSync('src/professional-system-v521.css','utf8');
  for(const marker of ['--pro-bg: #020d19','--pro-text: #f4fbff','--pro-accent: #55d9ff','font-size: 14px','min-height: var(--pro-control-height)','@media (max-width: 760px)','font-size: 16px !important','prefers-reduced-motion'])expect(css).toContain(marker);
 });
});
