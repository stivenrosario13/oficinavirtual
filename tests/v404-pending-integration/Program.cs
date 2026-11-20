using Microsoft.Data.SqlClient;

var databaseName="Codex_V404_Pending_QA_"+Guid.NewGuid().ToString("N");
const string masterConnection="Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5";
await using var master=new SqlConnection(masterConnection);
await master.OpenAsync();
var created=false;
try
{
    await Execute(master,$"CREATE DATABASE [{databaseName}]");
    created=true;
    Database.Connection=$"Server=localhost;Database={databaseName};Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5";
    await using var connection=new SqlConnection(Database.Connection);
    await connection.OpenAsync();
    await Execute(connection,"""
      CREATE TABLE maintenance_movements(id uniqueidentifier NOT NULL PRIMARY KEY,operational_area varchar(20),movement_type varchar(40));
      INSERT INTO maintenance_movements VALUES(NEWID(),'WAREHOUSE','REQUEST'),(NEWID(),'WAREHOUSE','ENTRY'),(NEWID(),'WORKSHOP','REPAIR');
      """);
    var ticketId=Guid.NewGuid();
    var request=new WarehouseRequestInfo("SOL-ALM-V404-QA",Guid.NewGuid(),"6010183","Agencia QA","Grupo QA","","","MEDIUM",[new("EQUIPMENT","Inversor",1,null,null,"QA-1")],ticketId,1515);
    var db=new Database();
    var documentId=await db.InstitutionalUpload(new InstitutionalFile{Area="communications",Channel="requirements",Title="Solicitud vinculada al ticket #1515",Department="Servicios Generales",OwnerUserName="support.sg",OwnerDisplayName="Soporte SG",RecipientDepartment="Almacén",Status="active",WarehouseRequest=request,OriginalFileName="SOL-ALM-V404-QA.pdf",ContentType="application/pdf"},"%PDF-QA"u8.ToArray(),CancellationToken.None);
    var warehouse=new DocumentActor("warehouse","Warehouse","WAREHOUSE","Encargada","almacen.qa");
    var inbox=await db.InstitutionalList(warehouse,"communications","requirements",CancellationToken.None);
    var saved=inbox.Single(item=>item.Id==documentId);
    if(saved.Status!="pending"||saved.ReceivedUtc is not null||saved.SentUtc is null||saved.WarehouseRequest?.TicketNumber!=1515)throw new Exception("La solicitud vinculada no quedó en Pendientes de recibir.");
    await using(var count=new SqlCommand("SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE' AND movement_type='REQUEST'",connection))if(Convert.ToInt32(await count.ExecuteScalarAsync())!=0)throw new Exception("La limpieza inicial no eliminó las solicitudes antiguas de Almacén.");
    await using(var count=new SqlCommand("SELECT COUNT(*) FROM maintenance_movements WHERE NOT(operational_area='WAREHOUSE' AND movement_type='REQUEST')",connection))if(Convert.ToInt32(await count.ExecuteScalarAsync())!=2)throw new Exception("La limpieza alteró inventario o Taller.");
    Console.WriteLine("PASS Ticket #1515 registrado en Pendientes de recibir");
    Console.WriteLine("PASS Limpieza limitada a solicitudes antiguas de Almacén");
}
finally
{
    if(created)
    {
        SqlConnection.ClearAllPools();
        await Execute(master,$"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]");
    }
}

static async Task Execute(SqlConnection connection,string sql){await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync();}

partial class Database
{
    public static string Connection="";
    static async Task<SqlConnection> Open(CancellationToken ct){var connection=new SqlConnection(Connection);await connection.OpenAsync(ct);return connection;}
    static Task<InstitutionalFile> WarehouseRecoverLegacyRequest(SqlConnection connection,InstitutionalFile document,string json,CancellationToken ct)=>Task.FromResult(document);
}
