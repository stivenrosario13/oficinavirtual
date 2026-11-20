import {describe, expect, it} from 'vitest';
import {indexWarehouseMovements, pageRows, warehouseProductKey} from '../src/lib/warehouse-render-index';

describe('warehouse large lists', () => {
  it('preserves the first matching movement and exact consumption totals', () => {
    const rows = [
      {id:1,operationalArea:'WORKSHOP',equipmentType:'IMPRESORA',movementType:'EXIT',quantity:99},
      {id:2,operationalArea:'WAREHOUSE',equipmentType:' Impresora ',movementType:'REQUEST',quantity:50},
      {id:3,operationalArea:'WAREHOUSE',equipmentType:'IMPRESORA',movementType:'NEW_DELIVERY',quantity:2},
      {id:4,operationalArea:'WAREHOUSE',equipmentType:'Impresora',movementType:'ENTRY',quantity:100},
      {id:5,operationalArea:'WAREHOUSE',equipmentType:'Impresora',movementType:'TRANSFER_TO_WORKSHOP',quantity:3},
      {id:6,operationalArea:'WAREHOUSE',equipmentType:'Impresora',movementType:'DISCHARGE',quantity:1},
      {id:7,operationalArea:'WAREHOUSE',equipmentType:'Otro',movementType:'EXIT',quantity:4},
    ];
    const result = indexWarehouseMovements(rows);
    for (const name of ['impresora', ' Otro ', 'inexistente']) {
      const key = warehouseProductKey(name);
      expect(result.firstByProduct.get(key)).toBe(rows.find(item => item.operationalArea === 'WAREHOUSE' && warehouseProductKey(item.equipmentType) === key));
      const expected = rows.filter(item => item.operationalArea === 'WAREHOUSE' && warehouseProductKey(item.equipmentType) === key && ['NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE'].includes(item.movementType)).reduce((sum,item) => sum + item.quantity,0);
      expect(result.consumption.get(key) || 0).toBe(expected);
    }
    expect(result.firstByProduct.get('impresora')?.id).toBe(2);
    expect(result.consumption.get('impresora')).toBe(6);
  });
  it('makes all 10,003 records reachable without rendering more than 50 at once', () => {
    const rows = Array.from({length: 10003}, (_, id) => ({id}));
    const visited: number[] = [];
    for (let page = 1; page <= 201; page++) {
      const result = pageRows(rows, page, 50);
      expect(result.rows.length).toBeLessThanOrEqual(50);
      visited.push(...result.rows.map(row => row.id));
    }
    expect(visited).toEqual(rows.map(row => row.id));
    expect(pageRows(rows.filter(row => row.id === 10002), 201, 50)).toMatchObject({page:1,pages:1,total:1,start:1,end:1,rows:[{id:10002}]});
    expect(pageRows([], 8, 25)).toMatchObject({page:1,pages:1,total:0,start:0,end:0,rows:[]});
  });
});
