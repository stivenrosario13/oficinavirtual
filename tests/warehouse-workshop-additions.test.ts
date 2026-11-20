import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
it('Almacén crea tickets internos con el catálogo vigente, sin asignarse permisos de resolución',()=>{
 const ui=readFileSync('src/components/vite/WarehouseSupport.tsx','utf8'),db=readFileSync('api/DatabaseSqlServerScoped.cs','utf8');
 expect(ui).toContain('/api/support/catalog');expect(ui).toContain("['TECHNOLOGY','GENERAL_SERVICES']");expect(ui).toContain("ticketType:'INTERNAL'");
 expect(db).toContain('body.TicketType!="INTERNAL"||body.AssignedDepartment is not ("TECHNOLOGY" or "GENERAL_SERVICES")');expect(db).toContain('OR (@supportTeam=\'WAREHOUSE\' AND t.created_by_user_id=@userId)');
});
it('oculta el botón superior de Almacén y la custodia de la entrada manual de Taller',()=>{
 const ui=readFileSync('src/components/vite/TicketCenter.tsx','utf8');
 expect(ui).toContain('{maintenanceArea!=="WAREHOUSE"&&<button');expect(ui).toContain('!(maintenanceDraft.operationalArea==="WORKSHOP"&&maintenanceDraft.movementType==="ENTRY")');
 expect(ui).toContain('<WorkshopBoard department={maintenanceDepartment}');
 expect(ui).toContain('maintenanceAssets.filter(asset=>asset.status==="ACTIVE").map');
 expect(ui).not.toContain('item.operationalArea === maintenanceArea ||');
 const api=readFileSync('api/Program.cs','utf8');expect(api).toContain('var missingCustody=!(area=="WORKSHOP"&&movement=="ENTRY")');
});
