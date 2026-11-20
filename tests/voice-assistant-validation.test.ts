import {describe,expect,it} from 'vitest';
import {resolveAgencyMentions,resolveAgencyStrict,resolveCategoryStrict,resolveDepartmentStrict,resolveInventoryStrict,searchAgencyOptions,searchInventoryOptions,selectSpokenOption,spokenAgencyCode,spokenAgencyDigitCandidates,spokenAgencyLabel,spokenNaturalText,spokenQuantityValues,spokenReferenceCode,spokenSiteName,suggestAgencyOptions,suggestInventoryOptions} from '../src/lib/voice-assistant-validation';

const agencies=[
  {id:'1',codigo:'10442',terminal:'Mango Principal',grupo:'Grupo Mango'},
  {id:'2',codigo:'20442',terminal:'Mango Norte',grupo:'Grupo Mango'},
  {id:'3',codigo:'77881',terminal:'Centro Sur',grupo:'Grupo Sur'},
];
const departments=[{code:'TECHNOLOGY',name:'Tecnología',categories:[
  {code:'MODEM',name:'Falla de módem',defaultPriority:'HIGH',isActive:true,requiresDetail:true,detailLabel:'Síntoma'},
  {code:'INTERNET',name:'Internet intermitente',defaultPriority:'MEDIUM',isActive:true},
  {code:'OLD',name:'Falla retirada',defaultPriority:'LOW',isActive:false},
]}];

describe('validación estricta del robot',()=>{
  it('no inventa una agencia y detecta ambigüedad por código y grupo',()=>{
    expect(resolveAgencyStrict('agencia 999 del grupo mango',agencies).kind).toBe('missing');
    expect(resolveAgencyStrict('agencia 442 del grupo mango',agencies).kind).toBe('ambiguous');
    expect(resolveAgencyStrict('agencia 10442 del grupo mango',agencies).value?.id).toBe('1');
  });
  it('reconoce varias agencias mencionadas en una misma conversación',()=>{
    expect(resolveAgencyMentions('será para las agencias 10442 y 77881',agencies).map(item=>item.id)).toEqual(['1','3']);
  });
  it('reconoce códigos de agencia hablados por bloques y ofrece opciones cercanas',()=>{
    expect(spokenAgencyDigitCandidates('agencia diez cuarenta y cuatro dos')).toContain('10442');
    expect(resolveAgencyStrict('agencia diez cuarenta y cuatro dos',agencies).value?.id).toBe('1');
    expect(suggestAgencyOptions('mango principal',agencies)[0].id).toBe('1');
    const sameGroup=Array.from({length:12},(_,index)=>({id:String(index),codigo:`60${1000+index}`,terminal:`Agencia ${index}`,grupo:'Grupo Completo'}));
    expect(resolveAgencyStrict('grupo completo',sameGroup).options).toHaveLength(12);
    expect(suggestAgencyOptions('grupo completo',sameGroup,sameGroup.length)).toHaveLength(12);
    expect(searchAgencyOptions('muéstrame 10 agencias del mango',agencies,10).map(item=>item.id)).toEqual(['1','2']);
    expect(searchAgencyOptions('agencias de un grupo inexistente',agencies,10)).toEqual([]);
  });
  it('pronuncia el número en pares, conserva ceros y añade el nombre',()=>{
    expect(spokenAgencyCode('6011055')).toBe('sesenta, once, cero cinco, cinco');
    expect(spokenAgencyLabel({codigo:'6011055',terminal:'Villa Mella',grupo:'Grupo Mango'})).toBe('agencia sesenta, once, cero cinco, cinco, villa mella');
    expect(spokenReferenceCode('M-204')).toBe('eme, veinte, cuatro');
  });
  it('lee nombres y lugares como palabras completas sin deletrear códigos internos',()=>{
    expect(spokenSiteName('010189 | 659 - PUNTA SPORT /VILLA MELLA (92737)')).toBe('punta sport, villa mella');
    expect(spokenSiteName('130-LUMISMART BAVARO | PUNTA CANA')).toBe('lumismart bavaro, punta cana');
    expect(spokenNaturalText('CPU, SIM, GPS y PDF')).toBe('procesador, tarjeta sim, geolocalización y documento digital');
    expect(spokenNaturalText('ticket TKT-20261030')).toBe('ticket veinte, veintiséis, diez, treinta');
  });
  it('permite escoger una opción hablada',()=>{
    expect(selectSpokenOption('la segunda',agencies.slice(0,2),item=>`${item.codigo} ${item.terminal}`)?.id).toBe('2');
    expect(selectSpokenOption('opción número seis',Array.from({length:8},(_,index)=>`Artículo ${index+1}`),item=>item)).toBe('Artículo 6');
    expect(selectSpokenOption('opción del artículo',[{productId:'M1',name:'Módem'}],item=>`${item.name} ${item.productId}`)?.productId).toBe('M1');
    expect(selectSpokenOption('Módem M1',[{productId:'C1',name:'Cable'},{productId:'M1',name:'Módem'}],item=>`${item.name} ${item.productId}`)?.productId).toBe('M1');
    expect(selectSpokenOption('yo quiero el módem de internet por favor',[{productId:'C1',name:'Cable de red'},{productId:'M1',name:'Módem de internet'},{productId:'T1',name:'Teclado USB'}],item=>`${item.name} ${item.productId}`)?.productId).toBe('M1');
    expect(selectSpokenOption('dame la que dice cable para la red',[{productId:'C1',name:'Cable de red'},{productId:'M1',name:'Módem de internet'}],item=>`${item.name} ${item.productId}`)?.productId).toBe('C1');
    expect(selectSpokenOption('prefiero mango principal',[...agencies],item=>`${item.codigo} ${item.terminal} ${item.grupo}`)?.id).toBe('1');
  });
  it('solo acepta áreas y fallas activas del catálogo',()=>{
    const department=resolveDepartmentStrict('es de tecnología',departments).value!;
    expect(resolveCategoryStrict('internet intermitente',department).value?.code).toBe('INTERNET');
    expect(resolveCategoryStrict('quiero reportar el problema del moden',department).value?.code).toBe('MODEM');
    expect(resolveCategoryStrict('falla retirada',department).kind).toBe('missing');
  });
  it('exige cantidades y detecta artículos homónimos',()=>{
    expect(resolveInventoryStrict('necesito modem',[{productId:'M1',name:'Modem'}]).missingQuantity).toHaveLength(1);
    expect(resolveInventoryStrict('necesito modem 2 modem',[{productId:'M1',name:'Modem'}]).lines[0].quantity).toBe(2);
    expect(resolveInventoryStrict('necesito 2 modem',[{productId:'M1',name:'Modem'},{productId:'M2',name:'Modem'}]).ambiguous).toHaveLength(2);
    expect(resolveInventoryStrict('necesito 2 cable de red',[{productId:'C1',name:'Cable de red'}]).lines[0].quantity).toBe(2);
    expect(resolveInventoryStrict('dos moden, código M1',[{productId:'M1',name:'Módem de internet'}]).lines[0]).toMatchObject({quantity:2,item:{productId:'M1'}});
    expect(resolveInventoryStrict('necesito cinco cables',[{productId:'C1',name:'Cable'}]).lines[0].quantity).toBe(5);
    expect(spokenQuantityValues('dos')).toEqual([2]);
    expect(spokenQuantityValues('treinta y cinco')).toEqual([35]);
  });
  it('sugiere artículos cercanos cuando la voz no coincide exactamente',()=>{
    const catalog=[{productId:'M1',name:'Módem de internet'},{productId:'C1',name:'Cable de red'},{productId:'T1',name:'Teclado USB'}];
    expect(suggestInventoryOptions('necesito dos moden',catalog)[0].productId).toBe('M1');
    expect(suggestInventoryOptions('cable para la red',catalog)[0].productId).toBe('C1');
    expect(searchInventoryOptions('muéstrame 10 artículos de moden',catalog,10)[0].productId).toBe('M1');
    expect(searchInventoryOptions('busca material inexistente',catalog,10)).toEqual([]);
  });
});
