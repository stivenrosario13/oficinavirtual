import {createRequire} from 'node:module';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 for(const width of [360,390,600,1365]){
  const page=await browser.newPage({viewport:{width,height:900}}),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/api/**',route=>route.fulfill({json:[]}));
  await page.goto('http://127.0.0.1:5178/scripts/qa-requester-layout.html?supervisor=1&composer=1');
  await page.locator('.warehouse-request-composer').waitFor();
  const invalid=await page.locator('.warehouse-request-composer input,.warehouse-request-composer select').evaluateAll((nodes,mobile)=>nodes.filter(n=>mobile&&(parseFloat(getComputedStyle(n).fontSize)<16||n.getBoundingClientRect().height<44)).map(n=>n.outerHTML),width<=600);
  const overflow=await page.locator('.warehouse-request-composer').evaluate(n=>n.scrollWidth>n.clientWidth+1);
  if(errors.length||invalid.length||overflow)throw Error(JSON.stringify({width,errors,invalid,overflow}));
  await page.screenshot({path:`outputs/qa-mobile-request-${width}.png`,fullPage:true});
  console.log(`PASS ${width}px: controles legibles, sin desbordamiento ni errores`);
  await page.close();
 }
}finally{await browser.close()}
