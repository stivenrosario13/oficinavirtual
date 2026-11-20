import {describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';

describe('catálogo completo de agencias para Oficina Virtual',()=>{
  const api=readFileSync('api/Program.cs','utf8');
  const database=readFileSync('api/DatabaseSqlServerScoped.cs','utf8');
  const assistant=readFileSync('src/components/vite/AdminHeaderVoiceAssistant.tsx','utf8');
  it('la API informa el total activo y no acepta un catálogo incompleto',()=>{
    expect(api).toContain('TicketAgencyCatalogStats');
    expect(api).toContain('catalogComplete=stats.InvalidIds==0&&agencies.Count==stats.TotalActive');
    expect(database).toContain('SELECT COUNT_BIG(*)');
    expect(database).toContain('TRY_CONVERT(uniqueidentifier,a.id)');
    expect(database).toContain('WHERE a.is_active=1');
    expect(database).not.toMatch(/SELECT TOP \(\d+\) a\.id,a\.codigo,a\.terminal,a\.grupo/);
  });
  it('el robot valida el total, elimina duplicados y permite buscar todas las agencias',()=>{
    expect(assistant).toContain('uniqueAgencies=[...new Map');
    expect(assistant).toContain('agencyData.catalogComplete===false');
    expect(assistant).toContain('agencies.length');
    expect(assistant).toContain('filteredAgencyChoices');
    expect(assistant).toContain('admin-voice-agency-picker');
    expect(assistant).toContain('Catálogo de agencias activas');
  });
});
