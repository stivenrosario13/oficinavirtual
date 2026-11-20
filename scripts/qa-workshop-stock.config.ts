import {defineConfig} from 'vite';import react from '@vitejs/plugin-react';import {fileURLToPath} from 'node:url';
export default defineConfig({plugins:[react()],resolve:{alias:{'@':fileURLToPath(new URL('../src',import.meta.url))}},build:{outDir:'tmp/workshop-stock-preview',rollupOptions:{input:'scripts/qa-workshop-v418.html'}},preview:{host:'127.0.0.1',port:5179,strictPort:true}});
