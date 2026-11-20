import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
process.env.PW_TEST_SCREENSHOT_NO_FONTS_READY='1';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 for(const width of [390,1365])for(const department of ['WAREHOUSE','GENERAL_SERVICES','TECHNOLOGY']){
  const page=await browser.newPage({viewport:{width,height:950}}),errors=[];
  page.on('pageerror',error=>errors.push(error.message));
  await page.route('**/api/**',route=>{
   const path=new URL(route.request().url()).pathname;
   return route.fulfill({json:path.endsWith('warehouse-groups')?{groups:['Grupo Norte','Grupo Sur','Grupo Bávaro']}:[]});
  });
  await page.goto(`http://127.0.0.1:5188/scripts/qa-ticket-request.html?department=${department}`,{waitUntil:'domcontentloaded',timeout:60000});
  await page.getByLabel('Destino de la solicitud').selectOption('GROUP');
  const search=page.getByRole('searchbox',{name:'Buscar grupo'}),select=page.getByLabel('Grupo de agencias');
  await search.fill('bavaro');
  await select.selectOption('Grupo Bávaro');
  assert.equal(await select.locator('option').count(),2);
  await search.fill('norte');
  assert.equal(await select.inputValue(),'Grupo Bávaro');
  await select.selectOption('');
  await search.fill('inexistente');
  await page.getByRole('status').filter({hasText:'No hay grupos'}).waitFor();
  await search.fill('');
  assert.equal(await select.locator('option').count(),4);
  assert.equal(await page.locator('.warehouse-request-composer').evaluate(node=>node.scrollWidth>node.clientWidth+1),false);
  assert.deepEqual(errors,[]);
  await page.screenshot({path:`outputs/qa-ticket-request-${department}-${width}.png`,fullPage:true});
  console.log(`PASS ${department} ${width}px: group search, accents, retained selection, empty results, no overflow`);
  await page.close();
 }
 const page=await browser.newPage();let movement=null,documentPosted=false;
 await page.route('**/api/**',async route=>{
  const path=new URL(route.request().url()).pathname;
  if(path==='/api/institutional-documents'){documentPosted=true;return route.fulfill({json:{id:123}});}
  if(path==='/api/maintenance/movements'){movement=route.request().postDataJSON();return route.fulfill({json:{ok:true}});}
  return route.fulfill({json:[]});
 });
 await page.goto('http://127.0.0.1:5188/scripts/qa-ticket-request.html?ticket=1',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('button',{name:'Solicitar artículo sin existencia'}).click();
 await page.getByPlaceholder('Nombre del equipo o material').fill('Equipo QA');
 await page.getByRole('spinbutton').fill('1500');
 await page.getByRole('button',{name:'Enviar solicitud'}).click();
 await page.waitForFunction(()=>document.title==='Solicitud enviada');
 assert.equal(documentPosted,true);assert.equal(movement.quantity,1500);
 assert.equal(movement.ticketId,'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
 console.log('PASS ticket-linked submission: PDF followed by movement, 1500 units and correct ticket');
}finally{await browser.close();}
