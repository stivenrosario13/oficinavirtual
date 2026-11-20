import base from '../vite.config';
import {defineConfig} from 'vite';
export default defineConfig({...base,server:{...base.server,host:'127.0.0.1',port:5174,watch:{ignored:['**/api/bin/**','**/api/obj/**','**/artifacts/**','**/outputs/**','**/tests/**','**/.git/**']}}});
