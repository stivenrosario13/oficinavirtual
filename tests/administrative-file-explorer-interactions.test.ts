import {readFileSync} from "node:fs";
import {describe,expect,it} from "vitest";

describe("interacciones completas del explorador",()=>{
  const component=readFileSync("src/components/vite/AdministrativeWorkspace.tsx","utf8");
  const styles=readFileSync("src/components/vite/administrativeWorkspace.css","utf8");

  it("permite arrastrar archivos a las carpetas del árbol",()=>{
    expect(component).toContain("draggable={view!=='trash'");
    expect(component).toContain("onDropFile(id,folder.id)");
    expect(component).toContain("classList.add('drag-target')");
    expect(styles).toContain("adw-tree-folder.drag-target");
  });

  it("separa las carpetas compartidas y muestra archivos al abrir una",()=>{
    expect(component).toContain('MIS CARPETAS');
    expect(component).toContain('COMPARTIDAS');
    expect(component).toContain("setView('shared')");
    expect(component).toContain("view==='shared'?!!folderId&&file.folderId===folderId");
    expect(component).toContain('Selecciona una carpeta compartida');
  });

  it("habilita acciones de ubicación, menú de fondo y búsqueda global superior",()=>{
    expect(component).toContain("canCreateHere=(view==='browse'&&canWriteHere)||(view==='shared'&&!!folderId&&canWriteHere)");
    expect(component).toContain('adw-context-menu adw-background-menu');
    expect(component).toContain('Buscar en todas las carpetas y archivos');
    expect(component.indexOf('adw-controls adw-global-search')).toBeLessThan(component.indexOf('<div className="adw-summary">'));
    expect(styles).toContain('adw-tree button.adw-tree-folder.active');
    expect(styles).toContain('background:linear-gradient(90deg,#18506b,#173b55)!important');
  });
});
