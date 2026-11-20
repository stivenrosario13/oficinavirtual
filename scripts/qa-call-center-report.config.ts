import {defineConfig} from 'vite';import react from '@vitejs/plugin-react';import {fileURLToPath} from 'node:url';
export default defineConfig({plugins:[react()],resolve:{alias:{'@':fileURLToPath(new URL('../src',import.meta.url))}},build:{outDir:'tmp/inventory-agencies-preview/cc-report-v446',rollupOptions:{input:'scripts/qa-call-center-report.html'}}});
