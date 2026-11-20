import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { createServer } from 'vite';
const require = createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const { chromium } = require('playwright');
const server = await createServer({ configFile: false, resolve: { alias: { '@': resolve('src') } }, optimizeDeps: { entries: [], noDiscovery: true, include: ['react', 'react/jsx-runtime', 'react/jsx-dev-runtime', 'react-dom/client', 'lucide-react'] }, esbuild: { jsx: 'automatic' }, server: { host: '127.0.0.1', port: 5198, strictPort: true, watch: null } });
await server.listen();
console.log('QA camera server ready');
const browser = await chromium.launch({ headless: true, channel: 'msedge' });
console.log('QA camera browser ready');
try {
  for (const viewport of [{ width: 390, height: 844 }, { width: 1366, height: 900 }]) {
    const page = await browser.newPage({ viewport });
    const errors = []; let addressRequests = 0;
    page.on('pageerror', error => errors.push(error.message));
    await page.route('**/*', async route => {
      const url = route.request().url();
      if (url.includes('/api/geocode/') || /geocode\.arcgis|nominatim|bigdatacloud/.test(url)) { addressRequests++; return; }
      if (url.includes('openstreetmap.org')) return route.abort();
      if (url.endsWith('/qa-camera')) return route.fulfill({ contentType: 'text/html', body: '<html><body><div id="root"></div></body></html>' });
      return route.continue();
    });
    await page.goto('http://127.0.0.1:5198/qa-camera');
    console.log('QA camera page ready', viewport.width);
    const dimensions = await page.evaluate(async () => {
      const { optimizeCameraPhoto } = await import('/src/lib/camera-photo.ts');
      const canvas = document.createElement('canvas'); canvas.width = 6000; canvas.height = 8000;
      canvas.getContext('2d').fillRect(0, 0, canvas.width, canvas.height);
      const original = new File([await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', .9))], '48mp.jpg', { type: 'image/jpeg' });
      canvas.width = 0; canvas.height = 0;
      const result = await optimizeCameraPhoto(original);
      const image = await createImageBitmap(result.file);
      const size = { width: image.width, height: image.height, bytes: result.file.size }; image.close();
      window.qaPhoto = result.file;
      const native = window.createImageBitmap; window.createImageBitmap = undefined;
      try { const fallback = await optimizeCameraPhoto(original); if (!fallback.file.size) throw new Error('Empty fallback'); }
      finally { window.createImageBitmap = native; }
      const reactModule = await import('/node_modules/.vite/deps/react.js');
      const React = reactModule.default || reactModule;
      const domModule = await import('/node_modules/.vite/deps/react-dom_client.js');
      const createRoot = domModule.createRoot || domModule.default.createRoot;
      const { default: Form } = await import('/src/components/vite/EmployeeAuditForm.tsx');
      Object.defineProperty(navigator.geolocation, 'watchPosition', { value(callback) {
        return setInterval(() => callback({ timestamp: Date.now(), coords: { latitude: 18.5, longitude: -69.9, accuracy: 5 } }), 1200);
      } });
      Object.defineProperty(navigator.geolocation, 'clearWatch', { value(id) { clearInterval(id); } });
      const root = createRoot(document.getElementById('root'));
      root.render(React.createElement(Form, { agency: { id: 'qa', codigo: 'QA', terminal: 'Prueba' }, group: 'QA', onBack: () => root.unmount(), onComplete() {} }));
      return size;
    });
    assert.deepEqual([dimensions.width, dimensions.height], [1200, 1600]);
    for (const key of ['frontal', 'operativa', 'entorno']) {
      await page.locator(`input[data-photo="${key}"]`).waitFor();
      await page.evaluate(key => {
        const input = document.querySelector(`input[data-photo="${key}"]`);
        const transfer = new DataTransfer(); transfer.items.add(window.qaPhoto); input.files = transfer.files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
      }, key);
      await page.waitForFunction(() => !document.querySelector('.atlas-photo-loading'), { timeout: 20000 });
      assert.equal(await page.locator(`input[data-photo="${key}"]`).inputValue(), '');
    }
    assert.equal(addressRequests, 0, 'Photo capture must not depend on geocoding');
    await page.getByRole('button', { name: /Continuar a datos/ }).click();
    await page.getByRole('heading', { name: 'Datos que completa el empleado' }).waitFor({ timeout: 1500 });
    await page.getByLabel('Calle y dirección', { exact: true }).fill('Dirección manual conservada');
    await page.waitForFunction(() => document.querySelector('.address-refresh')?.textContent.includes('Actualizar dirección'), { timeout: 22000 });
    assert.equal(await page.getByLabel('Calle y dirección', { exact: true }).inputValue(), 'Dirección manual conservada');
    assert.equal(addressRequests, 4);
    assert.deepEqual(errors, []);
    console.log(JSON.stringify({ viewport, dimensions, photos: 3, stalledAddressProviders: addressRequests, errors }));
    await page.close();
  }
} finally { await browser.close(); await server.close(); }
