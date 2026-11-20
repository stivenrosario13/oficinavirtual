import {readFileSync} from 'node:fs';import {it,expect} from 'vitest';
it('aligns form, document upload, ticket requests and dispatch at 10000 units without broadening other movements',()=>{
 const form=readFileSync('src/components/vite/WarehouseRequestComposer.tsx','utf8');
 expect(form).toContain('max="10000"');expect(form).toContain('line.quantity>10000');expect(form).toContain('total+line.quantity,0)>10000');
 const upload=readFileSync('api/InstitutionalDocumentEndpoints.cs','utf8');
 expect(upload).toContain('x.Quantity is <1 or >10000');expect(upload).toContain('warehouseRequest.Lines.Sum(x=>x.Quantity)>10000');
 const movements=readFileSync('api/Program.cs','utf8');expect(movements).toContain('body.Quantity>(area=="WAREHOUSE"&&movement=="REQUEST"?10000:1000)');
 const dispatch=readFileSync('api/DatabaseWarehouseFulfillment.cs','utf8');expect(dispatch).toContain('quantity is <1 or >10000');expect(dispatch).toContain('input.Equipment.Count>10000');
});
