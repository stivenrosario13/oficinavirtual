import {readFileSync} from "node:fs";
import {describe,expect,it} from "vitest";

const read=(path:string)=>readFileSync(path,"utf8");
const migration=read("database-mssql/044_warehouse_department_users.sql");
const program=read("api/Program.cs");
const endpoints=read("api/WarehouseFulfillmentEndpoints.cs");
const database=read("api/DatabaseSqlServerScoped.cs");

describe("usuarios del departamento de Almacén",()=>{
  it("crea las seis cuentas como Almacén independiente",()=>{
    for(const user of ["bryan.arias","diorly.abreu","ana.rodriguez","fernando.ortiz","jordan.hellys","joan.matos"])
      expect(migration).toContain(`${user}@grupotejeda.local`);
    expect(migration).toContain("role='WAREHOUSE'");
    expect(migration).toContain("role='WAREHOUSE' AND support_team='WAREHOUSE'");
  });

  it("limita autorización y carga de inventario según el perfil",()=>{
    expect(migration).toContain('{"canAuthorizeWarehouseRequests":true,"canLoadWarehouseInventory":true}');
    expect(migration).toContain('{"canAuthorizeWarehouseRequests":false,"canLoadWarehouseInventory":false}');
    expect(endpoints).toContain('Permission(context,"canAuthorizeWarehouseRequests")');
    expect(endpoints).toContain('Permission(context,"canLoadWarehouseInventory")');
    expect(database).toContain("$.canLoadWarehouseInventory");
  });

  it("admite el rol Warehouse en la autenticación del portal",()=>{
    expect(program).toContain('"HumanResources", "Warehouse"');
    expect(database).toContain('"WAREHOUSE" => "Warehouse"');
  });
});
