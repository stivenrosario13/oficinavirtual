import {describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {warehouseAgenciesForArticle} from '../src/components/vite/WarehouseRequestComposer';

const agencies=[
  {id:'a',codigo:'001',terminal:'Agencia Norte',grupo:'Grupo Uno'},
  {id:'b',codigo:'002',terminal:'Agencia Sur',grupo:'Grupo Uno'},
  {id:'c',codigo:'003',terminal:'Agencia Este',grupo:'Grupo Dos'},
];

describe('solicitudes de grupo y reemplazos por falla',()=>{
  it('limita y busca las agencias dentro del grupo elegido',()=>{
    expect(warehouseAgenciesForArticle(agencies,'Grupo Uno','', '')).toHaveLength(2);
    expect(warehouseAgenciesForArticle(agencies,'Grupo Uno','sur','').map(row=>row.id)).toEqual(['b']);
    expect(warehouseAgenciesForArticle(agencies,'Grupo Uno','003','')).toEqual([]);
    expect(warehouseAgenciesForArticle(agencies,'Grupo Uno','norte','b').map(row=>row.id)).toEqual(['b','a']);
    expect(warehouseAgenciesForArticle(agencies,'','', '')).toEqual([]);
  });
  it('mantiene validación y creación atómica del ticket en el servidor',()=>{
    const endpoint=readFileSync('api/InstitutionalDocumentEndpoints.cs','utf8');
    const store=readFileSync('api/InstitutionalDocumentStore.cs','utf8');
    const canonical=readFileSync('api/DatabaseWarehouseFulfillment.cs','utf8');
    expect(endpoint).toContain('Las fallas deben asignarse a Tecnología o Servicios Generales.');
    expect(canonical).toContain('request.DestinationType=="GROUP"&&request.AgencyId==Guid.Empty');
    expect(canonical).toContain('string.Equals(group,request.AgencyGroup?.Trim()');
    expect(store).toContain('ticketType","INTERNAL"');
    expect(store).toContain('await tx.CommitAsync(ct);return d.Id;');
  });
});
