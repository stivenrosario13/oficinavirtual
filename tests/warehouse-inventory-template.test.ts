import {describe,it,expect} from 'vitest';
import {readFileSync} from 'node:fs';
import ExcelJS from 'exceljs';
describe('plantilla e inventario de Almacén',()=>{
 it('conserva solo las 3 columnas requeridas y los 782 artículos del listado oficial',async()=>{
  const wb=new ExcelJS.Workbook();await wb.xlsx.load(readFileSync('public/templates/plantilla-inventario-almacen.xlsx') as never);
  const sheet=wb.worksheets[0];expect(wb.worksheets).toHaveLength(1);expect(sheet.name).toBe('Inventario');expect(sheet.rowCount).toBe(783);expect(sheet.getRow(1).values).toEqual([undefined,'IdProducto','Descripción','Existencia']);
  const source=readFileSync('src/components/vite/TicketCenter.tsx','utf8');
  expect(sheet.getRow(2).getCell(1).text).toBe('0001');expect(sheet.getRow(2).getCell(2).text).toBe('Bravo Jabon Rosas');
  expect(source).toContain('El Excel debe conservar las 3 columnas requeridas');expect(source).toContain('/templates/plantilla-inventario-almacen.xlsx');expect(source).toContain('/api/warehouse/fulfillment/inventory/replace');
 });
 it('reemplaza el saldo base sin eliminar la trazabilidad histórica',()=>{
  const api=readFileSync('api/DatabaseWarehouseFulfillment.cs','utf8');expect(api).toContain('ReplaceWarehouseCatalog');expect(api).toContain('MERGE dbo.warehouse_products');expect(api).toContain('imported_at');expect(api).not.toContain('DELETE FROM dbo.maintenance_movements');
 });
 it('aplica azul, verde y rojo únicamente a la cabecera de solicitudes a Almacén',()=>{
  const detail=readFileSync('src/components/vite/InstitutionalDocumentDetail.tsx','utf8'),css=readFileSync('src/components/vite/warehouseFulfillment.css','utf8');expect(detail).toContain('item.recipientDepartment==="Almacén"?" warehouse-request-detail"');expect(css).toContain('.native-doc-detail.warehouse-request-detail .native-doc-detail-toolbar');expect(css).toContain('button.warehouse-authorize{background:#16834b');expect(css).toContain('button.warehouse-reject{background:#b72d3d');
 });
});
