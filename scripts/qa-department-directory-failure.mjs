import {createRequire} from 'node:module';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 for(const width of [390,1365]){
  const page=await browser.newPage({viewport:{width,height:950}}),errors=[];
  page.on('pageerror',e=>errors.push(e.message));await page.route(/^https:\/\//,r=>r.abort());
  await page.route('**/api/**',async r=>{
   const path=new URL(r.request().url()).pathname;
   if(path==='/api/tickets/technicians')return r.fulfill({status:500,json:{error:'QA: fallo de personal'}});
   if(path.endsWith('/statistics'))return r.fulfill({status:503,json:{error:'QA unavailable'}});
   const body={tickets:[],findings:[],agencies:[],technicians:[],departments:[],projects:[],movements:[],procurement:[]};
   if(path==='/api/support/catalog')body.departments=['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','TREASURY','ACCOUNTING'].map((code,i)=>({code,name:['Tecnología','Servicios Generales','Recursos Humanos','Tesorería','Contabilidad'][i],isActive:true,accentColor:'#0F766E',categories:[]}));
   return r.fulfill({json:body});
  });
  await page.goto('http://127.0.0.1:5183/scripts/qa-supervisor-layout.html?role=admin',{waitUntil:'domcontentloaded'});
  await page.getByText('QA: fallo de personal',{exact:true}).waitFor();
  for(const name of ['Tecnología','Servicios Generales','Recursos Humanos','Tesorería','Contabilidad'])await page.locator('.department-card-grid button').filter({hasText:name}).waitFor();
  if(errors.length)throw Error(errors.join('\n'));
  console.log(`PASS ${width}px: administrator directory survives personnel failure and keeps error visible`);await page.close();
 }
}finally{await browser.close();}
