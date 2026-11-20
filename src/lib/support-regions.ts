export const supportRegions = ['Gran Santo Domingo', 'Norte', 'Este', 'Sur'] as const;
export const unknownSupportRegion = 'Sin región registrada';
export type SupportRegion = typeof supportRegions[number] | typeof unknownSupportRegion;
type Location = { region?: string | null; provincia?: string | null; municipio?: string | null };
type Agency = Location & { codigo: string; grupo: string };
const normalized = (value: unknown) => String(value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').trim().toUpperCase().replace(/\s+/g, ' ');
const key = (code: unknown, group: unknown) => `${normalized(code)}\0${normalized(group)}`;
const provinceRegions = new Map<string, SupportRegion>();
for (const [region, provinces] of [
  ['Gran Santo Domingo', ['SANTO DOMINGO', 'DISTRITO NACIONAL']],
  ['Norte', ['SANTIAGO', 'PUERTO PLATA', 'ESPAILLAT', 'LA VEGA', 'MONSEÑOR NOUEL', 'SANCHEZ RAMIREZ', 'DUARTE', 'HERMANAS MIRABAL', 'MARIA TRINIDAD SANCHEZ', 'SAMANA', 'VALVERDE', 'MONTE CRISTI', 'MONTECRISTI', 'DAJABON', 'SANTIAGO RODRIGUEZ']],
  ['Este', ['LA ALTAGRACIA', 'ALTAGRACIA', 'LA ROMANA', 'ROMANA', 'EL SEIBO', 'SEIBO', 'HATO MAYOR', 'SAN PEDRO DE MACORIS', 'MONTE PLATA']],
  ['Sur', ['SAN CRISTOBAL', 'PERAVIA', 'SAN JOSE DE OCOA', 'AZUA', 'SAN JUAN', 'ELIAS PIÑA', 'BARAHONA', 'BAHORUCO', 'BAORUCO', 'INDEPENDENCIA', 'PEDERNALES']],
] as const) for (const province of provinces) provinceRegions.set(normalized(province), region);

export function supportRegionForLocation(location: Location): SupportRegion {
  const region = normalized(location.region).replace(/^REGION\s+/, '');
  if (/^(GRAN SANTO DOMINGO|SANTO DOMINGO|DISTRITO NACIONAL|OZAMA|METROPOLITANA)$/.test(region)) return 'Gran Santo Domingo';
  if (/^(NORTE|NORDESTE|NOROESTE|CIBAO(?: .*)?)$/.test(region)) return 'Norte';
  if (/^(ESTE|YUMA|HIGUAMO)$/.test(region)) return 'Este';
  if (/^(SUR|SUROESTE|VALDESIA|ENRIQUILLO|EL VALLE)$/.test(region)) return 'Sur';
  return provinceRegions.get(normalized(location.provincia)) || unknownSupportRegion;
}

/** Match both agency code and group; a code can exist in more than one group. */
export function createSupportRegionResolver(agencies: readonly Agency[]) {
  const byKey = new Map<string, SupportRegion>();
  for (const agency of agencies) {
    const agencyKey = key(agency.codigo, agency.grupo), region = supportRegionForLocation(agency);
    const previous = byKey.get(agencyKey);
    byKey.set(agencyKey, previous && previous !== region ? unknownSupportRegion : region);
  }
  return (record: Location & { codigo: string; grupo: string }): SupportRegion =>
    byKey.get(key(record.codigo, record.grupo)) ?? supportRegionForLocation(record);
}
