import {describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';

describe('asistente de voz privado del administrador',()=>{
  const app=readFileSync('src/App.tsx','utf8');
  const assistant=readFileSync('src/components/vite/AdminHeaderVoiceAssistant.tsx','utf8');
  const assistantCss=readFileSync('src/components/vite/adminHeaderVoiceAssistant.css','utf8');
  it('solo se monta para el Administrador principal junto a las notificaciones',()=>{
    expect(app).toContain("{isAdministrator&&<Suspense fallback={null}><AdminHeaderVoiceAssistant");
    expect(app.indexOf('AdminHeaderVoiceAssistant')).toBeLessThan(app.indexOf('Centro de notificaciones'));
  });
  it('se activa con Oficina Virtual y permite tickets o solicitudes',()=>{
    expect(assistant).toContain('wakeCommand(phrase)');
    expect(assistant).toMatch(/fetch\(["']\/api\/tickets["']/);
    expect(assistant).toContain('¿Qué falla del área de');
    expect(assistant).toContain('onWarehouseCommand(batch)');
    expect(assistant).toContain('speechSynthesis');
  });
  it('envía automáticamente la solicitud de Almacén',()=>{
    expect(app).toContain('autoSubmitVoice');
    expect(readFileSync('src/components/vite/WarehouseRequestComposer.tsx','utf8')).toContain('autoSubmittedVoice.current=true;void submit()');
  });
  it('muestra agencias y permite solicitudes de voz para varias agencias',()=>{
    const composer=readFileSync('src/components/vite/WarehouseRequestComposer.tsx','utf8');
    expect(assistant).toContain('admin-voice-agency-options');
    expect(assistant).toContain('warehouseDrafts');
    expect(assistant).toContain('continuemos con otra agencia');
    expect(assistant).toContain('crear solicitudes');
    expect(composer).toContain('initialVoiceBatch');
    expect(composer).toContain("setDestination(prepared.multi?'MULTI_AGENCY':'AGENCY')");
    expect(assistant).toContain('showAgencyLookup');
    expect(assistant).toContain('searchAgencyOptions');
    expect(assistant).toContain('agencyLookup');
    expect(assistant).toContain('Agregar más artículos');
    expect(assistant).toContain('addMore');
    expect(assistant).toContain('const combined = new Map');
    expect(assistant).toContain('Esta solicitud ahora tiene');
  });
  it('crea tickets en los tres departamentos sin asumir Tecnología',()=>{
    expect(assistant).toContain('Tecnología, Servicios Generales o Recursos Humanos');
    expect(assistant).toContain('resolveCategoryStrict(context.transcript, department)');
    expect(assistant).not.toContain('departments.find((item) => item.code === "TECHNOLOGY")');
  });
  it('mantiene cada pregunta visible y ofrece artículos y todas las fallas',()=>{
    expect(assistant).toContain('admin-voice-question');
    expect(assistant).toContain('suggestInventoryOptions');
    expect(assistant).toContain('Selecciona el artículo del inventario');
    expect(assistant).toContain('Todas las fallas activas');
    expect(assistant).toContain('context.pending = { kind: "category", options: available }');
    expect(assistantCss).toContain('.header-account .admin-voice-popover :is(span,strong,small)');
    expect(assistantCss).toContain('display:block!important;visibility:visible!important');
  });
  it('mantiene una conversación profesional por turnos',()=>{
    expect(assistant).toContain('wakeCommand(phrase)');
    expect(assistant).toContain("interimResults = false");
    expect(assistant).toContain('setStatus("Ahora te escucho…")');
    expect(assistant).toContain('Robot respondiendo; cuando termine podrás contestar.');
    expect(assistant).not.toContain('likelySpeechEcho');
    expect(assistant).not.toContain('startRef.current(true)');
    expect(assistant).toContain('spokenAgencyLabel');
    expect(assistant).toContain('admin-voice-conversation');
    expect(assistant).toContain('Preguntas del robot y respuestas reconocidas');
    expect(assistant).toContain('role: "user"');
    expect(assistant).toContain('setText("")');
    expect(assistantCss).toContain('.admin-voice-conversation article.user');
    expect(assistant).toContain('Datos del sistema conectados');
    expect(assistant).toContain('Agencia confirmada');
    expect(assistant).toContain('suggestAgencyOptions');
    expect(assistant).toContain('No encuentro el artículo para esta agencia.');
    expect(assistant).toContain('admin-voice-inventory-picker');
    expect(assistant).toContain('filteredInventoryChoices');
    expect(assistant).toContain('`${item.name} ${item.productId}`');
    expect(assistant).not.toContain('Ahora dime los artículos y cantidades para la ${spokenAgencyLabel');
  });
  it('no enciende el micrófono automáticamente al entrar al sistema',()=>{
    expect(assistant).not.toContain('const automatic=');
    expect(assistant).not.toContain('setTimeout(()=>startRef.current(),700)');
    expect(assistant).toContain('Activar palabra clave');
  });
  it('los formularios de otros perfiles ya no muestran el control de voz',()=>{
    expect(readFileSync('src/components/vite/InternalTicketRequest.tsx','utf8')).not.toContain('VoiceOperationAssistant');
    expect(readFileSync('src/components/vite/WarehouseRequestComposer.tsx','utf8')).not.toContain('<VoiceOperationAssistant');
  });
});
