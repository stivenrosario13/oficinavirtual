using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

// Adapted documentary workflow. Uses the application's SQL connection and authenticated identities.
record DocumentActor(string Id,string Role,string Team,string Name,string Login)
{
    public string Department => Team=="WAREHOUSE"?"Almacén":Role switch {"Technology"=>"Tecnología","GeneralServices"=>"Servicios Generales","HumanResources"=>"Recursos Humanos",_=>"Administración"};
    public bool Manager => Role=="Administrator"||Team=="WAREHOUSE";
    public bool Allowed => Role is "Administrator" or "Technology" or "GeneralServices" or "HumanResources";
}
record DocumentAccess(long Id,string? RecipientUserName,string? RecipientDepartment,bool CanEdit,DateTime SharedUtc);
record InstitutionalEvent(string Action,string Actor,string Login,DateTime At,string PreviousStatus,string? ReceiptCode,string? ReceiverLogin,DateTime? ReceivedUtc,string? PreviousReceiptSignature);
sealed class InstitutionalFile
{
    public long Id {get;set;}
    public string Area {get;set;}="files";
    public string Channel {get;set;}="requests";
    public string Title {get;set;}="";
    public string Description {get;set;}="";
    public string OriginalFileName {get;set;}="";
    public string ContentType {get;set;}="application/octet-stream";
    public int Size {get;set;}
    public string OwnerUserName {get;set;}="";
    public string OwnerDisplayName {get;set;}="";
    public string OwnerLogin {get;set;}="";
    public string Department {get;set;}="";
    public string? RecipientUserName {get;set;}
    public string? RecipientDepartment {get;set;}
    public string Status {get;set;}="active";
    public bool Trashed {get;set;}
    public DateTime CreatedUtc {get;set;}=DateTime.UtcNow;
    public DateTime? SentUtc {get;set;}
    public DateTime? ReceivedUtc {get;set;}
    public string? ReceivedByUserName {get;set;}
    public string? ReceiverDisplayName {get;set;}
    public string? ReceiverLogin {get;set;}
    public string? ReceiptCode {get;set;}
    public string? VerificationToken {get;set;}
    // Links an archived PDF back to the editable template that produced it.
    // This lets saving a template update its communication PDF instead of
    // creating an ever-growing list of duplicate drafts.
    public long? TemplateId {get;set;}
    public double SealX {get;set;}=65;
    public double SealY {get;set;}=72;
    public string? ReceiptSignature {get;set;}
    public string? CorrectionNote {get;set;}
    public string? DecisionBy {get;set;}
    public DateTime? DecisionUtc {get;set;}
    public List<DocumentAccess> SharedWith {get;set;}=[];
    public List<InstitutionalEvent> History {get;set;}=[];
    public bool IsOwner {get;set;}
    public bool IsRecipient {get;set;}
    public bool CanEdit {get;set;}
    public bool CanMove {get;set;}
    public string FileUrl => $"/api/institutional-documents/{Id}/preview";
    public bool Recipient(DocumentActor a)=>OwnerUserName!=a.Id&&(RecipientUserName is not null?RecipientUserName==a.Id:RecipientDepartment==a.Department);
    public bool Shared(DocumentActor a)=>SharedWith.Any(s=>s.RecipientUserName is not null?s.RecipientUserName==a.Id:s.RecipientDepartment==a.Department);
    public bool Accessible(DocumentActor a)=>a.Allowed&&(Trashed?OwnerUserName==a.Id:a.Manager||OwnerUserName==a.Id||Recipient(a)||Shared(a)||(Area=="files"&&Department==a.Department));
    public InstitutionalFile For(DocumentActor a){IsOwner=OwnerUserName==a.Id;IsRecipient=Recipient(a);CanMove=ReceivedByUserName==a.Id;CanEdit=!Trashed&&ReceivedUtc is null&&(IsOwner||SharedWith.Any(s=>s.CanEdit&&(s.RecipientUserName is not null?s.RecipientUserName==a.Id:s.RecipientDepartment==a.Department)));return this;}
}
record InstitutionalTemplate(long Id,string Name,string Content,string Department,string OwnerUserName,bool IsSystem,DateTime UpdatedUtc);
partial class Database
{
    static readonly JsonSerializerOptions InstitutionalJson=new(JsonSerializerDefaults.Web);
    static readonly string[] InstitutionalDepartments=["Almacén","Tecnología","Servicios Generales","Recursos Humanos","Administración","Taller"];
    static readonly SemaphoreSlim InstitutionalSchemaLock=new(1,1);
    static bool institutionalSchemaReady;
    static async Task EnsureInstitutionalSchema(SqlConnection c,CancellationToken ct)
    {
        if(institutionalSchemaReady)return;await InstitutionalSchemaLock.WaitAsync(ct);
        try{if(institutionalSchemaReady)return;await using var cmd=new SqlCommand("""
        IF OBJECT_ID(N'dbo.institutional_files',N'U') IS NULL
        BEGIN
          CREATE TABLE dbo.institutional_files(id bigint IDENTITY PRIMARY KEY,owner_id nvarchar(80) NOT NULL,department nvarchar(80) NOT NULL,area varchar(20) NOT NULL,channel varchar(20) NOT NULL,metadata nvarchar(max) NOT NULL CHECK(ISJSON(metadata)=1),file_data varbinary(max) NOT NULL);
          CREATE INDEX IX_institutional_files_scope ON dbo.institutional_files(area,channel,owner_id);
        END;
        IF OBJECT_ID(N'dbo.institutional_editable_templates',N'U') IS NULL
          CREATE TABLE dbo.institutional_editable_templates(id bigint IDENTITY PRIMARY KEY,owner_id nvarchar(80) NOT NULL,department nvarchar(80) NOT NULL,name nvarchar(160) NOT NULL,content nvarchar(max) NOT NULL,updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
        """,c);await cmd.ExecuteNonQueryAsync(ct);institutionalSchemaReady=true;}finally{InstitutionalSchemaLock.Release();}
    }
    public async Task<List<InstitutionalFile>> InstitutionalList(DocumentActor a,string area,string channel,CancellationToken ct)
    {
        if(!a.Allowed)return [];await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);
        await using var cmd=new SqlCommand("SELECT id,metadata FROM dbo.institutional_files WHERE area=@area AND (@area='files' OR @channel='all' OR channel=@channel) AND (owner_id=@user OR @manager=1 OR (area='files' AND department=@dept) OR JSON_VALUE(metadata,'$.recipientUserName')=@user OR (JSON_VALUE(metadata,'$.recipientUserName') IS NULL AND JSON_VALUE(metadata,'$.recipientDepartment')=@dept) OR EXISTS(SELECT 1 FROM OPENJSON(metadata,'$.sharedWith') WITH(recipientUserName nvarchar(80),recipientDepartment nvarchar(80)) s WHERE s.recipientUserName=@user OR (s.recipientUserName IS NULL AND s.recipientDepartment=@dept))) ORDER BY id DESC",c);
        cmd.Parameters.AddWithValue("@area",area);cmd.Parameters.AddWithValue("@channel",channel);cmd.Parameters.AddWithValue("@user",a.Id);cmd.Parameters.AddWithValue("@manager",a.Manager);cmd.Parameters.AddWithValue("@dept",a.Department);
        var list=new List<InstitutionalFile>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)){var d=JsonSerializer.Deserialize<InstitutionalFile>(r.GetString(1),InstitutionalJson)!;d.Id=r.GetInt64(0);if(d.Accessible(a))list.Add(d.For(a));}return list;
    }
    public async Task<InstitutionalFile?> InstitutionalDetail(long id,DocumentActor a,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id=@id",c);cmd.Parameters.AddWithValue("@id",id);var json=await cmd.ExecuteScalarAsync(ct) as string;if(json is null)return null;var d=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;d.Id=id;return d.Accessible(a)?d.For(a):null;
    }
    public async Task<(InstitutionalFile Document,byte[] Data)?> InstitutionalBytes(long id,DocumentActor a,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand("SELECT metadata,file_data FROM dbo.institutional_files WHERE id=@id",c);cmd.Parameters.AddWithValue("@id",id);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;var d=JsonSerializer.Deserialize<InstitutionalFile>(r.GetString(0),InstitutionalJson)!;d.Id=id;return d.Accessible(a)?(d.For(a),(byte[])r[1]):null;
    }
    public async Task<long> InstitutionalUpload(InstitutionalFile d,byte[] data,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand("INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES(@owner,@dept,@area,@channel,@json,@bytes)",c);cmd.Parameters.AddWithValue("@owner",d.OwnerUserName);cmd.Parameters.AddWithValue("@dept",d.Department);cmd.Parameters.AddWithValue("@area",d.Area);cmd.Parameters.AddWithValue("@channel",d.Channel);cmd.Parameters.AddWithValue("@json",JsonSerializer.Serialize(d,InstitutionalJson));cmd.Parameters.AddWithValue("@bytes",data);return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }
    public async Task<long> InstitutionalArchiveTemplatePdf(DocumentActor a,long templateId,InstitutionalFile d,byte[] data,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);
        await using var find=new SqlCommand("SELECT TOP(1) id,metadata FROM dbo.institutional_files WHERE owner_id=@owner AND area='communications' AND channel='requests' AND JSON_VALUE(metadata,'$.templateId')=@template ORDER BY id DESC",c);
        find.Parameters.AddWithValue("@owner",a.Id);find.Parameters.AddWithValue("@template",templateId.ToString());
        await using var reader=await find.ExecuteReaderAsync(ct);long? existing=null;DateTime? created=null;
        if(await reader.ReadAsync(ct)){existing=reader.GetInt64(0);try{var previous=JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1),InstitutionalJson);created=previous?.CreatedUtc;}catch(JsonException){}}
        await reader.DisposeAsync();
        d.TemplateId=templateId;d.Id=existing??0;d.CreatedUtc=created??DateTime.UtcNow;
        if(existing is long id)
        {
            await using var update=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json,file_data=@bytes WHERE id=@id AND owner_id=@owner",c);
            update.Parameters.AddWithValue("@id",id);update.Parameters.AddWithValue("@owner",a.Id);update.Parameters.AddWithValue("@json",JsonSerializer.Serialize(d,InstitutionalJson));update.Parameters.AddWithValue("@bytes",data);await update.ExecuteNonQueryAsync(ct);return id;
        }
        await using var insert=new SqlCommand("INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES(@owner,@dept,'communications','requests',@json,@bytes)",c);
        insert.Parameters.AddWithValue("@owner",a.Id);insert.Parameters.AddWithValue("@dept",a.Department);insert.Parameters.AddWithValue("@json",JsonSerializer.Serialize(d,InstitutionalJson));insert.Parameters.AddWithValue("@bytes",data);return Convert.ToInt64(await insert.ExecuteScalarAsync(ct));
    }
    // All receipt, sharing and state changes are serialized per document. Restoring preserves its workflow status.
    public async Task<string?> InstitutionalChange(long id,DocumentActor a,string action,InstitutionalAction b,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var read=new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",c,tx);read.Parameters.AddWithValue("@id",id);var json=await read.ExecuteScalarAsync(ct) as string;if(json is null)return "Documento no disponible.";var d=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;d.Id=id;if(!d.Accessible(a))return "No tienes acceso a este documento.";d.For(a);
        d.History.Add(new(action,a.Name,a.Login,DateTime.UtcNow,d.Status,d.ReceiptCode,d.ReceiverLogin,d.ReceivedUtc,action=="resend"?d.ReceiptSignature:null));
        if(action=="trash"||action=="restore"||action=="purge"){if(!d.IsOwner)return "Solo el propietario administra su papelera.";if(action=="purge"&&!d.Trashed)return "Envía primero el archivo a la papelera.";d.Trashed=action!="restore";}
        else if(d.Trashed)return "Restaura el documento antes de modificarlo.";
        else if(action=="edit"){if(!d.CanEdit)return "No puedes modificar este documento.";d.Title=b.Title!.Trim();d.Description=b.Description?.Trim()??"";}
        else if(action=="share"||action=="unshare")
        {
            if(!d.IsOwner)return "Solo el propietario puede compartir o revocar accesos.";
            if(action=="unshare")d.SharedWith.RemoveAll(s=>s.Id==b.ShareId);
            else{d.SharedWith.RemoveAll(s=>s.RecipientUserName==b.RecipientUserName&&s.RecipientDepartment==b.RecipientDepartment);d.SharedWith.Add(new(Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),d.SharedWith.Select(s=>s.Id).DefaultIfEmpty(0).Max()+1),b.RecipientUserName,b.RecipientDepartment,b.CanEdit,DateTime.UtcNow));}
        }
        else if(action=="move") {if(!d.CanMove||d.ReceivedUtc is null)return "Solo quien recibió puede mover su sello.";d.SealX=b.SealX;d.SealY=b.SealY;}
        else if(action=="sign") {if(!d.CanMove||d.ReceivedUtc is null)return "Primero recibe el documento desde tu cuenta.";d.ReceiptSignature=b.SignatureData;}
        else if(action=="resend") {if(!d.IsOwner||d.Status is not ("rejected" or "correction_requested"))return "No admite reenvío.";d.Status="pending";d.SentUtc=DateTime.UtcNow;d.ReceivedUtc=null;d.ReceiptSignature=null;d.ReceivedByUserName=null;d.ReceiverDisplayName=null;d.ReceiverLogin=null;d.ReceiptCode=null;d.VerificationToken=null;}
        else if(action=="send") {if(!d.IsOwner||d.Area!="communications"||d.Status!="draft"||string.IsNullOrWhiteSpace(b.RecipientDepartment))return "Selecciona un departamento para enviar el borrador.";d.RecipientDepartment=b.RecipientDepartment.Trim();d.RecipientUserName=string.IsNullOrWhiteSpace(b.RecipientUserName)?null:b.RecipientUserName.Trim();d.Status="pending";d.SentUtc=DateTime.UtcNow;}
        else
        {
            if(d.Area!="communications"||!d.IsRecipient)return "Solo el destinatario puede responder.";
            if(action=="approve") {if(d.Channel!="requirements"||!a.Manager||d.Status is not ("pending" or "received" or "correction_requested"))return "Solo almacén puede autorizar un requerimiento pendiente.";d.Status="approved";d.DecisionBy=a.Name+" · "+a.Login;d.DecisionUtc=DateTime.UtcNow;d.CorrectionNote=b.Note;}
            else if(action=="receive"){if(d.ReceivedUtc is not null||d.Status is not ("pending" or "correction_requested" or "approved"))return "La recepción ya fue registrada o no está pendiente.";d.Status=d.Channel=="requests"&&d.RecipientDepartment=="Almacén"?"approved":d.Status=="approved"?"approved":"received";d.DecisionBy=d.Status=="approved"?a.Name+" · "+a.Login:d.DecisionBy;d.DecisionUtc=d.Status=="approved"?DateTime.UtcNow:d.DecisionUtc;d.CorrectionNote=d.Status=="approved"?"Recepción autorizada automáticamente por Almacén General.":d.CorrectionNote;d.ReceivedUtc=DateTime.UtcNow;d.ReceivedByUserName=a.Id;d.ReceiverDisplayName=a.Name;d.ReceiverLogin=a.Login;d.ReceiptCode=$"REAL-REC-{DateTime.UtcNow:yyyyMMdd}-{id:D6}";d.VerificationToken=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();d.SealX=b.SealX;d.SealY=b.SealY;}
            else if(action is "correction" or "reject") {if(d.Status is not ("pending" or "correction_requested" or "received"))return "La comunicación ya tiene una decisión.";d.Status=action=="reject"?"rejected":"correction_requested";d.CorrectionNote=b.Note;d.DecisionBy=a.Name+" · "+a.Login;d.DecisionUtc=DateTime.UtcNow;}
            else return "Acción desconocida.";
        }
        await using var save=new SqlCommand(action=="purge"?"DELETE FROM dbo.institutional_files WHERE id=@id":"UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id",c,tx);save.Parameters.AddWithValue("@id",id);save.Parameters.AddWithValue("@json",JsonSerializer.Serialize(d,InstitutionalJson));await save.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return null;
    }
    public async Task<object> InstitutionalOverview(DocumentActor a,string area,string channel,CancellationToken ct){var docs=(await InstitutionalList(a,area,channel,ct)).Where(d=>!d.Trashed).ToList();return new{total=docs.Count,mine=docs.Count(d=>d.IsOwner),shared=docs.Count(d=>!d.IsOwner&&d.Shared(a)),pending=docs.Count(d=>d.Status is "pending" or "correction_requested"),received=docs.Count(d=>d.Status=="received"||(d.Status=="approved"&&d.ReceivedUtc is not null)),sent=docs.Count(d=>d.IsOwner&&d.SentUtc is not null),rejected=docs.Count(d=>d.Status=="rejected"),departments=InstitutionalDepartments.Select(name=>new{name,count=docs.Count(d=>d.Department==name)})};}
    public async Task<string> InstitutionalUsers(CancellationToken ct){await using var c=await Open(ct);await using var cmd=new SqlCommand("SELECT id userName,display_name fullName,email,CASE WHEN support_team='WAREHOUSE' THEN N'Almacén' WHEN role='TECHNOLOGY' THEN N'Tecnología' WHEN role='GENERAL_SERVICES' THEN N'Servicios Generales' WHEN role='HUMAN_RESOURCES' THEN N'Recursos Humanos' ELSE N'Administración' END department FROM dbo.admin_users WHERE is_active=1 AND role IN('ADMINISTRATOR','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') ORDER BY display_name FOR JSON PATH",c);var json=new System.Text.StringBuilder();await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))json.Append(reader.GetString(0));return json.Length==0?"[]":json.ToString();}
    public async Task<bool> InstitutionalRecipient(string? user,string? department,CancellationToken ct){if(!InstitutionalDepartments.Contains(department))return false;if(user is null)return true;using var list=JsonDocument.Parse(await InstitutionalUsers(ct));return list.RootElement.EnumerateArray().Any(x=>x.GetProperty("userName").GetString()==user&&x.GetProperty("department").GetString()==department);}
    public async Task<List<InstitutionalTemplate>> InstitutionalTemplates(DocumentActor a,CancellationToken ct){await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand("SELECT id,name,content,department,owner_id,updated_at FROM dbo.institutional_editable_templates WHERE owner_id=@user OR department=@dept ORDER BY updated_at DESC",c);cmd.Parameters.AddWithValue("@user",a.Id);cmd.Parameters.AddWithValue("@dept",a.Department);var list=new List<InstitutionalTemplate>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))list.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),false,DateTime.SpecifyKind(r.GetDateTime(5),DateTimeKind.Utc)));return list;}
    public async Task<long?> InstitutionalSaveTemplate(DocumentActor a,long? id,string name,string content,CancellationToken ct){await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand(id is >0?"UPDATE dbo.institutional_editable_templates SET name=@name,content=@content,updated_at=SYSUTCDATETIME() OUTPUT INSERTED.id WHERE id=@id AND owner_id=@user":"INSERT INTO dbo.institutional_editable_templates(owner_id,department,name,content) OUTPUT INSERTED.id VALUES(@user,@dept,@name,@content)",c);cmd.Parameters.AddWithValue("@id",id??0);cmd.Parameters.AddWithValue("@user",a.Id);cmd.Parameters.AddWithValue("@dept",a.Department);cmd.Parameters.AddWithValue("@name",name);cmd.Parameters.AddWithValue("@content",content);var value=await cmd.ExecuteScalarAsync(ct);return value is null?null:Convert.ToInt64(value);}
    public async Task<bool> InstitutionalDeleteTemplate(DocumentActor a,long id,CancellationToken ct){await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand("DELETE FROM dbo.institutional_editable_templates WHERE id=@id AND owner_id=@user",c);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@user",a.Id);return await cmd.ExecuteNonQueryAsync(ct)==1;}
}
record InstitutionalAction(string? Title=null,string? Description=null,string? Note=null,string? RecipientUserName=null,string? RecipientDepartment=null,bool CanEdit=false,long ShareId=0,double SealX=65,double SealY=72,string? SignatureData=null);
