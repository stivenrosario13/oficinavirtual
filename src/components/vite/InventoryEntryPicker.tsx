import {useState} from 'react';

export type InventoryEntryOption={id:string;name:string;available:number};
export function filterEntryInventory(items:InventoryEntryOption[],query:string){
 const normalize=(value:string)=>value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLocaleLowerCase('es');
 const terms=normalize(query).trim().split(/\s+/).filter(Boolean);
 return items.filter(item=>terms.every(term=>normalize(`${item.id} ${item.name}`).includes(term)));
}
export default function InventoryEntryPicker({items,loaded,onSelect,onCreate,onRefresh,allowCreateNew=true}:{items:InventoryEntryOption[];loaded:boolean;onSelect:(item:InventoryEntryOption)=>void;onCreate?:(name:string)=>void;onRefresh?:()=>void;allowCreateNew?:boolean}){
 const [query,setQuery]=useState('');
 const matches=filterEntryInventory(items,query);
 const newName=query.trim(),alreadyExists=items.some(item=>item.name.localeCompare(newName,'es',{sensitivity:'base'})===0);
 return <fieldset className="maintenance-form-section"><legend>Listado real del inventario</legend>
  <label>Buscar artículo por nombre o código<input type="search" value={query} onChange={event=>setQuery(event.target.value)} placeholder="Escribe el nombre, la marca o el código"/></label>
  {!loaded?<p role="status">El inventario todavía no se ha cargado.</p>:<><label>Seleccionar artículo existente<select value="" onChange={event=>{const item=items.find(row=>row.id===event.target.value);if(item)onSelect(item)}}><option value="">Seleccionar para completar el nombre</option>{matches.slice(0,200).map(item=><option key={item.id} value={item.id}>{item.name} · {item.id} · {item.available} disponibles</option>)}</select></label><small>{matches.length>200?'Mostrando 200 resultados. Escribe para encontrar el artículo.':`${matches.length} artículo(s) encontrados.`} También se incluyen artículos sin existencias.</small></>}
  {onRefresh&&<button type="button" onClick={onRefresh}>Actualizar listado</button>}
  {allowCreateNew&&onCreate&&newName&&!alreadyExists&&<button type="button" onClick={()=>onCreate(newName)}>Crear nuevo artículo “{newName}”</button>}
  <p>{allowCreateNew?'Si es un artículo nuevo, escribe su nombre arriba y usa “Crear nuevo artículo”.':'Tu cuenta puede seleccionar artículos existentes; la creación de artículos nuevos requiere el permiso correspondiente.'} Seleccionar un artículo no copia seriales ni registra existencias: el ingreso se guarda al confirmar la entrada.</p>
 </fieldset>;
}
