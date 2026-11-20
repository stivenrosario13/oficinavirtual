import {describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';

describe('marcadores sincronizados de publicación',()=>{
 it('sincroniza el identificador web y el ejecutable V524',()=>{
  const html=readFileSync('index.html','utf8'),guard=readFileSync('public/release-guard.js','utf8'),api=readFileSync('api/Program.cs','utf8'),config=readFileSync('api/web.config','utf8'),version=JSON.parse(readFileSync('public/version-web.json','utf8'));
  expect(html).toContain('content="V524-WEB-20261116-1740"');
  expect(html).toContain('release-guard.js?v=V524-WEB-20261116-1740');
  expect(guard).toContain('meta[name="app-release"]');
  expect(api).toContain('X-Application-Release"] = "V524-WEB-20261116-1740"');
  expect(config).toContain('RegistroAgencias.SqlServer.V524.exe');
  expect(version.release).toBe('V524-WEB-20261116-1740');
  expect(version.cacheGuard).toBe('synchronized-web-release-v524-20261116-1740');
 });
});
