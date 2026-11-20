import {defineConfig} from 'vite';import react from '@vitejs/plugin-react';import {fileURLToPath} from 'node:url';
export default defineConfig({plugins:[react()],resolve:{alias:{'@':fileURLToPath(new URL('../src',import.meta.url))}},build:{outDir:'tmp/inventory-agencies-preview/warehouse-auth-v446',rollupOptions:{input:'scripts/qa-warehouse-authorization.html'}}});
