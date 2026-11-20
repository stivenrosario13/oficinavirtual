import {readFileSync} from "node:fs";
import {describe,expect,it} from "vitest";

describe("explorador compacto con menú contextual",()=>{
  const component=readFileSync("src/components/vite/AdministrativeWorkspace.tsx","utf8");
  const styles=readFileSync("src/components/vite/administrativeWorkspace.css","utf8");
  const store=readFileSync("api/InstitutionalDocumentStore.cs","utf8");
  const endpoints=readFileSync("api/InstitutionalDocumentEndpoints.cs","utf8");
  const webConfig=readFileSync("api/web.config","utf8");

  it("muestra archivos compactos y abre con doble clic",()=>{
    expect(component).toContain("onDoubleClick={()=>void openFile(file)");
    expect(component).toContain("Doble clic para abrir · clic derecho para ver las opciones");
    expect(component).not.toContain('<div className="adw-file-actions">');
    expect(styles).toContain("grid-template-columns:repeat(auto-fill,minmax(118px,150px))");
    expect(styles).toContain("min-height:118px");
  });

  it("concentra todas las acciones de archivos y carpetas en el clic derecho",()=>{
    for(const action of ["Abrir","Descargar","Mover a carpeta","Renombrar","Enviar a papelera","Eliminar definitivamente"])expect(component).toContain(action);
    for(const action of ["Renombrar carpeta","Mover carpeta","Administrar acceso","Eliminar carpeta vacía"])expect(component).toContain(action);
    expect(component).toContain('Abrir con…');
    expect(component).not.toContain("<div className=\"adw-folder-actions\"");
  });

  it("descarga Excel normalmente y mantiene Abrir con para los demás formatos",()=>{
    expect(component).toContain("scheme:'ms-excel'");
    expect(component).toContain("scheme:'ms-word'");
    expect(component).toContain("scheme:'ms-powerpoint'");
    expect(component).toContain("app.name==='Excel'){downloadForWindows(file)");
    expect(component).toContain(":ofv|u|");
    expect(component).toContain("el navegador no puede leer la lista completa de aplicaciones instaladas");
    expect(component).toContain("Descargar para elegir otra aplicación");
    expect(component).not.toContain("Habilitar edición en este equipo");
    expect(component).not.toContain("verifyEditableOfficeLink");
  });
  it('muestra inmediatamente los archivos confirmados y limpia filtros que los oculten',()=>{
    expect(component).toContain("setView('browse')");
    expect(component).toContain("setQuery('')");
    expect(component).toContain("setTypeFilter('all')");
    expect(component).toContain('...created,...current.filter');
    expect(component).toContain('cargado y visible');
  });
  it('permite reemplazar con una versión editada a quien tiene acceso',()=>{
    expect(store).toContain('CanEdit=!Trashed&&(Area=="files"');
    expect(store).toContain('d.Area!="files"||d.Trashed||!d.CanEdit');
    expect(store).toContain('Necesitas permiso de edición para reemplazar este archivo.');
    expect(component).toContain("Actualizar con versión editada");
    expect(endpoints).toContain('var editable=document.IsOwner||document.CanEdit||document.CanManageFiles');
  });
  it('expone la carpeta WebDAV y conserva el bloqueo al guardar desde Excel',()=>{
    expect(endpoints).toContain('app.MapMethods("/office-files/{token}",["OPTIONS","PROPFIND"]');
    expect(endpoints).toContain('response.Headers["MS-Author-Via"]="DAV"');
    expect(endpoints).toContain('response.Headers["DAV"]="1, 2"');
    expect(endpoints).toContain('request.Headers["If"]');
    expect(endpoints).toContain('context.Response.Headers.ETag');
    expect(webConfig).toContain('<remove name="WebDAVModule" />');
    expect(webConfig).toContain('<remove name="WebDAV" />');
    expect(webConfig).toContain('verb="*" modules="AspNetCoreModuleV2"');
  });
});
