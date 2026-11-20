using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;

// Adapted documentary workflow. Uses the application's SQL connection and authenticated identities.
record DocumentActor(string Id,string Role,string Team,string Name,string Login,string? AssignedDepartment=null,bool? InventoryDelete=null,bool WarehouseRequestOnly=false,bool WarehouseAuthorize=false)
{
    public string Department => AssignedDepartment ?? DepartmentAccounts.AccountDepartment(Role,Team);
    public bool Manager => Role is "Administrator" or "Warehouse"||Team=="WAREHOUSE";
    public bool WarehouseRequestsBlocked => !Allowed;
    public bool Allowed => Role is "Department" or "Administrative" or "Administrator" or "GroupAdministrator" or "Fiscalizador" or "Viewer" or "Technology" or "GeneralServices" or "HumanResources" or "Warehouse";
    public bool CanDeleteInventory => (Role=="Warehouse"||Team=="WAREHOUSE")&&(InventoryDelete??new[]{"diorly.abreu@grupotejeda.local","bryan.arias@grupotejeda.local"}.Contains(Login.Trim().ToLowerInvariant()));
    public bool CanReadTechnicalFailureWarehouseTeam => Role=="Technology"&&Team=="TECHNICAL_FAILURE"&&AdministrativeProfiles.Login(Login)=="rosmarlin.vittini";
}
record DocumentAccess(long Id,string? RecipientUserName,string? RecipientDepartment,bool CanEdit,DateTime SharedUtc);
record InstitutionalEvent(string Action,string Actor,string Login,DateTime At,string PreviousStatus,string? ReceiptCode,string? ReceiverLogin,DateTime? ReceivedUtc,string? PreviousReceiptSignature,AdministrativeRequestInfo? PreviousAdministrativeRequest=null,WarehouseRequestInfo? PreviousWarehouseRequest=null,string? Note=null);
record WarehouseRequestLine(string Kind,string Name,int Quantity,string? Serial,string? Note,string? ProductId=null);
record WarehouseRequestInfo(string Number,Guid AgencyId,string AgencyCode,string AgencyName,string AgencyGroup,string InstallerId,string InstallerName,string Priority,List<WarehouseRequestLine> Lines,Guid? TicketId=null,long? TicketNumber=null,string? DestinationType=null,string? FailureDepartment=null,string? FailureCategory=null,string? FailureDescription=null,string? FailureTeam=null,WeekendLoanPeriod? WeekendLoan=null);
record WarehouseAssignedEquipment(int LineIndex,string Name,string Serial,int Quantity,string? ProductId=null);
record WarehouseDeliveryInfo(string Number,string DeliveredBy,string DeliveredLogin,string ReceivedBy,DateTime At);
record WarehouseInstallationEvidence(Guid Id,string FileName,string ContentType,DateTime CapturedAt);
record WarehouseInstallationInfo(Guid TicketId,long TicketNumber,string Status,string Resolution,string ResolvedBy,DateTime ResolvedAt,string AgencyCode,string AgencyName,List<WarehouseInstallationEvidence> Evidence);
record InternetRequestInfo(Guid AgencyId,string AgencyCode,string AgencyName,string AgencyGroup,string ServiceKind,string ServiceNumber,string Company,int ModemQuantity,string ItemKind,string ChangeReason,Guid? TicketId=null,long? TicketNumber=null);
record AdministrativeRequestInfo(string Number,string AssetType,string TargetType,string TargetName,string? AgencyCode,string? Reason,InternetRequestInfo? Internet=null,string? FormType=null,JsonElement? FormData=null);
record IncidentTicketDecisionInput(int TicketIndex,string Status,string? Note=null,decimal? Amount=null);
record AdministrativeDeliveryInfo(string Number,string DeliveredBy,string DeliveredLogin,string ReceivedBy,DateTime At,Guid? AssetId,string? AssetIdentifier,List<string>? AssetIdentifiers=null);
sealed class InstitutionalFile
{
    static bool SameUser(string? left,string? right)=>string.Equals(left?.Trim(),right?.Trim(),StringComparison.OrdinalIgnoreCase);
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
    public Guid? FolderId {get;set;}
    [System.Text.Json.Serialization.JsonIgnore] public bool FolderReadAllowed {get;set;}
    [System.Text.Json.Serialization.JsonIgnore] public bool FolderWriteAllowed {get;set;}
    public bool CanManageFiles => Area=="files"&&FolderId is not null&&FolderWriteAllowed;
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
    public long? ArchivedFromDocumentId {get;set;}
    public double SealX {get;set;}=73;
    public double SealY {get;set;}=71;
    public string? ReceiptSignature {get;set;}
    public string? CorrectionNote {get;set;}
    public string? DecisionBy {get;set;}
    public DateTime? DecisionUtc {get;set;}
    public WarehouseRequestInfo? WarehouseRequest {get;set;}
    public List<WarehouseAssignedEquipment> WarehouseAssigned {get;set;}=[];
    public List<int> WarehousePendingPurchaseLines {get;set;}=[];
    public List<WarehouseAssignedEquipment> WarehouseAssignmentDraft {get;set;}=[];
    public string? WarehousePurchaseNote {get;set;}
    public string? WarehousePreparationNote {get;set;}
    public string? WarehousePreparedBy {get;set;}
    public string? WarehousePreparedLogin {get;set;}
    public DateTime? WarehousePreparedUtc {get;set;}
    public WarehouseDeliveryInfo? WarehouseDelivery {get;set;}
    public int WarehouseRevision {get;set;}
    public bool CanEditWarehouseRequest {get;set;}
    [System.Text.Json.Serialization.JsonIgnore] public bool TeamWarehouseReaderAllowed {get;set;}
    public WeekendLoanSettlement? WeekendLoanSettlement {get;set;}
    public WarehouseInstallationInfo? WarehouseInstallation {get;set;}
    public AdministrativeRequestInfo? AdministrativeRequest {get;set;}
    public bool AdministrativeRegistrationOnly => AdministrativeRequest?.FormType=="FUEL_LOG"&&!AdministrativeFuel.IsSulay(OwnerLogin);
    public int AdministrativeRevision {get;set;}
    public bool CanEditAdministrativeForm {get;set;}
    public bool CanDecideIncidentTickets {get;set;}
    public AdministrativeDeliveryInfo? AdministrativeDelivery {get;set;}
    public List<DocumentAccess> SharedWith {get;set;}=[];
    public List<InstitutionalEvent> History {get;set;}=[];
    public bool IsOwner {get;set;}
    public bool IsRecipient {get;set;}
    public bool CanEdit {get;set;}
    public bool CanMove {get;set;}
    // Recomputed for the authenticated reader; never trust a stored capability.
    public bool CanAuthorizeWarehouseRequest {get;set;}
    public InstitutionalFile ForListSummary()
    {
        // The list never renders signatures or the audit trail. Keeping these fields out
        // avoids sending old inline images and large histories before a card can appear.
        ReceiptSignature=null;
        History=[];
        WarehouseAssignmentDraft=[];
        return this;
    }
    public bool AwaitingWarehouseAuthorization()=>Channel=="requirements"&&RecipientDepartment=="Almacén"&&WarehouseDelivery is null&&(Status=="received"||(Status=="correction_requested"&&ReceivedUtc is not null));
    public void NormalizeWarehouseReceiptState()
    {
        // Some historic PDFs retain pending after receipt. Keep the original
        // receipt and require a separate approval; never reopen a later decision.
        if(Area=="communications"&&Channel=="requirements"&&RecipientDepartment=="Almacén"&&Status=="pending"&&ReceivedUtc is not null&&WarehouseDelivery is null)
            Status="received";
    }
    public string FileUrl => $"/api/institutional-documents/{Id}/preview";
    public bool Recipient(DocumentActor a)=>(AdministrativeRequest?.FormType=="INCIDENT_REPORT"&&AdministrativeProfiles.CanDecideIncidentTickets(a)||(RecipientUserName is not null?SameUser(RecipientUserName,a.Id):string.Equals(RecipientDepartment,a.Department,StringComparison.OrdinalIgnoreCase)))&&(!SameUser(OwnerUserName,a.Id)||AdministrativeRequest is not null);
    public bool Shared(DocumentActor a)=>SharedWith.Any(s=>s.RecipientUserName is not null?SameUser(s.RecipientUserName,a.Id):string.Equals(s.RecipientDepartment,a.Department,StringComparison.OrdinalIgnoreCase));
    public bool DepartmentWarehouseReader(DocumentActor a)=>!Trashed&&WarehouseRequest is not null&&Area=="communications"&&Channel=="requirements"&&RecipientDepartment=="Almacén"&&SentUtc is not null&&Status!="draft"&&Department==a.Department&&(a.Role!="GroupAdministrator"&&!(a.Role=="GeneralServices"&&a.Team=="TECHNICIANS")||OwnerUserName==a.Id);
    public bool Accessible(DocumentActor a){AdministrativeFuel.Normalize(this);return a.Allowed&&(Area!="files"||FolderId is null||FolderReadAllowed)&&(!a.WarehouseRequestOnly||(SameUser(OwnerUserName,a.Id)&&WarehouseRequest is not null&&RecipientDepartment=="Almacén"))&&(AdministrativeRequest is null||AdministrativeProfiles.CanRead(a,AdministrativeRequest))&&!(a.WarehouseRequestsBlocked&&(WarehouseRequest is not null||(Area=="communications"&&Channel=="requirements"&&RecipientDepartment=="Almacén")))&&(AdministrativeRequest is not null&&RecipientDepartment=="Administración"?a.Department=="Administración"||SameUser(OwnerUserName,a.Id)||AdministrativeRequest.FormType=="INCIDENT_REPORT"&&AdministrativeProfiles.CanReadIncidentReports(a):Trashed?SameUser(OwnerUserName,a.Id)||CanManageFiles:a.Manager||SameUser(OwnerUserName,a.Id)||Recipient(a)||Shared(a)||DepartmentWarehouseReader(a)||TeamWarehouseReaderAllowed||(Area=="files"&&(string.Equals(Department,a.Department,StringComparison.OrdinalIgnoreCase)||FolderReadAllowed)));}
    public InstitutionalFile For(DocumentActor a){AdministrativeFuel.Normalize(this);CanEditAdministrativeForm=AdministrativeFormEditing.CanEdit(this,a);CanDecideIncidentTickets=AdministrativeRequest?.FormType=="INCIDENT_REPORT"&&AdministrativeProfiles.CanDecideIncidentTickets(a);CanEditWarehouseRequest=WarehouseRequestEditing.CanEdit(this,a);NormalizeWarehouseReceiptState();CanAuthorizeWarehouseRequest=a.Manager&&!a.WarehouseRequestOnly&&a.WarehouseAuthorize&&!Trashed&&Channel=="requirements"&&RecipientDepartment=="Almacén"&&AwaitingWarehouseAuthorization();IsOwner=SameUser(OwnerUserName,a.Id);IsRecipient=Recipient(a)||(a.Manager&&RecipientDepartment=="Almacén");CanMove=SameUser(ReceivedByUserName,a.Id);CanEdit=!Trashed&&(Area=="files"||ReceivedUtc is null&&(IsOwner||SharedWith.Any(s=>s.CanEdit&&(s.RecipientUserName is not null?SameUser(s.RecipientUserName,a.Id):string.Equals(s.RecipientDepartment,a.Department,StringComparison.OrdinalIgnoreCase)))));var warehouseActor=a.Role is "Administrator" or "Warehouse"||a.Team=="WAREHOUSE";if(WarehouseRequest is not null&&!warehouseActor&&Status!="approved"){WarehouseAssigned=[];WarehousePreparationNote=null;WarehousePreparedBy=null;WarehousePreparedLogin=null;WarehousePreparedUtc=null;}if(Area=="files"&&FolderId is not null)CanMove=FolderWriteAllowed;return this;}
}
sealed class WarehouseSubmissionConflictException():Exception("Este número de solicitud ya se guardó con otros datos. Consulta la solicitud existente antes de crear una nueva.");
sealed class WarehouseFailureValidationException():Exception("La falla seleccionada ya no está disponible en el catálogo de soporte. Actualiza el formulario y vuelve a intentarlo.");
record InstitutionalTemplate(long Id,string Name,string Content,string Department,string OwnerUserName,bool IsSystem,DateTime UpdatedUtc);
partial class Database
{
    static readonly JsonSerializerOptions InstitutionalJson=new(JsonSerializerDefaults.Web);
    static readonly string[] InstitutionalDepartments=DepartmentAccounts.Names.Values.Distinct().ToArray();
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
        IF OBJECT_ID(N'dbo.application_one_time_actions',N'U') IS NULL
          CREATE TABLE dbo.application_one_time_actions(action_key varchar(100) NOT NULL PRIMARY KEY,executed_utc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME());
        -- Retire the old destructive cleanup; the current cleanup preserves pending requests and backups.
        IF NOT EXISTS(SELECT 1 FROM dbo.application_one_time_actions WHERE action_key='V403_CLEAR_WAREHOUSE_REQUESTS')
          INSERT INTO dbo.application_one_time_actions(action_key) VALUES('V403_CLEAR_WAREHOUSE_REQUESTS');
        """,c);await cmd.ExecuteNonQueryAsync(ct);await CleanupCompletedWarehouseRequests(c,ct);institutionalSchemaReady=true;}finally{InstitutionalSchemaLock.Release();}
    }
    static async Task ApplyTechnicalFailureWarehouseAccess(SqlConnection connection,IEnumerable<InstitutionalFile> files,DocumentActor actor,CancellationToken ct)
    {
        if(!actor.CanReadTechnicalFailureWarehouseTeam)return;
        var owners=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command=new SqlCommand("SELECT CONVERT(nvarchar(80),id) FROM dbo.admin_users WHERE role='TECHNOLOGY' AND support_team='TECHNICAL_FAILURE' AND is_active=1",connection);
        await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))owners.Add(reader.GetString(0));
        foreach(var document in files)
            document.TeamWarehouseReaderAllowed=document.Area=="communications"&&document.Channel=="requirements"&&document.WarehouseRequest is not null&&document.RecipientDepartment=="Almacén"&&owners.Contains(document.OwnerUserName);
    }
    public async Task<List<InstitutionalFile>> InstitutionalList(DocumentActor a,string area,string channel,CancellationToken ct,bool hydrate=true,bool administrativeOnly=false)
    {
        if(!a.Allowed)return [];await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await EnsureAdministrativeFolderSchema(c,ct);
        if(area=="communications"&&(a.Role=="Administrator"||a.Role=="Administrative"&&AdministrativeProfiles.Forms(a.Login)?.Contains("FUEL_LOG")==true))await EnsureSandraFuelImport(c,ct);
        await using var cmd=new SqlCommand("SELECT id,metadata FROM dbo.institutional_files f WHERE area=@area AND (@area='files' OR @channel='all' OR channel=@channel) AND (@administrativeOnly=0 OR JSON_QUERY(metadata,'$.administrativeRequest') IS NOT NULL) AND (owner_id=@user OR @manager=1 OR (@incidentReader=1 AND JSON_VALUE(metadata,'$.administrativeRequest.formType')='INCIDENT_REPORT') OR (area='files' AND (department=@dept OR TRY_CONVERT(uniqueidentifier,JSON_VALUE(metadata,'$.folderId')) IS NOT NULL)) OR (area='communications' AND channel='requirements' AND department=@dept AND JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL AND JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén') OR (@teamReader=1 AND area='communications' AND channel='requirements' AND JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL AND JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén' AND EXISTS(SELECT 1 FROM dbo.admin_users u WHERE CONVERT(nvarchar(80),u.id)=f.owner_id AND u.role='TECHNOLOGY' AND u.support_team='TECHNICAL_FAILURE' AND u.is_active=1)) OR JSON_VALUE(metadata,'$.recipientUserName')=@user OR (JSON_VALUE(metadata,'$.recipientUserName') IS NULL AND JSON_VALUE(metadata,'$.recipientDepartment')=@dept) OR EXISTS(SELECT 1 FROM OPENJSON(metadata,'$.sharedWith') WITH(recipientUserName nvarchar(80),recipientDepartment nvarchar(80)) s WHERE s.recipientUserName=@user OR (s.recipientUserName IS NULL AND s.recipientDepartment=@dept))) ORDER BY id DESC OPTION(RECOMPILE)",c);
        cmd.Parameters.AddWithValue("@area",area);cmd.Parameters.AddWithValue("@channel",channel);cmd.Parameters.AddWithValue("@administrativeOnly",administrativeOnly);cmd.Parameters.AddWithValue("@user",a.Id);cmd.Parameters.AddWithValue("@manager",a.Manager);cmd.Parameters.AddWithValue("@incidentReader",AdministrativeProfiles.CanReadIncidentReports(a));cmd.Parameters.AddWithValue("@dept",a.Department);cmd.Parameters.AddWithValue("@teamReader",a.CanReadTechnicalFailureWarehouseTeam);
        var list=new List<InstitutionalFile>();await using(var r=await cmd.ExecuteReaderAsync(ct)){while(await r.ReadAsync(ct)){var d=JsonSerializer.Deserialize<InstitutionalFile>(r.GetString(1),InstitutionalJson)!;d.Id=r.GetInt64(0);list.Add(d);}}await ApplyAdministrativeFolderAccess(c,list,a,ct);await ApplyTechnicalFailureWarehouseAccess(c,list,a,ct);var accessible=list.Where(d=>d.Accessible(a)).Select(d=>d.For(a)).ToList();if(hydrate)foreach(var item in accessible)await HydrateInstitutionalFile(item,ct);return accessible;
    }
    public async Task<InstitutionalFile?> InstitutionalDetail(long id,DocumentActor a,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id=@id",c);cmd.Parameters.AddWithValue("@id",id);var json=await cmd.ExecuteScalarAsync(ct) as string;if(json is null)return null;var d=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;d.Id=id;await ApplyAdministrativeFolderAccess(c,[d],a,ct);await ApplyTechnicalFailureWarehouseAccess(c,[d],a,ct);if(!d.Accessible(a))return null;d=await WarehouseRecoverLegacyRequest(c,d,json,ct);return d.Accessible(a)?await HydrateInstitutionalFile(d.For(a),ct):null;
    }
    public async Task<(InstitutionalFile Document,byte[] Data)?> InstitutionalBytes(long id,DocumentActor a,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand("SELECT metadata,file_data FROM dbo.institutional_files WHERE id=@id",c);cmd.Parameters.AddWithValue("@id",id);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;var d=JsonSerializer.Deserialize<InstitutionalFile>(r.GetString(0),InstitutionalJson)!;d.Id=id;var bytes=(byte[])r[1];await r.DisposeAsync();await ApplyAdministrativeFolderAccess(c,[d],a,ct);await ApplyTechnicalFailureWarehouseAccess(c,[d],a,ct);return d.Accessible(a)?(await HydrateInstitutionalFile(d.For(a),ct),await StorageFiles.Read(bytes,ct)):null;
    }
    public async Task<string?> InstitutionalReplaceFile(long id,DocumentActor actor,byte[] data,string contentType,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await EnsureAdministrativeFolderSchema(c,ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var read=new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",c,tx);read.Parameters.AddWithValue("@id",id);var json=await read.ExecuteScalarAsync(ct) as string;if(json is null)return "Documento no disponible.";
        var d=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;d.Id=id;await ApplyAdministrativeFolderAccess(c,[d],actor,ct,tx);if(!d.Accessible(actor))return "No tienes acceso a este documento.";d.For(actor);
        if(d.Area!="files"||d.Trashed||!d.CanEdit)return "Necesitas permiso de edición para reemplazar este archivo.";
        d.Size=data.Length;d.ContentType=contentType;
        await using var update=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json,file_data=@bytes WHERE id=@id",c,tx);update.Parameters.AddWithValue("@id",id);update.Parameters.AddWithValue("@json",await StorageFiles.TransformJson(JsonSerializer.Serialize(d,InstitutionalJson),true,ct));update.Parameters.AddWithValue("@bytes",await StorageFiles.Store(data,"institutional-document",ct));await update.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return null;
    }
    public async Task<(InstitutionalFile Document,byte[] Data)?> InstitutionalOpenBytes(long id,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand("SELECT metadata,file_data FROM dbo.institutional_files WHERE id=@id",c);cmd.Parameters.AddWithValue("@id",id);await using var reader=await cmd.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))return null;var document=JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(0),InstitutionalJson)!;document.Id=id;var stored=(byte[])reader[1];await reader.DisposeAsync();return(document,await StorageFiles.Read(stored,ct));
    }
    public async Task<(byte[] Data,string Mime,string FileName)?> InstitutionalWarehouseEvidence(long documentId,Guid evidenceId,DocumentActor actor,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureInstitutionalSchema(connection,ct);
        await using var documentCommand=new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id=@id",connection);documentCommand.Parameters.AddWithValue("@id",documentId);var json=await documentCommand.ExecuteScalarAsync(ct) as string;if(json is null)return null;
        var document=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson);if(document is null)return null;document.Id=documentId;if(!document.Accessible(actor)||document.WarehouseInstallation is null||document.WarehouseInstallation.Evidence.All(item=>item.Id!=evidenceId))return null;
        await using var evidence=new SqlCommand("SELECT photo_data,content_type,file_name FROM dbo.support_ticket_evidence WHERE id=@evidence AND ticket_id=@ticket",connection);evidence.Parameters.AddWithValue("@evidence",evidenceId);evidence.Parameters.AddWithValue("@ticket",document.WarehouseInstallation.TicketId);await using var reader=await evidence.ExecuteReaderAsync(ct);return await reader.ReadAsync(ct)?(await StorageFiles.Read((byte[])reader[0],ct),reader.GetString(1),reader.GetString(2)):null;
    }
    public async Task<long> InstitutionalUpload(InstitutionalFile d,byte[] data,CancellationToken ct)
    {
        if(d.WarehouseRequest is not null&&d.ReceivedUtc is null&&d.WarehouseDelivery is null&&d.Status is not ("approved" or "rejected"))
        {
            d.Status="pending";
            d.SentUtc??=DateTime.UtcNow;
        }
        if(d.AdministrativeRequest is not null&&d.Area=="communications"&&d.Channel=="requirements")return await InstitutionalAdministrativeUpload(d,data,ct);
        if(d.WarehouseRequest is not null&&d.Area=="communications"&&d.Channel=="requirements")return await InstitutionalWarehouseUpload(d,data,ct);
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await EnsureAdministrativeFolderSchema(c,ct);await using var uploadTx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        if(d.Area=="files"&&d.FolderId is Guid target){var actor=new DocumentActor(d.OwnerUserName,"Administrative","",d.OwnerDisplayName,d.OwnerLogin,d.Department);var folders=await LoadAdministrativeFolders(c,d.Department,ct,uploadTx);var folder=folders.Find(f=>f.Id==target);if(folder is null||!FolderWritable(folder,folders,actor))throw new InvalidOperationException("Selecciona una carpeta propia de tu departamento.");}
        await using var cmd=new SqlCommand("INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES(@owner,@dept,@area,@channel,@json,@bytes)",c,uploadTx);cmd.Parameters.AddWithValue("@owner",d.OwnerUserName);cmd.Parameters.AddWithValue("@dept",d.Department);cmd.Parameters.AddWithValue("@area",d.Area);cmd.Parameters.AddWithValue("@channel",d.Channel);cmd.Parameters.AddWithValue("@json",await StorageFiles.TransformJson(JsonSerializer.Serialize(d,InstitutionalJson),true,ct));cmd.Parameters.AddWithValue("@bytes",await StorageFiles.Store(data,"institutional-document",ct));var inserted=Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));await uploadTx.CommitAsync(ct);return inserted;
    }
    public async Task<InstitutionalFile?> InstitutionalWarehouseSubmission(string number,DocumentActor actor,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);
        await using var cmd=new SqlCommand("SELECT TOP(1) id,metadata FROM dbo.institutional_files WHERE owner_id=@owner AND area='communications' AND channel='requirements' AND JSON_VALUE(metadata,'$.warehouseRequest.number')=@number ORDER BY id",c);
        cmd.Parameters.AddWithValue("@owner",actor.Id);cmd.Parameters.AddWithValue("@number",number);
        await using var reader=await cmd.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))return null;
        var document=JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1),InstitutionalJson);
        if(document is null||!document.Accessible(actor))return null;document.Id=reader.GetInt64(0);return await HydrateInstitutionalFile(document.For(actor),ct);
    }
    async Task<long> InstitutionalWarehouseUpload(InstitutionalFile d,byte[] data,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);
        if(d.WarehouseRequest?.FailureCategory is not null)await EnsureTicketWorkflowSchema(c,ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        // Serialize simultaneous retries of the same owner's form across app instances.
        var key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(d.OwnerUserName+"|"+d.WarehouseRequest!.Number)));
        await using(var gate=new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @result<0 THROW 50001,'No fue posible confirmar el envio. Reintenta con el mismo formulario.',1;",c,tx)){
            gate.Parameters.AddWithValue("@key","warehouse-submission:"+key);await gate.ExecuteNonQueryAsync(ct);
        }
        await using(var find=new SqlCommand("SELECT TOP(1) id,metadata FROM dbo.institutional_files WHERE owner_id=@owner AND area='communications' AND channel='requirements' AND JSON_VALUE(metadata,'$.warehouseRequest.number')=@number ORDER BY id",c,tx)){
            find.Parameters.AddWithValue("@owner",d.OwnerUserName);find.Parameters.AddWithValue("@number",d.WarehouseRequest.Number);
            await using var reader=await find.ExecuteReaderAsync(ct);
            if(await reader.ReadAsync(ct)){
                var id=reader.GetInt64(0);var old=JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1),InstitutionalJson)!;
                var oldRequest=old.WarehouseRequest?.FailureCategory is not null?old.WarehouseRequest with{TicketId=null,TicketNumber=null,FailureTeam=d.WarehouseRequest.FailureTeam}:old.WarehouseRequest;
                if(old.Department!=d.Department||old.Description!=d.Description||JsonSerializer.Serialize(oldRequest,InstitutionalJson)!=JsonSerializer.Serialize(d.WarehouseRequest,InstitutionalJson))throw new WarehouseSubmissionConflictException();
                await reader.DisposeAsync();await tx.CommitAsync(ct);d.Id=id;d.WarehouseRequest=old.WarehouseRequest;return id;
            }
        }
        if(d.WarehouseRequest is {FailureCategory:not null} request)
        {
            if(request.AgencyId==Guid.Empty||request.TicketId is not null||request.FailureDepartment is not ("TECHNOLOGY" or "GENERAL_SERVICES")||string.IsNullOrWhiteSpace(request.FailureDescription))throw new WarehouseFailureValidationException();
            string categoryName;
            await using(var category=new SqlCommand("SELECT TOP(1)c.name FROM dbo.support_categories c JOIN dbo.support_departments dept ON dept.code=c.department_code WHERE c.department_code=@department AND c.code=@category AND c.is_active=1 AND dept.is_active=1",c,tx))
            {
                category.Parameters.AddWithValue("@department",request.FailureDepartment);
                category.Parameters.AddWithValue("@category",request.FailureCategory);
                categoryName=(await category.ExecuteScalarAsync(ct)) as string??throw new WarehouseFailureValidationException();
            }
            var ticketId=Guid.NewGuid();
            var subject=$"{categoryName} · {request.AgencyName}";
            if(subject.Length>180)subject=subject[..180];
            var details=string.Join("; ",request.Lines.Select(line=>$"{line.Name} x{line.Quantity}"));
            var description=$"Solicitud a Almacén {request.Number}. Falla: {request.FailureDescription!.Trim()}\nEquipos: {details}";
            if(description.Length>2000)description=description[..2000];
            await using(var ticket=new SqlCommand("""
                INSERT INTO dbo.support_tickets(id,agency_id,category,assigned_department,assigned_team,priority,status,subject,description,created_by_user_id,created_by_name,created_by_username,ticket_type,assigned_technician_id)
                OUTPUT INSERTED.ticket_number
                VALUES(@id,@agency,@category,@department,@team,@priority,'OPEN',@subject,@description,@userId,@name,@login,@ticketType,NULL)
                """,c,tx))
            {
                ticket.Parameters.AddWithValue("@id",ticketId);
                ticket.Parameters.AddWithValue("@agency",request.AgencyId.ToString());
                ticket.Parameters.AddWithValue("@category",request.FailureCategory);
                ticket.Parameters.AddWithValue("@department",request.FailureDepartment);
                ticket.Parameters.AddWithValue("@team",request.FailureTeam is not null?request.FailureTeam:DBNull.Value);
                ticket.Parameters.AddWithValue("@priority",request.FailureDepartment=="TECHNOLOGY"&&request.FailureCategory=="CPU_FAILURE"?"CRITICAL":request.Priority);
                ticket.Parameters.AddWithValue("@subject",subject);
                ticket.Parameters.AddWithValue("@description",description);
                ticket.Parameters.AddWithValue("@userId",d.OwnerUserName);
                ticket.Parameters.AddWithValue("@name",d.OwnerDisplayName);
                ticket.Parameters.AddWithValue("@login",d.OwnerLogin);
                ticket.Parameters.AddWithValue("@ticketType","INTERNAL");
                var ticketNumber=Convert.ToInt64(await ticket.ExecuteScalarAsync(ct));
                d.WarehouseRequest=request with{TicketId=ticketId,TicketNumber=ticketNumber};
            }
            await AddTicketHistory(c,tx,ticketId,"CREATED",null,request.FailureDepartment,null,request.FailureTeam,d.OwnerUserName,d.OwnerDisplayName,$"Creado desde solicitud a Almacén {request.Number}",ct);
            await AddTicketNotifications(c,tx,ticketId,"CREATED",null,request.FailureDepartment,request.AgencyGroup,d.OwnerUserName,d.OwnerDisplayName,$"Se abrió un ticket de falla desde la solicitud a Almacén {request.Number}.",true,ct);
        }
        await using var insert=new SqlCommand("INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES(@owner,@dept,@area,@channel,@json,@bytes)",c,tx);
        insert.Parameters.AddWithValue("@owner",d.OwnerUserName);insert.Parameters.AddWithValue("@dept",d.Department);insert.Parameters.AddWithValue("@area",d.Area);insert.Parameters.AddWithValue("@channel",d.Channel);insert.Parameters.AddWithValue("@json",await StorageFiles.TransformJson(JsonSerializer.Serialize(d,InstitutionalJson),true,ct));insert.Parameters.AddWithValue("@bytes",await StorageFiles.Store(data,"institutional-document",ct));
        var result=await insert.ExecuteScalarAsync(ct);if(result is null||Convert.ToInt64(result)<=0)throw new InvalidOperationException("No se obtuvo el identificador de la solicitud.");
        d.Id=Convert.ToInt64(result);await tx.CommitAsync(ct);return d.Id;
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
            update.Parameters.AddWithValue("@id",id);update.Parameters.AddWithValue("@owner",a.Id);update.Parameters.AddWithValue("@json",await StorageFiles.TransformJson(JsonSerializer.Serialize(d,InstitutionalJson),true,ct));update.Parameters.AddWithValue("@bytes",await StorageFiles.Store(data,"institutional-document",ct));await update.ExecuteNonQueryAsync(ct);return id;
        }
        await using var insert=new SqlCommand("INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES(@owner,@dept,'communications','requests',@json,@bytes)",c);
        insert.Parameters.AddWithValue("@owner",a.Id);insert.Parameters.AddWithValue("@dept",a.Department);insert.Parameters.AddWithValue("@json",await StorageFiles.TransformJson(JsonSerializer.Serialize(d,InstitutionalJson),true,ct));insert.Parameters.AddWithValue("@bytes",await StorageFiles.Store(data,"institutional-document",ct));return Convert.ToInt64(await insert.ExecuteScalarAsync(ct));
    }
    public async Task<string?> DecideIncidentTicket(long id,DocumentActor actor,IncidentTicketDecisionInput input,CancellationToken ct)
    {
        if(!AdministrativeProfiles.CanDecideIncidentTickets(actor))return "Los supervisores no pueden autorizar. Solo Yessica Mercedes, Lucianny González con perfil administrativo o administrativo departamental, y el administrador principal pueden decidir estos tickets.";
        if(input.Status is not ("APPROVED" or "REJECTED"))return "Selecciona Aprobar o Rechazar.";
        if(input.Note?.Trim().Length>2000)return "El motivo admite hasta 2,000 caracteres.";
        if(input.Status=="APPROVED"&&(input.Amount is null||input.Amount<=0||input.Amount>1000000000m))return "Indica un monto de compensación mayor que cero.";
        if(input.Status=="REJECTED"&&(string.IsNullOrWhiteSpace(input.Note)||input.Note.Trim().Length<5))return "Indica el motivo del rechazo (mínimo 5 caracteres).";

        await using var connection=await Open(ct);await EnsureInstitutionalSchema(connection,ct);
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var read=new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",connection,transaction);read.Parameters.AddWithValue("@id",id);
        var json=await read.ExecuteScalarAsync(ct) as string;if(json is null)return "Documento no disponible.";
        var document=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;document.Id=id;
        if(!document.Accessible(actor))return "No tienes acceso a este informe.";
        if(document.Trashed||document.AdministrativeRequest?.FormType!="INCIDENT_REPORT"||document.AdministrativeRequest.FormData is not {ValueKind:JsonValueKind.Object} formData)return "El informe de incidentes no es válido.";
        if(document.ReceivedUtc is null||document.Status is not ("received" or "approved" or "rejected"))return "Primero recibe y sella el informe antes de decidir sus tickets.";

        var root=JsonNode.Parse(formData.GetRawText())?.AsObject();var rows=root?["rows"]?.AsArray();
        if(rows is null||input.TicketIndex<0||input.TicketIndex>=rows.Count||rows[input.TicketIndex] is not JsonObject row)return "El ticket seleccionado no existe.";
        var ticketCode=row["ticketCode"]?.GetValue<string>()?.Trim();
        if(string.IsNullOrWhiteSpace(ticketCode))return "El ticket seleccionado no tiene número.";
        var previous=row["decisionStatus"]?.GetValue<string>()?.ToUpperInvariant();
        if((previous is "APPROVED" or "REJECTED")&&previous!=input.Status)return "Este ticket ya tiene una decisión y no puede cambiarse por la opción contraria.";

        var now=DateTime.UtcNow;var previousDocumentStatus=document.Status;
        row["decisionStatus"]=input.Status;row["decisionNote"]=input.Note?.Trim()??"";row["decisionBy"]=actor.Name;row["decisionLogin"]=actor.Login;row["decisionUtc"]=now;
        if(input.Status=="APPROVED")row["approvedAmount"]=input.Amount!.Value;else row.Remove("approvedAmount");
        document.AdministrativeRequest=document.AdministrativeRequest with{FormData=JsonSerializer.SerializeToElement(root,InstitutionalJson)};
        var active=rows.OfType<JsonObject>().Where(item=>!string.IsNullOrWhiteSpace(item["ticketCode"]?.GetValue<string>())).ToList();
        var decisions=active.Select(item=>item["decisionStatus"]?.GetValue<string>()?.ToUpperInvariant()).ToList();
        if(active.Count>0&&decisions.All(status=>status is "APPROVED" or "REJECTED"))document.Status=decisions.Any(status=>status=="APPROVED")?"approved":"rejected";else document.Status="received";
        document.DecisionBy=active.Count>0&&decisions.All(status=>status is "APPROVED" or "REJECTED")?actor.Name+" · "+actor.Login:null;
        document.DecisionUtc=active.Count>0&&decisions.All(status=>status is "APPROVED" or "REJECTED")?now:null;
        document.CorrectionNote=input.Note?.Trim();document.AdministrativeRevision++;
        document.History.Add(new(input.Status=="APPROVED"?"incident-ticket-approved":"incident-ticket-rejected",actor.Name,actor.Login,now,previousDocumentStatus,document.ReceiptCode,document.ReceiverLogin,document.ReceivedUtc,null,Note:$"Ticket {ticketCode}: {input.Note?.Trim()}"));
        await using var update=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id",connection,transaction);update.Parameters.AddWithValue("@id",id);update.Parameters.AddWithValue("@json",await StorageFiles.TransformJson(JsonSerializer.Serialize(document,InstitutionalJson),true,ct));await update.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);return null;
    }

    // All receipt, sharing and state changes are serialized per document. Restoring preserves its workflow status.
    public async Task<string?> InstitutionalChange(long id,DocumentActor a,string action,InstitutionalAction b,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await EnsureAdministrativeFolderSchema(c,ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var read=new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",c,tx);read.Parameters.AddWithValue("@id",id);var json=await read.ExecuteScalarAsync(ct) as string;if(json is null)return "Documento no disponible.";var d=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;d.Id=id;await ApplyAdministrativeFolderAccess(c,[d],a,ct,tx);if(!d.Accessible(a))return "No tienes acceso a este documento.";d.For(a);var incidentAdministrator=d.AdministrativeRequest?.FormType=="INCIDENT_REPORT"&&AdministrativeProfiles.CanManageIncidentReports(a);
        if(AdministrativeFuel.BlocksAction(d,action))return "Los registros diarios de gasolina se guardan directamente y no requieren autorización ni recepción.";
        if(d.AdministrativeRequest?.FormType=="INCIDENT_REPORT"&&(action is "approve" or "reject" or "correction"))return "Decide cada ticket por separado desde las opciones del informe.";
        if(d.AdministrativeRequest?.FormType=="GAS_LOG"&&d.OwnerUserName==a.Id&&(action is "approve" or "reject"))return "La relación de gas debe ser autorizada por otra persona con permiso.";
        d.History.Add(new(action,a.Name,a.Login,DateTime.UtcNow,d.Status,d.ReceiptCode,d.ReceiverLogin,d.ReceivedUtc,action=="resend"?d.ReceiptSignature:null));
        if(action is "trash" or "purge"&&d.WarehouseAssigned.Count>0&&d.WarehouseDelivery is null)return "La solicitud tiene equipos pendientes de retiro. Completa la entrega antes de enviarla a la papelera.";
        if(action is "trash" or "purge"&&d.AdministrativeRequest is not null&&d.AdministrativeRequest.FormType!="INCIDENT_REPORT"&&d.Status=="approved"&&d.AdministrativeDelivery is null)return "Completa la entrega administrativa antes de enviar la solicitud a la papelera.";
        if(action=="trash"||action=="restore"||action=="purge"){if(!d.IsOwner&&!d.CanManageFiles)return "Solo el propietario o los colaboradores de la carpeta administran su papelera.";if(action=="purge"&&!d.Trashed)return "Envía primero el archivo a la papelera.";d.Trashed=action!="restore";}
        else if(d.Trashed)return "Restaura el documento antes de modificarlo.";
        else if(action=="cancel")
        {
            if(d.WarehouseRequest is null||d.RecipientDepartment!="Almacén"||(!d.IsOwner&&!a.Manager))return "Solo el solicitante, Almacén o Administración puede anular esta solicitud.";
            if(d.WarehouseDelivery is not null||d.Status is not ("pending" or "received" or "correction_requested" or "approved"))return "Solo puedes anular solicitudes activas que todavía no se han entregado.";
            if(string.IsNullOrWhiteSpace(b.Note)||b.Note.Trim().Length is <5 or >2000)return "Indica el motivo de anulación (5 a 2,000 caracteres).";
            await using var release=new SqlCommand("""
              IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.warehouse_equipment_allocations') AND name='CK_warehouse_allocation_status' AND definition NOT LIKE '%CANCELLED%')
              BEGIN
                ALTER TABLE dbo.warehouse_equipment_allocations DROP CONSTRAINT CK_warehouse_allocation_status;
                ALTER TABLE dbo.warehouse_equipment_allocations WITH CHECK ADD CONSTRAINT CK_warehouse_allocation_status CHECK(status IN('PENDING_PICKUP','WITHDRAWN','CANCELLED'));
              END;
              IF OBJECT_ID(N'dbo.warehouse_equipment_allocations',N'U') IS NOT NULL UPDATE dbo.warehouse_equipment_allocations SET status='CANCELLED' WHERE document_id=@id AND status='PENDING_PICKUP';
              """,c,tx);
            release.Parameters.AddWithValue("@id",id);await release.ExecuteNonQueryAsync(ct);
            d.Status="cancelled";d.CorrectionNote=b.Note.Trim();d.DecisionBy=a.Name+" · "+a.Login;d.DecisionUtc=DateTime.UtcNow;
        }
        else if(action=="edit"){if(!d.CanEdit||d.Status=="cancelled")return "No puedes modificar este documento.";d.Title=b.Title!.Trim();d.Description=b.Description?.Trim()??"";}
        else if(action=="share"||action=="unshare")
        {
            if(!d.IsOwner)return "Solo el propietario puede compartir o revocar accesos.";
            if(d.Area=="files"&&d.FolderId is not null)return "Los archivos de una carpeta heredan sus destinatarios. Administra los accesos desde la carpeta principal.";
            if(action=="unshare")d.SharedWith.RemoveAll(s=>s.Id==b.ShareId);
            else{d.SharedWith.RemoveAll(s=>s.RecipientUserName==b.RecipientUserName&&s.RecipientDepartment==b.RecipientDepartment);d.SharedWith.Add(new(Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),d.SharedWith.Select(s=>s.Id).DefaultIfEmpty(0).Max()+1),b.RecipientUserName,b.RecipientDepartment,b.CanEdit,DateTime.UtcNow));}
        }
        else if(action=="move") {if(!d.CanMove||d.ReceivedUtc is null)return "Solo quien recibió puede mover su sello.";d.SealX=b.SealX;d.SealY=b.SealY;}
        else if(action=="sign") {if(!d.CanMove||d.ReceivedUtc is null)return "Primero recibe el documento desde tu cuenta.";d.ReceiptSignature=b.SignatureData;}
        else if(action=="resend") {if(!d.IsOwner||d.Status is not ("rejected" or "correction_requested"))return "No admite reenvío.";if(d.AwaitingWarehouseAuthorization()){d.Status="received";d.SentUtc=DateTime.UtcNow;}else{d.Status="pending";d.SentUtc=DateTime.UtcNow;d.ReceivedUtc=null;d.ReceiptSignature=null;d.ReceivedByUserName=null;d.ReceiverDisplayName=null;d.ReceiverLogin=null;d.ReceiptCode=null;d.VerificationToken=null;}}
        else if(action=="send") {if(!d.IsOwner||d.Area!="communications"||d.Status!="draft"||string.IsNullOrWhiteSpace(b.RecipientDepartment))return "Selecciona un departamento para enviar el borrador.";d.RecipientDepartment=b.RecipientDepartment.Trim();d.RecipientUserName=string.IsNullOrWhiteSpace(b.RecipientUserName)?null:b.RecipientUserName.Trim();d.Status="pending";d.SentUtc=DateTime.UtcNow;}
        else
        {
            if(d.Area!="communications"||(!d.IsRecipient&&!incidentAdministrator))return "Solo el destinatario puede responder.";
            if(action=="approve") {if(d.WarehouseRequest is not null)return "Selecciona los equipos y seriales desde Inventario antes de autorizar.";if(d.Channel!="requirements"||(!a.Manager&&!(a.Role=="Administrative"&&d.AdministrativeRequest is not null&&AdministrativeProfiles.CanRead(a,d.AdministrativeRequest)))||d.Status!="received")return "Primero marca el requerimiento como recibido antes de autorizarlo.";d.Status="approved";d.DecisionBy=a.Name+" · "+a.Login;d.DecisionUtc=DateTime.UtcNow;d.CorrectionNote=b.Note;}
            else if(action=="receive"){if(d.ReceivedUtc is not null||d.Status is not ("pending" or "correction_requested" or "approved"))return "La recepción ya fue registrada o no está pendiente.";d.Status=d.Channel=="requests"&&d.RecipientDepartment=="Almacén"?"approved":d.Status=="approved"?"approved":"received";d.DecisionBy=d.Status=="approved"?a.Name+" · "+a.Login:d.DecisionBy;d.DecisionUtc=d.Status=="approved"?DateTime.UtcNow:d.DecisionUtc;d.CorrectionNote=d.Status=="approved"?"Recepción autorizada automáticamente por Almacén General.":d.CorrectionNote;d.ReceivedUtc=DateTime.UtcNow;d.ReceivedByUserName=a.Id;d.ReceiverDisplayName=a.Name;d.ReceiverLogin=a.Login;d.ReceiptSignature=b.SignatureData;d.ReceiptCode=$"REAL-REC-{DateTime.UtcNow:yyyyMMdd}-{id:D6}";d.VerificationToken=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();d.SealX=b.SealX;d.SealY=b.SealY;}
            else if(action is "correction" or "reject") {if(d.Channel=="requirements"&&d.Status!="received")return "Almacén debe marcar primero el requerimiento como recibido antes de decidir.";if(d.Status is not ("pending" or "correction_requested" or "received"))return "La comunicación ya tiene una decisión.";if(d.WarehouseRequest is not null&&d.WarehouseAssigned.Count>0){await using var release=new SqlCommand("DELETE FROM dbo.warehouse_equipment_allocations WHERE document_id=@id AND status='PENDING_PICKUP'",c,tx);release.Parameters.AddWithValue("@id",id);await release.ExecuteNonQueryAsync(ct);d.WarehouseAssigned=[];d.WarehousePreparationNote=null;d.WarehousePreparedBy=null;d.WarehousePreparedLogin=null;d.WarehousePreparedUtc=null;}d.Status=action=="reject"?"rejected":"correction_requested";d.CorrectionNote=b.Note;d.DecisionBy=a.Name+" · "+a.Login;d.DecisionUtc=DateTime.UtcNow;}
            else return "Acción desconocida.";
        }
        await using var save=new SqlCommand(action=="purge"?"DELETE FROM dbo.institutional_files WHERE id=@id":"UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id",c,tx);save.Parameters.AddWithValue("@id",id);save.Parameters.AddWithValue("@json",await StorageFiles.TransformJson(JsonSerializer.Serialize(d,InstitutionalJson),true,ct));await save.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return null;
    }
    static async Task LinkWarehouseTicketResolution(SqlConnection connection,SqlTransaction transaction,Guid ticketId,string actorId,string actorName,CancellationToken ct)
    {
        await using(var exists=new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.institutional_files',N'U') IS NULL THEN 0 ELSE 1 END",connection,transaction))if(Convert.ToInt32(await exists.ExecuteScalarAsync(ct))==0)return;
        long ticketNumber;string status,resolution,agencyCode,agencyName;DateTime resolvedAt;
        await using(var ticket=new SqlCommand("SELECT t.ticket_number,t.status,ISNULL(t.resolution,''),COALESCE(t.resolved_at,SYSUTCDATETIME()),a.codigo,a.terminal FROM dbo.support_tickets t JOIN dbo.agencies a ON a.id=t.agency_id WHERE t.id=@ticket",connection,transaction))
        {ticket.Parameters.AddWithValue("@ticket",ticketId);await using var reader=await ticket.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))return;ticketNumber=reader.GetInt64(0);status=reader.GetString(1);resolution=reader.GetString(2);resolvedAt=reader.GetDateTime(3);agencyCode=reader.GetString(4);agencyName=reader.GetString(5);}
        var evidence=new List<WarehouseInstallationEvidence>();
        await using(var photos=new SqlCommand("SELECT id,file_name,content_type,captured_at FROM dbo.support_ticket_evidence WHERE ticket_id=@ticket ORDER BY captured_at,id",connection,transaction))
        {photos.Parameters.AddWithValue("@ticket",ticketId);await using var reader=await photos.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))evidence.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetDateTime(3)));}
        var linked=new List<(long Id,InstitutionalFile Document)>();
        await using(var documents=new SqlCommand("SELECT id,metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE TRY_CONVERT(uniqueidentifier,JSON_VALUE(metadata,'$.warehouseRequest.ticketId'))=@ticket",connection,transaction))
        {documents.Parameters.AddWithValue("@ticket",ticketId);await using var reader=await documents.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct)){var document=JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1),InstitutionalJson);if(document is not null){document.Id=reader.GetInt64(0);linked.Add((document.Id,document));}}}
        foreach(var (id,document) in linked)
        {
            document.WarehouseInstallation=new(ticketId,ticketNumber,status,resolution,actorName,resolvedAt,agencyCode,agencyName,evidence);
            document.History.Add(new("installed",actorName,actorId,resolvedAt,document.Status,document.ReceiptCode,document.ReceiverLogin,document.ReceivedUtc,null));
            await using var update=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id",connection,transaction);update.Parameters.AddWithValue("@json",JsonSerializer.Serialize(document,InstitutionalJson));update.Parameters.AddWithValue("@id",id);await update.ExecuteNonQueryAsync(ct);
        }
    }
    public async Task<object> InstitutionalOverview(DocumentActor a,string area,string channel,CancellationToken ct){var docs=(await InstitutionalList(a,area,channel,ct,hydrate:false)).Where(d=>!d.Trashed).ToList();return new{total=docs.Count,mine=docs.Count(d=>d.IsOwner),shared=docs.Count(d=>!d.IsOwner&&d.Shared(a)),pending=docs.Count(d=>(d.Status is "pending" or "correction_requested")&&!d.AwaitingWarehouseAuthorization()),received=docs.Count(d=>d.Status=="received"||d.AwaitingWarehouseAuthorization()),approved=docs.Count(d=>d.Status=="approved"),sent=docs.Count(d=>d.IsOwner&&d.SentUtc is not null),rejected=docs.Count(d=>d.Status=="rejected"),departments=InstitutionalDepartments.Select(name=>{var scoped=docs.Where(d=>d.Department==name).ToList();return new{name,count=scoped.Count,pending=scoped.Count(d=>(d.Status is "pending" or "correction_requested")&&!d.AwaitingWarehouseAuthorization()),received=scoped.Count(d=>d.Status=="received"||d.AwaitingWarehouseAuthorization()),approved=scoped.Count(d=>d.Status=="approved"),rejected=scoped.Count(d=>d.Status=="rejected")};})};}

    public async Task<bool> InstitutionalRecipient(string? user,string? department,CancellationToken ct){if(!InstitutionalDepartments.Contains(department))return false;if(user is null)return true;using var list=JsonDocument.Parse(await InstitutionalUsers(ct));return list.RootElement.EnumerateArray().Any(x=>x.GetProperty("userName").GetString()==user&&x.GetProperty("department").GetString()==department);}
    public async Task<List<InstitutionalTemplate>> InstitutionalTemplates(DocumentActor a,CancellationToken ct){await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand("SELECT id,name,content,department,owner_id,updated_at FROM dbo.institutional_editable_templates WHERE owner_id=@user OR department=@dept ORDER BY updated_at DESC",c);cmd.Parameters.AddWithValue("@user",a.Id);cmd.Parameters.AddWithValue("@dept",a.Department);var list=new List<InstitutionalTemplate>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))list.Add(new(r.GetInt64(0),r.GetString(1),(await StorageFiles.TransformJson(r.GetString(2),false,ct))!,r.GetString(3),r.GetString(4),false,DateTime.SpecifyKind(r.GetDateTime(5),DateTimeKind.Utc)));return list;}
    public async Task<long?> InstitutionalSaveTemplate(DocumentActor a,long? id,string name,string content,CancellationToken ct){await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand(id is >0?"UPDATE dbo.institutional_editable_templates SET name=@name,content=@content,updated_at=SYSUTCDATETIME() OUTPUT INSERTED.id WHERE id=@id AND owner_id=@user":"INSERT INTO dbo.institutional_editable_templates(owner_id,department,name,content) OUTPUT INSERTED.id VALUES(@user,@dept,@name,@content)",c);cmd.Parameters.AddWithValue("@id",id??0);cmd.Parameters.AddWithValue("@user",a.Id);cmd.Parameters.AddWithValue("@dept",a.Department);cmd.Parameters.AddWithValue("@name",name);cmd.Parameters.AddWithValue("@content",await StorageFiles.TransformJson(content,true,ct));var value=await cmd.ExecuteScalarAsync(ct);return value is null?null:Convert.ToInt64(value);}
    public async Task<bool> InstitutionalDeleteTemplate(DocumentActor a,long id,CancellationToken ct){await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await using var cmd=new SqlCommand("DELETE FROM dbo.institutional_editable_templates WHERE id=@id AND owner_id=@user",c);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@user",a.Id);return await cmd.ExecuteNonQueryAsync(ct)==1;}
}
record InstitutionalAction(string? Title=null,string? Description=null,string? Note=null,string? RecipientUserName=null,string? RecipientDepartment=null,bool CanEdit=false,long ShareId=0,double SealX=73,double SealY=71,string? SignatureData=null);
