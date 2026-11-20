import {readFileSync} from "node:fs";
import {describe,expect,it} from "vitest";

describe("árbol moderno de archivos administrativos",()=>{
  const component=readFileSync("src/components/vite/AdministrativeWorkspace.tsx","utf8");
  const styles=readFileSync("src/components/vite/administrativeWorkspace.css","utf8");

  it("mantiene las carpetas exclusivamente en el árbol lateral",()=>{
    expect(component).toContain('function FolderTreeNode');
    expect(component).toContain('role="treeitem"');
    expect(component).toContain('className="adw-tree-toggle"');
    expect(component).not.toContain("view==='browse'&&!query&&directFolders.length");
    expect(component).not.toContain('adw-folder-grid adw-shared-cards');
  });

  it("marca la ruta y diferencia visualmente cada tipo de archivo",()=>{
    expect(component).toContain('export function administrativeFileVisual');
    for(const kind of ['pdf','excel','word','image','text','other'])expect(component).toContain(`kind:'${kind}'`);
    expect(component).toContain('className={`adw-file file-${visual.kind}`}');
    expect(styles).toContain('.administrative-workspace .adw-breadcrumb span:last-child button');
    expect(styles).toContain('.administrative-workspace .adw-file.file-excel');
    expect(styles).toContain('.administrative-workspace .adw-file.file-word');
  });

  it("evita que el estado seleccionado global pinte un bloque blanco",()=>{
    expect(component).toContain("' is-selected'");
    expect(component).not.toContain("' selected'");
    expect(styles).toContain('.adw-tree-node.is-selected{display:block!important');
    expect(styles).toContain('background:transparent!important');
  });
});
