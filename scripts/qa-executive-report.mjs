import {createRequire} from 'node:module';
import {mkdir,readFile} from 'node:fs/promises';
import assert from 'node:assert/strict';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
await mkdir('output/pdf',{recursive:true});
const statuses=['OPEN','IN_PROGRESS','RESOLVED','CLOSED'];
const catalogSql=await readFile('database-mssql/005_import_agencies_ltk.sql','utf8');
const realCatalog=[...catalogSql.matchAll(/\(N'LTK:[^']+',N'([^']+)',N'([^']+)',N'([^']+)',N'([^']+)'\)/g)].slice(0,72).map(match=>({code:match[1],agency:match[2],group:match[3]}));
assert.ok(realCatalog.length>=50,'El catálogo real de agencias no pudo cargarse');
const agencies=realCatalog.map((row,i)=>({...row,status:i%5===0?'PENDING':i%7===0?'REVIEW_REQUIRED':'COMPLETED',completedAt:i%5===0?undefined:`202${3+i%4}-${String(i%12+1).padStart(2,'0')}-15T14:00:00Z`,submittedBy:i%4===0?'robert.medina':'supervisor.real'}));
const auditGroups=Object.entries(agencies.reduce((acc,row)=>{const value=acc[row.group]??={total:0,completed:0,pending:0,review:0};value.total++;if(row.status==='COMPLETED')value.completed++;else if(row.status==='PENDING')value.pending++;else value.review++;return acc},{ })).map(([group,value])=>({group,...value,completedInPeriod:value.completed}));
const report={period:'global',fromUtc:'2000-01-01T00:00:00Z',toUtc:'2026-10-31T23:30:00Z',generatedUtc:'2026-10-31T19:30:00Z',
 tickets:[...['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'].flatMap((area,i)=>statuses.map((status,j)=>({area,team:area==='TECHNOLOGY'?(j%2?'CALL_CENTER':'TECHNICAL_FAILURE'):'GENERAL',status,total:(i+1)*(j+2)})))],
 findings:[{area:'TECHNOLOGY',team:'HALLAZGOS',status:'OPEN',total:8},{area:'GENERAL_SERVICES',team:'HALLAZGOS',status:'RESOLVED',total:17}],
 findingPeople:[{role:'SUPERVISOR',person:'robert.medina',department:'TECHNOLOGY',status:'OPEN',total:8},{role:'TECNICO',person:'Técnico de demostración',department:'TECHNOLOGY',status:'RESOLVED',total:12},{role:'SOPORTE',person:'Soporte General',department:'GENERAL_SERVICES',status:'RESOLVED',total:5}],
 warehouse:[{department:'Tecnología',status:'pending',total:9},{department:'Servicios Generales',status:'received',total:12},{department:'Administración',status:'approved',total:7}],
 auditGroups,agencies};
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/api/executive-report**',route=>route.fulfill({json:report}));
 await page.goto('http://127.0.0.1:5220/scripts/qa-executive-report.html',{waitUntil:'domcontentloaded'});
 await page.getByRole('heading',{name:'Reporte ejecutivo global'}).waitFor();assert.equal(await page.locator('.executive-metrics article').count(),7);assert.equal(await page.locator('.executive-breakdowns section').count(),3);
 await page.screenshot({path:'output/pdf/reporte-ejecutivo-seccion.png',fullPage:true});
 const download=page.waitForEvent('download');await page.getByRole('button',{name:/Descargar PDF global/}).click();const item=await download;await item.saveAs('output/pdf/reporte-ejecutivo-oficina-virtual.pdf');
 assert.deepEqual(errors,[]);console.log('PASS reporte histórico global, 7 indicadores, 3 desgloses y PDF descargado');
}finally{await browser.close()}
