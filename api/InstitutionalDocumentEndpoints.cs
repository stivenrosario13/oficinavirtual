using System.Security.Claims;
using System.Security.Cryptography;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;

static class InstitutionalDocumentEndpoints
{
    sealed record NativeEditTicket(long DocumentId,DocumentActor Actor);
    static readonly string[] OfficeExtensions=[".xlsx",".xls",".xlsm",".csv",".ods"];
    static DocumentActor Actor(HttpContext c)=>new(c.User.FindFirstValue(ClaimTypes.NameIdentifier)??"",c.User.FindFirstValue(ClaimTypes.Role)??"",c.User.FindFirstValue("support_team")??"",c.User.FindFirstValue("display_name")??"Usuario",c.User.Identity?.Name??"",AreaManagerAccess.Scope(c.User) is string scope&&scope!="ALL"?DepartmentAccounts.Names.GetValueOrDefault(scope):null,WarehouseRequestOnly:WarehouseRequesterProfile.IsRequester(c.User),WarehouseAuthorize:Permission(c,"canAuthorizeWarehouseRequests")&&OperationalPermissions.Allowed(c.User,"canDecideDocuments"));
    static bool Permission(HttpContext c,string key)=>WarehousePermission.Has(c.User,key);
    static string Area(HttpContext c)=>c.Request.Query["area"]=="communications"?"communications":"files";
    static string Channel(HttpContext c)=>c.Request.Query["channel"].ToString() switch { "requirements"=>"requirements", "all"=>"all", _=>"requests" };
    static IResult Error(string message,int status=422)=>Results.Json(new{message},statusCode:status);
    static NativeEditTicket? NativeTicket(string token,IDataProtectionProvider protection)
    {
        try
        {
            var protector=protection.CreateProtector("InstitutionalDocumentNativeEditV1").ToTimeLimitedDataProtector();
            var ticket=JsonSerializer.Deserialize<NativeEditTicket>(protector.Unprotect(token,out _));
            return ticket is {DocumentId:>0}?ticket:null;
        }
        catch(CryptographicException){return null;}
        catch(JsonException){return null;}
    }
    static void DavHeaders(HttpResponse response)
    {
        response.Headers.CacheControl="private,no-store";
        response.Headers["DAV"]="1, 2";
        response.Headers["MS-Author-Via"]="DAV";
        response.Headers["Allow"]="OPTIONS, PROPFIND, GET, HEAD, LOCK, UNLOCK, PUT";
        response.Headers["Public"]="OPTIONS, PROPFIND, GET, HEAD, LOCK, UNLOCK, PUT";
    }
    static string RequestHref(HttpContext context,bool collection=false)
    {
        var href=(context.Request.PathBase+context.Request.Path).ToString();
        if(collection&&!href.EndsWith('/'))href+="/";
        return SecurityElement.Escape(href)??"/";
    }
    static string LockToken(HttpRequest request)
    {
        var header=request.Headers["If"].ToString();
        const string marker="opaquelocktoken:";
        var start=header.IndexOf(marker,StringComparison.OrdinalIgnoreCase);
        if(start>=0)
        {
            var end=header.IndexOfAny(['>',' ',')'],start);
            return header[start..(end<0?header.Length:end)];
        }
        return marker+Guid.NewGuid().ToString("D");
    }
    public static void MapInstitutionalDocumentEndpoints(this WebApplication app)
    {
        // Excel probes the containing folder before it decides whether a remote workbook is editable.
        app.MapMethods("/office-files/{token}",["OPTIONS","PROPFIND"],async(string token,HttpContext context,Database db,IDataProtectionProvider protection,CancellationToken ct)=>
        {
            var ticket=NativeTicket(token,protection);if(ticket is null)return Results.NotFound();
            DavHeaders(context.Response);
            if(HttpMethods.IsOptions(context.Request.Method))return Results.NoContent();
            var current=await db.InstitutionalBytes(ticket.DocumentId,ticket.Actor,ct);if(current is null)return Results.NotFound();
            var doc=current.Value.Document;var bytes=current.Value.Data;
            if(!OfficeExtensions.Contains(Path.GetExtension(doc.OriginalFileName),StringComparer.OrdinalIgnoreCase))return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
            var collectionHref=RequestHref(context,true);var modified=doc.CreatedUtc.ToUniversalTime().ToString("R");
            var childHref=collectionHref+(SecurityElement.Escape(Uri.EscapeDataString(Path.GetFileName(doc.OriginalFileName)))??"");
            var etag="\""+Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()+"\"";
            var collection=$"<d:response><d:href>{collectionHref}</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype><d:displayname>Office Virtual</d:displayname><d:supportedlock><d:lockentry><d:lockscope><d:exclusive/></d:lockscope><d:locktype><d:write/></d:locktype></d:lockentry></d:supportedlock></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>";
            var child=$"<d:response><d:href>{childHref}</d:href><d:propstat><d:prop><d:resourcetype/><d:displayname>{SecurityElement.Escape(doc.OriginalFileName)}</d:displayname><d:getcontentlength>{bytes.Length}</d:getcontentlength><d:getcontenttype>{SecurityElement.Escape(doc.ContentType)}</d:getcontenttype><d:getlastmodified>{modified}</d:getlastmodified><d:getetag>{etag}</d:getetag></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>";
            var includeChild=context.Request.Headers["Depth"].ToString()!="0";
            var xml=$"<?xml version=\"1.0\" encoding=\"utf-8\"?><d:multistatus xmlns:d=\"DAV:\">{collection}{(includeChild?child:"")}</d:multistatus>";
            return Results.Content(xml,"application/xml",Encoding.UTF8,statusCode:207);
        });
        app.MapMethods("/office-files/{token}/{fileName}",["OPTIONS","PROPFIND","GET","HEAD","LOCK","UNLOCK","PUT"],async(string token,string fileName,HttpContext context,Database db,IDataProtectionProvider protection,LiveNotificationBroker broker,CancellationToken ct)=>
        {
            var ticket=NativeTicket(token,protection);if(ticket is null)return Results.NotFound();
            var method=context.Request.Method.ToUpperInvariant();
            DavHeaders(context.Response);
            if(method=="OPTIONS")return Results.NoContent();
            var current=await db.InstitutionalBytes(ticket.DocumentId,ticket.Actor,ct);if(current is null)return Results.NotFound();
            var doc=current.Value.Document;var bytes=current.Value.Data;
            var extension=Path.GetExtension(doc.OriginalFileName);
            if(!OfficeExtensions.Contains(extension,StringComparer.OrdinalIgnoreCase))return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
            context.Response.Headers.ETag="\""+Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()+"\"";
            context.Response.Headers.LastModified=doc.CreatedUtc.ToUniversalTime().ToString("R");
            if(method=="GET"||method=="HEAD")
            {
                context.Response.Headers.ContentDisposition="inline; filename*=UTF-8''"+Uri.EscapeDataString(Path.GetFileName(doc.OriginalFileName));
                context.Response.ContentType=doc.ContentType;context.Response.ContentLength=bytes.Length;
                if(method=="HEAD")return Results.Empty;
                return Results.Bytes(bytes,doc.ContentType,enableRangeProcessing:true);
            }
            if(method=="PROPFIND")
            {
                var href=RequestHref(context);var modified=doc.CreatedUtc.ToUniversalTime().ToString("R");
                var xml=$"<?xml version=\"1.0\" encoding=\"utf-8\"?><d:multistatus xmlns:d=\"DAV:\"><d:response><d:href>{href}</d:href><d:propstat><d:prop><d:resourcetype/><d:getcontentlength>{bytes.Length}</d:getcontentlength><d:getcontenttype>{SecurityElement.Escape(doc.ContentType)}</d:getcontenttype><d:getlastmodified>{modified}</d:getlastmodified><d:getetag>{context.Response.Headers.ETag}</d:getetag><d:supportedlock><d:lockentry><d:lockscope><d:exclusive/></d:lockscope><d:locktype><d:write/></d:locktype></d:lockentry></d:supportedlock><d:lockdiscovery/></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response></d:multistatus>";
                return Results.Content(xml,"application/xml",Encoding.UTF8,statusCode:207);
            }
            if(method=="LOCK")
            {
                var lockToken=LockToken(context.Request);context.Response.Headers["Lock-Token"]="<"+lockToken+">";context.Response.Headers["Timeout"]="Second-3600";
                var xml=$"<?xml version=\"1.0\" encoding=\"utf-8\"?><d:prop xmlns:d=\"DAV:\"><d:lockdiscovery><d:activelock><d:locktype><d:write/></d:locktype><d:lockscope><d:exclusive/></d:lockscope><d:depth>0</d:depth><d:timeout>Second-3600</d:timeout><d:locktoken><d:href>{lockToken}</d:href></d:locktoken><d:lockroot><d:href>{RequestHref(context)}</d:href></d:lockroot></d:activelock></d:lockdiscovery></d:prop>";
                return Results.Content(xml,"application/xml",Encoding.UTF8);
            }
            if(method=="UNLOCK")return Results.NoContent();
            if(method=="PUT")
            {
                if(context.Request.ContentLength is >10485760)return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                await using var output=new MemoryStream();var buffer=new byte[65536];int read;
                while((read=await context.Request.Body.ReadAsync(buffer,ct))>0){if(output.Length+read>10485760)return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);await output.WriteAsync(buffer.AsMemory(0,read),ct);}
                if(output.Length==0)return Results.BadRequest();
                var writeError=await db.InstitutionalReplaceFile(ticket.DocumentId,ticket.Actor,output.ToArray(),doc.ContentType,ct);
                if(writeError is not null)return Results.StatusCode(writeError.Contains("permiso",StringComparison.OrdinalIgnoreCase)?StatusCodes.Status403Forbidden:StatusCodes.Status409Conflict);
                broker.Publish("institutional-document-updated");
                context.Response.Headers.ETag="\""+Convert.ToHexString(SHA256.HashData(output.ToArray())).ToLowerInvariant()+"\"";
                return Results.NoContent();
            }
            return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
        });
        app.MapGet("/api/institutional-open/{token}/{fileName}",async(string token,string fileName,HttpContext context,Database db,IDataProtectionProvider protection,CancellationToken ct)=>
        {
            try
            {
                var protector=protection.CreateProtector("InstitutionalDocumentNativeOpenV1").ToTimeLimitedDataProtector();
                var value=protector.Unprotect(token,out _);
                if(!long.TryParse(value,out var id)||id<1)return Results.NotFound();
                var file=await db.InstitutionalOpenBytes(id,ct);if(file is null)return Results.NotFound();
                context.Response.Headers.CacheControl="private,no-store";context.Response.Headers.ContentDisposition="inline";
                return Results.File(file.Value.Data,file.Value.Document.ContentType,enableRangeProcessing:true);
            }
            catch(CryptographicException){return Results.NotFound();}
        });
        var group=app.MapGroup("/api/institutional-documents").RequireAuthorization("PortalUser");
        group.AddEndpointFilter(async(context,next)=>{if(!Actor(context.HttpContext).Allowed)return Results.Forbid();context.HttpContext.Response.Headers.CacheControl="private,no-store";try{return await next(context);}catch(AdministrativeFormConflictException error){return Error(error.Message,409);}catch(WarehouseSubmissionConflictException error){return Error(error.Message,409);}catch(WarehouseFailureValidationException error){return Error(error.Message);}});
        group.MapGet("/users",async(Database db,CancellationToken ct)=>Results.Content(await db.InstitutionalUsers(ct),"application/json"));
        group.MapGet("/warehouse-queue",async(HttpContext c,Database db,CancellationToken ct)=>Results.Ok(await db.InstitutionalWarehouseQueue(Actor(c),ct)));
        group.MapGet("/warehouse-groups",async(HttpContext c,Database db,CancellationToken ct)=>{
            var actor=Actor(c);if(actor.WarehouseRequestsBlocked||!OperationalPermissions.Allowed(c.User,"canRequestWarehouse"))return Results.Forbid();
            return Results.Ok(new{groups=await db.WarehouseRequestGroups(actor,ct)});
        });
        group.MapGet("/warehouse-submission/{number}",async(string number,HttpContext c,Database db,CancellationToken ct)=>{
            if(number.Length>40||!number.StartsWith("SOL-ALM-",StringComparison.Ordinal))return Results.NotFound();
            var document=await db.InstitutionalWarehouseSubmission(number,Actor(c),ct);
            return document is null?Results.NotFound():Results.Ok(new{document.Id,document.WarehouseRequest});
        });
        group.MapGet("/overview",async(HttpContext c,Database db,CancellationToken ct)=>Results.Ok(await db.InstitutionalOverview(Actor(c),Area(c),Channel(c),ct)));
        group.MapGet("",async(HttpContext c,Database db,CancellationToken ct)=>{
            var a=Actor(c);var summary=c.Request.Query["summary"]=="1";var administrativeOnly=c.Request.Query["administrative"]=="1";IEnumerable<InstitutionalFile> docs=await db.InstitutionalList(a,Area(c),Channel(c),ct,hydrate:!summary,administrativeOnly:administrativeOnly);var view=c.Request.Query["view"].ToString();var dept=c.Request.Query["department"].ToString();
            docs=docs.Where(d=>view=="trash"?d.Trashed&&(d.IsOwner||d.CanManageFiles):!d.Trashed);docs=view switch{"mine"=>docs.Where(d=>d.IsOwner),"shared"=>docs.Where(d=>!d.IsOwner&&d.Shared(a)),"transit"=>docs.Where(d=>(d.Status is "pending" or "correction_requested")&&!d.AwaitingWarehouseAuthorization()),"received"=>docs.Where(d=>d.AwaitingWarehouseAuthorization()||(d.ReceivedUtc is not null&&(d.Status is "received" or "approved" or "rejected"))),"rejected"=>docs.Where(d=>d.Status=="rejected"),"approved"=>docs.Where(d=>d.Status=="approved"),"sent"=>docs.Where(d=>d.IsOwner&&d.SentUtc is not null),_=>docs};if(dept!="")docs=docs.Where(d=>d.Department==dept);if(summary)docs=docs.Select(d=>d.ForListSummary());return Results.Ok(docs);
        });
        group.MapGet("/lookup/{number}",async(string number,HttpContext c,Database db,CancellationToken ct)=>{
            if(number.Length>40||!number.StartsWith("SOL-ADM-",StringComparison.Ordinal))return Results.NotFound();
            var actor=Actor(c);var document=(await db.InstitutionalList(actor,"communications","requirements",ct)).FirstOrDefault(d=>d.AdministrativeRequest?.Number==number&&d.RecipientDepartment=="Administración"&&!d.Trashed);
            return document is null?Results.NotFound():Results.Ok(new{document.Id,document.Title,document.Status,document.Department,document.ReceivedUtc,document.DecisionUtc,document.DecisionBy,document.AdministrativeDelivery,document.AdministrativeRequest});
        });
        group.MapGet("/{id:long}",async(long id,HttpContext c,Database db,CancellationToken ct)=>{var d=await db.InstitutionalDetail(id,Actor(c),ct);return d is null?Results.NotFound():Results.Ok(d);});
        group.MapGet("/{id:long}/warehouse-evidence/{evidenceId:guid}",async(long id,Guid evidenceId,HttpContext c,Database db,CancellationToken ct)=>{var photo=await db.InstitutionalWarehouseEvidence(id,evidenceId,Actor(c),ct);return photo is null?Results.NotFound():Results.File(photo.Value.Data,photo.Value.Mime,enableRangeProcessing:true);});
        group.MapGet("/{id:long}/download",async(long id,HttpContext c,Database db,CancellationToken ct)=>{var f=await db.InstitutionalBytes(id,Actor(c),ct);return f is null?Results.NotFound():Results.File(f.Value.Data,f.Value.Document.ContentType,f.Value.Document.OriginalFileName,enableRangeProcessing:true);});
        group.MapPut("/{id:long}/content",async(long id,HttpContext c,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            if(!c.Request.HasFormContentType)return Error("Selecciona el archivo editado.");var form=await c.Request.ReadFormAsync(ct);var file=form.Files.GetFile("file");if(file is null||file.Length is <1 or >10485760)return Error("El archivo debe tener contenido y pesar como máximo 10 MB.");
            var extension=Path.GetExtension(file.FileName);var allowed=new[]{".xlsx",".xls",".xlsm",".csv",".ods"};if(!allowed.Contains(extension,StringComparer.OrdinalIgnoreCase))return Error("Selecciona un archivo de Excel compatible (XLSX, XLS, XLSM, CSV u ODS).");
            await using var stream=new MemoryStream();await file.CopyToAsync(stream,ct);var error=await db.InstitutionalReplaceFile(id,Actor(c),stream.ToArray(),string.IsNullOrWhiteSpace(file.ContentType)?"application/octet-stream":file.ContentType,ct);if(error is not null)return Error(error,409);broker.Publish("institutional-document-updated");return Results.Ok(new{ok=true});
        }).RequireRateLimiting("sensitive-write");
        group.MapGet("/{id:long}/preview",async(long id,HttpContext c,Database db,CancellationToken ct)=>{var f=await db.InstitutionalBytes(id,Actor(c),ct);if(f is null)return Results.NotFound();c.Response.Headers.ContentDisposition="inline";return Results.File(f.Value.Data,contentType:f.Value.Document.ContentType,enableRangeProcessing:true);});
        group.MapGet("/{id:long}/open-link",async(long id,HttpContext c,Database db,IDataProtectionProvider protection,CancellationToken ct)=>
        {
            var document=await db.InstitutionalDetail(id,Actor(c),ct);if(document is null)return Results.NotFound();
            if(OfficeExtensions.Contains(Path.GetExtension(document.OriginalFileName),StringComparer.OrdinalIgnoreCase))
            {
                var editable=document.IsOwner||document.CanEdit||document.CanManageFiles;
                if(!editable)return Error("Necesitas permiso de edición para abrir este archivo en Excel.",403);
                var ticket=new NativeEditTicket(id,Actor(c));var token=protection.CreateProtector("InstitutionalDocumentNativeEditV1").ToTimeLimitedDataProtector().Protect(JsonSerializer.Serialize(ticket),TimeSpan.FromHours(8));
                var fileName=Uri.EscapeDataString(Path.GetFileName(document.OriginalFileName));var url=$"{c.Request.Scheme}://{c.Request.Host}{c.Request.PathBase}/office-files/{Uri.EscapeDataString(token)}/{fileName}";
                return Results.Ok(new{url,expiresInSeconds=28800,editable=true});
            }
            var protector=protection.CreateProtector("InstitutionalDocumentNativeOpenV1").ToTimeLimitedDataProtector();
            var token=protector.Protect(id.ToString(),TimeSpan.FromMinutes(2));
            var fileName=Uri.EscapeDataString(Path.GetFileName(document.OriginalFileName));
            var url=$"{c.Request.Scheme}://{c.Request.Host}{c.Request.PathBase}/api/institutional-open/{Uri.EscapeDataString(token)}/{fileName}";
            return Results.Ok(new{url,expiresInSeconds=120});
        });
        async Task<IResult> SaveDocument(HttpContext c,Database db,LiveNotificationBroker broker,CancellationToken ct){
            var a=Actor(c);if(!c.Request.HasFormContentType)return Error("Selecciona un archivo.");var form=await c.Request.ReadFormAsync(ct);var f=form.Files.GetFile("file");var area=form["area"]=="communications"?"communications":"files";var channel=form["channel"]=="requirements"?"requirements":"requests";var draft=string.Equals(form["draft"].ToString(),"true",StringComparison.OrdinalIgnoreCase);long? templateId=long.TryParse(form["templateId"].ToString(),out var parsedTemplateId)&&parsedTemplateId>0?parsedTemplateId:null;
            var title=form["title"].ToString().Trim();var description=form["description"].ToString().Trim();string? recipient=form["recipientUserName"].ToString().Trim();if(recipient=="")recipient=null;string? destination=form["recipientDepartment"].ToString().Trim();if(destination=="")destination=null;
            if(a.WarehouseRequestOnly&&(area!="communications"||channel!="requirements"||destination!="Almacén"||draft||string.IsNullOrWhiteSpace(form["warehouseRequest"])||!string.IsNullOrWhiteSpace(form["administrativeRequest"])))return Results.Forbid();
            var requestPermission=string.IsNullOrWhiteSpace(form["warehouseRequest"])?"canUploadDocuments":"canRequestWarehouse";
            if(!OperationalPermissions.Allowed(c.User,requestPermission))return Results.Forbid();
            var sourceDepartment=a.Department;var requestedSource=form["sourceDepartment"].ToString().Trim();
            if(!string.IsNullOrWhiteSpace(form["warehouseRequest"])){
                var requestActor=WarehouseRequestSource.Resolve(a,requestedSource);
                if(requestActor is null||(a.WarehouseRequestOnly&&requestActor.Department!=a.Department))return Error("El departamento solicitante no corresponde a tu cuenta.",403);
                a=requestActor;sourceDepartment=a.Department;
            }
            // Avería Técnica owns internet forms under its parent department Tecnología.
            if(a.Role=="Technology"&&a.Team=="TECHNICAL_FAILURE"&&requestedSource=="Tecnología"&&!string.IsNullOrWhiteSpace(form["administrativeRequest"])){
                a=a with{AssignedDepartment="Tecnología"};sourceDepartment=a.Department;
            }
            if(a.WarehouseRequestsBlocked&&(!string.IsNullOrWhiteSpace(form["warehouseRequest"])||(area=="communications"&&channel=="requirements"&&destination=="Almacén")))return Results.Forbid();
            if(requestedSource!="")
            {
                if(requestedSource is not ("Supervisión" or "Almacén" or "Administración" or "Tecnología" or "Servicios Generales" or "Recursos Humanos" or "Taller" or "Comunicación" or "Comercialización" or "Tesorería" or "Contabilidad" or "Call Center" or "Avería Técnica"))return Error("El departamento solicitante no es válido.");
                if(!a.Manager&&requestedSource!=a.Department)return Results.Forbid();
                sourceDepartment=requestedSource;
            }
            Guid? folderId=null;var requestedFolder=form["folderId"].ToString().Trim();if(requestedFolder!="") {if(area!="files"||!Guid.TryParse(requestedFolder,out var parsedFolder))return Error("Selecciona una carpeta válida.");var folderDepartment=await db.AdministrativeFolderDepartment(parsedFolder,a,ct);if(folderDepartment is null)return Error("Selecciona una carpeta propia o compartida contigo.");folderId=parsedFolder;sourceDepartment=folderDepartment;}
            if(f is null||f.Length is <1 or >10485760||form.Files.Count!=1||title.Length is <1 or >160||description.Length>2000)return Error("Indica título, descripción de hasta 2,000 caracteres y un archivo de hasta 10 MB.");
            if(area=="communications"&&!draft) {var departmentRequest=channel=="requirements"&&(destination is "Almacén" or "Administración" or "Contabilidad" or "Caja")&&DepartmentAccounts.Names.ContainsValue(sourceDepartment);var administrativeSelfRequest=(a.Role is "Administrator" or "Administrative")&&channel=="requirements"&&destination=="Administración"&&sourceDepartment=="Administración";var warehouseSelfRequest=a.Manager&&channel=="requirements"&&destination=="Almacén"&&sourceDepartment=="Almacén"&&!string.IsNullOrWhiteSpace(form["warehouseRequest"]);var supervisorVirtualReception=a.Role=="GroupAdministrator"&&channel=="requirements"&&(destination is "Administración" or "Almacén")&&sourceDepartment=="Supervisión";if(channel=="requirements"&&!a.Manager&&!departmentRequest&&!supervisorVirtualReception)return Results.Forbid();if(!await db.InstitutionalRecipient(recipient,destination,ct)||recipient==a.Id||((recipient is null&&destination==sourceDepartment)&&!administrativeSelfRequest&&!warehouseSelfRequest))return Error("Selecciona un departamento o destinatario válido distinto del remitente.");}
            if(area=="communications"&&draft){recipient=null;destination=null;channel="requests";}
            var filename=Path.GetFileName(f.FileName.Replace('\\','/'));if(filename.Length>180)return Error("El nombre del archivo es demasiado largo.");var ext=Path.GetExtension(filename).ToLowerInvariant();var mime=ext switch{".pdf"=>"application/pdf",".png"=>"image/png",".jpg" or ".jpeg"=>"image/jpeg",".docx"=>"application/vnd.openxmlformats-officedocument.wordprocessingml.document",".xlsx"=>"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",".txt"=>"text/plain",_=>""};if(mime=="")return Error("Formatos permitidos: PDF, PNG, JPG, DOCX, XLSX y TXT.");
            using var stream=new MemoryStream();await f.CopyToAsync(stream,ct);var bytes=stream.ToArray();if((ext==".pdf"&&(bytes.Length<5||!bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8)))||(ext==".png"&&(bytes.Length<8||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})))||((ext==".jpg"||ext==".jpeg")&&(bytes.Length<3||bytes[0]!=255||bytes[1]!=216||bytes[2]!=255))||((ext==".docx"||ext==".xlsx")&&(bytes.Length<4||bytes[0]!=80||bytes[1]!=75)))return Error("El contenido no corresponde al formato declarado.");
            WarehouseRequestInfo? warehouseRequest=null;var requestJson=form["warehouseRequest"].ToString();
            if(requestJson!="")
            {
                if(!OperationalPermissions.Allowed(c.User,"canRequestWarehouse"))return Results.Forbid();
                if(area!="communications"||channel!="requirements"||destination!="Almacén"||mime!="application/pdf"||requestJson.Length>20000)return Error("Solicitud de Almacén no válida.");
                try{warehouseRequest=JsonSerializer.Deserialize<WarehouseRequestInfo>(requestJson,new JsonSerializerOptions(JsonSerializerDefaults.Web));}catch(JsonException){return Error("Datos de equipos no válidos.");}
                if(warehouseRequest is null||warehouseRequest.Number is null||!warehouseRequest.Number.StartsWith("SOL-ALM-",StringComparison.Ordinal)||warehouseRequest.Number.Length>40||warehouseRequest.Lines is null||warehouseRequest.Lines.Count is <1 or >30||warehouseRequest.Lines.Any(x=>x is null||x.Kind!="EQUIPMENT"||string.IsNullOrWhiteSpace(x.Name)||x.Name.Length>100||x.Quantity is <1 or >10000)||(warehouseRequest.InstallerId?.Length??0)>80||(warehouseRequest.InstallerName?.Length??0)>160||warehouseRequest.Lines.Sum(x=>x.Quantity)>10000||warehouseRequest.Priority is not ("LOW" or "MEDIUM" or "HIGH" or "CRITICAL"))return Error("Indica equipos o materiales y cantidades válidas (máximo 10,000 unidades por solicitud).");
                if(warehouseRequest.FailureCategory is not null&&(
                    warehouseRequest.AgencyId==Guid.Empty||warehouseRequest.TicketId is not null||
                    warehouseRequest.FailureDepartment is not ("TECHNOLOGY" or "GENERAL_SERVICES")||
                    warehouseRequest.FailureCategory.Length is <1 or >60||
                    string.IsNullOrWhiteSpace(warehouseRequest.FailureDescription)||warehouseRequest.FailureDescription.Length>1200))
                    return Error("Selecciona una agencia, el área de soporte, la falla y su descripción para crear el ticket.");
                if(warehouseRequest.FailureCategory is null&&(warehouseRequest.FailureDepartment is not null||warehouseRequest.FailureDescription is not null||warehouseRequest.FailureTeam is not null))return Error("Completa la falla y el área de soporte.");
                // A warehouse request is authorized separately from general ticket creation.
                // Its scoped agency and active failure category are validated below, then
                // the internal ticket and document are committed in one transaction.
                if(warehouseRequest.FailureCategory is not null)
                {
                    warehouseRequest=WarehouseRequestSource.RouteFailure(warehouseRequest,a,sourceDepartment);
                    if(warehouseRequest is null)return Error("Las fallas deben asignarse a Tecnología o Servicios Generales.");
                }
            }
            if(warehouseRequest is not null){var checkedRequest=await db.WarehouseCanonicalRequestChecked(warehouseRequest,a,sourceDepartment,ct);if(checkedRequest.Request is null)return Error(checkedRequest.Error??"No fue posible validar la solicitud. Actualiza el formulario y vuelve a intentarlo.");warehouseRequest=checkedRequest.Request;}
            AdministrativeRequestInfo? administrativeRequest=null;var administrativeJson=form["administrativeRequest"].ToString();
            if(administrativeJson!="")
            {
                if(area!="communications"||channel!="requirements"||destination!="Administración"||mime!="application/pdf"||administrativeJson.Length>30000)return Error("Solicitud administrativa no válida.");
                try{administrativeRequest=JsonSerializer.Deserialize<AdministrativeRequestInfo>(administrativeJson,new JsonSerializerOptions(JsonSerializerDefaults.Web));}catch(JsonException){return Error("Datos de la solicitud no válidos.");}
                if(administrativeRequest is null||administrativeRequest.Number is null||!administrativeRequest.Number.StartsWith("SOL-ADM-",StringComparison.Ordinal)||administrativeRequest.Number.Length>40||administrativeRequest.AssetType is not ("FLOTA" or "MODEM" or "OTRO" or "INTERNET")||administrativeRequest.TargetType is not ("AGENCIA" or "EMPLEADO")||string.IsNullOrWhiteSpace(administrativeRequest.TargetName)||administrativeRequest.TargetName.Length>160||(administrativeRequest.AgencyCode?.Length??0)>40||(administrativeRequest.Reason?.Length??0)>4000)return Error("Completa el tipo de activo, destino y motivo de la solicitud.");
                var allowedForms=new[]{"FLEET","MODEM","GENERAL","SIM","MARBETE","DEVICE","PAYMENT","INTERNET","CONTRACT_REQUEST","CONTRACT_RENEWAL","RENTAL_CONTRACT","RENT_RECEIPT","INCREASE_LETTER","FUEL_LOG","GAS_LOG","INCIDENT_REPORT","LABOR_BENEFITS_REQUEST","BLANK_CHECKS_DELIVERY","SIGNED_BENEFITS_CHECKS_DELIVERY"};
                if(administrativeRequest.FormType is not null&&(!allowedForms.Contains(administrativeRequest.FormType)||administrativeRequest.FormData is not {ValueKind:JsonValueKind.Object}))return Error("El formulario administrativo no es válido.");
                if(administrativeRequest.FormType=="LABOR_BENEFITS_REQUEST"&&(destination!="Contabilidad"||sourceDepartment is not ("Recursos Humanos" or "Administración")))return Error("La solicitud de prestaciones debe ser enviada por Recursos Humanos o Administración a Contabilidad.");
                if(administrativeRequest.FormType=="BLANK_CHECKS_DELIVERY"&&(destination!="Contabilidad"||sourceDepartment!="Administración"))return Error("La entrega de cheques en blanco debe ser enviada por Administración a Contabilidad.");
                if(administrativeRequest.FormType=="SIGNED_BENEFITS_CHECKS_DELIVERY"&&(destination!="Caja"||sourceDepartment!="Administración"))return Error("La entrega de cheques firmados debe ser enviada por Administración a Caja.");
                if(administrativeRequest.FormType=="INCIDENT_REPORT")
                {
                    var data=administrativeRequest.FormData!.Value;
                    if(!data.TryGetProperty("rows",out var incidentRows)||incidentRows.ValueKind!=JsonValueKind.Array||incidentRows.GetArrayLength() is <1 or >100||
                       !data.TryGetProperty("groupName",out var groupName)||groupName.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(groupName.GetString())||
                       !data.TryGetProperty("incidentAgencyId",out var agencyId)||agencyId.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(agencyId.GetString())||
                       incidentRows.EnumerateArray().Any(row=>row.ValueKind!=JsonValueKind.Object||!row.TryGetProperty("ticketDate",out var ticketDate)||ticketDate.ValueKind!=JsonValueKind.String||!DateOnly.TryParseExact(ticketDate.GetString(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)||!row.TryGetProperty("ticketCode",out var code)||code.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(code.GetString())||code.GetString()!.Length>180||!row.TryGetProperty("agentPhone",out var contact)||contact.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(contact.GetString())||contact.GetString()!.Length>180||row.TryGetProperty("decisionStatus",out _)))
                        return Error("Completa la agencia, el grupo y cada ticket con fecha, número y contacto. Las decisiones se registran después de recibir el informe.");
                    if(administrativeRequest.AssetType!="OTRO"||administrativeRequest.TargetType!="AGENCIA"||string.IsNullOrWhiteSpace(administrativeRequest.AgencyCode))return Error("El informe debe estar vinculado a una agencia válida.");
                    if(!Guid.TryParse(agencyId.GetString(),out var incidentAgencyGuid))return Error("Selecciona una agencia válida del catálogo.");
                    var incidentAgency=(await db.TicketAgencies(a.Id,a.Role,ct)).FirstOrDefault(row=>row.Id==incidentAgencyGuid);
                    if(incidentAgency is null)return Error("La agencia seleccionada no está disponible para tu cuenta.");
                    var normalized=JsonNode.Parse(data.GetRawText())!.AsObject();normalized["groupName"]=incidentAgency.Grupo;normalized["incidentAgencyName"]=$"{incidentAgency.Codigo} · {incidentAgency.Terminal}";
                    foreach(var row in normalized["rows"]!.AsArray().OfType<JsonObject>())row["agencyId"]=incidentAgency.Codigo;
                    administrativeRequest=administrativeRequest with{TargetName=incidentAgency.Terminal,AgencyCode=incidentAgency.Codigo,FormData=JsonSerializer.SerializeToElement(normalized)};
                }
                if(administrativeRequest.FormType is "FUEL_LOG" or "GAS_LOG"&&administrativeRequest.AssetType!="OTRO")return Error("El combustible debe registrarse como formulario de control, sin entrega de activos.");
                if(administrativeRequest.FormType=="FLEET"&&(administrativeRequest.AssetType!="FLOTA"||administrativeRequest.TargetType!="EMPLEADO"))return Error("La flota es un teléfono asignado a un empleado.");
                if(administrativeRequest.FormType=="MODEM"&&(administrativeRequest.AssetType!="MODEM"||administrativeRequest.TargetType!="AGENCIA"||string.IsNullOrWhiteSpace(administrativeRequest.AgencyCode)))return Error("Indica la agencia destinataria del módem.");
                if(administrativeRequest.FormType=="FUEL_LOG"&&AdministrativeFuel.IsSulay(a.Login))administrativeRequest=administrativeRequest with{FormType="GAS_LOG"};
                if(!AdministrativeProfiles.CanRead(a,administrativeRequest))return Results.Forbid();
                if(a.Role=="GroupAdministrator"&&administrativeRequest.FormType is not ("CONTRACT_REQUEST" or "CONTRACT_RENEWAL" or "INCIDENT_REPORT"))return Results.Forbid();
                if(administrativeRequest.AssetType=="INTERNET")
                {
                    var internetRequestActor=a.Role=="Administrator"||a.Role=="Technology"&&a.Team=="TECHNICAL_FAILURE";
                    if(!internetRequestActor||sourceDepartment!="Tecnología")return Results.Forbid();
                    var internet=administrativeRequest.Internet;
                    if(internet is null||internet.AgencyId==Guid.Empty||internet.ServiceKind is not ("BANDA_ANCHA" or "LINEA_FIJA")||string.IsNullOrWhiteSpace(internet.ServiceNumber)||internet.ServiceNumber.Length>80||internet.Company is not ("CLARO" or "ALTICE")||internet.ItemKind is not ("EQUIPO" or "SIM")||internet.ModemQuantity is <0 or >20||(internet.ItemKind=="EQUIPO"&&internet.ModemQuantity<1)||(internet.ItemKind=="SIM"&&internet.ModemQuantity!=0)||string.IsNullOrWhiteSpace(internet.ChangeReason)||internet.ChangeReason.Trim().Length is <5 or >2000)return Error("Completa la agencia, línea, compañía, equipo y motivo del cambio.");
                    var agency=(await db.TicketAgencies(a.Id,a.Role,ct)).FirstOrDefault(row=>row.Id==internet.AgencyId);
                    if(agency is null)return Error("Selecciona una agencia activa del catálogo.");
                    if(internet.TicketId is Guid linkedId){
                        var linked=(await db.Tickets(a.Id,a.Role,(WarehousePermission.Has(c.User,"canAssignTickets")||AreaManagerAccess.Allowed(c.User)),a.Team,a.Login,ct,AreaManagerAccess.Allowed(c.User),linkedId)).FirstOrDefault();
                        if(linked is null||linked.TicketType!="INTERNAL"||linked.AssignedDepartment!="TECHNOLOGY"||!await db.InternetTicketAgencyMatches(linkedId,internet.AgencyId,ct)||linked.Status is "RESOLVED" or "CLOSED" or "CANCELLED")return Error("El ticket interno debe estar abierto y pertenecer a la agencia seleccionada.");
                        internet=internet with{TicketNumber=linked.TicketNumber};
                    }else internet=internet with{TicketNumber=null};
                    administrativeRequest=administrativeRequest with{TargetType="AGENCIA",TargetName=agency.Terminal,AgencyCode=agency.Codigo,Reason=internet.ChangeReason.Trim(),Internet=internet with{AgencyCode=agency.Codigo,AgencyName=agency.Terminal,AgencyGroup=agency.Grupo,ServiceNumber=internet.ServiceNumber.Trim(),ChangeReason=internet.ChangeReason.Trim()}};
                }
                else if(administrativeRequest.Internet is not null)return Error("El detalle de internet solo corresponde a una solicitud de Avería Técnica.");
            }
            if(a.Role=="GroupAdministrator"&&administrativeRequest is null&&warehouseRequest is null)return Results.Forbid();
            var d=new InstitutionalFile{Area=area,Channel=channel,Title=title,Description=description,OriginalFileName=filename,ContentType=mime,Size=bytes.Length,OwnerUserName=a.Id,OwnerDisplayName=a.Name,OwnerLogin=a.Login,Department=sourceDepartment,FolderId=folderId,RecipientDepartment=area=="communications"?destination:null,RecipientUserName=area=="communications"?recipient:null,Status=area=="communications"?(draft?"draft":"pending"):"active",SentUtc=area=="communications"&&!draft?DateTime.UtcNow:null,TemplateId=area=="communications"&&draft?templateId:null,WarehouseRequest=warehouseRequest,AdministrativeRequest=administrativeRequest};AdministrativeFuel.Normalize(d);if(c.Request.RouteValues.TryGetValue("id",out var editId)){if(administrativeRequest is null||warehouseRequest is not null||!long.TryParse(editId?.ToString(),out var id)||!int.TryParse(form["revision"],out var revision))return Error("Edición no válida.");var editError=await db.InstitutionalEditAdministrativeForm(id,revision,a,d,bytes,ct);if(editError is not null)return Error(editError,409);broker.Publish("institutional-document-updated");return Results.Ok(new{id});}d.Id=draft&&templateId is long tid?await db.InstitutionalArchiveTemplatePdf(a,tid,d,bytes,ct):await db.InstitutionalUpload(d,bytes,ct);broker.Publish("institutional-document-created");if(warehouseRequest?.FailureCategory is not null)broker.Publish("ticket-created");return Results.Ok(d.For(a));
        }
        group.MapPost("",SaveDocument).RequireRateLimiting("sensitive-write");
        group.MapPut("/{id:long}/administrative-form",SaveDocument).RequireRateLimiting("sensitive-write");
        group.MapPost("/{id:long}/incident-tickets/decision",async(long id,IncidentTicketDecisionInput body,HttpContext c,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            var error=await db.DecideIncidentTicket(id,Actor(c),body,ct);if(error is not null)return Error(error,409);
            broker.Publish("institutional-document-updated");return Results.Ok(new{ok=true,document=await db.InstitutionalDetail(id,Actor(c),ct)});
        }).RequireRateLimiting("sensitive-write");
        group.MapPost("/{id:long}/actions/{action}",async(long id,string action,InstitutionalAction body,HttpContext c,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            if(body.SignatureData is not null&&!OperationalPermissions.Allowed(c.User,"canSignDocuments"))return Results.Forbid();
            if(action is not ("receive" or "move" or "sign" or "approve" or "reject" or "correction" or "cancel" or "edit" or "share" or "unshare" or "trash" or "restore" or "purge" or "resend" or "send"))return Error("Acción no válida.");
            if(!double.IsFinite(body.SealX)||!double.IsFinite(body.SealY)||body.SealX is <2 or >98||body.SealY is <2 or >98)return Error("El sello debe quedar dentro de la página.");
            if(action=="edit"&&(string.IsNullOrWhiteSpace(body.Title)||body.Title.Length>160||(body.Description?.Length??0)>2000))return Error("Revisa el título y la descripción.");
            if(action is "approve" or "reject" or "correction" or "cancel"&&(string.IsNullOrWhiteSpace(body.Note)||body.Note.Trim().Length is <5 or >2000))return Error("Indica un motivo de 5 a 2,000 caracteres.");
            if((action=="sign"||(action=="receive"&&body.SignatureData is not null))&&!MaintenanceDocumentEndpoints.ValidSignature(body.SignatureData))return Error("Dibuja una firma PNG válida.");
            if(action=="share"&&!await db.InstitutionalRecipient(body.RecipientUserName,body.RecipientDepartment,ct))return Error("Selecciona un destinatario válido.");
            if(action=="send"&&!await db.InstitutionalRecipient(body.RecipientUserName,body.RecipientDepartment,ct))return Error("Selecciona un destinatario válido.");
            if(action=="approve")
            {
                var actor=Actor(c);
                var document=await db.InstitutionalDetail(id,actor,ct);
                if(document is null)return Results.NotFound(new{message="Documento no disponible."});
                if(actor.Department=="Almacén"&&!Permission(c,"canAuthorizeWarehouseRequests"))return Results.Forbid();
                if(document.WarehouseRequest is not null&&document.RecipientDepartment=="Almacén")
                {
                if(!actor.Manager||!Permission(c,"canAuthorizeWarehouseRequests"))return Results.Json(new{message="No tienes permiso para autorizar solicitudes de Almacén."},statusCode:403);
                var warehouseError=await db.WarehouseApprovePrepared(id,actor,new(body.Note!),ct);if(warehouseError is not null)return Error(warehouseError,409);
                broker.Publish("institutional-document-updated");return Results.Ok(new{ok=true,document=await db.InstitutionalDetail(id,actor,ct)});
                }
            }
            var error=await db.InstitutionalChange(id,Actor(c),action,body,ct);if(error is not null)return Error(error,409);broker.Publish("institutional-document-updated");return Results.Ok(new{ok=true});
        }).RequireRateLimiting("sensitive-write");
        // Verification remains authenticated and follows the same document access checks.
        group.MapGet("/verify/{token}",async(string token,HttpContext c,Database db,CancellationToken ct)=>{var a=Actor(c);var docs=(await db.InstitutionalList(a,"communications","requests",ct)).Concat(await db.InstitutionalList(a,"communications","requirements",ct));var d=docs.FirstOrDefault(x=>x.VerificationToken==token&&x.ReceivedUtc is not null&&!x.Trashed);return d is null?Results.NotFound():Results.Ok(new{valid=true,institution="REAL · Grupo Tejeda",d.Title,d.ReceiptCode,d.ReceivedUtc,d.ReceiverDisplayName,d.ReceiverLogin,d.Status,statusText=d.Status switch{"pending"=>"En tránsito","received"=>"Recibido","approved"=>"Autorizado","rejected"=>"Rechazado","correction_requested"=>"Corrección solicitada",_=>d.Status},steps=new[]{new{label="Enviado",complete=d.SentUtc is not null,at=d.SentUtc,actor=(string?)d.OwnerDisplayName},new{label="Recibido en destino",complete=d.ReceivedUtc is not null,at=d.ReceivedUtc,actor=d.ReceiverDisplayName},new{label=d.Status=="rejected"?"Rechazado":"Autorizado",complete=d.Status is "approved" or "rejected",at=d.DecisionUtc,actor=d.DecisionBy}},history=d.History.Select(h=>new{h.Action,h.Actor,h.Login,h.At,h.PreviousStatus,h.ReceiptCode,h.ReceiverLogin,h.ReceivedUtc})});});
        group.MapGet("/templates",async(HttpContext c,Database db,CancellationToken ct)=>Results.Ok(await db.InstitutionalTemplates(Actor(c),ct)));
        group.MapPost("/templates",async(EditableTemplateInput b,HttpContext c,Database db,CancellationToken ct)=>{if(string.IsNullOrWhiteSpace(b.Name)||b.Name.Length>160||string.IsNullOrWhiteSpace(b.Content)||b.Content.Length>100000)return Error("Nombre de hasta 160 caracteres y contenido de hasta 100,000.");try{using var parsed=JsonDocument.Parse(b.Content);if(parsed.RootElement.ValueKind!=JsonValueKind.Object)return Error("Plantilla no válida.");}catch(JsonException){return Error("Plantilla no válida.");}var a=Actor(c);var id=await db.InstitutionalSaveTemplate(a,b.Id,b.Name.Trim(),b.Content,ct);return id is null?Results.Forbid():Results.Ok(new InstitutionalTemplate(id.Value,b.Name.Trim(),b.Content,a.Department,a.Id,false,DateTime.UtcNow));}).RequireRateLimiting("sensitive-write");
        group.MapDelete("/templates/{id:long}",async(long id,HttpContext c,Database db,CancellationToken ct)=>await db.InstitutionalDeleteTemplate(Actor(c),id,ct)?Results.NoContent():Results.NotFound()).RequireRateLimiting("sensitive-write");
    }
}
record EditableTemplateInput(long? Id,string Name,string Content);
