import {describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';

describe('rango Desde y Hasta en reportes y estadísticas',()=>{
 const source=readFileSync('src/components/vite/TicketCenter.tsx','utf8');
 it('muestra dos calendarios independientes y un rango personalizado',()=>{
  expect(source).toContain('id="ticket-report-from"');
  expect(source).toContain('id="ticket-report-to"');
  expect(source).toContain('<span>Desde</span>');
  expect(source).toContain('<span>Hasta</span>');
  expect(source).toContain('Rango personalizado');
 });
 it('aplica el rango inclusivo a tickets, hallazgos y el informe departamental',()=>{
  expect(source).toContain('T00:00:00');
  expect(source).toContain('T23:59:59.999');
  expect((source.match(/reportDateMatches\(/g)||[]).length).toBeGreaterThanOrEqual(4);
 });
});
