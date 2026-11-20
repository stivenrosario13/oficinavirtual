import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { maintenanceQuantityError, maintenanceQuantityLimit } from '../src/lib/maintenance-quantity';

describe('cantidad de entradas de almacén', () => {
  it.each([1, 1000, 1001, 9999, 10000])('permite guardar %i unidades', quantity => {
    expect(maintenanceQuantityError(quantity, 'WAREHOUSE', 'ENTRY')).toBe('');
  });
  it.each([0, -1, 1.5, 10001, NaN, Infinity])('rechaza %s con un mensaje de cantidad', quantity => {
    expect(maintenanceQuantityError(quantity, 'WAREHOUSE', 'ENTRY')).toContain('entre 1 y 10,000');
  });
  it('conserva los límites de las demás operaciones', () => {
    expect(maintenanceQuantityLimit('WAREHOUSE', 'REQUEST')).toBe(10000);
    expect(maintenanceQuantityLimit('WAREHOUSE', 'TRANSFER_TO_WORKSHOP')).toBe(10000);
    expect(maintenanceQuantityError(10000, 'WAREHOUSE', 'TRANSFER_TO_WORKSHOP')).toBe('');
    expect(maintenanceQuantityError(10001, 'WAREHOUSE', 'TRANSFER_TO_WORKSHOP')).toContain('10,000');
    for (const movement of ['EXIT', 'NEW_DELIVERY', 'DISCHARGE']) {
      expect(maintenanceQuantityLimit('WAREHOUSE', movement)).toBe(1000);
    }
    expect(maintenanceQuantityError(1001, 'WORKSHOP', 'ENTRY')).toContain('1,000');
  });
  it('conecta el mismo límite con la API y amplía la restricción antes de insertar la entrada', () => {
    const api = readFileSync('api/Program.cs', 'utf8');
    expect(api).toContain('quantityLimit=area=="WAREHOUSE"&&movement is ("ENTRY" or "REQUEST" or "TRANSFER_TO_WORKSHOP")?10000:1000');
    expect(api).toContain('body.Quantity<1||body.Quantity>quantityLimit');
    const database = readFileSync('api/DatabaseSqlServerScoped.cs', 'utf8');
    const migration = database.indexOf('if(requestedArea=="WAREHOUSE"&&movementType is ("ENTRY" or "REQUEST" or "TRANSFER_TO_WORKSHOP"))await EnsureWarehouseWithdrawalSchema(connection,ct);');
    expect(migration).toBeGreaterThan(0);
    expect(database.indexOf('await EnsureWarehouseEntryProduct(', migration)).toBeGreaterThan(migration);
    const schema = readFileSync('api/WarehouseWithdrawalSchema.cs', 'utf8');
    expect(schema).toContain('CHECK(quantity BETWEEN 1 AND 10000)');
    expect(schema).not.toMatch(/DELETE\s+FROM|TRUNCATE\s+TABLE|DROP\s+TABLE/i);
  });
});
