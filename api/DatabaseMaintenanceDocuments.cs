using System.Data;
using Microsoft.Data.SqlClient;

partial class Database
{
    static readonly SemaphoreSlim DocumentSchemaLock = new(1,1);
    static bool documentSchemaReady;
    static async Task EnsureDocumentSchema(SqlConnection connection,CancellationToken ct)
    {
        await EnsureMaintenanceSchema(connection,ct);
        if(documentSchemaReady)return;
        await DocumentSchemaLock.WaitAsync(ct);
        try
        {
            if(documentSchemaReady)return;
            const string sql="""
                IF OBJECT_ID(N'dbo.maintenance_document_marks',N'U') IS NULL
                CREATE TABLE dbo.maintenance_document_marks(
                    movement_id uniqueidentifier NOT NULL PRIMARY KEY REFERENCES dbo.maintenance_movements(id) ON DELETE CASCADE,
                    received_at datetime2(3) NULL,receiver_user_id nvarchar(80) NULL,receiver_name nvarchar(160) NULL,receiver_login nvarchar(256) NULL,
                    stamp_x float NOT NULL DEFAULT(109),stamp_y float NOT NULL DEFAULT(191),
                    signature_data nvarchar(max) NULL,signer_user_id nvarchar(80) NULL,signer_name nvarchar(160) NULL,signer_login nvarchar(256) NULL,signed_at datetime2(3) NULL
                );
                IF OBJECT_ID(N'dbo.maintenance_document_signatures',N'U') IS NULL
                CREATE TABLE dbo.maintenance_document_signatures(
                    movement_id uniqueidentifier NOT NULL REFERENCES dbo.maintenance_movements(id) ON DELETE CASCADE,
                    signature_role varchar(10) NOT NULL CHECK(signature_role IN('DELIVERY','RECEIPT')),
                    signature_data nvarchar(max) NOT NULL,signer_user_id nvarchar(80) NOT NULL,
                    signer_name nvarchar(160) NOT NULL,signer_login nvarchar(256) NOT NULL,signed_at datetime2(3) NOT NULL,
                    PRIMARY KEY(movement_id,signature_role)
                );
                IF COL_LENGTH(N'dbo.maintenance_document_marks',N'signature_layout_version') IS NULL
                BEGIN
                    ALTER TABLE dbo.maintenance_document_marks ADD signature_layout_version tinyint NOT NULL DEFAULT(2);
                    UPDATE dbo.maintenance_document_marks SET stamp_y=191 WHERE stamp_x=109 AND stamp_y=237;
                END;
                IF OBJECT_ID(N'dbo.maintenance_saved_signatures',N'U') IS NULL
                CREATE TABLE dbo.maintenance_saved_signatures(user_id nvarchar(80) NOT NULL PRIMARY KEY,signature_data nvarchar(max) NOT NULL,updated_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()));
                IF OBJECT_ID(N'dbo.maintenance_department_requests',N'U') IS NULL
                CREATE TABLE dbo.maintenance_department_requests(
                    id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT(NEWID()),department varchar(40) NOT NULL,
                    product_name nvarchar(160) NOT NULL,quantity int NOT NULL,notes nvarchar(2000) NULL,
                    created_by_name nvarchar(160) NOT NULL,created_by_login nvarchar(256) NOT NULL,
                    created_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()),status varchar(20) NOT NULL DEFAULT('SENT')
                );
                IF COL_LENGTH(N'dbo.maintenance_requisitions',N'decided_by_login') IS NULL ALTER TABLE dbo.maintenance_requisitions ADD decided_by_login nvarchar(256) NULL,decided_by_name nvarchar(160) NULL,decided_at datetime2(3) NULL,decision_reason nvarchar(1000) NULL;
                IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_requisitions') AND name='CK_maintenance_requisition_status' AND definition NOT LIKE '%APPROVED%')
                BEGIN
                    ALTER TABLE dbo.maintenance_requisitions DROP CONSTRAINT CK_maintenance_requisition_status;
                    ALTER TABLE dbo.maintenance_requisitions WITH CHECK ADD CONSTRAINT CK_maintenance_requisition_status CHECK(status IN('PENDING','APPROVED','REJECTED','ORDERED','PARTIAL','FULFILLED','CANCELLED'));
                END;
                """;
            await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync(ct);documentSchemaReady=true;
        }
        finally{DocumentSchemaLock.Release();}
    }

    static async Task<bool> CanAccessMaintenanceDocument(SqlConnection connection,Guid id,string userId,string role,string? team,CancellationToken ct)
    {
        const string sql="""
            SELECT COUNT(*) FROM dbo.maintenance_movements m WHERE m.id=@id AND (
                @role='Administrator'
                OR m.created_by_user_id=@user OR m.technician_user_id=@user
                OR (@team='WAREHOUSE' AND m.operational_area='WAREHOUSE' AND m.department='TECHNOLOGY')
                OR (@team='WORKSHOP' AND m.department=@department AND (m.operational_area='WORKSHOP' OR m.movement_type='TRANSFER_TO_WORKSHOP'))
                OR (@team NOT IN('WAREHOUSE','WORKSHOP','TECHNICIANS') AND @department<>'' AND m.department=@department)
                OR (@team='TECHNICIANS' AND m.department=@department AND m.technician_user_id=@user)
                OR (@role='GroupAdministrator' AND m.technician_user_id=@user AND EXISTS(SELECT 1 FROM dbo.agencies a JOIN dbo.admin_user_groups g ON g.group_name=a.grupo WHERE a.id=m.agency_id AND g.user_id=@user))
            );
            """;
        await using var cmd=new SqlCommand(sql,connection);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@user",userId);cmd.Parameters.AddWithValue("@role",role);cmd.Parameters.AddWithValue("@team",team??"");cmd.Parameters.AddWithValue("@department",DepartmentForRole(role));return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))==1;
    }

    public async Task<string?> MaintenanceDocument(Guid id,string userId,string role,string? team,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureDocumentSchema(connection,ct);
        if(!await CanAccessMaintenanceDocument(connection,id,userId,role,team,ct))return null;
        const string sql="""
            SELECT m.id,m.document_number documentNumber,d.received_at receivedAt,d.receiver_name receiverName,d.receiver_login receiverLogin,
                COALESCE(d.stamp_x,109) stampX,COALESCE(d.stamp_y,191) stampY,d.signature_data signatureData,d.signer_name signerName,d.signer_login signerLogin,d.signed_at signedAt,
                JSON_QUERY((SELECT s.signature_data data,s.signer_name name,s.signer_login login,s.signed_at signedAt FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='DELIVERY' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) deliverySignature,
                JSON_QUERY((SELECT s.signature_data data,s.signer_name name,s.signer_login login,s.signed_at signedAt FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='RECEIPT' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) receiptSignature,
                CAST(CASE WHEN d.received_at IS NULL
                    AND EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='DELIVERY')
                    AND EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='RECEIPT')
                    AND (m.movement_type<>'REQUEST' OR m.operational_area<>'WAREHOUSE' OR @team='WAREHOUSE') THEN 1 ELSE 0 END AS bit) canReceive,
                CAST(CASE WHEN d.receiver_user_id=@user OR @admin=1 THEN 1 ELSE 0 END AS bit) canMove,
                CAST(CASE WHEN (m.movement_type<>'REQUEST' OR m.operational_area<>'WAREHOUSE' OR @team<>'WAREHOUSE') AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='DELIVERY' AND s.signer_user_id<>@user) THEN 1 ELSE 0 END AS bit) canSignDelivery,
                CAST(CASE WHEN (m.movement_type<>'REQUEST' OR m.operational_area<>'WAREHOUSE' OR @team='WAREHOUSE') AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='RECEIPT' AND s.signer_user_id<>@user) THEN 1 ELSE 0 END AS bit) canSignReceipt,
                CASE WHEN m.movement_type IN('EXIT','NEW_DELIVERY') THEN 'ENTREGADO' ELSE 'RECIBIDO' END stampLabel,
                CAST(1 AS bit) requiresBothSignatures
            FROM dbo.maintenance_movements m LEFT JOIN dbo.maintenance_document_marks d ON d.movement_id=m.id WHERE m.id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
            """;
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@user",userId);command.Parameters.AddWithValue("@team",team??"");command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);return (string?)await command.ExecuteScalarAsync(ct);
    }

    public async Task<TicketActionResult> MarkMaintenanceDocument(Guid id,MaintenanceDocumentAction body,string userId,string role,string? team,string actor,string login,CancellationToken ct)
    {
        if(body.Action=="SIGN"&&body.SignatureRole is not ("DELIVERY" or "RECEIPT"))return new(false,"VALIDATION","Selecciona si firmas como quien entrega o quien recibe. Actualiza el formulario.");
        await using var connection=await Open(ct);await EnsureDocumentSchema(connection,ct);
        if(!await CanAccessMaintenanceDocument(connection,id,userId,role,team,ct))return new(false,"FORBIDDEN","No tienes acceso a este formulario.");
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            await using(var ensure=new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.maintenance_document_marks WITH(UPDLOCK,HOLDLOCK) WHERE movement_id=@id) INSERT INTO dbo.maintenance_document_marks(movement_id,stamp_y) VALUES(@id,191);",connection,transaction)){ensure.Parameters.AddWithValue("@id",id);await ensure.ExecuteNonQueryAsync(ct);}
            string movementType,operationalArea;var hasDelivery=false;var hasReceipt=false;var isRequestToWarehouse=false;
            await using(var meta=new SqlCommand("SELECT m.movement_type,m.operational_area,CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='DELIVERY') THEN 1 ELSE 0 END AS bit),CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='RECEIPT') THEN 1 ELSE 0 END AS bit) FROM dbo.maintenance_movements m WHERE m.id=@id;",connection,transaction))
            {
                meta.Parameters.AddWithValue("@id",id);
                await using var reader=await meta.ExecuteReaderAsync(ct);
                if(!await reader.ReadAsync(ct)){await transaction.RollbackAsync(ct);return new(false,"CONFLICT","Formulario no encontrado.");}
                movementType=reader.GetString(0);operationalArea=reader.GetString(1);hasDelivery=reader.GetBoolean(2);hasReceipt=reader.GetBoolean(3);isRequestToWarehouse=movementType=="REQUEST"&&operationalArea=="WAREHOUSE";
            }
            if(body.Action=="SIGN"&&isRequestToWarehouse&&body.SignatureRole=="DELIVERY"&&team=="WAREHOUSE"){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Almacén General solo firma como quien recibe esta solicitud.");}
            if(body.Action=="SIGN"&&isRequestToWarehouse&&body.SignatureRole=="RECEIPT"&&team!="WAREHOUSE"){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","El departamento solicitante solo firma como quien entrega esta solicitud.");}
            if(body.Action=="RECEIVE"&&isRequestToWarehouse&&team!="WAREHOUSE"){await transaction.RollbackAsync(ct);return new(false,"FORBIDDEN","Solo Almacén General puede recibir esta solicitud.");}
            if(body.Action=="RECEIVE"&&(!hasDelivery||!hasReceipt)){await transaction.RollbackAsync(ct);return new(false,"CONFLICT","El formulario debe estar firmado por quien entrega y quien recibe antes de confirmar el sello.");}
            var sql=body.Action switch
            {
                "RECEIVE"=>"UPDATE dbo.maintenance_document_marks SET received_at=SYSUTCDATETIME(),receiver_user_id=@user,receiver_name=@actor,receiver_login=@login,stamp_x=@x,stamp_y=@y WHERE movement_id=@id AND received_at IS NULL;",
                "MOVE"=>"UPDATE dbo.maintenance_document_marks SET stamp_x=@x,stamp_y=@y WHERE movement_id=@id AND received_at IS NOT NULL AND (receiver_user_id=@user OR @admin=1);",
                "SIGN"=>"MERGE dbo.maintenance_document_signatures WITH(HOLDLOCK) AS target USING(SELECT @id movement_id,@signatureRole signature_role) AS source ON target.movement_id=source.movement_id AND target.signature_role=source.signature_role WHEN MATCHED AND target.signer_user_id=@user THEN UPDATE SET signature_data=@signature,signer_name=@actor,signer_login=@login,signed_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(movement_id,signature_role,signature_data,signer_user_id,signer_name,signer_login,signed_at) VALUES(@id,@signatureRole,@signature,@user,@actor,@login,SYSUTCDATETIME());",
                _=>throw new InvalidOperationException("Acción no válida.")
            };
            await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@user",userId);command.Parameters.AddWithValue("@actor",actor);command.Parameters.AddWithValue("@login",login);command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);command.Parameters.AddWithValue("@x",body.X);command.Parameters.AddWithValue("@y",body.Y);command.Parameters.AddWithValue("@signature",Db(body.SignatureData));
            command.Parameters.AddWithValue("@signatureRole",Db(body.SignatureRole));
            if(await command.ExecuteNonQueryAsync(ct)!=1){await transaction.RollbackAsync(ct);return new(false,"CONFLICT","El formulario ya fue recibido o firmado por otra persona. Actualiza para consultar los datos.");}
            await transaction.CommitAsync(ct);return new(true,"OK",null);
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    public async Task<string?> OwnSavedSignature(string userId,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureDocumentSchema(connection,ct);await using var command=new SqlCommand("SELECT signature_data FROM dbo.maintenance_saved_signatures WHERE user_id=@user",connection);command.Parameters.AddWithValue("@user",userId);return await command.ExecuteScalarAsync(ct) as string;
    }
    public async Task SaveOwnSignature(string userId,string signature,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureDocumentSchema(connection,ct);await using var command=new SqlCommand("MERGE dbo.maintenance_saved_signatures WITH(HOLDLOCK) AS target USING(SELECT @user user_id) AS source ON target.user_id=source.user_id WHEN MATCHED THEN UPDATE SET signature_data=@signature,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(user_id,signature_data) VALUES(@user,@signature);",connection);command.Parameters.AddWithValue("@user",userId);command.Parameters.AddWithValue("@signature",signature);await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<string> MaintenanceTrack(string code,string userId,string role,string? team,CancellationToken ct)
    {
        var normalized=MaintenanceTraceCode.Normalize(code);var shortCode=normalized.Contains('|')?normalized[(normalized.IndexOf('|')+1)..]:normalized;
        await using var connection=await Open(ct);await EnsureDocumentSchema(connection,ct);
        const string sql="""
            DECLARE @movement uniqueidentifier=(SELECT TOP 1 m.id FROM dbo.maintenance_movements m WHERE m.qr_token IN(@code,@shortCode) OR m.document_number IN(@code,@shortCode) ORDER BY m.occurred_at DESC,m.created_at DESC);
            SELECT COALESCE((SELECT m.id,m.document_number documentNumber,m.equipment_type equipmentType,m.serial_number serialNumber,m.department,m.movement_type movementType,m.operational_area operationalArea,
                CASE WHEN d.received_at IS NOT NULL AND m.movement_type IN('EXIT','NEW_DELIVERY') THEN 'DELIVERED' WHEN d.received_at IS NOT NULL THEN 'RECEIVED' ELSE 'SENT' END status,
                CASE WHEN d.received_at IS NOT NULL AND m.movement_type IN('EXIT','NEW_DELIVERY') THEN 'Entregado' WHEN d.received_at IS NOT NULL THEN 'Recibido' ELSE 'Pendiente de firmas o recepción' END statusText,
                m.created_at sentAt,m.created_by_name senderName,COALESCE(m.delivered_by_name,m.created_by_name) returnName,d.receiver_name receivedByName,CAST(NULL AS nvarchar(120)) replacementSerial,
                JSON_QUERY(COALESCE((SELECT * FROM (
                    SELECT CONVERT(varchar(36),m.id)+'-created' id,'CREATED' action,'SENT' status,m.failure_cause note,m.created_by_name actorName,'' actorLogin,m.created_at occurredAt,CAST(NULL AS nvarchar(120)) replacementSerial
                    UNION ALL SELECT CONVERT(varchar(36),m.id)+'-delivery','SIGN_DELIVERY','SENT','Firma de quien entrega',s.signer_name,s.signer_login,s.signed_at,NULL FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='DELIVERY'
                    UNION ALL SELECT CONVERT(varchar(36),m.id)+'-receipt','SIGN_RECEIPT','SENT','Firma de quien recibe',s.signer_name,s.signer_login,s.signed_at,NULL FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='RECEIPT'
                    UNION ALL SELECT CONVERT(varchar(36),m.id)+'-received','RECEIVE',CASE WHEN m.movement_type IN('EXIT','NEW_DELIVERY') THEN 'DELIVERED' ELSE 'RECEIVED' END,CASE WHEN m.movement_type IN('EXIT','NEW_DELIVERY') THEN 'Sello de entregado aplicado' ELSE 'Sello de recibido aplicado' END,d.receiver_name,d.receiver_login,d.received_at,NULL WHERE d.received_at IS NOT NULL
                ) events ORDER BY occurredAt FOR JSON PATH),'[]')) events
                FROM dbo.maintenance_movements m LEFT JOIN dbo.maintenance_document_marks d ON d.movement_id=m.id WHERE m.id=@movement AND (
                @role='Administrator'
                OR (@team='WAREHOUSE' AND m.operational_area='WAREHOUSE' AND m.department='TECHNOLOGY')
                OR (@team='WORKSHOP' AND m.department=@department AND (m.operational_area='WORKSHOP' OR m.movement_type='TRANSFER_TO_WORKSHOP'))
                OR (@team NOT IN('WAREHOUSE','WORKSHOP','TECHNICIANS') AND @department<>'' AND m.department=@department)
                OR (@team='TECHNICIANS' AND m.department=@department AND m.technician_user_id=@user)
            ) FOR JSON PATH),'[]');
            """;
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@code",normalized);command.Parameters.AddWithValue("@shortCode",shortCode);command.Parameters.AddWithValue("@user",userId);command.Parameters.AddWithValue("@role",role);command.Parameters.AddWithValue("@team",team??"");command.Parameters.AddWithValue("@department",DepartmentForRole(role));return (string?)await command.ExecuteScalarAsync(ct)??"[]";
    }

    public async Task<string?> WarehouseRequestBoard(string role,string? team,CancellationToken ct)
    {
        var department=DepartmentForRole(role);var all=role=="Administrator";var warehouse=team=="WAREHOUSE";
        if(!all&&department is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES"))return null;
        await using var connection=await Open(ct);await EnsureDocumentSchema(connection,ct);
        const string sql="""
            SELECT JSON_QUERY(COALESCE((SELECT id,department,product_name productName,quantity,notes,created_by_name createdByName,created_by_login createdByLogin,created_at createdAt,status FROM dbo.maintenance_department_requests WHERE @all=1 OR (@warehouse=1 AND department IN('TECHNOLOGY','GENERAL_SERVICES')) OR department=@dept ORDER BY created_at DESC FOR JSON PATH),'[]')) requests,
            JSON_QUERY(COALESCE((SELECT id,requisition_number requisitionNumber,department,product_name productName,quantity_requested quantityRequested,priority,status,notes,requested_by_name requestedByName,created_at createdAt,decided_by_login decidedByLogin,decided_by_name decidedByName,decided_at decidedAt,decision_reason decisionReason FROM dbo.maintenance_requisitions WHERE @all=1 OR (@warehouse=1 AND department IN('TECHNOLOGY','GENERAL_SERVICES')) OR department=@dept ORDER BY created_at DESC FOR JSON PATH),'[]')) requirements FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
            """;
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@all",all?1:0);command.Parameters.AddWithValue("@warehouse",warehouse?1:0);command.Parameters.AddWithValue("@dept",department);return await command.ExecuteScalarAsync(ct) as string;
    }
    public async Task<TicketActionResult> SendWarehouseRequest(WarehouseDepartmentRequest body,string role,string? team,string actor,string login,CancellationToken ct)
    {
        if(role!="Administrator"&&team!="WAREHOUSE")return new(false,"FORBIDDEN","Solo Almacén o Administración puede enviar solicitudes a los departamentos.");
        if(body.Department is not ("TECHNOLOGY" or "GENERAL_SERVICES"))return new(false,"FORBIDDEN","Las comunicaciones del Almacén solo pueden dirigirse a Tecnología o Servicios Generales.");
        await using var connection=await Open(ct);await EnsureDocumentSchema(connection,ct);await using var command=new SqlCommand("INSERT INTO dbo.maintenance_department_requests(department,product_name,quantity,notes,created_by_name,created_by_login) VALUES(@dept,@product,@quantity,@notes,@actor,@login);",connection);command.Parameters.AddWithValue("@dept",body.Department);command.Parameters.AddWithValue("@product",body.ProductName.Trim());command.Parameters.AddWithValue("@quantity",body.Quantity);command.Parameters.AddWithValue("@notes",Db(body.Notes));command.Parameters.AddWithValue("@actor",actor);command.Parameters.AddWithValue("@login",login);await command.ExecuteNonQueryAsync(ct);return new(true,"OK",null);
    }
    public async Task<TicketActionResult> DecideWarehouseRequirement(Guid id,WarehouseDecision body,string role,string? team,string actor,string login,CancellationToken ct)
    {
        if(role!="Administrator"&&team!="WAREHOUSE")return new(false,"FORBIDDEN","Solo Almacén o Administración puede autorizar o rechazar requerimientos.");
        var warehouse=team=="WAREHOUSE";await using var connection=await Open(ct);await EnsureDocumentSchema(connection,ct);await using var command=new SqlCommand("UPDATE dbo.maintenance_requisitions SET status=@status,decided_by_login=@login,decided_by_name=@actor,decided_at=SYSUTCDATETIME(),decision_reason=@reason,updated_at=SYSUTCDATETIME() WHERE id=@id AND status='PENDING' AND (@admin=1 OR (@warehouse=1 AND department IN('TECHNOLOGY','GENERAL_SERVICES')));",connection);command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@status",body.Status);command.Parameters.AddWithValue("@login",login);command.Parameters.AddWithValue("@actor",actor);command.Parameters.AddWithValue("@reason",body.Reason.Trim());command.Parameters.AddWithValue("@admin",role=="Administrator"?1:0);command.Parameters.AddWithValue("@warehouse",warehouse?1:0);return await command.ExecuteNonQueryAsync(ct)==1?new(true,"OK",null):new(false,"CONFLICT","El requerimiento ya fue decidido, no pertenece al alcance del almacén o dejó de estar pendiente.");
    }
}

record MaintenanceDocumentAction(string Action,double X=109,double Y=191,string? SignatureData=null,string? SignatureRole=null);
record SavedSignatureInput(string SignatureData);
record WarehouseDepartmentRequest(string Department,string ProductName,int Quantity,string? Notes);
record WarehouseDecision(string Status,string Reason);

static class MaintenanceTraceCode
{
    public static string Normalize(string value)
    {
        var text=value.Trim();try{text=Uri.UnescapeDataString(text);}catch(UriFormatException){}text=text.ToUpperInvariant().Replace('’','\'').Replace('‘','\'').Replace('´','\'').Replace('`','\'').Replace('Ç','|').Replace('¦','|');
        var match=System.Text.RegularExpressions.Regex.Match(text,@"REAL\W*MNT\W*(ALM|TAL)\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{5,})$");
        if(match.Success)return $"REAL-MNT|{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}-{match.Groups[4].Value}";
        match=System.Text.RegularExpressions.Regex.Match(text,@"(?:^|\W)(ALM|TAL)\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{5,})$");
        return match.Success?$"{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}-{match.Groups[4].Value}":text;
    }
}
