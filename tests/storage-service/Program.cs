using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

if(args.Contains("--live"))
{
    var settings=new Dictionary<string,string?>();
    foreach(var line in File.ReadLines(".env.local"))
    {
        var split=line.IndexOf('=');if(split<=0||line.TrimStart().StartsWith('#'))continue;
        settings[line[..split].Trim()]=line[(split+1)..].Trim().Trim('"','\'');
    }
    StorageFiles.Configure(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var report = await StorageVerification.Run("public", timeout.Token, Console.WriteLine);
    Directory.CreateDirectory("outputs");
    await File.WriteAllTextAsync("outputs/storage-live-verification.json", System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    foreach(var check in report.Checks) Console.WriteLine($"{(check.Passed ? "PASS" : "FAIL")} {check.Name}: {check.Message}");
    Environment.ExitCode = report.Passed ? 0 : 1;
    return;
}

var count=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
var pdf=Encoding.UTF8.GetBytes("%PDF-1.7\nverified test document");
var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=");
var requests=new List<(string Path,string? Key,string Body)>();
int uploads=0, polls=0, downloads=0, pauses=0;
var handler=new FakeHandler(async request=>{
    var path=request.RequestUri!.AbsolutePath;
    Check(request.Headers.Authorization?.Parameter=="test-only", "Credencial solo en la API");
    requests.Add((path,request.Headers.TryGetValues("Idempotency-Key",out var keys)?keys.Single():null,request.Content is null?"":await request.Content.ReadAsStringAsync()));
    if(path=="/v1/files"){
        uploads++;
        if(uploads==1){var response=Json(HttpStatusCode.ServiceUnavailable,new{error=new{code="busy"}});response.Headers.RetryAfter=new(TimeSpan.Zero);return response;}
        return Json(HttpStatusCode.Created,new{id="file-1",status="processing"});
    }
    if(path=="/v1/files/file-1"){polls++;return Json(HttpStatusCode.OK,new{id="file-1",status="ready"});}
    return Json(HttpStatusCode.OK,new{url="https://files.gptcomer.com/p/test?sig=private"});
});
var downloadHandler=new FakeHandler(request=>{
    Check(request.Headers.Authorization is null,"Descarga sin API key");downloads++;
    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(pdf)});
});
var service=new StorageService("test-only",new HttpClient(handler),new HttpClient(downloadHandler),(_,_)=>{pauses++;return Task.CompletedTask;});
StorageFiles.Configure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"STORAGE_ENABLED","true"}}).Build(),service);
var stored=await StorageFiles.Store(pdf,"document",default);
Check(StorageFiles.IsReference(stored),"Solo se persiste la referencia");
Check(uploads==2&&polls==1&&pauses>=2,"Reintento transitorio y consulta con pausa; peticiones limitadas");
Check(requests[0].Key==requests[1].Key&&requests[0].Key is not null,"Idempotencia estable al reintentar");
Check(requests[0].Body.Contains("private")&&!Encoding.UTF8.GetString(stored).Contains("sig="),"Visibilidad privada; no persiste URL firmada");
Check((await StorageFiles.Read(stored,default)).SequenceEqual(pdf)&&downloads==2,"Lectura con verificación de integridad");
Check((await StorageFiles.Store(stored,"document",default)).SequenceEqual(stored),"Una referencia no vuelve a subirse");
Check((await StorageFiles.Read(png,default)).SequenceEqual(png),"Lectura heredada conserva los bytes");
Check((await StorageFiles.Store("PK office"u8.ToArray(),"office",default)).SequenceEqual("PK office"u8.ToArray()),"Office conserva compatibilidad");

async Task Expect(string code,Func<Task> action){try{await action();throw new Exception("Expected "+code);}catch(StorageServiceException e){Check(e.Code==code,code);}}
StorageService Scenario(Func<HttpRequestMessage,Task<HttpResponseMessage>> handle)=>new("test-only",new HttpClient(new FakeHandler(handle)),new HttpClient(downloadHandler),(_,_)=>Task.CompletedTask);
var rejected=Scenario(_=>Task.FromResult(Json(HttpStatusCode.Created,new{id="rejected",status="rejected",reject_reason="pdf_active_content: actions"})));
await Expect("STORAGE_REJECTED",()=>rejected.Upload(pdf,"application/pdf","document",default));
int deniedCalls=0;var denied=Scenario(_=>{deniedCalls++;return Task.FromResult(Json(HttpStatusCode.Forbidden,new{error=new{code="forbidden",request_id="test"}}));});
await Expect("forbidden",()=>denied.Upload(pdf,"application/pdf","document",default));Check(deniedCalls==1,"Errores definitivos sin reintentos");
var hostile=Scenario(r=>Task.FromResult(r.RequestUri!.AbsolutePath=="/v1/files"?Json(HttpStatusCode.Created,new{id="hostile",status="ready"}):Json(HttpStatusCode.OK,new{url="https://evil.example/private"})));
await Expect("STORAGE_URL",()=>hostile.Upload(pdf,"application/pdf","document",default));
int busyCalls=0;var busy=Scenario(_=>{busyCalls++;return Task.FromResult(Json(HttpStatusCode.ServiceUnavailable,new{error=new{code="busy"}}));});
await Expect("busy",()=>busy.Upload(pdf,"application/pdf","document",default));Check(busyCalls==5,"Reintentos limitados; conserva código del proveedor");
var pending=Scenario(_=>Task.FromResult(Json(HttpStatusCode.OK,new{id="pending",status="processing"})));
await Expect("STORAGE_PROCESSING",()=>pending.Upload(pdf,"application/pdf","document",default));
await Expect("STORAGE_VERIFY",()=>service.Download(StorageFiles.Decode(stored) with{Sha256="wrong"},default));
StorageFiles.Configure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"STORAGE_ENABLED","true"}}).Build());
await Expect("STORAGE_CONFIG",()=>StorageFiles.Store(pdf,"document",default));
Check((await StorageFiles.Read(png,default)).SequenceEqual(png),"Lectura antigua funciona sin credencial");
var missingReport = await StorageVerification.Run("public", default);
Check(!missingReport.Passed && missingReport.Checks.Count == 3 && missingReport.Checks[1].Code == "STORAGE_NOT_TESTED", "No declara éxito sin credencial ni inventa pruebas de carga");
var verifiedFiles = new Dictionary<string, byte[]>();
var verificationApi = new FakeHandler(async request => {
    if(request.RequestUri!.AbsolutePath == "/v1/whoami") return Json(HttpStatusCode.OK, new { tenant = "test" });
    if(request.RequestUri.AbsolutePath == "/v1/files") {
        var content = ((MultipartFormDataContent)request.Content!).First(x => x.Headers.ContentDisposition?.Name?.Trim('"') == "file");
        var bytes = await content.ReadAsByteArrayAsync(); var id = StorageFiles.Hash(bytes); verifiedFiles[id] = bytes;
        return Json(HttpStatusCode.Created, new { id, status = "ready" });
    }
    var fileId = request.RequestUri.AbsolutePath.Split('/')[3];
    return Json(HttpStatusCode.OK, new { url = "https://files.gptcomer.com/" + fileId });
});
var verificationDownloads = new FakeHandler(request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(verifiedFiles[request.RequestUri!.AbsolutePath.Trim('/')]) }));
StorageFiles.Configure(new ConfigurationBuilder().Build(), new StorageService("test-only", new HttpClient(verificationApi), new HttpClient(verificationDownloads)));
var successfulReport = await StorageVerification.Run("public", default);
Check(successfulReport.Passed && verifiedFiles.Count == 2, "Diagnóstico exige subir y descargar imagen y PDF");
StorageFiles.Configure(new ConfigurationBuilder().Build(), Scenario(request => Task.FromResult(request.RequestUri!.AbsolutePath == "/v1/whoami" ? Json(HttpStatusCode.OK, new { tenant = "test" }) : Json(HttpStatusCode.TooManyRequests, new { error = new { code = "storage_quota_exceeded", message="Espacio agotado (0 MB)", request_id = "quota-test" } }))));
var quotaReport = await StorageVerification.Run("public", default);
Check(!quotaReport.Passed && quotaReport.Checks[0].Passed && quotaReport.Checks.Skip(1).All(x => x.Code == "storage_quota_exceeded"), "Credencial válida con cuota agotada no declara almacenamiento operativo");
Check(quotaReport.Checks[1].Message.Contains("Espacio agotado (0 MB)")&&quotaReport.Checks[1].Message.Contains("velocidad"),"Diagnóstico conserva la cuota real informada y la distingue del límite de velocidad");
StorageFiles.Configure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["STORAGE_ENABLED"] = "false" }).Build(), service);
var disabledReport = await StorageVerification.Run("public", default);
await Expect("STORAGE_DISABLED",()=>StorageFiles.Store(pdf,"document",default));
await Expect("STORAGE_FORMAT",()=>StorageFiles.StoreDataUrl("data:image/svg+xml,<svg/>",default));
await Expect("STORAGE_FORMAT",()=>StorageFiles.StoreDataUrl("data:image/png;base64,YmFk",default));
Check(!disabledReport.Passed && disabledReport.Checks[0].Code == "STORAGE_DISABLED", "Servicio desactivado nunca se muestra verificado");
Console.WriteLine($"{count} comprobaciones correctas.");
static HttpResponseMessage Json(HttpStatusCode code,object body)=>new(code){Content=JsonContent.Create(body)};
sealed class FakeHandler(Func<HttpRequestMessage,Task<HttpResponseMessage>> handle):HttpMessageHandler
{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>handle(request);}
