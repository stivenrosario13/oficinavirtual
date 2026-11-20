import {expect,it} from 'vitest';
import {requestDepartmentChoices} from '../src/lib/warehouse-request-departments';
it('includes all requested department filters without losing historical names',()=>{
 const items=[{department:'Departamento histórico'},{department:'Almacén'},{department:''}];
 const result=requestDepartmentChoices(items);
 for(const name of ['Administración','Almacén','Taller','Comunicación','Comercialización','Tecnología','Servicios Generales','Recursos Humanos','Departamento histórico'])expect(result).toContain(name);
 expect(result.filter(name=>name==='Almacén')).toHaveLength(1);
 expect(result).not.toContain('');
 expect(items).toHaveLength(3);
});
