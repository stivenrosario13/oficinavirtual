using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Data.SqlClient;

record AdministrativeAsset(Guid Id,string Kind,string Label,string Identifier,string Status,string? HolderType,string? HolderName,string? AgencyCode,string? Notes,DateTime CreatedUtc,DateTime UpdatedUtc,long? RequestId);
record AdministrativeAssetInput(string Kind,string Label,string Identifier,string Status,string? HolderType,string? HolderName,string? AgencyCode,string? Notes);
record AdministrativeDeliveryInput(Guid? AssetId,string ReceivedBy,List<Guid>? AssetIds=null,string? SealQr=null);

static class AdministrativeAssetEndpoints
{
    static IResult Error(string message,int code=422)=>Results.Json(new{message},statusCode:code);
    static DocumentActor Actor(HttpContext c)=>new(c.User.FindFirstValue(ClaimTypes.NameIdentifier)??"", c.User.FindFirstValue(ClaimTypes.Role)??"", c.User.FindFirstValue("support_team")??"",c.User.FindFirstValue("display_name")??"Administrador",c.User.Identity?.Name??"");
    public static void MapAdministrativeAssetEndpoints(this WebApplication app)
    {
        var group=app.MapGroup("/api/administrative-assets").RequireAuthorization("PortalUser");
        group.AddEndpointFilter(async(context,next)=>(context.HttpContext.User.IsInRole("Administrator")||context.HttpContext.User.IsInRole("Administrative")||context.HttpContext.User.IsInRole("Department")&&AreaManagerAccess.Allowed(context.HttpContext.User))?await next(context):Results.Forbid());
        group.MapGet("",async(HttpContext c,Database db,CancellationToken ct)=>Results.Ok((await db.AdministrativeAssets(ct,Actor(c).Department)).Where(asset=>CanUseAsset(Actor(c),asset.Kind))));
        group.MapPost("",async(AdministrativeAssetInput input,HttpContext c,Database db,CancellationToken ct)=>{
            if(!CanUseAsset(Actor(c),input.Kind))return Results.Forbid();
            var error=Validate(input);if(error is not null)return Error(error);
            var saved=await db.SaveAdministrativeAsset(null,input,ct,Actor(c).Department);return saved is null?Error("El identificador ya está registrado.",409):Results.Ok(saved);
        }).RequireRateLimiting("sensitive-write");
        group.MapPut("/{id:guid}",async(Guid id,AdministrativeAssetInput input,HttpContext c,Database db,CancellationToken ct)=>{
            var old=(await db.AdministrativeAssets(ct,Actor(c).Department)).FirstOrDefault(a=>a.Id==id);
            if(old is null||!CanUseAsset(Actor(c),old.Kind)||!CanUseAsset(Actor(c),input.Kind))return Results.Forbid();
            var error=Validate(input);if(error is not null)return Error(error);
            var saved=await db.SaveAdministrativeAsset(id,input,ct,Actor(c).Department);return saved is null?Error("El activo no existe o su identificador ya está registrado.",409):Results.Ok(saved);
        }).RequireRateLimiting("sensitive-write");
        group.MapPost("/requests/{id:long}/deliver",async(long id,AdministrativeDeliveryInput input,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            if(string.IsNullOrWhiteSpace(input.ReceivedBy)||input.ReceivedBy.Trim().Length is <3 or >160)return Error("Indica quién recibe el activo o documento.");
            if(string.IsNullOrWhiteSpace(input.SealQr)||input.SealQr.Length>2048)return Error("Escanea el QR del sello AUTORIZADO A RETIRAR de esta solicitud.");
            var error=await db.DeliverAdministrativeRequest(id,Actor(context),input,ct);
            if(error is not null)return Error(error,409);broker.Publish("institutional-document-updated");return Results.Ok(new{ok=true});
        }).RequireRateLimiting("sensitive-write");
    }
    static bool CanUseAsset(DocumentActor actor,string kind){var forms=AdministrativeProfiles.Forms(actor.Login);return forms is null?actor.Role is "Administrator" or "Department":forms.Contains(kind=="FLOTA"?"FLEET":"MODEM");}
    static string? Validate(AdministrativeAssetInput input)
    {
        if(input.Kind is not ("FLOTA" or "MODEM")||string.IsNullOrWhiteSpace(input.Label)||input.Label.Length>160||string.IsNullOrWhiteSpace(input.Identifier)||input.Identifier.Length>100||input.Status is not ("DISPONIBLE" or "ASIGNADO" or "MANTENIMIENTO" or "BAJA"))return "Completa el tipo, nombre, placa o serial y estado del activo.";
        if(input.HolderType is not (null or "AGENCIA" or "EMPLEADO")||(input.HolderName?.Length??0)>160||(input.AgencyCode?.Length??0)>40||(input.Notes?.Length??0)>2000)return "Revisa los datos de custodia del activo.";
        if(input.Status=="ASIGNADO"&&(string.IsNullOrWhiteSpace(input.HolderType)||string.IsNullOrWhiteSpace(input.HolderName)))return "Una asignación requiere agencia o empleado responsable.";
        return null;
    }
}

partial class Database
{
    static readonly SemaphoreSlim AdministrativeAssetSchemaLock=new(1,1);
    static bool administrativeAssetSchemaReady;
    static async Task EnsureAdministrativeAssetSchema(SqlConnection c,CancellationToken ct)
    {
        if(administrativeAssetSchemaReady)return;await AdministrativeAssetSchemaLock.WaitAsync(ct);
        try{if(administrativeAssetSchemaReady)return;await using var cmd=new SqlCommand("""
        IF OBJECT_ID(N'dbo.administrative_assets',N'U') IS NULL
        BEGIN
          CREATE TABLE dbo.administrative_assets(id uniqueidentifier NOT NULL PRIMARY KEY,kind varchar(12) NOT NULL,label nvarchar(160) NOT NULL,identifier nvarchar(100) NOT NULL,status varchar(20) NOT NULL,holder_type varchar(12) NULL,holder_name nvarchar(160) NULL,agency_code nvarchar(40) NULL,notes nvarchar(2000) NULL,created_utc datetime2 NOT NULL,updated_utc datetime2 NOT NULL,request_id bigint NULL);
          CREATE UNIQUE INDEX UX_administrative_assets_identifier ON dbo.administrative_assets(identifier);
          CREATE INDEX IX_administrative_assets_kind_status ON dbo.administrative_assets(kind,status);
        END
        IF COL_LENGTH(N'dbo.administrative_assets',N'department') IS NULL
          EXEC(N'ALTER TABLE dbo.administrative_assets ADD department nvarchar(80) NOT NULL CONSTRAINT DF_administrative_asset_department DEFAULT N''Administración'';');
        """,c);await cmd.ExecuteNonQueryAsync(ct);administrativeAssetSchemaReady=true;}finally{AdministrativeAssetSchemaLock.Release();}
    }
    static AdministrativeAsset ReadAdministrativeAsset(SqlDataReader r)=>new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.IsDBNull(5)?null:r.GetString(5),r.IsDBNull(6)?null:r.GetString(6),r.IsDBNull(7)?null:r.GetString(7),r.IsDBNull(8)?null:r.GetString(8),r.GetDateTime(9),r.GetDateTime(10),r.IsDBNull(11)?null:r.GetInt64(11));
    public async Task<List<AdministrativeAsset>> AdministrativeAssets(CancellationToken ct,string department="Administración")
    {
        await using var c=await Open(ct);await EnsureAdministrativeAssetSchema(c,ct);await using var cmd=new SqlCommand("SELECT id,kind,label,identifier,status,holder_type,holder_name,agency_code,notes,created_utc,updated_utc,request_id FROM dbo.administrative_assets WHERE department=@department ORDER BY kind,label",c);cmd.Parameters.AddWithValue("@department",department);var rows=new List<AdministrativeAsset>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))rows.Add(ReadAdministrativeAsset(r));return rows;
    }
    public async Task<AdministrativeAsset?> SaveAdministrativeAsset(Guid? id,AdministrativeAssetInput input,CancellationToken ct,string department="Administración")
    {
        await using var c=await Open(ct);await EnsureAdministrativeAssetSchema(c,ct);var key=id??Guid.NewGuid();
        await using var cmd=new SqlCommand(id is null?"INSERT INTO dbo.administrative_assets(id,kind,label,identifier,status,holder_type,holder_name,agency_code,notes,created_utc,updated_utc,department) VALUES(@id,@kind,@label,@identifier,@status,@holderType,@holderName,@agencyCode,@notes,SYSUTCDATETIME(),SYSUTCDATETIME(),@department)":"UPDATE dbo.administrative_assets SET kind=@kind,label=@label,identifier=@identifier,status=@status,holder_type=@holderType,holder_name=@holderName,agency_code=@agencyCode,notes=@notes,updated_utc=SYSUTCDATETIME() WHERE id=@id AND department=@department",c);
        cmd.Parameters.AddWithValue("@department",department);cmd.Parameters.AddWithValue("@id",key);cmd.Parameters.AddWithValue("@kind",input.Kind);cmd.Parameters.AddWithValue("@label",input.Label.Trim());cmd.Parameters.AddWithValue("@identifier",input.Identifier.Trim());cmd.Parameters.AddWithValue("@status",input.Status);cmd.Parameters.AddWithValue("@holderType",(object?)input.HolderType??DBNull.Value);cmd.Parameters.AddWithValue("@holderName",(object?)input.HolderName?.Trim()??DBNull.Value);cmd.Parameters.AddWithValue("@agencyCode",(object?)input.AgencyCode?.Trim()??DBNull.Value);cmd.Parameters.AddWithValue("@notes",(object?)input.Notes?.Trim()??DBNull.Value);
        try{if(await cmd.ExecuteNonQueryAsync(ct)==0)return null;}catch(SqlException ex) when(ex.Number is 2601 or 2627){return null;}
        await using var read=new SqlCommand("SELECT id,kind,label,identifier,status,holder_type,holder_name,agency_code,notes,created_utc,updated_utc,request_id FROM dbo.administrative_assets WHERE id=@id",c);read.Parameters.AddWithValue("@id",key);await using var r=await read.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?ReadAdministrativeAsset(r):null;
    }
    public async Task<string?> DeliverAdministrativeRequest(long id,DocumentActor actor,AdministrativeDeliveryInput input,CancellationToken ct)
    {
        if(actor.Role is not ("Administrator" or "Administrative"))return "Solo Administración puede registrar esta entrega.";
        var token=string.IsNullOrWhiteSpace(input.SealQr)?null:WarehouseQrToken(input.SealQr);
        var number=AdministrativeDeliveryQr.RequestNumber(input.SealQr);
        if(token is null&&number is null)return "Escanea completo el QR del formulario o del sello AUTORIZADO A RETIRAR de esta solicitud.";
        if(string.IsNullOrWhiteSpace(input.ReceivedBy)||input.ReceivedBy.Trim().Length is <3 or >160)return "Indica quién recibe el activo o documento.";
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await EnsureAdministrativeAssetSchema(c,ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var read=new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",c,tx);read.Parameters.AddWithValue("@id",id);var json=await read.ExecuteScalarAsync(ct) as string;if(json is null)return "Solicitud no disponible.";
        var d=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;d.Id=id;if(!d.Accessible(actor))return "No tienes acceso a este formulario.";if(d.AdministrativeRequest is null||d.RecipientDepartment!="Administración"||d.Channel!="requirements"||d.Status!="approved"||d.AdministrativeDelivery is not null||d.Trashed)return "Autoriza primero la solicitud administrativa pendiente de entrega.";
        if(d.AdministrativeRequest.FormType=="INCIDENT_REPORT")return "Los informes de incidentes no requieren entrega de activos.";
        if(!(token is not null&&string.Equals(token,d.VerificationToken,StringComparison.OrdinalIgnoreCase))&&!(number is not null&&string.Equals(number,d.AdministrativeRequest.Number,StringComparison.Ordinal)))return "El QR escaneado no corresponde a esta solicitud. Escanea su sello AUTORIZADO A RETIRAR.";
        var internet=d.AdministrativeRequest?.Internet;
        var requiredKind=d.AdministrativeRequest?.AssetType is "FLOTA" or "MODEM"?d.AdministrativeRequest.AssetType:internet?.ItemKind=="EQUIPO"?"MODEM":null;
        var requiredCount=requiredKind is null?0:internet?.ItemKind=="EQUIPO"?internet.ModemQuantity:1;
        var requestedIds=(input.AssetIds??[]).Concat(input.AssetId is Guid single?[single]:[]).ToList();
        if(requestedIds.Count!=requiredCount||requestedIds.Distinct().Count()!=requestedIds.Count)return requiredCount==0?"Esta solicitud no requiere activos de inventario.":$"Selecciona exactamente {requiredCount} unidad(es) disponible(s) del inventario.";
        var identifiers=new List<string>();
        foreach(var assetId in requestedIds.OrderBy(value=>value))
        {
            await using var asset=new SqlCommand("SELECT kind,identifier,status FROM dbo.administrative_assets WITH(UPDLOCK,HOLDLOCK) WHERE id=@asset AND department=N'Administración'",c,tx);asset.Parameters.AddWithValue("@asset",assetId);await using var r=await asset.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return "Uno de los activos no existe.";var kind=r.GetString(0);var identifier=r.GetString(1);var status=r.GetString(2);await r.DisposeAsync();if(kind!=requiredKind||status!="DISPONIBLE")return "Selecciona activos disponibles del tipo solicitado.";identifiers.Add(identifier);
            await using var assign=new SqlCommand("UPDATE dbo.administrative_assets SET status='ASIGNADO',holder_type=@type,holder_name=@name,agency_code=@agency,request_id=@request,updated_utc=SYSUTCDATETIME() WHERE id=@asset AND department=N'Administración'",c,tx);assign.Parameters.AddWithValue("@type",d.AdministrativeRequest!.TargetType);assign.Parameters.AddWithValue("@name",d.AdministrativeRequest.TargetName);assign.Parameters.AddWithValue("@agency",(object?)d.AdministrativeRequest.AgencyCode??DBNull.Value);assign.Parameters.AddWithValue("@request",id);assign.Parameters.AddWithValue("@asset",assetId);await assign.ExecuteNonQueryAsync(ct);
        }
        var now=DateTime.UtcNow;d.AdministrativeDelivery=new(d.AdministrativeRequest?.Number??$"ADM-{id:D6}",actor.Name,actor.Login,input.ReceivedBy.Trim(),now,requestedIds.FirstOrDefault()==Guid.Empty?null:requestedIds.FirstOrDefault(),identifiers.FirstOrDefault(),identifiers);d.History.Add(new("deliver",actor.Name,actor.Login,now,d.Status,d.ReceiptCode,d.ReceiverLogin,d.ReceivedUtc,null));
        await using var save=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id",c,tx);save.Parameters.AddWithValue("@id",id);save.Parameters.AddWithValue("@json",JsonSerializer.Serialize(d,InstitutionalJson));await save.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return null;
    }
}
