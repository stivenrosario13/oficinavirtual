using System.Data;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;

record WorkshopActor(string Id,string Role,string? Team,string Name,string Login)
{
    public string Department=>Role switch{"Technology"=>"TECHNOLOGY","GeneralServices"=>"GENERAL_SERVICES","HumanResources"=>"HUMAN_RESOURCES",_=>""};
    public bool IsTechnologyManager=>Role=="Technology"&&Team is null;
    public bool CanOperate=>Role=="Administrator"||Team=="WORKSHOP"||IsTechnologyManager;
    public static WorkshopActor From(HttpContext c)=>new(c.User.FindFirstValue(ClaimTypes.NameIdentifier)??"",c.User.FindFirstValue(ClaimTypes.Role)??"",c.User.FindFirstValue("support_team"),c.User.FindFirstValue("display_name")??"Usuario",c.User.Identity?.Name??"");
}
record WorkshopAction(string Action,string? ExpectedStatus=null,string? Note=null,string? ReplacementSerial=null);
static partial class WorkshopCode
{
 public static string Normalize(string raw)
 {
  var code=raw.Trim();
  if(Uri.TryCreate(code,UriKind.Absolute,out var uri))
  {
   var query=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
   if(query.TryGetValue("equipment",out var equipment)&&!string.IsNullOrWhiteSpace(equipment))code=equipment.ToString();
  }
  try{code=Uri.UnescapeDataString(code);}catch(UriFormatException){}
  code=code.Trim().ToUpperInvariant()
   .Replace('’','\'').Replace('‘','\'').Replace('´','\'').Replace('`','\'')
   .Replace('Ç','|').Replace('¦','|');
  var prefixed=MalformedPrefixedQr().Match(code);
  if(prefixed.Success)return $"REAL-MNT|{prefixed.Groups[1].Value}-{prefixed.Groups[2].Value}-{prefixed.Groups[3].Value}-{prefixed.Groups[4].Value}";
  var document=MalformedDocument().Match(code);
  if(document.Success)return $"{document.Groups[1].Value}-{document.Groups[2].Value}-{document.Groups[3].Value}-{document.Groups[4].Value}";
  return code;
 }
 [GeneratedRegex(@"REAL\W*MNT\W*(ALM|TAL)\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{5,})$")]
 private static partial Regex MalformedPrefixedQr();
 [GeneratedRegex(@"(?:^|\W)(ALM|TAL)\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{5,})$")]
 private static partial Regex MalformedDocument();
}
static class WorkshopEndpoints
{
 public static void MapWorkshopEndpoints(this WebApplication app)
 {
  app.MapGet("/api/workshop",async(HttpContext context,Database db,CancellationToken ct)=>Results.Content(await db.WorkshopSnapshot(WorkshopActor.From(context),null,ct),"application/json")).RequireAuthorization("PortalUser");
  app.MapGet("/api/workshop/track",async(string code,HttpContext context,Database db,CancellationToken ct)=>{
   if(string.IsNullOrWhiteSpace(code)||code.Length>400)return Results.UnprocessableEntity(new{error="Escanea el QR o escribe el número del formulario."});
   context.Response.Headers.CacheControl="no-store";
   return Results.Content(await db.WorkshopSnapshot(WorkshopActor.From(context),WorkshopCode.Normalize(code),ct),"application/json");
  }).RequireAuthorization("PortalUser");
  app.MapPost("/api/workshop/{id:guid}/actions",async(Guid id,WorkshopAction body,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
   if(body.Action is not ("RECEIVE" or "REPAIR" or "REPLACE" or "WORK" or "DELIVER")||(body.Note?.Length??0)>2000||(body.ReplacementSerial?.Length??0)>120)return Results.UnprocessableEntity(new{error="La acción o el detalle del trabajo no es válido."});
   var actor=WorkshopActor.From(context);
   var r=await db.WorkshopTransition(id,body,actor,ct);
   if(r.Ok){
    broker.Publish("maintenance-workshop-updated");
    if(body.Action=="DELIVER"&&await db.NotifyWorkshopDeliveryReady(id,actor,ct)){broker.Publish("chat-message");broker.Publish("institutional-document-created");}
   }
   return r.Ok?Results.Ok(new{ok=true}):Results.Json(new{error=r.Error},statusCode:r.Code=="FORBIDDEN"?403:r.Code=="VALIDATION"?422:409);
  }).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
 }
}

partial class Database
{
 static readonly SemaphoreSlim WorkshopSchemaLock=new(1,1);
 static bool workshopSchemaReady;
 static async Task EnsureWorkshopSchema(SqlConnection connection,CancellationToken ct)
 {
  await EnsureMaintenanceSchema(connection,ct);
  if(workshopSchemaReady)return;
  await WorkshopSchemaLock.WaitAsync(ct);
  try{
   if(workshopSchemaReady)return;
   const string sql="""
    IF OBJECT_ID(N'dbo.maintenance_workshop_cases',N'U') IS NULL
    CREATE TABLE dbo.maintenance_workshop_cases(
      source_id uniqueidentifier NOT NULL PRIMARY KEY REFERENCES dbo.maintenance_movements(id),
      status varchar(20) NOT NULL CHECK(status IN('RECEIVED','REPAIR','REPLACEMENT','DELIVERED')),
      return_name nvarchar(160) NOT NULL,
      sender_user_id nvarchar(80) NULL,
      received_by_user_id nvarchar(80) NOT NULL,
      received_by_name nvarchar(160) NOT NULL,
      received_at datetime2(3) NOT NULL,
      replacement_serial nvarchar(120) NULL,
      updated_at datetime2(3) NOT NULL
    );
    IF OBJECT_ID(N'dbo.maintenance_workshop_events',N'U') IS NULL
    CREATE TABLE dbo.maintenance_workshop_events(
      id uniqueidentifier NOT NULL PRIMARY KEY,
      source_id uniqueidentifier NOT NULL REFERENCES dbo.maintenance_workshop_cases(source_id),
      action varchar(20) NOT NULL,
      status varchar(20) NOT NULL,
      note nvarchar(2000) NULL,
      actor_user_id nvarchar(80) NOT NULL,
      actor_name nvarchar(160) NOT NULL,
      actor_login nvarchar(256) NOT NULL,
      occurred_at datetime2(3) NOT NULL,
      movement_id uniqueidentifier NULL REFERENCES dbo.maintenance_movements(id),
      replacement_serial nvarchar(120) NULL
    );
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_workshop_events') AND name='IX_workshop_source')
      CREATE INDEX IX_workshop_source ON dbo.maintenance_workshop_events(source_id,occurred_at);
    """;
   await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync(ct);workshopSchemaReady=true;
  }finally{WorkshopSchemaLock.Release();}
 }
 const string WorkshopAccess="""
  (@admin=1 OR m.created_by_user_id=@user OR m.technician_user_id=@user
    OR (@team='WAREHOUSE' AND m.operational_area='WAREHOUSE')
    OR (@technologyManager=1 AND m.department='TECHNOLOGY')
    OR (@team='WORKSHOP' AND m.department=@department))
  """;
 static void WorkshopParams(SqlCommand cmd,WorkshopActor actor){
  cmd.Parameters.AddWithValue("@admin",actor.Role=="Administrator"?1:0);
  cmd.Parameters.AddWithValue("@technologyManager",actor.IsTechnologyManager?1:0);
  cmd.Parameters.AddWithValue("@user",actor.Id);cmd.Parameters.AddWithValue("@team",actor.Team??"");
  cmd.Parameters.AddWithValue("@department",actor.Department);
 }
 public async Task<string> WorkshopSnapshot(WorkshopActor actor,string? code,CancellationToken ct)
 {
  code=string.IsNullOrWhiteSpace(code)?null:WorkshopCode.Normalize(code);
  await using var connection=await Open(ct);await EnsureWorkshopSchema(connection,ct);
  var sql=$"""
   SELECT m.id,m.document_number documentNumber,m.qr_token qrToken,m.equipment_type equipmentType,
     m.serial_number serialNumber,m.quantity,m.department,m.delivered_by_name deliveredByName,
     m.created_by_name senderName,m.created_at sentAt,m.failure_cause failureCause,
     COALESCE(c.status,'SENT') status,COALESCE(c.return_name,m.delivered_by_name) returnName,
     c.received_by_name receivedByName,c.received_at receivedAt,c.replacement_serial replacementSerial,
     CAST(CASE WHEN @admin=1 OR @technologyManager=1 OR (@team='WORKSHOP' AND m.department=@department) THEN 1 ELSE 0 END AS bit) canOperate,
     JSON_QUERY(COALESCE((SELECT e.id,e.action,e.status,e.note,e.actor_name actorName,e.actor_login actorLogin,
       e.occurred_at occurredAt,e.replacement_serial replacementSerial,e.movement_id movementId
       FROM dbo.maintenance_workshop_events e WHERE e.source_id=m.id ORDER BY e.occurred_at,e.id FOR JSON PATH),'[]')) events
   FROM dbo.maintenance_movements m
   LEFT JOIN dbo.maintenance_workshop_cases c ON c.source_id=m.id
   WHERE m.movement_type='TRANSFER_TO_WORKSHOP' AND m.department<>'HUMAN_RESOURCES'
    AND (m.department<>'GENERAL_SERVICES' OR UPPER(m.equipment_type) LIKE '%INVERSOR%' OR UPPER(m.equipment_type) LIKE '%INVERTER%') AND {WorkshopAccess}
    AND (@code IS NULL OR m.qr_token=@code OR m.document_number=@code OR CONVERT(varchar(36),m.id)=@code
      OR EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events e JOIN dbo.maintenance_movements child ON child.id=e.movement_id
        WHERE e.source_id=m.id AND (child.qr_token=@code OR child.document_number=@code)))
   ORDER BY m.created_at DESC FOR JSON PATH;
   """;
  await using var cmd=new SqlCommand(sql,connection);WorkshopParams(cmd,actor);cmd.Parameters.AddWithValue("@code",Db(code));
  return (string?)await cmd.ExecuteScalarAsync(ct)??"[]";
 }
 public async Task<TicketActionResult> WorkshopTransition(Guid id,WorkshopAction input,WorkshopActor actor,CancellationToken ct)
 {
  if(!actor.CanOperate)return new(false,"FORBIDDEN","Solo Taller puede avanzar el equipo; el remitente puede consultar su seguimiento.");
  await using var connection=await Open(ct);await EnsureWorkshopSchema(connection,ct);
  await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  async Task<TicketActionResult> Fail(string code,string error){await tx.RollbackAsync(ct);return new(false,code,error);}
  try{
   string? status=null,returnName=null,sourceSerial=null;bool exists=false;
   await using(var cmd=new SqlCommand("""
    SELECT COALESCE(c.status,'SENT'),COALESCE(c.return_name,m.delivered_by_name),m.serial_number
    FROM dbo.maintenance_movements m WITH(UPDLOCK,HOLDLOCK)
    LEFT JOIN dbo.maintenance_workshop_cases c WITH(UPDLOCK,HOLDLOCK) ON c.source_id=m.id
    WHERE m.id=@id AND m.movement_type='TRANSFER_TO_WORKSHOP' AND m.department<>'HUMAN_RESOURCES'
      AND (m.department<>'GENERAL_SERVICES' OR UPPER(m.equipment_type) LIKE '%INVERSOR%' OR UPPER(m.equipment_type) LIKE '%INVERTER%')
      AND (@admin=1 OR m.department=@department)
    """,connection,tx)){
    cmd.Parameters.AddWithValue("@id",id);WorkshopParams(cmd,actor);
    await using var reader=await cmd.ExecuteReaderAsync(ct);
    if(await reader.ReadAsync(ct)){exists=true;status=reader.GetString(0);returnName=reader.IsDBNull(1)?null:reader.GetString(1);sourceSerial=reader.IsDBNull(2)?null:reader.GetString(2);}
   }
   if(!exists)return await Fail("FORBIDDEN","El envío no existe o no pertenece a tu departamento.");
   if(input.ExpectedStatus!=status)return await Fail("CONFLICT","El equipo cambió de estado. Actualiza la tabla antes de continuar.");
   var next=input.Action switch{
    "RECEIVE" when status=="SENT"=>"RECEIVED",
    "REPAIR" when status=="RECEIVED"=>"REPAIR",
    "REPLACE" when status=="RECEIVED"=>"REPLACEMENT",
    "WORK" when status is "REPAIR" or "REPLACEMENT"=>status,
    "DELIVER" when status is "REPAIR" or "REPLACEMENT"=>"DELIVERED",
    _=>null
   };
   if(next is null)return await Fail("CONFLICT","Esta acción no corresponde al estado actual del equipo.");
   if(string.IsNullOrWhiteSpace(returnName))return await Fail("VALIDATION","El envío original no identifica quién entregó el equipo. Debe corregirse ese dato antes de recibirlo.");
   var note=input.Note?.Trim();
   if(input.Action=="WORK"&&(note?.Length??0)<5)return await Fail("VALIDATION","Describe el diagnóstico, trabajo realizado o resultado (mínimo 5 caracteres).");
   var replacement=input.ReplacementSerial?.Trim().ToUpperInvariant();
   if(!string.IsNullOrEmpty(replacement)&&(status!="REPLACEMENT"||input.Action!="WORK"))return await Fail("VALIDATION","El serial sustituto se registra en la bitácora de reemplazo.");
   if(!string.IsNullOrEmpty(replacement)&&string.Equals(replacement,sourceSerial?.Trim(),StringComparison.OrdinalIgnoreCase))return await Fail("VALIDATION","El equipo sustituto debe tener un serial distinto al original.");
   if(input.Action=="DELIVER"){
    await using var check=new SqlCommand("""
     SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events WHERE source_id=@id AND action='WORK')
       AND (@replacement=0 OR EXISTS(SELECT 1 FROM dbo.maintenance_workshop_cases WHERE source_id=@id AND NULLIF(replacement_serial,'') IS NOT NULL))
     THEN 1 ELSE 0 END
     """,connection,tx);
    check.Parameters.AddWithValue("@id",id);check.Parameters.AddWithValue("@replacement",status=="REPLACEMENT"?1:0);
    if(Convert.ToInt32(await check.ExecuteScalarAsync(ct))!=1)return await Fail("VALIDATION","Registra primero el trabajo realizado y, si es reemplazo, el serial del equipo sustituto.");
   }
   var movement=input.Action switch{"RECEIVE"=>"ENTRY","REPAIR"=>"REPAIR","REPLACE"=>"REPLACEMENT","DELIVER"=>"EXIT",_=>null};
   var eventId=Guid.NewGuid();var movementId=movement is null?(Guid?)null:Guid.NewGuid();
   var document="TAL-"+Guid.NewGuid().ToString("N");
   const string write="""
    DECLARE @now datetime2(3)=SYSUTCDATETIME();
    IF @action='RECEIVE'
      INSERT INTO dbo.maintenance_workshop_cases(source_id,status,return_name,sender_user_id,received_by_user_id,received_by_name,received_at,updated_at)
      SELECT id,@next,delivered_by_name,created_by_user_id,@user,@actor,@now,@now FROM dbo.maintenance_movements WHERE id=@id;
    ELSE UPDATE dbo.maintenance_workshop_cases SET status=@next,updated_at=@now,
      replacement_serial=COALESCE(NULLIF(@replacementSerial,''),replacement_serial) WHERE source_id=@id;
    IF @movement IS NOT NULL
    BEGIN
      INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,
       movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,
       created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,occurred_at)
      SELECT @movementId,m.agency_id,m.ticket_id,m.department,m.technician_user_id,@actor,
       @movement,m.equipment_type,m.component_type,@cause,
       CASE WHEN @action='DELIVER' THEN COALESCE(c.replacement_serial,m.serial_number) ELSE m.serial_number END,
       m.quantity,@note,@user,@actor,'WORKSHOP',@document,CONCAT('REAL-MNT|',@document),
       CASE WHEN @action='RECEIVE' THEN c.return_name ELSE @actor END,
       CASE WHEN @action='DELIVER' THEN c.return_name ELSE @actor END,
       CASE WHEN @action='DELIVER' THEN c.return_name ELSE N'Taller' END,@now
      FROM dbo.maintenance_movements m JOIN dbo.maintenance_workshop_cases c ON c.source_id=m.id WHERE m.id=@id;
    END;
    INSERT INTO dbo.maintenance_workshop_events(id,source_id,action,status,note,actor_user_id,actor_name,actor_login,occurred_at,movement_id,replacement_serial)
     VALUES(@eventId,@id,@action,@next,@note,@user,@actor,@login,@now,@movementId,NULLIF(@replacementSerial,''));
    """;
   await using(var cmd=new SqlCommand(write,connection,tx)){
    cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@action",input.Action);cmd.Parameters.AddWithValue("@next",next);
    cmd.Parameters.AddWithValue("@replacementSerial",Db(replacement));cmd.Parameters.AddWithValue("@movement",Db(movement));
    cmd.Parameters.AddWithValue("@movementId",Db(movementId));cmd.Parameters.AddWithValue("@eventId",eventId);
    cmd.Parameters.AddWithValue("@note",Db(note));cmd.Parameters.AddWithValue("@user",actor.Id);cmd.Parameters.AddWithValue("@actor",actor.Name);
    cmd.Parameters.AddWithValue("@login",actor.Login);cmd.Parameters.AddWithValue("@document",document);
    cmd.Parameters.AddWithValue("@cause",input.Action switch{"RECEIVE"=>"Recepción automática de envío a Taller","REPAIR"=>"Equipo en reparación","REPLACE"=>"Equipo en reemplazo","DELIVER"=>"Entrega a quien entregó el equipo original",_=>"Bitácora de trabajo"});
    await cmd.ExecuteNonQueryAsync(ct);
   }
   await tx.CommitAsync(ct);return new(true,"OK",null);
  }catch{await tx.RollbackAsync(ct);throw;}
 }

 public async Task<bool> NotifyWorkshopDeliveryReady(Guid id,WorkshopActor actor,CancellationToken ct)
 {
  try{
   await using var connection=await Open(ct);await EnsureWorkshopSchema(connection,ct);await EnsureWorkshopDeliveryNotificationSchema(connection,ct);
   string? target=null,returnName=null,equipment=null,serial=null,document=null,movementDocument=null;Guid? movementId=null;DateTime? deliveredAt=null;
   await using(var read=new SqlCommand("""
    SELECT TOP(1) NULLIF(c.sender_user_id,N''),c.return_name,m.equipment_type,COALESCE(c.replacement_serial,m.serial_number),m.document_number,e.movement_id,child.document_number,e.occurred_at
    FROM dbo.maintenance_movements m
    JOIN dbo.maintenance_workshop_cases c ON c.source_id=m.id
    JOIN dbo.maintenance_workshop_events e ON e.source_id=m.id AND e.action='DELIVER'
    LEFT JOIN dbo.maintenance_movements child ON child.id=e.movement_id
    WHERE m.id=@id
    ORDER BY e.occurred_at DESC,e.id DESC;
    """,connection)){
    read.Parameters.AddWithValue("@id",id);await using var r=await read.ExecuteReaderAsync(ct);
    if(!await r.ReadAsync(ct))return false;
    target=r.IsDBNull(0)?null:r.GetString(0);returnName=r.IsDBNull(1)?null:r.GetString(1);equipment=r.IsDBNull(2)?"Equipo":r.GetString(2);serial=r.IsDBNull(3)?"Sin serial":r.GetString(3);document=r.IsDBNull(4)?"Formulario Taller":r.GetString(4);movementId=r.IsDBNull(5)?null:r.GetGuid(5);movementDocument=r.IsDBNull(6)?document:r.GetString(6);deliveredAt=r.IsDBNull(7)?DateTime.UtcNow:r.GetDateTime(7);
   }
   if(string.IsNullOrWhiteSpace(target)||movementId is null)return false;
   var conversationId=Guid.NewGuid();Guid? existing=null;var category="DIRECT:"+actor.Id;
   await using(var find=new SqlCommand("SELECT TOP(1) id FROM dbo.support_conversations WHERE supervisor_user_id=@target AND category=@category AND deleted_at IS NULL ORDER BY last_message_at DESC",connection)){find.Parameters.AddWithValue("@target",target);find.Parameters.AddWithValue("@category",category);var found=await find.ExecuteScalarAsync(ct);if(found is Guid g)existing=g;}
   if(existing is null){
    await using var create=new SqlCommand("INSERT INTO dbo.support_conversations(id,supervisor_user_id,supervisor_name,supervisor_username,assigned_department,category,status,requested_agent) VALUES(@id,@target,@returnName,@target,N'WORKSHOP',@category,'IN_PROGRESS',0);",connection);
    create.Parameters.AddWithValue("@id",conversationId);create.Parameters.AddWithValue("@target",target);create.Parameters.AddWithValue("@returnName",Db(returnName??target));create.Parameters.AddWithValue("@category",category);await create.ExecuteNonQueryAsync(ct);
   }else conversationId=existing.Value;
   var pdfUrl=$"/api/maintenance/movements/{movementId}/document";
   var message=$"Tu equipo reparado está listo para retirar. Equipo: {equipment}. Serial: {serial}. Comprobante PDF + QR: {movementDocument}. Abre el comprobante aquí: {pdfUrl}";
   await using(var msg=new SqlCommand("""
    INSERT INTO dbo.support_messages(conversation_id,sender_user_id,sender_name,sender_role,message,is_bot)
    VALUES(@conversation,@sender,@senderName,@role,@message,0);
    UPDATE dbo.support_conversations SET status='IN_PROGRESS',updated_at=SYSUTCDATETIME(),last_message_at=SYSUTCDATETIME() WHERE id=@conversation;
    """,connection)){
    msg.Parameters.AddWithValue("@conversation",conversationId);msg.Parameters.AddWithValue("@sender",actor.Id);msg.Parameters.AddWithValue("@senderName","Taller - "+actor.Name);msg.Parameters.AddWithValue("@role",actor.Role);msg.Parameters.AddWithValue("@message",message.Length>2000?message[..2000]:message);await msg.ExecuteNonQueryAsync(ct);
   }
   var title=$"Equipo reparado listo para retirar - {movementDocument}";var fileName=$"comprobante-taller-{SafeFileName(movementDocument??document??"taller")}.pdf";
   var pdfBody=string.Join("\n",new[]{$"Remitente: {returnName??target}",$"Equipo: {equipment}",$"Serial: {serial}",$"Formulario: {movementDocument}",$"Fecha: {deliveredAt:yyyy-MM-dd HH:mm} UTC",$"Enlace del comprobante: {pdfUrl}"});
   var pdf=WorkshopReceiptPdf(title,pdfBody);
   var metadata=JsonSerializer.Serialize(new { area="communications",channel="requests",title,description=message,originalFileName=fileName,contentType="application/pdf",size=pdf.Length,ownerUserName=actor.Id,ownerDisplayName="Taller - "+actor.Name,ownerLogin=actor.Login,department="Taller",recipientUserName=target,recipientDepartment=(string?)null,status="received",trashed=false,createdUtc=DateTime.UtcNow,sentUtc=DateTime.UtcNow,receivedUtc=DateTime.UtcNow,receivedByUserName=target,receiverDisplayName=returnName??target,receiverLogin=(string?)null,receiptCode=$"REAL-TAL-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000,999999)}",verificationToken=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),sealX=65,sealY=72,workshopMovementId=movementId.Value.ToString(),sharedWith=Array.Empty<object>(),history=new[]{new{action="workshop-delivery",actor=actor.Name,login=actor.Login,at=DateTime.UtcNow,previousStatus="DELIVERED",receiptCode=(string?)null,receiverLogin=(string?)null,receivedUtc=(DateTime?)null,previousReceiptSignature=(string?)null}}},new JsonSerializerOptions(JsonSerializerDefaults.Web));
   await using(var file=new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.institutional_files WHERE area='communications' AND channel='requests' AND JSON_VALUE(metadata,'$.workshopMovementId')=@movement) INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) VALUES(@owner,N'Taller','communications','requests',@json,@bytes);",connection)){
    file.Parameters.AddWithValue("@owner",actor.Id);file.Parameters.AddWithValue("@json",metadata);file.Parameters.AddWithValue("@bytes",pdf);file.Parameters.AddWithValue("@movement",movementId.Value.ToString());await file.ExecuteNonQueryAsync(ct);
   }
   return true;
  }catch(Exception error) when(!ct.IsCancellationRequested){Console.Error.WriteLine($"Workshop delivery notification: {error.Message}");return false;}
 }

 public async Task<bool> NotifyMaintenanceReceiptCreated(string documentNumber,WorkshopActor actor,CancellationToken ct)
 {
  try{
   await using var connection=await Open(ct);await EnsureWorkshopDeliveryNotificationSchema(connection,ct);
   Guid movementId;string target,recipientName,equipment,serial,movement,department,qr;DateTime createdAt;
   await using(var read=new SqlCommand("""
    SELECT TOP(1) m.id,m.technician_user_id,COALESCE(NULLIF(u.display_name,N''),NULLIF(m.received_by_name,N''),m.technician_user_id),m.equipment_type,m.serial_number,m.movement_type,m.department,m.qr_token,m.created_at
    FROM dbo.maintenance_movements m LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=m.technician_user_id
    WHERE m.document_number=@document AND NULLIF(m.technician_user_id,N'') IS NOT NULL ORDER BY m.created_at DESC;
    """,connection)){
    read.Parameters.AddWithValue("@document",documentNumber.Trim().ToUpperInvariant());await using var r=await read.ExecuteReaderAsync(ct);
    if(!await r.ReadAsync(ct))return false;
    movementId=r.GetGuid(0);target=r.GetString(1);recipientName=r.GetString(2);equipment=r.GetString(3);serial=r.GetString(4);movement=r.GetString(5);department=r.GetString(6);qr=r.GetString(7);createdAt=r.GetDateTime(8);
   }
   var category="DIRECT:"+actor.Id;Guid conversationId;var existing=(object?)null;
   await using(var find=new SqlCommand("SELECT TOP(1) id FROM dbo.support_conversations WHERE supervisor_user_id=@target AND category=@category AND deleted_at IS NULL ORDER BY last_message_at DESC",connection)){find.Parameters.AddWithValue("@target",target);find.Parameters.AddWithValue("@category",category);existing=await find.ExecuteScalarAsync(ct);}
   if(existing is Guid found)conversationId=found;else{
    conversationId=Guid.NewGuid();await using var create=new SqlCommand("INSERT INTO dbo.support_conversations(id,supervisor_user_id,supervisor_name,supervisor_username,assigned_department,category,status,requested_agent) VALUES(@id,@target,@name,@target,@department,@category,'IN_PROGRESS',0);",connection);
    create.Parameters.AddWithValue("@id",conversationId);create.Parameters.AddWithValue("@target",target);create.Parameters.AddWithValue("@name",recipientName);create.Parameters.AddWithValue("@department",department);create.Parameters.AddWithValue("@category",category);await create.ExecuteNonQueryAsync(ct);
   }
   var pdfUrl=$"/api/maintenance/movements/{movementId}/document";var trackingUrl=$"/?equipment={Uri.EscapeDataString(qr)}";
   var message=$"Se registró un comprobante para ti. Formulario: {documentNumber}. Equipo: {equipment}. Serial: {serial}. Consulta el PDF: {pdfUrl}. Seguimiento QR: {trackingUrl}";
   await using(var msg=new SqlCommand("INSERT INTO dbo.support_messages(conversation_id,sender_user_id,sender_name,sender_role,message,is_bot) VALUES(@conversation,@sender,@name,@role,@message,0); UPDATE dbo.support_conversations SET status='IN_PROGRESS',updated_at=SYSUTCDATETIME(),last_message_at=SYSUTCDATETIME() WHERE id=@conversation;",connection)){msg.Parameters.AddWithValue("@conversation",conversationId);msg.Parameters.AddWithValue("@sender",actor.Id);msg.Parameters.AddWithValue("@name",actor.Name);msg.Parameters.AddWithValue("@role",actor.Role);msg.Parameters.AddWithValue("@message",message);await msg.ExecuteNonQueryAsync(ct);}
   var title=$"Comprobante de entrega - {documentNumber}";var fileName=$"comprobante-{SafeFileName(documentNumber)}.pdf";
   var pdf=WorkshopReceiptPdf(title,string.Join("\n",new[]{$"Recibe: {recipientName}",$"Entrega: {actor.Name}",$"Movimiento: {movement}",$"Departamento: {department}",$"Equipo: {equipment}",$"Serial: {serial}",$"Fecha: {createdAt:yyyy-MM-dd HH:mm} UTC",$"Formulario PDF: {pdfUrl}",$"Seguimiento QR: {trackingUrl}"}));
   var metadata=JsonSerializer.Serialize(new {area="communications",channel="requests",title,description=message,originalFileName=fileName,contentType="application/pdf",size=pdf.Length,ownerUserName=actor.Id,ownerDisplayName=actor.Name,ownerLogin=actor.Login,department,recipientUserName=target,recipientDepartment=department,status="received",trashed=false,createdUtc=DateTime.UtcNow,sentUtc=DateTime.UtcNow,receivedUtc=DateTime.UtcNow,receivedByUserName=target,receiverDisplayName=recipientName,receiverLogin=(string?)null,receiptCode=qr,verificationToken=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),sealX=65,sealY=72,maintenanceMovementId=movementId.ToString(),sharedWith=Array.Empty<object>(),history=new[]{new{action="maintenance-receipt-created",actor=actor.Name,login=actor.Login,at=DateTime.UtcNow}}},new JsonSerializerOptions(JsonSerializerDefaults.Web));
   await using(var file=new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.institutional_files WHERE JSON_VALUE(metadata,'$.maintenanceMovementId')=@movement) INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) VALUES(@owner,@department,'communications','requests',@json,@bytes);",connection)){file.Parameters.AddWithValue("@movement",movementId.ToString());file.Parameters.AddWithValue("@owner",actor.Id);file.Parameters.AddWithValue("@department",department);file.Parameters.AddWithValue("@json",metadata);file.Parameters.AddWithValue("@bytes",pdf);await file.ExecuteNonQueryAsync(ct);}
   return true;
  }catch(Exception error) when(!ct.IsCancellationRequested){Console.Error.WriteLine($"Maintenance receipt notification: {error.Message}");return false;}
 }
 static async Task EnsureWorkshopDeliveryNotificationSchema(SqlConnection connection,CancellationToken ct)
 {
  const string sql="""
   IF OBJECT_ID(N'dbo.support_conversations',N'U') IS NULL
   CREATE TABLE dbo.support_conversations(id uniqueidentifier NOT NULL PRIMARY KEY,supervisor_user_id nvarchar(80) NOT NULL,supervisor_name nvarchar(160) NOT NULL,supervisor_username nvarchar(180) NOT NULL,assigned_department varchar(40) NOT NULL,category varchar(60) NULL,status varchar(25) NOT NULL DEFAULT('WAITING_SUPPORT'),requested_agent bit NOT NULL DEFAULT(0),created_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()),updated_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()),last_message_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()),deleted_at datetime2(3) NULL,is_restricted bit NOT NULL DEFAULT(0),is_blocked bit NOT NULL DEFAULT(0));
   IF COL_LENGTH(N'dbo.support_conversations',N'deleted_at') IS NULL ALTER TABLE dbo.support_conversations ADD deleted_at datetime2(3) NULL;
   IF COL_LENGTH(N'dbo.support_conversations',N'is_restricted') IS NULL ALTER TABLE dbo.support_conversations ADD is_restricted bit NOT NULL DEFAULT(0);
   IF COL_LENGTH(N'dbo.support_conversations',N'is_blocked') IS NULL ALTER TABLE dbo.support_conversations ADD is_blocked bit NOT NULL DEFAULT(0);
   IF OBJECT_ID(N'dbo.support_messages',N'U') IS NULL
   CREATE TABLE dbo.support_messages(id uniqueidentifier NOT NULL DEFAULT(NEWID()) PRIMARY KEY,conversation_id uniqueidentifier NOT NULL,sender_user_id nvarchar(80) NULL,sender_name nvarchar(160) NOT NULL,sender_role varchar(40) NOT NULL,message nvarchar(2000) NOT NULL,is_bot bit NOT NULL DEFAULT(0),created_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()),deleted_at datetime2(3) NULL);
   IF COL_LENGTH(N'dbo.support_messages',N'deleted_at') IS NULL ALTER TABLE dbo.support_messages ADD deleted_at datetime2(3) NULL;
   IF OBJECT_ID(N'dbo.institutional_files',N'U') IS NULL
   CREATE TABLE dbo.institutional_files(id bigint IDENTITY PRIMARY KEY,owner_id nvarchar(80) NOT NULL,department nvarchar(80) NOT NULL,area varchar(20) NOT NULL,channel varchar(20) NOT NULL,metadata nvarchar(max) NOT NULL CHECK(ISJSON(metadata)=1),file_data varbinary(max) NOT NULL);
   """;
  await using var cmd=new SqlCommand(sql,connection);await cmd.ExecuteNonQueryAsync(ct);
 }
 static string SafeFileName(string value)=>Regex.Replace(value,"[^A-Za-z0-9._-]+","-").Trim('-');
 static byte[] WorkshopReceiptPdf(string title,string body)
 {
  static string PdfText(string value)=>value.Replace("\\","\\\\").Replace("(","\\(").Replace(")","\\)").Replace("\r","");
  var lines=(title+"\n\n"+body).Split('\n').Take(24).Select(PdfText).ToArray();
  var content=new StringBuilder("BT /F1 13 Tf 50 780 Td ");
  foreach(var line in lines){content.Append('(').Append(line).Append(") Tj 0 -20 Td ");}
  content.Append("ET");
  var stream=Encoding.ASCII.GetBytes(content.ToString());
  var objects=new List<string>{
   "<< /Type /Catalog /Pages 2 0 R >>",
   "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
   $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
   $"<< /Length {stream.Length} >>\nstream\n{content}\nendstream",
   "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
  };
  using var ms=new MemoryStream();
  using var writer=new StreamWriter(ms,Encoding.ASCII,1024,true){NewLine="\n"};
  writer.Write("%PDF-1.4\n");
  var offsets=new List<long>{0};
  for(var index=0;index<objects.Count;index++){offsets.Add(ms.Position);writer.Write($"{index+1} 0 obj\n{objects[index]}\nendobj\n");writer.Flush();}
  var xref=ms.Position;
  writer.Write($"xref\n0 {objects.Count+1}\n0000000000 65535 f \n");
  foreach(var offset in offsets.Skip(1))writer.Write($"{offset:0000000000} 00000 n \n");
  writer.Write($"trailer << /Size {objects.Count+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
  writer.Flush();return ms.ToArray();
 }

}
