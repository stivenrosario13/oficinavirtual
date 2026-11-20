import {describe,expect,it} from 'vitest';
import {wakeCommand} from '../src/components/vite/AdminHeaderVoiceAssistant';

describe('palabra clave de Oficina Virtual',()=>{
  it('solo activa el asistente cuando la frase comienza con la llamada exacta',()=>{
    expect(wakeCommand('Oficina Virtual')).toEqual({command:''});
    expect(wakeCommand('Hola Oficina Virtual')).toEqual({command:''});
    expect(wakeCommand('Oficina Virtual créame un ticket')).toEqual({command:'creame un ticket'});
    expect(wakeCommand('estoy trabajando en la oficina virtual')).toBeNull();
    expect(wakeCommand('oficina')).toBeNull();
    expect(wakeCommand('virtual')).toBeNull();
  });
});
