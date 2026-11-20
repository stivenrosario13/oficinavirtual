import {readFileSync} from 'node:fs';
import {describe,expect,it} from 'vitest';
import {allPermissionCatalog} from '../src/lib/permission-catalog';
import {userAccessProfile} from '../src/lib/user-access-profiles';

describe('permiso para crear artículos de Almacén',()=>{
  it('aparece una sola vez en el catálogo completo de permisos',()=>{
    const matches=allPermissionCatalog.filter(item=>item.key==='canCreateWarehouseArticles');
    expect(matches).toHaveLength(1);
    expect(matches[0].section).toBe('Almacén');
    const workspace=readFileSync('src/components/vite/UsersPermissionsWorkspace.tsx','utf8');
    expect(workspace).not.toContain("profile==='SUPPORT'||profile==='WAREHOUSE_REQUESTER'");
  });

  it('queda restringido por defecto y disponible para asignarlo a un gerente de Almacén',()=>{
    expect(userAccessProfile('SUPPORT','WAREHOUSE').permissions.canCreateWarehouseArticles).toBe(false);
    expect(userAccessProfile('DEPARTMENT_MANAGER','WAREHOUSE').permissions.canCreateWarehouseArticles).toBe(true);
  });

  it('protege el alta en el servidor y restaura el permiso solicitado',()=>{
    const database=readFileSync('api/DatabaseSqlServerScoped.cs','utf8');
    const grants=readFileSync('api/WarehouseArticleCreationAccess.cs','utf8');
    expect(database).toContain('$.canCreateWarehouseArticles');
    expect(database).toContain('no crear artículos nuevos');
    for(const login of ['diorly.abreu','ana.rodriguez'])expect(grants).toContain(login);
    expect(grants).toContain('$.canLoadWarehouseInventory');
    expect(grants).toContain('$.canCreateWarehouseArticles');
  });
});
