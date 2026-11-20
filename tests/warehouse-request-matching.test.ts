import {describe,it,expect} from 'vitest';
import {warehouseAllocationProblem} from '../src/components/vite/WarehouseAllocationForm';
import type {DocumentDetail} from '../src/components/vite/DocumentSystemModule';
const item={warehouseRequest:{lines:[{kind:'EQUIPMENT',productId:'CPU-01',name:'CPU Dell compacto',quantity:1}]}} as DocumentDetail;
const stock=[{department:'',productId:'CPU-01',name:'CPU Dell compacto',available:1,serials:[],warehouse:'PRINCIPAL',unitId:'UND',division:'Producto Terminado',group:'Equipos',tracksSerial:true},{department:'',productId:'RTR-01',name:'Router',available:10,serials:[],warehouse:'PRINCIPAL',unitId:'UND',division:'Producto Terminado',group:'Equipos',tracksSerial:true}];
describe('asignación real con sustitución desde Almacén',()=>{
 it('acepta un serial escrito o escaneado aunque no viniera en el Excel',()=>expect(warehouseAllocationProblem(item,stock,[{lineIndex:0,productId:'CPU-01',name:'CPU Dell compacto',serial:'NUEVO-ESCANEADO-001',quantity:1}])).toBe(''));
 it('acepta otro artículo disponible sin cambiar lo solicitado',()=>{expect(warehouseAllocationProblem(item,stock,[{lineIndex:0,productId:'RTR-01',name:'Router',serial:'ROUTER-001',quantity:1}])).toBe('');expect(item.warehouseRequest!.lines[0].productId).toBe('CPU-01');});
 it('mantiene pendiente una solicitud sin existencia',()=>expect(warehouseAllocationProblem(item,[stock[1]],[])).toContain('seguirá pendiente'));
 it('requiere serial cuando el catálogo activa su control',()=>expect(warehouseAllocationProblem(item,stock,[{lineIndex:0,productId:'CPU-01',name:'CPU Dell compacto',serial:'',quantity:1}])).toContain('Escanea o escribe'));
 it('suma los equipos realmente asignados antes de comprobar stock',()=>expect(warehouseAllocationProblem({...item,warehouseRequest:{...item.warehouseRequest!,lines:[...item.warehouseRequest!.lines,...item.warehouseRequest!.lines]}},stock,[{lineIndex:0,productId:'CPU-01',name:'CPU Dell compacto',serial:'ONE',quantity:1},{lineIndex:1,productId:'CPU-01',name:'CPU Dell compacto',serial:'TWO',quantity:1}])).toContain('requiere 2'));
 it('no admite cantidades mayores a las solicitadas',()=>expect(warehouseAllocationProblem(item,stock,[{lineIndex:0,productId:'RTR-01',name:'Router',serial:'ROUTER-001',quantity:2}])).toContain('seguirá pendiente'));
 it('no admite productos inexistentes',()=>expect(warehouseAllocationProblem(item,stock,[{lineIndex:0,productId:'FAKE',name:'No existe',serial:'QA',quantity:1}])).toContain('Selecciona un artículo'));
});
