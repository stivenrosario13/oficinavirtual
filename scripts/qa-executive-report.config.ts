import {defineConfig} from 'vite';
import react from '@vitejs/plugin-react';
import {fileURLToPath} from 'node:url';
export default defineConfig({plugins:[react()],resolve:{alias:{'@':fileURLToPath(new URL('../src',import.meta.url))}},build:{outDir:'tmp/executive-report',rollupOptions:{input:'scripts/qa-executive-report.html'}},preview:{host:'127.0.0.1',port:5220,strictPort:true}});
