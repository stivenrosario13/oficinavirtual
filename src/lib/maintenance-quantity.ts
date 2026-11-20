export function maintenanceQuantityLimit(area: string, movement: string): number {
  return area === 'WAREHOUSE' && ['ENTRY', 'REQUEST', 'TRANSFER_TO_WORKSHOP'].includes(movement) ? 10000 : 1000;
}

export function maintenanceQuantityError(quantity: number, area: string, movement: string): string {
  const limit = maintenanceQuantityLimit(area, movement);
  return Number.isInteger(quantity) && quantity >= 1 && quantity <= limit
    ? ''
    : `Indica una cantidad entera entre 1 y ${limit === 10000 ? '10,000' : '1,000'} unidades.`;
}
