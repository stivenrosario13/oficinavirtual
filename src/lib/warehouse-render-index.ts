export const warehouseProductKey = (name: string) => name.trim().toLocaleLowerCase('es');

// Keep the first movement exactly as Array.find did, without rescanning all
// movements for every product in a catalogue containing thousands of rows.
export function indexWarehouseMovements<T extends {operationalArea: string; equipmentType: string; movementType: string; quantity: number}>(movements: readonly T[]) {
  const firstByProduct = new Map<string, T>();
  const consumption = new Map<string, number>();
  for (const item of movements) {
    if (item.operationalArea !== 'WAREHOUSE') continue;
    const key = warehouseProductKey(item.equipmentType);
    if (!firstByProduct.has(key)) firstByProduct.set(key, item);
    if (['NEW_DELIVERY', 'EXIT', 'TRANSFER_TO_WORKSHOP', 'DISCHARGE'].includes(item.movementType)) consumption.set(key, (consumption.get(key) || 0) + item.quantity);
  }
  return {firstByProduct, consumption};
}

export function pageRows<T>(rows: readonly T[], requestedPage: number, size: number) {
  const pages = Math.max(1, Math.ceil(rows.length / size));
  const page = Math.max(1, Math.min(requestedPage, pages));
  const start = (page - 1) * size;
  return {rows: rows.slice(start, start + size), page, pages, total: rows.length, start: rows.length ? start + 1 : 0, end: Math.min(start + size, rows.length)};
}
