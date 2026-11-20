import {describe,expect,it} from 'vitest';
import {findVoiceAgency,findVoiceCatalogItem,parseVoiceWarehouseLines,prepareVoiceWarehouseBatch,voiceSubject} from '../src/lib/voice-operation-assistant';

describe('asistente de operaciones por voz',()=>{
  const agencies=[{id:'a1',codigo:'6011055',terminal:'Villa Mella Antiguo 400',grupo:'Grupo Santos'}];
  it('reconoce un código parcial junto con el grupo hablado',()=>{
    const rows=[...agencies,{id:'a2',codigo:'88442',terminal:'Agencia Mango 442',grupo:'Grupo Mango'}];
    expect(findVoiceAgency('agencia 442 de grupo mango',rows)?.id).toBe('a2');
  });
  it('reconoce agencia, departamento y asunto hablado',()=>{
    const text='Crea un ticket de internet para la agencia 6011055 descripcion el modem se desconecta';
    expect(findVoiceAgency(text,agencies)?.id).toBe('a1');
    expect(findVoiceCatalogItem(text,[{code:'TECHNOLOGY',name:'Tecnología'},{code:'GS',name:'Servicios Generales'}])?.code).toBeUndefined();
    expect(voiceSubject(text)).toContain('internet');
  });
  it('convierte artículos y cantidades del catálogo',()=>{
    const lines=parseVoiceWarehouseLines('Solicita 2 modem y 3 cable de red para la agencia 6011055',[
      {productId:'M1',name:'Modem'},{productId:'C1',name:'Cable de red'}
    ]);
    expect(lines).toEqual([
      {productId:'M1',name:'Modem',quantity:2,manual:false},
      {productId:'C1',name:'Cable de red',quantity:3,manual:false}
    ]);
  });
  it('conserva como manual un artículo que aún no existe',()=>{
    expect(parseVoiceWarehouseLines('Necesito 4 adaptadores especiales para la agencia 6011055',[])[0]).toMatchObject({name:'adaptadores especiales',quantity:4,manual:true});
  });
  it('prepara solicitudes separadas y conserva los artículos de cada agencia',()=>{
    const batch={
      mode:'MULTI_AGENCY' as const,
      targets:[
        {agencyId:'a1',agencyLabel:'Agencia Uno',lines:[{productId:'M1',name:'Módem',quantity:2,manual:false}]},
        {agencyId:'a2',agencyLabel:'Agencia Dos',lines:[{productId:'C1',name:'Cable de red',quantity:5,manual:false}]}
      ]
    };
    expect(prepareVoiceWarehouseBatch(batch,new Set(['a1','a2']),new Set(['M1','C1']))).toEqual({
      valid:true,
      multi:true,
      lines:[
        {productId:'M1',name:'Módem',quantity:2,manual:false,agencyId:'a1'},
        {productId:'C1',name:'Cable de red',quantity:5,manual:false,agencyId:'a2'}
      ]
    });
  });
  it('rechaza artículos inexistentes y cantidades fuera del límite por agencia',()=>{
    const missingProduct={
      mode:'AGENCY' as const,
      targets:[{agencyId:'a1',agencyLabel:'Agencia Uno',lines:[{productId:'X1',name:'Desconocido',quantity:1,manual:false}]}]
    };
    const excessiveQuantity={
      mode:'AGENCY' as const,
      targets:[{agencyId:'a1',agencyLabel:'Agencia Uno',lines:[
        {productId:'M1',name:'Módem',quantity:6000,manual:false},
        {productId:'C1',name:'Cable',quantity:5000,manual:false}
      ]}]
    };
    expect(prepareVoiceWarehouseBatch(missingProduct,new Set(['a1']),new Set(['M1']))?.valid).toBe(false);
    expect(prepareVoiceWarehouseBatch(excessiveQuantity,new Set(['a1']),new Set(['M1','C1']))?.valid).toBe(false);
  });
});
