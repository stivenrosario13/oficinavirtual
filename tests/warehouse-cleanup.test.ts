import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
it('limpia solo Almacén y conserva los estados pendientes',()=>{
 const source=readFileSync('api/DatabaseWarehouseRequestCleanup.cs','utf8');
 expect(source).toContain("JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén'");
 expect(source).toContain("NOT IN('pending','received','correction_requested')");
 expect(source).toContain('warehouse_request_cleanup_backup');expect(source).toContain('allocations_json');expect(source).toContain('application_one_time_actions');
 expect(source).not.toContain('DELETE FROM');expect(source).not.toContain('UPDATE dbo.warehouse_products');
});
it('la limpieza antigua ya no elimina pendientes ni movimientos',()=>{
 const store=readFileSync('api/InstitutionalDocumentStore.cs','utf8');
 const schema=store.slice(store.indexOf('static async Task EnsureInstitutionalSchema'),store.indexOf('public async Task<List<InstitutionalFile>> InstitutionalList'));
 expect(schema).not.toContain('DELETE');expect(schema).toContain('CleanupCompletedWarehouseRequests(c,ct)');
});
