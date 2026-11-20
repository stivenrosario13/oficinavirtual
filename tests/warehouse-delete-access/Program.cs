var passed=0;
void Check(bool value){if(!value)throw new Exception("Fallo de autorización");passed++;}
foreach(var login in new[]{"diorly.abreu@grupotejeda.local","bryan.arias@grupotejeda.local"}){
 Check(WarehouseInventoryDeleteAccess.Allowed("Warehouse","WAREHOUSE",login));
 Check(WarehouseInventoryDeleteAccess.Allowed("Warehouse","",login.ToUpperInvariant()));
 Check(!WarehouseInventoryDeleteAccess.Allowed("Administrator","",login));
 Check(!WarehouseInventoryDeleteAccess.Allowed("Technology","TECHNICIANS",login));
}
foreach(var login in new[]{"admin@local.com","ana.rodriguez@grupotejeda.local","bryan.arias@otro.local","diorly.abreu.falso@grupotejeda.local",""})Check(!WarehouseInventoryDeleteAccess.Allowed("Warehouse","WAREHOUSE",login));
Console.WriteLine($"{passed} pruebas de acceso aprobadas. Sin conexión a bases de datos.");
