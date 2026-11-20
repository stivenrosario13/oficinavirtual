import {describe, expect, it} from 'vitest';
import {createSupportRegionResolver, supportRegionForLocation, unknownSupportRegion} from '../src/lib/support-regions';

describe('department regional navigation', () => {
  it.each([
    [' GRAN SANTO DOMINGO ', 'Gran Santo Domingo'], ['Región Norte', 'Norte'], ['Cibao Nordeste', 'Norte'],
    ['Este', 'Este'], ['Sur', 'Sur'], ['El Valle', 'Sur'], ['Santo Domingo', 'Gran Santo Domingo'],
  ])('normalizes %s without confusing Santo Domingo with the east/north', (region, expected) => {
    expect(supportRegionForLocation({region})).toBe(expected);
  });
  it('uses province only when a registered region is not recognized', () => {
    expect(supportRegionForLocation({provincia:'Elías Piña'})).toBe('Sur');
    expect(supportRegionForLocation({region:'Norte', provincia:'Santo Domingo'})).toBe('Norte');
    expect(supportRegionForLocation({provincia:'La Altagracia'})).toBe('Este');
    expect(supportRegionForLocation({provincia:'Santo Domingo', municipio:'Santo Domingo Este'})).toBe('Gran Santo Domingo');
  });
  it('isolates equal agency codes in different groups and never guesses unknown records', () => {
    const resolve = createSupportRegionResolver([{codigo:'001', grupo:'A',region:'Norte'}, {codigo:'001',grupo:'B',region:'Sur'}]);
    expect(resolve({codigo:'001',grupo:' a '})).toBe('Norte');
    expect(resolve({codigo:'001',grupo:'B'})).toBe('Sur');
    expect(resolve({codigo:'001',grupo:'C'})).toBe(unknownSupportRegion);
    expect(resolve({codigo:'',grupo:''})).toBe(unknownSupportRegion);
  });
  it('keeps conflicting duplicate agencies unassigned instead of mixing their cases', () => {
    const resolve = createSupportRegionResolver([{codigo:'001', grupo:'A',region:'Norte'}, {codigo:'001',grupo:'A',region:'Sur'}]);
    expect(resolve({codigo:'001',grupo:'A'})).toBe(unknownSupportRegion);
  });
});
