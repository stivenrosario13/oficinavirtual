using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

var name="Codex_Storage_QA_"+Guid.NewGuid().ToString("N");
var remote=new Dictionary<string,byte[]>();int uploads=0,checks=0;bool corruptDownload=false;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
var api=new HttpClient(new FakeHandler(async request=>{
    var path=request.RequestUri!.AbsolutePath;
    if(path=="/v1/whoami")return Json(new{tenant="test"});
    if(path=="/v1/usage")return Json(new{files=new{processing=0},limits=new{max_pending=100}});
    if(path=="/v1/files"){
        var part=((MultipartFormDataContent)request.Content!).Single(p=>p.Headers.ContentDisposition!.Name!.Trim('"')=="file");
        var bytes=await part.ReadAsByteArrayAsync();var id=StorageFiles.Hash(bytes);remote[id]=bytes;uploads++;
        return Json(new{id,status="ready"});
    }
    return Json(new{url="https://files.gptcomer.com/p/"+path.Split('/')[3]});
}));
var download=new HttpClient(new FakeHandler(request=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(corruptDownload?"corrupt"u8.ToArray():remote[request.RequestUri!.Segments.Last()])})));
StorageFiles.Configure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"STORAGE_ENABLED","true"}}).Build(),new StorageService("test-only",api,download,(_,_)=>Task.CompletedTask));
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True");await master.OpenAsync();
await Exec(master,$"CREATE DATABASE [{name}]");
try
{
    await using var c=new SqlConnection($"Server=localhost;Database={name};Integrated Security=True;TrustServerCertificate=True");await c.OpenAsync();
    await Exec(c,"""
        CREATE TABLE audit_photos(id int PRIMARY KEY,photo_data varbinary(max));
        CREATE TABLE institutional_files(id bigint PRIMARY KEY,file_data varbinary(max),metadata nvarchar(max));
        CREATE TABLE institutional_editable_templates(id bigint PRIMARY KEY,content nvarchar(max));
        """);
    var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=");
    var pdf=Encoding.UTF8.GetBytes("%PDF-1.7\ntest");
    var signature="data:image/png;base64,"+Convert.ToBase64String(png);
    await using(var seed=new SqlCommand("INSERT audit_photos VALUES(1,@png);INSERT institutional_files VALUES(1,@pdf,@json),(2,0x504B0304,N'{}');INSERT institutional_editable_templates VALUES(1,@json)",c))
    {seed.Parameters.AddWithValue("@png",png);seed.Parameters.AddWithValue("@pdf",pdf);seed.Parameters.AddWithValue("@json",System.Text.Json.JsonSerializer.Serialize(new{signature}));await seed.ExecuteNonQueryAsync();}
    var inventory=await StorageMigrationRunner.Run(c,false,default);
    Check(inventory.Candidates==4&&uploads==0,"Inventario sin subidas");
    Check(Convert.ToInt32(await Scalar(c,"SELECT COUNT(*) FROM sys.tables WHERE name='storage_migration_files'"))==0,"Inventario sin cambios de esquema");
    var result=await StorageMigrationRunner.Run(c,true,default);
    Check(result.Copied==4&&result.Failed==0,"Copia binarios, firmas y plantillas");
    Check(((byte[])await Scalar(c,"SELECT photo_data FROM audit_photos WHERE id=1")).SequenceEqual(png),"Foto original intacta");
    Check(((byte[])await Scalar(c,"SELECT file_data FROM institutional_files WHERE id=1")).SequenceEqual(pdf),"PDF original intacto");
    Check(((string)await Scalar(c,"SELECT metadata FROM institutional_files WHERE id=1")).Contains("base64"),"Firma original intacta");
    Check(Convert.ToInt32(await Scalar(c,"SELECT COUNT(*) FROM dbo.storage_migration_files"))==4,"Referencias verificadas registradas");
    var previous=uploads;
    var again=await StorageMigrationRunner.Run(c,true,default);
    Check(again.Existing==4&&again.Copied==0&&uploads==previous,"Reanudar no duplica cargas");
    await Exec(c,"UPDATE institutional_files SET file_data=0x255044462D312E370A6E6577 WHERE id=1");
    var changed=await StorageMigrationRunner.Run(c,true,default);
    Check(changed.Copied==1&&changed.Existing==3,"Detecta cambios del original sin perder la copia anterior");
    Check(Convert.ToInt32(await Scalar(c,"SELECT COUNT(*) FROM dbo.storage_migration_files"))==5,"Historial de versiones preservado");
    var queueClock=DateTime.UtcNow;
    var queuePath=Path.GetFullPath(Path.Combine("outputs","migration-queue-tests-"+Guid.NewGuid().ToString("N")));
    var queueConfig=new ConfigurationBuilder().Build();
    var queue=new StorageUploadQueue(queuePath,queueConfig,()=>queueClock); StorageFiles.Queue=queue;
    var newPng=png.Concat(new byte[4096]).ToArray(); var newPdf=Encoding.UTF8.GetBytes("%PDF-1.7\nqueued legacy document"+new string(' ',4096));
    await using(var seedQueue=new SqlCommand("INSERT audit_photos VALUES(2,@png);INSERT institutional_files VALUES(3,@pdf,@json)",c))
    {seedQueue.Parameters.AddWithValue("@png",newPng);seedQueue.Parameters.AddWithValue("@pdf",newPdf);seedQueue.Parameters.AddWithValue("@json",System.Text.Json.JsonSerializer.Serialize(new{signature="data:image/png;base64,"+Convert.ToBase64String(newPng)}));await seedQueue.ExecuteNonQueryAsync();}
    previous=uploads;
    var queued=await StorageMigrationRunner.Run(c,true,default);
    Check(queued.Queued==3&&queued.Copied==0&&uploads==previous,"Encola foto, PDF y firma sin subir ni declarar copias completas");
    Check(Convert.ToInt32(await Scalar(c,"SELECT COUNT(*) FROM dbo.storage_migration_files"))==5&&Convert.ToInt32(await Scalar(c,"SELECT COUNT(*) FROM dbo.storage_migration_pending"))==3,"Pendientes separados del diario de copias verificadas");
    var totals=await StorageMigrationRunner.FinalizePending(c,default);
    Check(totals.Pending==3&&totals.Verified==5,"Finalizador no da por completados los archivos pendientes");
    queue=new StorageUploadQueue(queuePath,queueConfig,()=>queueClock);StorageFiles.Queue=queue;
    for(int i=0;i<10&&queue.Status().Pending>0;i++){await queue.ProcessOne(default);queueClock=queueClock.AddSeconds(3);}
    totals=await StorageMigrationRunner.FinalizePending(c,default);
    Check(totals.Pending==0&&totals.Verified==8,"Tras reiniciar y verificar, consolida binarios y firmas JSON en SQL");
    Check(((byte[])await Scalar(c,"SELECT photo_data FROM audit_photos WHERE id=2")).SequenceEqual(newPng)&&((byte[])await Scalar(c,"SELECT file_data FROM institutional_files WHERE id=3")).SequenceEqual(newPdf),"Cola y finalización conservan originales SQL");
    previous=uploads;var resumedQueue=await StorageMigrationRunner.Run(c,true,default);
    Check(resumedQueue.Queued==0&&resumedQueue.Existing==7&&uploads==previous,"Nueva revisión no duplica archivos ya completados por la cola");
    StorageFiles.Queue=new StorageUploadQueue(Path.Combine(queuePath,"tiny"),new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"STORAGE_QUEUE_MAX_BYTES","1"}}).Build());
    await using(var newRow=new SqlCommand("INSERT audit_photos VALUES(3,@png)",c)){newRow.Parameters.AddWithValue("@png",png.Concat(new byte[]{2}).ToArray());await newRow.ExecuteNonQueryAsync();}
    var fullQueue=await StorageMigrationRunner.Run(c,true,default);
    Check(fullQueue.NeedsRescan&&fullQueue.Copied==0&&fullQueue.Failed==0,"Al llenarse la cola deja revisión automática pendiente sin perder el original");
    corruptDownload=true;
    var release=await StorageMigrationRunner.ReleaseVerified(c,default);
    Check(release.Released==0&&release.Pending==8&&release.Message is not null,"Descarga corrupta impide retirar cualquier original de SQL");
    Check(((byte[])await Scalar(c,"SELECT photo_data FROM audit_photos WHERE id=2")).SequenceEqual(newPng),"Una verificación fallida conserva todos los bytes originales");
    corruptDownload=false;
    release=await StorageMigrationRunner.ReleaseVerified(c,default);
    Check(release.Released==7&&release.Pending==0&&release.Changed==1,"Retira solo las siete versiones actuales; no sobrescribe la versión modificada");
    Check(release.BytesFreed>8000,"Contabiliza la reducción neta de contenido SQL");
    var photoRef=(byte[])await Scalar(c,"SELECT photo_data FROM audit_photos WHERE id=2");
    Check(StorageFiles.IsReference(photoRef)&&(await StorageFiles.Read(photoRef,default)).SequenceEqual(newPng),"Foto retirada de SQL sigue descargándose desde API");
    var pdfRef=(byte[])await Scalar(c,"SELECT file_data FROM institutional_files WHERE id=3");
    Check(StorageFiles.IsReference(pdfRef)&&(await StorageFiles.Read(pdfRef,default)).SequenceEqual(newPdf),"PDF retirado de SQL sigue descargándose desde API");
    var metadata=(string)await Scalar(c,"SELECT metadata FROM institutional_files WHERE id=3");
    var hydrated=System.Text.Json.Nodes.JsonNode.Parse((await StorageFiles.TransformJson(metadata,false,default))!);
    Check(!metadata.Contains("base64")&&hydrated!["signature"]!.GetValue<string>()=="data:image/png;base64,"+Convert.ToBase64String(newPng),"Firmas JSON se retiran y se reconstruyen sin perder su contenido");
    Check(!StorageFiles.IsReference((byte[])await Scalar(c,"SELECT photo_data FROM audit_photos WHERE id=3")),"Archivo pendiente de transferencia no se retira");
    Check(((byte[])await Scalar(c,"SELECT file_data FROM institutional_files WHERE id=2")).SequenceEqual(new byte[]{0x50,0x4b,3,4}),"Documentos ajenos al alcance permanecen intactos");
    var repeated=await StorageMigrationRunner.ReleaseVerified(c,default);
    Check(repeated.Released==release.Released&&repeated.BytesFreed==release.BytesFreed,"Reiniciar liberación no duplica contadores ni transferencias");
    await Exec(c,"ALTER TABLE dbo.institutional_files ADD area varchar(20) NOT NULL DEFAULT 'files',channel varchar(20) NOT NULL DEFAULT 'requests';");
    var documentQueue=new StorageUploadQueue(Path.Combine(queuePath,"documents"),queueConfig,()=>queueClock);StorageFiles.Queue=documentQueue;
    byte[] warehouseRef,formRef;
    using(StorageOrigin.Scope("new","supervisors"))warehouseRef=await StorageFiles.Store(pdf,"institutional-document",default);
    await documentQueue.ProcessOne(default);
    using(StorageOrigin.Scope("new","support"))formRef=await StorageFiles.Store(Encoding.UTF8.GetBytes("%PDF-1.7\nnew administrative form"),"institutional-document",default);
    await using(var seedDocuments=new SqlCommand("""
        INSERT dbo.institutional_files(id,file_data,metadata) VALUES
          (100,@warehouse,N'{"warehouseRequest":{"number":"SOL-ALM-QA"},"contentType":"application/pdf","size":500}'),
          (101,@warehouse,N'{"warehouseRequest":{"number":"SOL-ALM-QA-COPY"},"contentType":"application/pdf","size":500}'),
          (102,@form,N'{"administrativeRequest":{"formType":"fuel"},"contentType":"application/pdf","size":800}'),
          (103,@raw,N'{"warehouseRequest":{"number":"SOL-ALM-OLD"},"contentType":"application/pdf","size":100}');
        """,c)){seedDocuments.Parameters.AddWithValue("@warehouse",warehouseRef);seedDocuments.Parameters.AddWithValue("@form",formRef);seedDocuments.Parameters.AddWithValue("@raw",pdf);await seedDocuments.ExecuteNonQueryAsync();}
    var documentUsage=await StorageDocumentInventory.Read(c,documentQueue,default);
    var warehouseUsage=documentUsage.Single(x=>x.Category=="warehouse-request"&&x.Profile=="supervisors");
    Check(warehouseUsage.Pdfs==1&&warehouseUsage.Verified==1&&warehouseUsage.InSql==0,"Solicitudes verificadas se clasifican por perfil sin duplicar referencias compartidas");
    Check(documentUsage.Single(x=>x.Category=="administrative-form").Pending==1,"Formulario nuevo en cola no se declara recibido por la API");
    Check(documentUsage.Single(x=>x.Category=="warehouse-request"&&x.Profile=="unknown").InSql==1,"Solicitud antigua sin transferencia conserva contenido SQL y perfil desconocido");
    Check(((byte[])await Scalar(c,"SELECT file_data FROM dbo.institutional_files WHERE id=100")).SequenceEqual(warehouseRef)&&StorageFiles.IsReference(warehouseRef),"Consultar detalle no vuelve a incrustar el PDF en SQL");
    Console.WriteLine($"{checks} comprobaciones de migración correctas; ninguna base de producción modificada.");
}
finally
{
    SqlConnection.ClearAllPools();
    // This exact generated database belongs only to this test invocation.
    if(!name.StartsWith("Codex_Storage_QA_")||name.Length!=49)throw new Exception("Unsafe test database name");
    await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE [{name}]");
}
static async Task Exec(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
static async Task<object> Scalar(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);return (await cmd.ExecuteScalarAsync())!;}
static HttpResponseMessage Json(object body)=>new(HttpStatusCode.OK){Content=JsonContent.Create(body)};
sealed class FakeHandler(Func<HttpRequestMessage,Task<HttpResponseMessage>> handle):HttpMessageHandler
{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>handle(request);}
