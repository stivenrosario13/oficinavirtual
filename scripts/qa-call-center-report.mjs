import {createRequire} from 'node:module';import assert from 'node:assert/strict';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{for(const width of [1365,390]){
 const context=await browser.newContext({viewport:{width,height:1000}});await context.addInitScript(()=>{window.print=()=>{window.__printed=true;};});
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:5201/cc-report-v446/scripts/qa-call-center-report.html');await page.getByLabel('Fecha del reporte Call Center').fill('2026-10-15');
 for(const [days,count] of [['1',4],['7',28],['15',60],['30',60]]){
  await page.getByLabel('Periodo del reporte Call Center').selectOption(days);await page.getByText(`Total de casos: ${count}`,{exact:true}).waitFor();
  const pending=context.waitForEvent('page');await page.getByRole('button',{name:'Imprimir reporte'}).click();const popup=await pending;await popup.waitForFunction(()=>window.__printed===true);
  assert.equal(await popup.locator('.details tbody tr').count(),count);assert.equal(await popup.locator('.details thead th').count(),13);
  assert.equal(await popup.locator('h1').count(),2);
  if(width===1365&&days==='15'){await popup.pdf({path:'outputs/qa-v447-call-center-print.pdf',preferCSSPageSize:true,printBackground:true});await popup.screenshot({path:'outputs/qa-v447-call-center-print.png',fullPage:false});}
  await popup.close();console.log(`PASS ${width}px ${days} days: historical period, ${count} full rows and print dialog`);
 }
 for(const kind of ['Excel','PDF']){
  const pending=page.waitForEvent('download');await page.getByRole('button',{name:'Descargar '+kind,exact:true}).click();const file=await pending;await file.saveAs(`outputs/qa-v447-report-${width}.${kind==='Excel'?'xlsx':'pdf'}`);assert.equal(await file.failure(),null);
 }
 await page.screenshot({path:`outputs/qa-v447-call-center-report-${width}.png`,fullPage:true});
 await page.getByLabel('Fecha del reporte Call Center').fill('2020-01-01');await page.getByText('No hay casos para este periodo.').waitFor();
 await page.getByLabel('Fecha del reporte Call Center').fill('');assert.ok(await page.getByRole('button',{name:'Imprimir reporte'}).isDisabled());assert.deepEqual(errors,[]);await context.close();
}}finally{await browser.close();}
