using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

var testRoot=Path.GetFullPath(Path.Combine("outputs","storage-queue-tests-"+Guid.NewGuid().ToString("N")));
var clock=DateTime.UtcNow; int checks=0,uploads=0,requests=0,polls=0;
var quota=true; var denied=false; var processing=false; var reject=false; var pending=0;var quotaRetryAfter=true;
var usageUnavailable=false;var usageRequests=0;
var remote=new Dictionary<string,byte[]>(); var idempotency=new List<string>();
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
var api=new HttpClient(new Handler(async request=>{
    requests++; var path=request.RequestUri!.AbsolutePath;
    if(path=="/v1/whoami")return Json(new{tenant="qa"});
    if(path=="/v1/usage"){
        usageRequests++;
        if(usageUnavailable){var busy=new HttpResponseMessage(HttpStatusCode.TooManyRequests){Content=JsonContent.Create(new{error=new{code="rate_limit"}})};busy.Headers.RetryAfter=new(TimeSpan.FromMinutes(2));return busy;}
        return Json(new{files=new{ready=7,processing=pending,rejected=1},by_type=new{image=new{count=5},pdf=new{count=2}},limits=new{max_pending=2,max_image_bytes=26214400,max_pdf_bytes=52428800,max_pdf_pages=200}});
    }
    if(path=="/v1/files"){
        uploads++;idempotency.Add(request.Headers.GetValues("Idempotency-Key").Single());
        if(denied)return new(HttpStatusCode.Forbidden){Content=JsonContent.Create(new{error=new{code="forbidden"}})};
        if(quota){var response=new HttpResponseMessage(HttpStatusCode.TooManyRequests){Content=JsonContent.Create(new{error=new{code="storage_quota_exceeded",request_id="test-quota"}})};if(quotaRetryAfter)response.Headers.RetryAfter=new(TimeSpan.FromHours(2));return response;}
        var file=((MultipartFormDataContent)request.Content!).Single(x=>x.Headers.ContentDisposition!.Name!.Trim('"')=="file");
        var bytes=await file.ReadAsByteArrayAsync();var id=StorageFiles.Hash(bytes);remote[id]=bytes;
        return Json(new{id,status=reject?"rejected":processing?"processing":"ready",reject_reason="pdf_active_content: test"});
    }
    var remoteId=path.Split('/')[3];
    if(path.EndsWith("/url"))return Json(new{url="https://files.gptcomer.com/"+remoteId});
    polls++;return Json(new{id=remoteId,status=processing?"processing":"ready"});
}));
var download=new HttpClient(new Handler(request=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(remote[request.RequestUri!.AbsolutePath.Trim('/')])})));
var service=new StorageService("test-only",api,download,(_,_)=>Task.CompletedTask);
IConfiguration Config(long daily=10_000_000_000,long capacity=2_000_000_000)=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"STORAGE_ENABLED","true"},{"STORAGE_DAILY_BYTES",daily.ToString()},{"STORAGE_QUEUE_MAX_BYTES",capacity.ToString()}}).Build();
StorageFiles.Configure(Config(),service);
var usageMonitor=new StorageUsageMonitor(()=>clock);
var firstUsage=await usageMonitor.Read(default);
Check(firstUsage.Provider?["files"]?["ready"]?.GetValue<int>()==7&&firstUsage.Provider?["by_type"]?["pdf"]?["count"]?.GetValue<int>()==2,"Inventario remoto distingue listos, imágenes y PDF sin contar cola local");
await Task.WhenAll(Enumerable.Range(0,8).Select(_=>usageMonitor.Read(default)));
Check(usageRequests==1&&uploads==0,"Actualizar estado simultáneamente comparte una lectura y no sube archivos");
clock=clock.AddSeconds(31);usageUnavailable=true;
var staleUsage=await usageMonitor.Read(default);
Check(staleUsage.Error is not null&&staleUsage.CheckedAt==firstUsage.CheckedAt&&staleUsage.Provider?["files"]?["ready"]?.GetValue<int>()==7,"Un fallo conserva los contadores y la fecha de la última lectura correcta");
var usageBefore=usageRequests;clock=clock.AddSeconds(31);await usageMonitor.Read(default);
Check(usageRequests==usageBefore,"Consultar estado respeta Retry-After del proveedor");
clock=clock.AddMinutes(2);usageUnavailable=false;var restoredUsage=await usageMonitor.Read(default);
Check(restoredUsage.Error is null&&restoredUsage.CheckedAt==clock,"La lectura se recupera tras terminar la espera");
requests=0;
StorageUploadQueue Queue(string name,IConfiguration? config=null)=>new(Path.Combine(testRoot,name),config??Config(),()=>clock);
var queue=Queue("restart");StorageFiles.Queue=queue;
var pdf=Encoding.UTF8.GetBytes("%PDF-1.7\nqueue test document");
var stored=await StorageFiles.Store(pdf,"test",default);var reference=StorageFiles.Decode(stored);
Check(StorageFiles.IsQueued(reference)&&requests==0,"Guardar en cola no depende de la API");
Check(!Encoding.UTF8.GetString(stored).Contains("queue test document"),"El registro contiene referencia, sin bytes del archivo");
Check((await StorageFiles.Read(stored,default)).SequenceEqual(pdf),"Archivo pendiente accesible por la ruta autorizada existente");
var duplicate=await StorageFiles.Store(pdf,"test",default);
Check(stored.SequenceEqual(duplicate)&&queue.Status().Pending==1,"Doble guardado no duplica trabajo");
Check(!await queue.ProcessOne(default)&&uploads==1,"Una respuesta de cuota suspende la cola sin bucle de reintentos");
Check(queue.Status().NextAttempt>=clock.AddHours(2),"Respeta Retry-After mayor de 60 segundos");
queue=Queue("restart");StorageFiles.Queue=queue;
Check(queue.Status().Pending==1&&queue.Status().NextAttempt>=clock.AddHours(2),"Pendientes y espera sobreviven al reinicio");
Check(queue.Status().VerificationBlockedUntil>=clock.AddHours(2),"La espera obligatoria del proveedor también protege las pruebas manuales tras reiniciar");
using(await queue.BeginVerification(default))
{
    try{await queue.ReserveVerificationUpload(10,default);throw new Exception("Retry-After ignored by manual check");}catch(StorageServiceException e){Check(e.Code=="STORAGE_RETRY_AFTER","Prueba manual respeta Retry-After sin enviar una subida");}
}
queue.Resume();var previous=requests;await queue.ProcessOne(default);
Check(requests==previous,"Reanudar manualmente no salta Retry-After");
clock=clock.AddHours(2).AddSeconds(1);quota=false;
Check(await queue.ProcessOne(default),"Reintenta automáticamente cuando vence la espera");
Check(idempotency[0]==idempotency[1],"Conserva Idempotency-Key tras fallo y reinicio");
Check(queue.Status().Complete==1&&queue.Status().Pending==0,"Solo declara completo después de verificar descarga");
Check((await StorageFiles.Read(stored,default)).SequenceEqual(pdf),"La referencia pendiente sigue leyendo tras convertirse en remota");
Check(queue.Status().StagedBytes==0,"Libera únicamente el temporal después de confirmar la copia remota");
Check((await StorageFiles.ResolveQueuedReferences(Encoding.UTF8.GetString(stored),default))!.Contains("queue:")==false,"Migración recibe referencia final verificada");

var processQueue=Queue("processing");StorageFiles.Queue=processQueue;processing=true;clock=clock.AddSeconds(3);
await StorageFiles.Store(pdf,"slow",default);previous=uploads;
await processQueue.ProcessOne(default);
Check(uploads==previous+1&&polls>=45&&processQueue.Status().Pending==1,"Procesamiento largo conserva id y espera");
processQueue=Queue("processing");StorageFiles.Queue=processQueue;processing=false;clock=clock.AddSeconds(3);
await processQueue.ProcessOne(default);
Check(uploads==previous+1&&processQueue.Status().Complete==1,"Tras reiniciar consulta el id aceptado, sin repetir POST");

var dailyQueue=Queue("daily",Config(daily:pdf.Length));StorageFiles.Queue=dailyQueue;
await StorageFiles.Store(pdf,"day-one",default);await StorageFiles.Store(pdf,"day-two",default);
await dailyQueue.ProcessOne(default);clock=clock.AddSeconds(3);previous=uploads;await dailyQueue.ProcessOne(default);
Check(uploads==previous&&dailyQueue.Status().NextAttempt==clock.Date.AddDays(1),"Presupuesto diario espera hasta medianoche UTC");
clock=clock.Date.AddDays(1).AddSeconds(1);await dailyQueue.ProcessOne(default);
Check(dailyQueue.Status().Complete==2,"Medianoche UTC reanuda el presupuesto diario");

var deniedQueue=Queue("denied");StorageFiles.Queue=deniedQueue;denied=true;
await StorageFiles.Store(pdf,"denied",default);await deniedQueue.ProcessOne(default);previous=requests;
clock=clock.AddDays(1);await deniedQueue.ProcessOne(default);
Check(deniedQueue.Status().State=="blocked"&&requests==previous,"401/403 no se reintenta automáticamente");
denied=false;deniedQueue.Resume();await deniedQueue.ProcessOne(default);
Check(deniedQueue.Status().Complete==1,"Permite reanudar después de corregir credencial");

var reviewQueue=Queue("review");StorageFiles.Queue=reviewQueue;reject=true;
var rejected=await StorageFiles.Store(pdf,"rejected",default);await reviewQueue.ProcessOne(default);previous=uploads;
clock=clock.AddDays(1);await reviewQueue.ProcessOne(default);
Check(reviewQueue.Status().Review==1&&uploads==previous,"Rechazo definitivo queda en revisión sin reintentos");
try{await StorageFiles.Read(rejected,default);throw new Exception("Rejected content served");}catch(StorageServiceException e){Check(e.Code=="STORAGE_REJECTED","No sirve contenido rechazado por el proveedor");}
reject=false;
var capacityQueue=Queue("capacity",Config(capacity:1));StorageFiles.Queue=capacityQueue;
try{await StorageFiles.Store(pdf,"full",default);throw new Exception("Disk capacity ignored");}catch(StorageServiceException e){Check(e.Code=="STORAGE_QUEUE_FULL","Cola llena nunca confirma un archivo sin conservarlo");}
var pendingQueue=Queue("pending");StorageFiles.Queue=pendingQueue;pending=2;
await StorageFiles.Store(pdf,"pending",default);previous=uploads;await pendingQueue.ProcessOne(default);
Check(uploads==previous&&pendingQueue.Status().State=="waiting","Respeta max_pending leído desde usage");
var detailQueue=Queue("analytics");StorageFiles.Queue=detailQueue;
using(StorageOrigin.Scope("new","supervisors"))await StorageFiles.Store(pdf,"supervisor-pdf",default);
using(StorageOrigin.Scope("legacy","unknown"))await StorageFiles.Store(pdf,"legacy-documents",default);
var details=System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(detailQueue.Analytics()))!;
Check(details["byOrigin"]!.AsArray().Count==2&&details["byOrigin"]!.AsArray().All(n=>n!["pdfs"]!.GetValue<int>()==1),"Desglose separa PDF nuevos y anteriores sin sumar imágenes inexistentes");
Check(details["byGroup"]!.AsArray().Any(n=>n!["key"]!.GetValue<string>()=="supervisors")&&StorageOrigin.Current.Group=="unknown","Procedencia de supervisor se conserva sin contaminar otras solicitudes");
detailQueue=new StorageUploadQueue(detailQueue.Root,new ConfigurationBuilder().Build());
Check(System.Text.Json.JsonSerializer.Serialize(detailQueue.Analytics()).Contains("supervisors"),"Procedencia persiste al reiniciar la cola");
var manualQueue=Queue("manual-check");StorageFiles.Queue=manualQueue;quota=true;quotaRetryAfter=false;pending=0;
await StorageFiles.Store(pdf,"manual-pending",default);await manualQueue.ProcessOne(default);clock=clock.AddSeconds(3);
Check(manualQueue.Status().NextAttempt>clock&&manualQueue.Status().VerificationBlockedUntil is null,"Pausa automática por cuota no bloquea una nueva prueba manual");
manualQueue.CompleteVerification(false);
Check(manualQueue.Status().NextAttempt>clock,"Prueba fallida conserva la pausa automática y el archivo pendiente");
quota=false;
using(await manualQueue.BeginVerification(default))
{
    Check(!await manualQueue.ProcessOne(default),"Prueba y trabajador no suben archivos simultáneamente");
    var report=await StorageVerification.Run("public",default,beforeUpload:manualQueue.ReserveVerificationUpload,onStorageError:manualQueue.RecordVerificationError);
    Check(report.Passed&&report.Checks.Count==3,"Tras corregir cuota verifica realmente credencial, imagen y PDF durante la pausa local");
    manualQueue.CompleteVerification(report.Passed);
}
Check(manualQueue.Status().NextAttempt is null&&manualQueue.Status().DailyAttemptedBytes>pdf.Length,"Prueba consume presupuesto de subidas y reanuda la cola solo al completarse");
clock=clock.AddSeconds(3);Check(await manualQueue.ProcessOne(default),"Pendiente se transfiere sin esperar el antiguo retroceso tras una prueba correcta");
var limitedCheck=Queue("manual-daily",Config(daily:1));
using(await limitedCheck.BeginVerification(default))
{
    try{await limitedCheck.ReserveVerificationUpload(2,default);throw new Exception("Daily manual budget ignored");}catch(StorageServiceException e){Check(e.Code=="STORAGE_DAILY_BUDGET","La prueba manual no elude el presupuesto diario");}
}
Console.WriteLine($"{checks} comprobaciones de cola correctas; no se llamó al proveedor real.");
static HttpResponseMessage Json(object body)=>new(HttpStatusCode.OK){Content=JsonContent.Create(body)};
sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> handle):HttpMessageHandler
{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>handle(request);}
