import {defineConfig} from 'vite';
import react from '@vitejs/plugin-react';
import {fileURLToPath} from 'node:url';
export default defineConfig({plugins:[react()],resolve:{alias:{'@':fileURLToPath(new URL('../src',import.meta.url))}},optimizeDeps:{entries:['scripts/qa-internet-ticket-request.html']},server:{host:'127.0.0.1',port:5189,strictPort:true,watch:null}});
