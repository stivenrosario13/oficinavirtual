import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';

it('mantiene el acceso interno junto al QR y conserva la navegación',()=>{
 const source=readFileSync('src/components/vite/TicketCenter.tsx','utf8');
 const toolbar=source.slice(source.indexOf('<div className="maintenance-global-trace">'),source.indexOf('Escanear QR grande y consultar etapa'));
 expect(toolbar).toContain('maintenanceArea === "WAREHOUSE"');
 expect(toolbar).toContain('warehouse-department-shortcut');
 expect(toolbar).toContain('setMaintenanceSelectedDepartment("WAREHOUSE")');
 expect(toolbar).toContain('setMaintenanceView("requests")');
 expect(toolbar).toContain('setTrackingCode("")');
 expect(readFileSync('src/components/vite/MaintenanceOverview.tsx','utf8')).not.toContain('className="wlqp-back"');
});
it('incluye adaptación móvil y foco de teclado',()=>{
 const css=readFileSync('src/components/vite/maintenanceModern.css','utf8');
 expect(css).toContain('.warehouse-department-shortcut');
 expect(css).toContain('@media(max-width:640px)');
 expect(css).toContain('.maintenance-global-trace button:focus-visible');
});
