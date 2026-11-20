import {describe,it,expect} from 'vitest';
import {permissionsForUserSave} from '../src/lib/user-permission-edit';
describe('guardar usuarios sin cambiar accesos involuntariamente',()=>{
 it('conserva los permisos almacenados al editar solamente el nombre',()=>{
  const saved={canResolveTickets:false,customFlag:true};
  const effective={...saved,canResolveTickets:true,canView:true};
  expect(permissionsForUserSave(saved,effective,{...effective})).toEqual(saved);
 });
 it('aplica solo la casilla cambiada conservando los valores personalizados',()=>{
  const saved={canResolveTickets:false,customFlag:true};
  const effective={...saved,canResolveTickets:true,canView:true,canAssignTickets:true};
  expect(permissionsForUserSave(saved,effective,{...effective,canAssignTickets:false})).toEqual({...effective,...saved,canAssignTickets:false,usesExplicitPermissions:true});
 });
 it('restablece permisos únicamente cuando se selecciona un perfil',()=>{
  expect(permissionsForUserSave({oldFlag:true},{canAssignTickets:true},{canAssignTickets:false},true)).toEqual({canAssignTickets:false,usesExplicitPermissions:true});
 });
});
