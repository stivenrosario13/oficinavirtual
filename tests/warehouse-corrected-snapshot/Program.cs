
using System.Reflection;
using Microsoft.Data.SqlClient;
using System.Text.Json;
var name="Codex_Inventory_QA_"+Guid.NewGuid().ToString("N");
const string master="Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True";
await using var root=new SqlConnection(master);await root.OpenAsync();
async Task Exec(SqlConnection c,string sql){await using var command=new SqlCommand(sql,c);await command.ExecuteNonQueryAsync();}
await Exec(root,$"CREATE DATABASE [{name}]");int checks=0;
void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
try{
 var cs=new SqlConnectionStringBuilder(master){InitialCatalog=name}.ConnectionString;
 await using var c=new SqlConnection(cs);await c.OpenAsync();
 await Exec(c,"""
 CREATE TABLE warehouse_products(product_id nvarchar(40) PRIMARY KEY,description nvarchar(240),is_product bit,warehouse nvarchar(80),official_stock int,unit_id nvarchar(40),division nvarchar(120),product_group nvarchar(120),last_purchase_date date,tracks_serial bit,catalog_version nvarchar(40),imported_at datetime2(3),is_active bit);
 INSERT warehouse_products VALUES('KEEP','Anterior',1,'PRINCIPAL',99,'UND','','',NULL,0,'old',DATEADD(day,-1,SYSUTCDATETIME()),1),('RETIRED','No incluido',1,'PRINCIPAL',50,'UND','','',NULL,0,'old',SYSUTCDATETIME(),1);
 CREATE TABLE maintenance_movements(id int,quantity int);INSERT maintenance_movements VALUES(1,5);
 CREATE TABLE warehouse_equipment_allocations(id int,status varchar(30));INSERT warehouse_equipment_allocations VALUES(1,'PENDING_PICKUP');
 CREATE TABLE institutional_files(id int,metadata nvarchar(max));INSERT institutional_files VALUES(1,'{"request":"preserve"}');
 """);
 var assembly=Assembly.LoadFrom(Path.GetFullPath("api/bin/Debug/net8.0/"+System.Xml.Linq.XDocument.Load("api/RegistroAgencias.Api.csproj").Descendants("AssemblyName").Single().Value+".dll"));
 var method=assembly.GetType("Database",true)!.GetMethod("ApplyWarehouseCatalogSnapshot",BindingFlags.NonPublic|BindingFlags.Static)!;
 var record=assembly.GetType("WarehouseCatalogImport")!;
 var json="""{"version":"QA-corrected","sourceFile":"QA.xlsx","products":[{"idProducto":"KEEP","descripcion":"Corregido","producto":true,"almacen":"PRINCIPAL","existencia":7,"idUnidad":"UND","division":"","grupo":"","seriales":false},{"idProducto":"NEW","descripcion":"Nuevo","producto":true,"almacen":"PRINCIPAL","existencia":0,"idUnidad":"UND","division":"","grupo":"","seriales":false}]}""";
 var catalog=JsonSerializer.Deserialize(json,record,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
 async Task Apply(object data){await using var connection=new SqlConnection(cs);await connection.OpenAsync();await (Task)method.Invoke(null,[connection,data,CancellationToken.None])!;}
 async Task<int> Number(string sql){await using var cmd=new SqlCommand(sql,c);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
 await Task.WhenAll(Apply(catalog),Apply(catalog));
 Check(await Number("SELECT COUNT(*) FROM warehouse_catalog_snapshots")==1,"Concurrent starts apply one snapshot only");
 Check(await Number("SELECT official_stock FROM warehouse_products WHERE product_id='KEEP'")==7,"Existing product receives corrected stock without changing ID");
 Check(await Number("SELECT COUNT(*) FROM warehouse_products WHERE is_active=1")==2,"Only corrected catalog remains active, including zero stock");
 Check(await Number("SELECT COUNT(*) FROM warehouse_products WHERE product_id='RETIRED' AND is_active=0")==1,"Retired product retained for history");
 Check(await Number("SELECT COUNT(*) FROM warehouse_catalog_snapshots WHERE previous_catalog LIKE '%Anterior%' AND applied_catalog LIKE '%Corregido%'")==1,"Before and after catalogs retained for recovery");
 Check(await Number("SELECT COUNT(*) FROM maintenance_movements")==1&&await Number("SELECT COUNT(*) FROM warehouse_equipment_allocations WHERE status='PENDING_PICKUP'")==1&&await Number("SELECT COUNT(*) FROM institutional_files")==1,"Movements, pending allocations and requests preserved");
 await Exec(c,"UPDATE warehouse_products SET official_stock=6 WHERE product_id='KEEP'");await Apply(catalog);
 Check(await Number("SELECT official_stock FROM warehouse_products WHERE product_id='KEEP'")==6,"Restart does not reset later stock changes");
 var invalid=JsonSerializer.Deserialize(json.Replace("QA-corrected","QA-invalid").Replace("\"NEW\"","\"KEEP\""),record,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
 var rejected=false;try{await Apply(invalid);}catch(InvalidOperationException){rejected=true;}Check(rejected&&await Number("SELECT COUNT(*) FROM warehouse_catalog_snapshots")==1,"Duplicate IDs rejected without partial inventory change");
 if(File.Exists("api/Data/warehouse-products-2026-10-27.json")){
  var real=JsonSerializer.Deserialize(File.ReadAllText("api/Data/warehouse-products-2026-10-27.json"),record,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;await Apply(real);
  Check(await Number("SELECT COUNT(*) FROM warehouse_products WHERE is_active=1")==134,"All 134 corrected Excel products apply to real SQL");
 }
 Console.WriteLine($"{checks} checks passed");
}finally{SqlConnection.ClearAllPools();await Exec(root,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");}
