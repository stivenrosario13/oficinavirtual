import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {describe,it,expect} from 'vitest';
import {allPermissionCatalog} from '../src/components/vite/UsersPermissionsWorkspace';
import UsersPermissionsWorkspace from '../src/components/vite/UsersPermissionsWorkspace';
import {accessProfiles,userAccessProfile,editablePermissionKeys} from '../src/lib/user-access-profiles';
import {userDepartments} from '../src/lib/user-departments';
import type {PortalSession} from '../src/lib/session';
describe('unified users workspace',()=>{
 it('shows every configurable permission once',()=>{
  expect(new Set(allPermissionCatalog.map(item=>item.key)).size).toBe(allPermissionCatalog.length);
  expect(new Set(allPermissionCatalog.map(item=>item.key))).toEqual(new Set(editablePermissionKeys));
 });
 for(const [department] of userDepartments)for(const [profile] of accessProfiles)it(`${department} / ${profile} persists a complete preset`,()=>{
  const preset=userAccessProfile(profile,department);
  expect(preset.permissions.usesExplicitPermissions).toBe(true);
  for(const key of editablePermissionKeys)expect(typeof preset.permissions[key]).toBe('boolean');
  const stored=JSON.parse(JSON.stringify(preset));expect(stored).toEqual(preset);
  if(profile==='SUPPORT'){expect(preset.permissions.canManageUsers).toBe(false);expect(preset.permissions.canRequestWarehouse).toBe(true);}
  if(profile==='TECHNICIAN')expect(preset.permissions.canAssignTickets).toBe(false);
 });
 it('denies the workspace without user-administration access',()=>{
  const html=renderToStaticMarkup(<UsersPermissionsWorkspace session={{id:'qa',permissions:{canManageUsers:false}} as PortalSession}/>);
  expect(html).toContain('No tienes permiso');expect(html).not.toContain('Nuevo usuario');
 });
});
