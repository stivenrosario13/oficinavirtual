import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';

const css=readFileSync('src/components/vite/administrativeFormFrame.css','utf8');
it('reserva el mismo panel para catálogo, formularios e internet',()=>{
  expect(css).toContain('.adw-dialog.admin-request-form{');
  expect(css).toContain('width:min(1180px,calc(100vw - 36px))');
  expect(css).toContain('height:min(780px,calc(100dvh - 36px))');
  expect(css).toContain('overflow:auto');
  expect(css).toContain('overscroll-behavior:contain');
  for(const file of ['AdministrativeRequestComposer.tsx','AdministrativeInternetRequestComposer.tsx']){
    expect(readFileSync(`src/components/vite/${file}`,'utf8')).toContain('admin-request-form');
  }
});
it('mantiene las acciones visibles y adapta el panel a móviles sin cambiar Almacén',()=>{
  expect(css).toContain('position:sticky');
  expect(css).toContain('>header{top:0');
  expect(css).toContain('>footer{bottom:0');
  expect(css).toContain('@media(max-width:650px)');
  expect(css).toContain('height:calc(100dvh - 8px)');
  expect(css).not.toContain('.warehouse-request-composer');
});
it('impide controles blancos cuando el formulario se abre desde cualquier ruta',()=>{
  expect(css).toContain('.admin-request-form.admin-special-form.admin-contract-form input:not([type="checkbox"])');
  expect(css).toContain('background-color:#071b2f!important');
  expect(css).toContain('-webkit-text-fill-color:#fff!important');
  expect(css).toContain('color-scheme:dark!important');
  expect(css).toContain('input:-webkit-autofill');
});
