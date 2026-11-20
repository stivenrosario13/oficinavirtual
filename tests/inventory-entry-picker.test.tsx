import {describe,it,expect} from 'vitest';
import {renderToStaticMarkup} from 'react-dom/server';
import InventoryEntryPicker,{filterEntryInventory} from '../src/components/vite/InventoryEntryPicker';
const items=[{id:'001',name:'Silla de agencia',available:0},{id:'002',name:'Monitor Dell',available:3},{id:'003',name:'Teléfono',available:1}];
describe('Real inventory entry picker',()=>{
 it('includes zero stock products for replenishment',()=>expect(filterEntryInventory(items,'silla')).toEqual([items[0]]));
 it('searches name, code and accents',()=>{expect(filterEntryInventory(items,'002')).toEqual([items[1]]);expect(filterEntryInventory(items,'telefono')).toEqual([items[2]]);expect(filterEntryInventory(items,'monitor dell')).toEqual([items[1]])});
 it('shows real stock and manual entry explanation',()=>{const html=renderToStaticMarkup(<InventoryEntryPicker items={items} loaded onSelect={()=>{}}/>);expect(html).toContain('0 disponibles');expect(html).toContain('3 disponibles');expect(html).toContain('Si es un artículo nuevo');expect(html).not.toContain('required=')});
 it('does not show failed loading as empty inventory',()=>expect(renderToStaticMarkup(<InventoryEntryPicker items={[]} loaded={false} onSelect={()=>{}}/>)).toContain('todavía no se ha cargado'));
});
