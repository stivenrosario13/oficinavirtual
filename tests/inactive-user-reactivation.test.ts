import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

describe('reactivación de usuarios administrativos', () => {
  it('busca la cuenta aunque esté inactiva antes de actualizarla', () => {
    const program = readFileSync('api/Program.cs', 'utf8');
    const updateEndpoint = program.slice(
      program.indexOf('MapPut("/api/admin/users/{id:guid}"'),
      program.indexOf('MapDelete("/api/admin/users/{id:guid}"'),
    );

    expect(updateEndpoint).toContain('db.AdminUsers(ct)');
    expect(updateEndpoint).not.toContain('db.Account(id, ct)');
  });
});
