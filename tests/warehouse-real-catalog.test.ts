import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
const catalog=JSON.parse(readFileSync('api/Data/warehouse-products-2026-09-28.json','utf8'));
it('conserva códigos y las 172 filas del inventario real, sin importar el pie de tabla',()=>{
 expect(catalog.products).toHaveLength(172);
 expect(new Set(catalog.products.map((p:{idProducto:string})=>p.idProducto)).size).toBe(172);
 expect(catalog.products.reduce((total:number,p:{existencia:number})=>total+p.existencia,0)).toBe(45004);
 expect(catalog.products.find((p:{idProducto:string})=>p.idProducto==='00300035').existencia).toBe(1887);
 expect(catalog.products.every((p:{existencia:number})=>Number.isInteger(p.existencia)&&p.existencia>=0)).toBe(true);
});
it('entrega el snapshot junto con la API y preserva un registro de aplicación única',()=>{
 expect(readFileSync('api/RegistroAgencias.Api.csproj','utf8')).toContain('Data\\warehouse-products-2026-09-28.json');
 const db=readFileSync('api/DatabaseWarehouseFulfillment.cs','utf8');
 expect(db).toContain('warehouse_catalog_snapshots');expect(db).toContain('previous_catalog');expect(db).toContain('sp_getapplock');
});
