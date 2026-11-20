import {expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {userDepartments,departmentAssignment,assignedUserDepartment} from '../src/lib/user-departments';
import {warehouseRequestDepartments} from '../src/lib/warehouse-request-departments';
it('incluye todos los departamentos actuales, sin duplicados',()=>{
 expect(new Set(userDepartments.map(([,name])=>name))).toEqual(new Set(warehouseRequestDepartments));
 expect(new Set(userDepartments.map(([code])=>code)).size).toBe(userDepartments.length);
});
it.each(userDepartments)('conserva la asignación %s al guardarla y volver a leerla',code=>{
 const assignment=departmentAssignment(code);
 expect(assignedUserDepartment(assignment)).toBe(code);
 expect(assignment.role).not.toBe('ADMINISTRATOR');
});
it('el servidor conserva DEPARTMENT al crear y actualizar, no solo en el formulario',()=>{
 const source=readFileSync('api/DatabaseSqlServerScoped.cs','utf8');
 const parameters=source.split('\n').filter(line=>line.includes('command.Parameters.AddWithValue("@supportTeam",Db((body.Role=='));
 expect(parameters).toHaveLength(2);
 for(const parameter of parameters){
  expect(parameter).toContain('body.Role=="DEPARTMENT"');
  expect(parameter).toContain('!string.IsNullOrWhiteSpace(body.SupportTeam)?body.SupportTeam:null');
 }
});
it('la biblioteca independiente reutiliza el contenedor del tema existente',()=>{
 const source=readFileSync('src/components/vite/AdministrativeWorkspace.tsx','utf8');
 expect(source).toContain('administrative-workspace administrative-portal department-document-workspace');
});
