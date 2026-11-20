import {describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';

describe('recepción virtual de contratos de agencias',()=>{
  const store=readFileSync('api/InstitutionalDocumentStore.cs','utf8');
  const endpoint=readFileSync('api/InstitutionalDocumentEndpoints.cs','utf8');
  const departments=readFileSync('api/DepartmentAccounts.cs','utf8');
  const requests=readFileSync('src/components/vite/AdministrativeRequests.tsx','utf8');
  const routing=readFileSync('src/lib/administrative-requests.ts','utf8');

  it('identifica supervisión como origen separado y conserva acceso solo del propietario',()=>{
    expect(departments).toContain('"GROUPADMINISTRATOR" or "GROUP_ADMIN"=>"Supervisión"');
    expect(store).toContain('a.Department=="Administración"||SameUser(OwnerUserName,a.Id)');
    expect(routing).toContain("SUPERVISION: 'Supervisión'");
    expect(routing).toContain("row.isOwner");
  });

  it('limita a supervisores a contratos e informes de incidentes',()=>{
    expect(endpoint).toContain('a.Role=="GroupAdministrator"&&administrativeRequest.FormType is not ("CONTRACT_REQUEST" or "CONTRACT_RENEWAL" or "INCIDENT_REPORT")');
    expect(endpoint).toContain('supervisorVirtualReception');
  });
});
