import {createRequire} from 'node:module';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
const errors=[];
try{
 for(const supervisor of [false,true])for(const width of [360,768,1365]){
 const page=await browser.newPage({viewport:{width,height:950}});page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/api/**',route=>{
  const path=new URL(route.request().url()).pathname;
  const docs=[{id:1,area:'communications',channel:'requirements',title:'Solicitud administrativa de prueba con título largo para verificar adaptación',description:'Prueba visual sin conexión al servidor.',department:supervisor?'Supervisión':'Administración',recipientDepartment:'Administración',status:'pending',size:1234,originalFileName:'QA.pdf',ownerUserName:'qa',ownerDisplayName:'QA',isOwner:true,createdUtc:'2026-10-06T14:00:00Z',administrativeRequest:{formType:'SIM',number:'SOL-ADM-QA'}}];
  return route.fulfill({json:path==='/api/institutional-documents'?docs:[]});
 });
 await page.goto('http://127.0.0.1:5178/scripts/qa-requester-layout.html'+(supervisor?'?supervisor=1':''),{waitUntil:'domcontentloaded',timeout:30000});
 await page.getByText('Solicitud administrativa de prueba con título largo para verificar adaptación',{exact:true}).waitFor({timeout:20000}).catch(async e=>{console.log('ERRORS',errors,'BODY',await page.locator('body').innerText());throw e});
 await page.screenshot({path:`outputs/qa-requester-${supervisor?'supervisor':'admin'}-${width}.png`,fullPage:true});
 const dimensions=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth}));
 if(dimensions.scroll>dimensions.width+1)throw Error(`Overflow ${supervisor}/${width}: ${JSON.stringify(dimensions)}`);
 console.log(`PASS ${supervisor?'Supervisor':'Administrativo'} ${width}px sin desbordamiento`);await page.close();
 }
 if(errors.length)throw Error(errors.join('\n'));
}finally{await browser.close();}
