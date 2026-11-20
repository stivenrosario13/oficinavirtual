import {createRequire} from 'node:module';
import {mkdir} from 'node:fs/promises';
import assert from 'node:assert/strict';

const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const base=process.env.QA_URL||'http://127.0.0.1:5199';
await mkdir('outputs/incident-form-theme',{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 for(const width of [1365,390]){
  const page=await browser.newPage({viewport:{width,height:950},serviceWorkers:'block'}),errors=[];
  page.on('pageerror',error=>errors.push(error.message));
  await page.addInitScript(()=>localStorage.setItem('push-prompt-dismissed','1'));
  await page.route(/^https:\/\//,route=>route.abort());
  await page.route('**/api/**',route=>{
   const path=new URL(route.request().url()).pathname;
   if(path==='/api/admin/session')return route.fulfill({json:{authenticated:true,id:'yesica-qa',email:'yesica.mercedes@grupotejeda.local',displayName:'Yesica Mercedes',role:'Department',assignedDepartment:'CALL_CENTER',supportDepartment:'CALL_CENTER',supportTeam:'CALL_CENTER',scope:'all',mustChangePassword:false,permissions:{canView:true,canCreateTickets:true,canViewDocuments:true,canUploadDocuments:true,usesExplicitPermissions:true}}});
   if(path==='/api/license/status')return route.fulfill({json:{enabled:true,allowed:true,status:'ACTIVE',code:'ACTIVE',message:'QA'}});
   if(path==='/api/institutional-documents')return route.fulfill({json:[]});
   if(path==='/api/tickets')return route.fulfill({json:{tickets:[]}});
   return route.fulfill({json:[]});
  });
  await page.goto(base,{waitUntil:'domcontentloaded',timeout:60000});
  const incidentNav=page.getByRole('button',{name:'Juegos tradicionales',exact:true});
  if(width<700)await incidentNav.evaluate(element=>element.click());
  else await incidentNav.click();
  const search=page.getByPlaceholder('Buscar formulario, cédula, RNC, terminal o remitente…');
  await search.waitFor();await search.fill('prueba visible');
  assert.equal(await search.inputValue(),'prueba visible');
  assert.equal(await search.evaluate(el=>getComputedStyle(el).color),'rgb(255, 255, 255)');
  assert.notEqual(await search.evaluate(el=>getComputedStyle(el.parentElement).backgroundColor),'rgb(255, 255, 255)');
  await page.getByRole('button',{name:'Nueva solicitud',exact:true}).click();
  const form=page.locator('.admin-request-form.admin-special-form');await form.waitFor();
  assert.notEqual(await form.evaluate(el=>getComputedStyle(el).backgroundColor),'rgb(255, 255, 255)');
  assert.equal(await page.getByLabel('Responsable de recepción').locator('option').allTextContents().then(items=>items.join('|')),'Juegos tradicionales · YESICA MERCEDES|Juegos tradicionales · LUCIANNY GONZÁLEZ');
  assert.equal(await page.getByLabel('Responsable de recepción').inputValue(),'YESICA MERCEDES');
  await page.getByLabel('Asunto *').fill('Texto visible en blanco');
  assert.equal(await page.getByLabel('Asunto *').evaluate(el=>getComputedStyle(el).color),'rgb(255, 255, 255)');
  await page.screenshot({path:`outputs/incident-form-theme/form-${width}.png`,fullPage:true,animations:'disabled'});
  await page.getByRole('button',{name:'Importar masivo desde Excel',exact:true}).click();
  const importer=page.locator('.admin-excel-import');await importer.waitFor();
  assert.notEqual(await importer.evaluate(el=>getComputedStyle(el).backgroundColor),'rgb(255, 255, 255)');
  assert.ok(await page.getByRole('button',{name:'Descargar plantilla Excel',exact:true}).isEnabled());
  assert.ok(await page.getByRole('button',{name:'Cargar Excel .xlsx',exact:true}).isEnabled());
  assert.deepEqual(errors,[]);
  console.log(`PASS ${width}px: buscador oscuro, texto blanco, responsables exclusivos e importación disponible.`);
  await page.close();
 }
}finally{await browser.close();}
