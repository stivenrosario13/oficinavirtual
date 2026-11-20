import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
process.env.PW_TEST_SCREENSHOT_NO_FONTS_READY='1';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 for(const width of [390,1365]){
  const page=await browser.newPage({viewport:{width,height:950}}),errors=[];
  page.on('pageerror',error=>errors.push(error.message));
  await page.route('**/api/tickets/agencies',route=>route.fulfill({json:{agencies:[{id:'agency-1',codigo:'0632084',terminal:'Villa Mella Antiguo 400',grupo:'GRUPO SANTO'}]}}));
  await page.goto('http://127.0.0.1:5189/scripts/qa-internet-ticket-request.html',{waitUntil:'networkidle',timeout:60000});
  const dialog=page.getByRole('dialog',{name:'Solicitud de equipo de internet'});
  await dialog.waitFor();
  assert.equal(await page.getByLabel('Agencia vinculada al ticket').inputValue(),'agency-1');
  assert.match(await page.getByLabel('Agencia vinculada al ticket').locator('option:checked').textContent(),/Villa Mella Antiguo 400/);
  assert.equal(await dialog.evaluate(node=>node.scrollWidth>node.clientWidth+1),false);
  assert.equal(await page.locator('.adw-dialog-backdrop').evaluate(node=>getComputedStyle(node).position),'fixed');
  assert.deepEqual(errors,[]);
  await page.screenshot({path:`outputs/qa-internet-ticket-request-${width}.png`,fullPage:true});
  console.log(`PASS internet ticket request ${width}px: linked agency selected, fixed dialog, no horizontal overflow`);
  await page.close();
 }
}finally{await browser.close();}
