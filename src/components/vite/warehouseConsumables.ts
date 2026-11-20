export const warehouseConsumables = [
  "Papel de baño",
  "Vasos",
  "Cucharas",
  "Marcadores",
  "Lapiceros",
  "Liquipapers",
  "Folders",
  "Hojas blancas",
  "Grapas",
  "Clips",
  "Cinta adhesiva",
  "Servilletas",
  "Bolsas de basura",
] as const;

const normalizeConsumable = (value: string) =>
  value.trim().normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("es");

const warehouseConsumableKeys = new Set(warehouseConsumables.map(normalizeConsumable));

export const isWarehouseConsumable = (value: string) =>
  warehouseConsumableKeys.has(normalizeConsumable(value));
