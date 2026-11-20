import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {resolve} from 'node:path';
import {readFileSync, writeFileSync, mkdirSync} from 'node:fs';
import {createServer} from 'vite';
import {PDFDocument} from 'pdf-lib';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const server=await createServer({configFile:false,resolve:{alias:{'@':resolve('src')}},optimizeDeps:{entries:[],noDiscovery:true,include:['react','react/jsx-runtime','react/jsx-dev-runtime','react-dom/client','lucide-react']},esbuild:{jsx:'automatic'},server:{host:'127.0.0.1',port:5201,strictPort:true,watch:null}});
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
 await page.goto('http://127.0.0.1:5201/qa-performance');
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
  const props={session,departmentEntry:{department:'TECHNOLOGY',section:'agencies',key:1},onSessionExpired(){},onOpenTicket(){},onClose(){window.root.unmount()},mode:mode==='audit'?'audit':'queue',section:'files',url:'/api/qa/file',title:'PDF de prueba'};
  if(mode==='institutionalPdf')props.data=new Uint8Array(await (await fetch('/api/qa/file')).arrayBuffer());
  window.root.render(window.React.createElement(m.default,props));
 },mode);
 await page.waitForFunction(()=>document.getElementById('root').textContent.length>10);
}
fixtures['/api/tickets/agencies']={agencies:[{id:'11111111-1111-1111-1111-111111111111',codigo:'QA-COORD',terminal:'Agencia prueba',grupo:'Grupo prueba',direccion:'Calle original',sector:'Sector original',municipio:'Municipio original',provincia:'Provincia original',latitude:18.5,longitude:-69.9}]};
try{
 for(const viewport of [{width:390,height:844},{width:1366,height:900}]){
  const {page,errors}=await setup(viewport);const saved=[];
  await page.route('**/api/technology/agencies/*',route=>{saved.push(route.request().postDataJSON());return route.fulfill({json:{ok:true}})});
  await mount(page,'tickets');
  for(const [latitude,longitude] of [['18.735693018293','-70.162651293847'],['-33.868819712345','151.209295598765'],['90','-180']]){
   await page.getByRole('button',{name:/QA-COORD/}).click();
   await page.getByLabel('Latitud',{exact:true}).fill(latitude);
   await page.getByLabel('Longitud',{exact:true}).fill(longitude);
   assert(await page.getByLabel('Latitud',{exact:true}).evaluate(e=>e.checkValidity()));
   assert(await page.getByLabel('Longitud',{exact:true}).evaluate(e=>e.checkValidity()));
   const response=page.waitForResponse(r=>r.url().includes('/api/technology/agencies/'));
   await page.getByRole('button',{name:'Guardar actualización',exact:true}).click();await response;
   await page.getByLabel('Latitud',{exact:true}).waitFor({state:'hidden'});
   const data=saved.at(-1);assert.equal(data.latitude,Number(latitude));assert.equal(data.longitude,Number(longitude));
   for(const field of ['codigo','terminal','grupo','direccion','sector','municipio','provincia'])assert.equal(data[field],fixtures['/api/tickets/agencies'].agencies[0][field]);
  }
  await page.getByRole('button',{name:/QA-COORD/}).click();await page.getByLabel('Latitud',{exact:true}).fill('91');assert.equal(await page.getByLabel('Latitud',{exact:true}).evaluate(e=>e.checkValidity()),false);
  assert.deepEqual(errors,[]);console.log('PASS coordinates precision, worldwide ranges, unchanged fields',viewport.width);await page.close();
 }
}finally{await browser.close();await server.close();}
