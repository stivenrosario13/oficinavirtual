import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {resolve} from 'node:path';
import {readFileSync, writeFileSync, mkdirSync} from 'node:fs';
import {createServer} from 'vite';
import {PDFDocument} from 'pdf-lib';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const server=await createServer({configFile:false,resolve:{alias:{'@':resolve('src')}},optimizeDeps:{entries:[],noDiscovery:true,include:['react','react/jsx-runtime','react/jsx-dev-runtime','react-dom/client','lucide-react']},esbuild:{jsx:'automatic'},server:{host:'127.0.0.1',port:5199,strictPort:true,watch:null}});
await server.listen();
const browser=await chromium.launch({headless:true,channel:'msedge'});
const pdf=await PDFDocument.create();pdf.addPage().drawText('Primera pagina');pdf.addPage().drawText('Segunda pagina');const pdfBytes=Buffer.from(await pdf.save());
const fixtures={
 '/api/admin/users':{users:[]},'/api/admin/groups-catalog':{groups:[]},'/api/admin/access-profiles':{profiles:[]},
 '/api/administrative-files':[], '/api/administrative-files/folders':[], '/api/administrative-files/users':[],
 '/api/tickets':{tickets:[]},'/api/tickets/agencies':{agencies:[]},'/api/support/catalog':{departments:[]},'/api/tickets/technicians':{technicians:[]},'/api/tickets/findings':{findings:[]},'/api/tickets/statistics':{},
 '/api/workshop/manage':[], '/api/agency-locations':{agencies:[]},
 '/api/admin/storage/status':{enabled:true,configured:true,state:'idle',message:'Estado disponible'},
 '/api/admin/storage/verification':{state:'unknown',message:'Sin revisión'},
 '/api/admin/storage/queue':{pending:0,complete:0,review:0,stagedBytes:0,state:'idle',message:'',dailyAttemptedBytes:0,dailyLimitBytes:100,uploadsPerMinute:10,maxPdfPages:10,issues:[]},
 '/api/admin/storage/analytics':{byOrigin:[],byGroup:[],byModule:[]},'/api/admin/storage/usage':{},'/api/admin/storage/documents':{rows:[],checkedAt:null,error:null},
};
const report=[];
async function setup(viewport,stall){
 const page=await browser.newPage({viewport});const requests=[],errors=[];let blocked=!!stall;
 page.on('pageerror',error=>errors.push(error.message));
 await page.route('**/*',async route=>{
  const path=new URL(route.request().url()).pathname;
  if(['/vendor/pdfjs/pdf.mjs','/vendor/pdfjs/pdf.worker.mjs'].includes(path))return route.fulfill({contentType:'text/javascript',body:readFileSync('public'+path)});
  if(path==='/qa-performance')return route.fulfill({contentType:'text/html',body:'<html><body><div id="root"></div></body></html>'});
  if(path.startsWith('/api/')){
   requests.push(path);
   if(blocked&&path===stall)return;
   if(path==='/api/qa/file')return route.fulfill({contentType:'application/pdf',body:pdfBytes});
   if(!(path in fixtures))throw Error('Missing QA fixture: '+path);
   return route.fulfill({json:fixtures[path]});
  }
  return route.continue();
 });
 await page.goto('http://127.0.0.1:5199/qa-performance');
 await page.evaluate(async()=>{
  const r=await import('/node_modules/.vite/deps/react.js'),d=await import('/node_modules/.vite/deps/react-dom_client.js');
  window.React=r.default||r;window.root=(d.createRoot||d.default.createRoot)(document.getElementById('root'));
  window.pdfWorkers=0;window.pdfWorkersTerminated=0;
  const NativeWorker=window.Worker;
  window.Worker=class extends NativeWorker {constructor(...args){super(...args);window.pdfWorkers++}terminate(){window.pdfWorkersTerminated++;super.terminate()}};
 });
 return {page,requests,errors,unblock(){blocked=false}};
}
async function mount(page,mode){
 await page.evaluate(async mode=>{
  const {permissions}= (await import('/src/lib/user-access-profiles.ts')).userAccessProfile('ADMINISTRATOR','TECHNOLOGY');
  const session={authenticated:true,id:'qa',email:'qa@example.test',displayName:'Prueba',role:'Administrator',permissions,mustChangePassword:false,avatarUrl:null,scope:'all',supportDepartment:null,supportTeam:null,groups:[]};
  const modules={users:'UsersPermissionsWorkspace',files:'AdministrativeWorkspace',tickets:'TicketCenter',queue:'OperationsCenter',audit:'OperationsCenter',workshop:'WorkshopWorkspace',locations:'AgencyLocations',storage:'StorageMigrationPanel',pdf:'PdfFilePreview',institutionalPdf:'InstitutionalPdfCanvas'};
  const m=await import(`/src/components/vite/${modules[mode]}.tsx`);
  const props={session,onSessionExpired(){},onOpenTicket(){},onClose(){window.root.unmount()},mode:mode==='audit'?'audit':'queue',section:'files',url:'/api/qa/file',title:'PDF de prueba'};
  if(mode==='institutionalPdf')props.data=new Uint8Array(await (await fetch('/api/qa/file')).arrayBuffer());
  window.root.render(window.React.createElement(m.default,props));
 },mode);
 await page.waitForFunction(()=>document.getElementById('root').textContent.length>10);
}
try{
 for(const viewport of [{width:390,height:844},{width:1366,height:900}]){
  for(const mode of ['users','files','tickets','queue','audit','workshop','locations','storage']){
   const {page,requests,errors}=await setup(viewport);await mount(page,mode);
   await page.waitForFunction(()=>!Array.from(document.querySelectorAll('[role="status"]')).some(e=>/Cargando|Preparando/.test(e.textContent)));
   // Wait for primary effects and optional imports to settle, not for polling timers.
   await page.waitForTimeout(500);
   assert.deepEqual(errors,[],mode+' browser errors');
   assert(requests.length>0,mode+' should request data');
   if(mode==='users')assert(requests.every(p=>p.startsWith('/api/admin/')));
   if(mode==='files')assert.deepEqual([...new Set(requests)].sort(),['/api/administrative-files','/api/administrative-files/folders']);
   if(mode==='files'){
    const recipients=page.waitForResponse(response=>response.url().includes('/api/administrative-files/users'));
    await page.getByRole('button',{name:'Nueva carpeta',exact:true}).click();
    await recipients;
   }
   if(mode==='tickets')assert(!requests.some(p=>/maintenance|transition|workshop|administrative-files/.test(p)));
   if(mode==='workshop')assert.deepEqual([...new Set(requests)],['/api/workshop/manage']);
   if(mode==='storage')assert.equal(requests.length,6,'No restarting all storage queries when the first status arrives');
   report.push({viewport:viewport.width,mode,requests:[...new Set(requests)],errors});
   console.log('PASS',viewport.width,mode,requests.length);await page.close();
  }
  // A failed optional directory must not hold the primary list or hide its error.
  const q=await setup(viewport,'/api/tickets/agencies');await mount(q.page,'queue');
  await q.page.waitForTimeout(300);
  await q.page.locator('.operations-center').waitFor({timeout:1500});
  assert.equal(await q.page.locator('.operations-loading').count(),0);
  await q.page.waitForFunction(()=>document.body.textContent.includes('Los tickets están disponibles.'),{timeout:25000});
  assert.deepEqual(q.errors,[]);report.push({viewport:viewport.width,mode:'queue-optional-timeout',passed:true});await q.page.close();
  const l=await setup(viewport,'/api/agency-locations');await mount(l.page,'locations');
  await l.page.getByRole('alert').waitFor({timeout:25000});l.unblock();
  await l.page.getByRole('button',{name:/Actualizar/}).click();
  await l.page.waitForFunction(()=>!document.querySelector('[role="alert"]')&&!document.body.textContent.includes('Cargando ubicaciones'));
  assert.equal(l.requests.length,2);assert.deepEqual(l.errors,[]);report.push({viewport:viewport.width,mode:'timeout-retry',passed:true});await l.page.close();
  for(const mode of ['pdf','institutionalPdf']){
  const p=await setup(viewport);await mount(p.page,mode);
  await p.page.getByRole('button',{name:'Siguiente',exact:true}).click();
  await p.page.getByRole('button',{name:'Anterior',exact:true}).click();
  assert.equal(p.requests.filter(path=>path==='/api/qa/file').length,1,'PDF must be downloaded once across page changes');
  assert.equal(await p.page.evaluate(()=>window.pdfWorkers),1,'One PDF parser worker across page changes');
  await p.page.evaluate(()=>window.root.unmount());
  await p.page.waitForFunction(()=>window.pdfWorkersTerminated===1);
  assert.deepEqual(p.errors,[]);
  report.push({viewport:viewport.width,mode:mode+'-two-pages-one-download-worker-released',passed:true});await p.page.close();
  }
 }
 const lazy=await setup({width:390,height:844});await lazy.page.clock.install();
 await lazy.page.evaluate(async()=>{
  const {lazySection}=await import('/src/components/vite/LazySection.tsx');let attempts=0;
  const Section=lazySection(()=>++attempts===1?new Promise(()=>{}):Promise.resolve({default:()=>window.React.createElement('p',null,'Sección recuperada')}));
  window.root.render(window.React.createElement(Section));
 });
 await lazy.page.getByRole('status').waitFor();await lazy.page.clock.fastForward(21000);
 await lazy.page.getByRole('button',{name:'Reintentar sección'}).click();await lazy.page.getByText('Sección recuperada').waitFor();
 report.push({mode:'lazy-chunk-timeout-retry',passed:true});await lazy.page.close();
 mkdirSync('outputs',{recursive:true});writeFileSync('outputs/qa-performance.json',JSON.stringify(report,null,2));console.log('PASS all performance browser scenarios',report.length);
}finally{await browser.close();await server.close();}
