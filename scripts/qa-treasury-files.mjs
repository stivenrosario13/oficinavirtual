import {createRequire} from 'node:module';
process.env.PW_TEST_SCREENSHOT_NO_FONTS_READY='1';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{for(const width of [390,1365]){
 const page=await browser.newPage({viewport:{width,height:950}}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));await page.route(/^https:\/\//,r=>r.abort());
 await page.route('**/api/**',r=>r.fulfill({json:[]}));
 await page.goto('http://127.0.0.1:5183/scripts/qa-supervisor-layout.html?files=TREASURY',{waitUntil:'domcontentloaded'});
 await page.getByRole('heading',{name:'Biblioteca de Tesorería'}).waitFor();
 const bg=await page.locator('.adw-content').evaluate(el=>getComputedStyle(el).backgroundColor);
 if(bg==='rgb(255, 255, 255)')throw Error('White library background');
 if(errors.length)throw Error(errors.join('\n'));
 await page.screenshot({path:`outputs/qa-treasury-files-${width}.png`,fullPage:true});
 console.log(`PASS ${width}px: Tesorería library, background ${bg}`);await page.close();
}}finally{await browser.close();}
