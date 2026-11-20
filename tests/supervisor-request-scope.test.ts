import {describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {supervisorCanRequestSupport,supervisorCreatedTicket} from '../src/lib/supervisor-support';

describe('supervisor support and warehouse requests',()=>{
  it('limits support creation to the three requested departments',()=>{
    for(const department of ['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES']) expect(supervisorCanRequestSupport(department)).toBe(true);
    for(const department of ['WAREHOUSE','ADMINISTRATIVE','ACCOUNTING','TREASURY','WORKSHOP','COMMUNICATION','']) expect(supervisorCanRequestSupport(department)).toBe(false);
  });
  it('recognizes creator independently of the resolving technician',()=>{
    expect(supervisorCreatedTicket({createdByUsername:' Supervisor.QA '},{email:'supervisor.qa'})).toBe(true);
    expect(supervisorCreatedTicket({createdByUsername:'otro'},{email:'supervisor.qa'})).toBe(false);
    expect(supervisorCreatedTicket({},{email:''})).toBe(false);
    expect(supervisorCreatedTicket({createdByUsername:null},{})).toBe(false);
    expect(supervisorCreatedTicket({isCreatedByCurrentUser:true,createdByUsername:'old.login'},{email:'new.login'})).toBe(true);
    expect(supervisorCreatedTicket({isCreatedByCurrentUser:false,createdByUsername:'same'},{email:'same'})).toBe(false);
  });
  it('uses assigned-only personal inbox and separates creation from the department home',()=>{
    const source=readFileSync('src/components/vite/TicketCenter.tsx','utf8');
    expect(source).toContain('const belongsToPersonalInbox = assignedToCurrentUser;');
    expect(source).toContain('supervisorCreateOpen || !!reportAgency');
    expect(source).toContain('(isTechnologyTechnician || session.role === "GroupAdministrator")');
    expect(source).toContain('"Casos del supervisor" : "Casos del técnico"');
    expect(source).toContain('strictTechnologyTechnician || openingSupervisorTicket ? "ALL" : sessionRoutingTeam');
    expect(source).toContain('setExpandedTicketId(target.id)');
  });
  it('enforces supervisor agency destinations server-side and keeps other roles unchanged',()=>{
    const server=readFileSync('api/DatabaseWarehouseFulfillment.cs','utf8');
    expect(server).toContain('actor.Role=="GroupAdministrator"&&(request.AgencyId==Guid.Empty||request.DestinationType is not (null or "AGENCY")');
    expect(server).toContain('WarehouseRequestAgencyScope(actor,ct)).ContainsKey(request.AgencyId)');
    const ui=readFileSync('src/components/vite/WarehouseRequestComposer.tsx','utf8');
    expect(ui).toContain("if(session.role==='GroupAdministrator')return [{value:'AGENCY',label:'Para una agencia'},{value:'MULTI_AGENCY',label:'Para varias agencias'}]");
    expect(ui).toContain("{value:'DEPARTMENT',label:'Para mi departamento'}");
  });
  it('protects support creation on the API and database entry point',()=>{
    const server=readFileSync('api/DatabaseSqlServerScoped.cs','utf8');
    expect(server).toContain('if(role=="GroupAdministrator"&&body.AssignedDepartment is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES"))return null;');
  });
});
