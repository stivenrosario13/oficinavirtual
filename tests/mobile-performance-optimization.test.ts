import {describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';

describe('optimización de carga para dispositivos móviles',()=>{
  const app=readFileSync('src/App.tsx','utf8');
  const assistant=readFileSync('src/components/vite/AdminHeaderVoiceAssistant.tsx','utf8');
  const operations=readFileSync('src/components/vite/OperationsCenter.tsx','utf8');

  it('separa el robot del paquete inicial y aplaza sus catálogos',()=>{
    expect(app).toContain("reactLazy(()=>import('./components/vite/AdminHeaderVoiceAssistant'))");
    expect(assistant).not.toContain('useEffect(() => {\n    void loadCatalogs();');
    expect(assistant).toContain('void loadCatalogs();');
    expect(assistant).toContain('catalogLoadRef');
  });

  it('reduce consultas de notificaciones en móvil y pestañas ocultas',()=>{
    expect(app).toContain('document.hidden) return');
    expect(app).toContain("matches?30000:10000");
    expect(app).toContain("visibilitychange',refreshWhenVisible");
  });

  it('carga el directorio global únicamente al solicitar un departamento',()=>{
    const initialLoad=operations.slice(0,operations.indexOf('const openDepartmentDirectory'));
    expect(initialLoad).not.toContain('fetch("/api/manager-team-members"');
    expect(operations).toContain('const openDepartmentDirectory');
    expect(operations).toContain('onClick={()=>void openDepartmentDirectory(metric.code)}');
  });
});
