import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {resolve} from 'node:path';
import {readFileSync,writeFileSync} from 'node:fs';
import {createServer} from 'vite';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const server=await createServer({configFile:false,resolve:{alias:{'@':resolve('src')}},optimizeDeps:{entries:[],noDiscovery:true,include:['react','react/jsx-runtime','react/jsx-dev-runtime','react-dom','react-dom/client','lucide-react']},esbuild:{jsx:'automatic'},server:{host:'127.0.0.1',port:5205,strictPort:true,watch:null}});
await server.listen();const browser=await chromium.launch({headless:true,channel:'msedge'});
const css=[...readFileSync('src/main.tsx','utf8').matchAll(/import "(\.\/[^"\n]+\.css)"/g)].map(m=>`<link rel="stylesheet" href="/src/${m[1].slice(2)}">`).join('');
const report=[];
try{
 for(const width of [1366,390]){
  const page=await browser.newPage({viewport:{width,height:1000}});const errors=[],requests=[];let reject=false;
  page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/*',r=>{
   const path=new URL(r.request().url()).pathname;
   if(path==='/qa')return r.fulfill({contentType:'text/html',body:`<html><head><meta name="viewport" content="width=device-width,initial-scale=1">${css}</head><body><div id="root"></div></body></html>`});
   if(path.startsWith('/api/')){
    requests.push({path,body:r.request().postDataJSON()});
    if(path==='/api/account/recovery-code/generate')return r.fulfill({status:reject?400:200,json:reject?{error:'Revisa tu usuario y contraseña actual.'}:{code:'ABCD-1234-EF56-7890-ABCD-1234-EF56-7890'}});
    if(path==='/api/account/password/recover')return r.fulfill({json:{ok:true}});
    throw Error('Unexpected API '+path);
   }
   return r.continue();
  });
  await page.goto('http://127.0.0.1:5205/qa');
  await page.evaluate(async()=>{
   const React=(await import('/node_modules/.vite/deps/react.js')).default;const dom=await import('/node_modules/.vite/deps/react-dom_client.js');
   const Login=(await import('/src/components/vite/PortalLogin.tsx')).default;
   const Profile=(await import('/src/components/vite/ProfileSettings.tsx')).default;
   const root=(dom.createRoot||dom.default.createRoot)(document.getElementById('root'));
   window.renderLogin=()=>root.render(React.createElement(Login,{onSuccess(){throw Error('Generation must not sign in');}}));
   window.renderProfile=()=>root.render(React.createElement(Profile,{session:{authenticated:true,id:'qa',email:'qa@grupotejeda.local',displayName:'QA',role:'Administrator',scope:'all',permissions:{}},onClose(){},onSession(){}}));
   window.renderLogin();
  });
  await page.getByLabel('Usuario corporativo').fill('prueba.usuario');
  await page.getByRole('button',{name:'Código de recuperación',exact:true}).click();
  await page.getByRole('heading',{name:'Código de recuperación',exact:true}).waitFor();
  assert.equal(await page.getByLabel('Usuario',{exact:true}).inputValue(),'prueba.usuario');
  assert.equal(await page.getByRole('button',{name:'Generar o reemplazar código'}).isDisabled(),true);
  await page.getByLabel('Confirma tu contraseña actual').fill('ClaveQA-123!');reject=true;
  await page.getByRole('button',{name:'Generar o reemplazar código'}).click();
  await page.getByText('Revisa tu usuario y contraseña actual.',{exact:true}).waitFor();assert.equal(await page.getByLabel('Código privado').count(),0);
  reject=false;await page.getByRole('button',{name:'Generar o reemplazar código'}).click();
  await page.getByLabel('Código privado').waitFor();assert.equal(await page.getByLabel('Confirma tu contraseña actual').inputValue(),'');
  assert.deepEqual(requests.at(-1),{path:'/api/account/recovery-code/generate',body:{userName:'prueba.usuario',currentPassword:'ClaveQA-123!'}});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.screenshot({path:`outputs/recovery-login-${width}.png`,fullPage:true});
  await page.getByRole('button',{name:'Volver al inicio de sesión'}).click();
  await page.getByRole('button',{name:'Olvidé mi contraseña'}).click();
  await page.getByLabel('Código de recuperación',{exact:true}).fill('ABCD-1234-EF56-7890-ABCD-1234-EF56-7890');
  await page.getByLabel('Nueva contraseña',{exact:true}).fill('NuevaQA-123!');
  await page.getByLabel('Confirmar nueva contraseña',{exact:true}).fill('NuevaQA-123!');
  await page.getByRole('button',{name:'Restablecer contraseña'}).click();
  await page.getByText(/Contraseña restablecida/).waitFor();assert.equal(requests.at(-1).path,'/api/account/password/recover');
  await page.evaluate(()=>window.renderProfile());
  await page.getByRole('dialog',{name:'Configuración de mi perfil'}).waitFor();
  assert.equal(await page.getByText('Código de recuperación',{exact:true}).count(),0);
  await page.getByRole('button',{name:'Guardar nueva contraseña'}).waitFor();
  assert.deepEqual(errors,[]);report.push({width,checks:'generation, credential failure, recovery, profile removal, no overflow',passed:true});await page.close();
 }
 writeFileSync('outputs/v453-recovery-login-qa.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
}finally{await browser.close();await server.close();}
