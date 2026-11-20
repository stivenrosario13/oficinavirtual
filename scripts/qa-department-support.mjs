import {createRequire} from 'node:module';
process.env.PW_TEST_SCREENSHOT_NO_FONTS_READY='1';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{for(const width of [390,1365]){
 const page=await browser.newPage({viewport:{width,height:950}}),errors=[],calls=[];
 page.on('pageerror',e=>errors.push(e.message));await page.route(/^https:\/\//,r=>r.abort());
 await page.route('**/api/**',async r=>{
  const path=new URL(r.request().url()).pathname;calls.push(path);
  if(path.endsWith('/statistics'))return r.fulfill({status:503,json:{error:'QA unavailable'}});
  let body={tickets:[],findings:[],agencies:[],technicians:[],departments:[],projects:[]};
  if(path==='/api/support/catalog')body.departments=['TREASURY','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'].map((code,i)=>({code,name:['Tesorería','Tecnología','Servicios Generales','Recursos Humanos'][i],isActive:true,accentColor:'#0F766E',categories:[{code:'DEPARTMENT_REQUEST',name:'Solicitud al departamento',isActive:true,requiresDetail:true,defaultPriority:'MEDIUM',options:[]}]}));
  await r.fulfill({json:body});
 });
 await page.goto('http://127.0.0.1:5183/scripts/qa-supervisor-layout.html?department=TREASURY',{waitUntil:'domcontentloaded'});
 await page.getByRole('heading',{name:'Tesorería',exact:true}).waitFor();
 await page.getByRole('navigation',{name:'Opciones de Soporte'}).waitFor();
 await page.screenshot({path:`outputs/qa-treasury-support-${width}.png`,fullPage:true});
 await page.getByRole('button',{name:'Solicitar a otro departamento'}).click();
 await page.locator('.department-card-grid button').filter({hasText:'Tecnología'}).click();
 await page.locator('form').filter({has:page.locator('.selected-support-department')}).waitFor();
 if(!await page.locator('.selected-support-department').getByText('Tecnología',{exact:true}).count())throw Error('Wrong destination');
 if(calls.some(path=>path.startsWith('/api/maintenance/')||path==='/api/agency-transitions'))throw Error('Loading unrelated restricted modules');
 if(errors.length)throw Error(errors.join('\n'));
 console.log(`PASS Treasury ${width}px: departmental support, outbound request form, no unrelated module calls`);await page.close();
}}finally{await browser.close();}
