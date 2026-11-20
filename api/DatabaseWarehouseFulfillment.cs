using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

record WarehouseInventoryItem(string Department,string ProductId,string Name,int Available,List<string> Serials,bool IsConsumable,string Warehouse,string UnitId,string Division,string Group,string? LastPurchaseDate,bool TracksSerial);
record WarehouseAllocationItem(long Id,long DocumentId,string RequestNumber,string Department,string AgencyCode,string AgencyName,string AgencyGroup,string Equipment,string Serial,int Quantity,string Status,string? DeliveryNumber,string? DeliveredBy,string? ReceivedBy,DateTime? WithdrawnUtc);
record WarehouseAuthorizationInput(string Note,List<WarehouseAssignedEquipment> Equipment);
record WarehouseApprovalInput(string Note);
record WarehousePurchasePendingInput(string Note,List<int> LineIndexes,List<WarehouseAssignedEquipment> Equipment);
record WarehouseWithdrawalInput(string SealQr,string ReceivedBy,long? DocumentId=null);
record WarehouseInventoryRemovalInput(string Department,string Equipment,int ExpectedQuantity,string? Reason=null);
record WarehouseCatalogProduct(string IdProducto,string Descripcion,bool Producto,string Almacen,int Existencia,string IdUnidad,string Division,string Grupo,string? FechaUltimaCompra,bool Seriales);
record WarehouseCatalogImport(string Version,string? SourceFile,List<WarehouseCatalogProduct> Products);

partial class Database
{
    static readonly HashSet<string> WarehouseConsumables=new(StringComparer.OrdinalIgnoreCase)
    {
        "Papel de baño","Vasos","Cucharas","Marcadores","Lapiceros","Liquipapers","Folders",
        "Hojas blancas","Grapas","Clips","Cinta adhesiva","Servilletas","Bolsas de basura"
    };
    static bool IsWarehouseConsumable(string? name)=>!string.IsNullOrWhiteSpace(name)&&WarehouseConsumables.Contains(name.Trim());

    public async Task<string?> WarehouseRemoveInventory(DocumentActor actor,WarehouseInventoryRemovalInput input,CancellationToken ct)
    {
        if(!actor.CanDeleteInventory)return "Solo Diorly Abreu y Bryan Arias pueden eliminar existencias.";
        if(input.Department is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES")||string.IsNullOrWhiteSpace(input.Equipment)||input.Equipment.Length>100||input.ExpectedQuantity<1)return "Equipo o cantidad no válido.";
        await using var connection=await Open(ct);
        await EnsureMaintenanceSchema(connection,ct);
        await EnsureWarehouseFulfillmentSchema(connection,ct);
        await EnsureRepairedInventorySchema(connection,ct);
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using(var stockLock=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)",connection,tx))await stockLock.ExecuteScalarAsync(ct);
        await using(var pending=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK) WHERE department=@department AND LOWER(equipment_name)=LOWER(@equipment) AND status='PENDING_PICKUP'",connection,tx))
        {
            pending.Parameters.AddWithValue("@department",input.Department);pending.Parameters.AddWithValue("@equipment",input.Equipment.Trim());
            if(Convert.ToInt64(await pending.ExecuteScalarAsync(ct))>0)return "Este equipo tiene unidades pendientes de retiro. No se puede eliminar mientras estén reservadas.";
        }
        var balances=new List<(string Serial,int Quantity)>();
        await using(var stock=new SqlCommand("SELECT serial_number,SUM(CASE WHEN movement_type IN('ENTRY','DAMAGED_RETURN') THEN quantity WHEN movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -quantity ELSE 0 END) FROM dbo.maintenance_movements m WHERE department=@department AND operational_area='WAREHOUSE' AND LOWER(LTRIM(RTRIM(equipment_type)))=LOWER(@equipment) AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr JOIN dbo.maintenance_movements r ON r.id=rr.movement_id WHERE r.department=m.department AND UPPER(LTRIM(RTRIM(r.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND r.occurred_at<=m.occurred_at) GROUP BY serial_number",connection,tx))
        {
            stock.Parameters.AddWithValue("@department",input.Department);stock.Parameters.AddWithValue("@equipment",input.Equipment.Trim());
            await using var reader=await stock.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))balances.Add((reader.IsDBNull(0)?"":reader.GetString(0),reader.GetInt32(1)));
        }
        if(balances.Sum(row=>row.Quantity)!=input.ExpectedQuantity)return "Las existencias cambiaron. Actualiza el inventario antes de eliminar.";
        if(balances.Any(row=>row.Quantity<0))return "Este equipo necesita una revisión de movimientos antes de eliminar sus existencias.";
        foreach(var row in balances.Where(row=>row.Quantity>0))
        {
            var number=$"DEL-ALM-{Guid.NewGuid():N}";
            await using var insert=new SqlCommand("""
              INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at)
              VALUES(NEWID(),NULL,NULL,@department,NULL,@actorName,'DISCHARGE',@equipment,NULL,'Eliminación de existencias desde inventario',@serial,@quantity,@reason,@actorId,@actorName,'WAREHOUSE',@number,CONCAT('REAL-MNT|',@number),@actorName,@actorName,'Almacén central',NULL,SYSUTCDATETIME())
              """,connection,tx);
            insert.Parameters.AddWithValue("@department",input.Department);insert.Parameters.AddWithValue("@equipment",input.Equipment.Trim());insert.Parameters.AddWithValue("@serial",row.Serial);insert.Parameters.AddWithValue("@quantity",row.Quantity);insert.Parameters.AddWithValue("@actorId",actor.Id);insert.Parameters.AddWithValue("@actorName",actor.Name);insert.Parameters.AddWithValue("@number",number);
            insert.Parameters.AddWithValue("@reason",string.IsNullOrWhiteSpace(input.Reason)?"Eliminado desde inventario; se conserva el historial.":input.Reason.Trim()[..Math.Min(input.Reason.Trim().Length,500)]);
            await insert.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);return null;
    }
    async Task<InstitutionalFile> WarehouseRecoverLegacyRequest(SqlConnection connection,InstitutionalFile document,string originalJson,CancellationToken ct)
    {
        if(document.WarehouseRequest is not null||document.Area!="communications"||document.Channel!="requirements"||document.RecipientDepartment!="Almacén")return document;
        var number=Path.GetFileNameWithoutExtension(document.OriginalFileName);
        if(!number.StartsWith("SOL-ALM-",StringComparison.Ordinal)||number.Length>40)return document;
        await EnsureMaintenanceSchema(connection,ct);
        await using var find=new SqlCommand("SELECT m.agency_id,m.technician_user_id,m.technician_name,m.quantity,m.notes,a.codigo,a.terminal,a.grupo FROM dbo.maintenance_movements m JOIN dbo.agencies a ON a.id=m.agency_id WHERE m.document_number=@number AND m.movement_type='REQUEST' AND m.operational_area='WAREHOUSE' AND m.department=@department",connection);
        find.Parameters.AddWithValue("@number",number);
        find.Parameters.AddWithValue("@department",WarehouseDepartmentCode(document.Department)??"");
        WarehouseRequestInfo? recovered=null;
        await using(var reader=await find.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct)||reader.IsDBNull(0)||reader.IsDBNull(1)||reader.IsDBNull(4))return document;
            var lines=new List<WarehouseRequestLine>();
            foreach(var raw in reader.GetString(4).Split('\n'))
            {
                var parts=raw.Trim().Split(" · ",StringSplitOptions.None);
                if(parts.Length<2||!int.TryParse(parts[1],out var quantity))continue;
                var match=System.Text.RegularExpressions.Regex.Match(parts[0],@"^\d+\. (Equipo|Componente|Devolución): (.+)$");
                if(!match.Success||quantity is <1 or >10000||match.Groups[2].Value.Length>100)continue;
                var kind=match.Groups[1].Value switch{"Equipo"=>"EQUIPMENT","Componente"=>"COMPONENT",_=>"RETURN"};
                var serial=parts.Skip(2).FirstOrDefault(part=>part.StartsWith("Serial: ",StringComparison.Ordinal))?[8..];
                if(kind=="RETURN"&&string.IsNullOrWhiteSpace(serial))return document;
                lines.Add(new(kind,match.Groups[2].Value,quantity,serial,null));
            }
            // Never infer missing rows from a truncated historic summary.
            if(lines.Count is <1 or >30||lines.Sum(line=>line.Quantity)!=reader.GetInt32(3))return document;
            recovered=new(number,Guid.Parse(reader.GetString(0)),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.GetString(1),reader.GetString(2),"MEDIUM",lines);
        }
        document.WarehouseRequest=recovered;
        await using var save=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@metadata WHERE id=@id AND metadata=@original",connection);
        save.Parameters.AddWithValue("@metadata",JsonSerializer.Serialize(document,InstitutionalJson));
        save.Parameters.AddWithValue("@id",document.Id);
        save.Parameters.AddWithValue("@original",originalJson);
        if(await save.ExecuteNonQueryAsync(ct)==1)return document;
        await using var latest=new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id=@id",connection);
        latest.Parameters.AddWithValue("@id",document.Id);
        var json=await latest.ExecuteScalarAsync(ct) as string;
        var current=json is null?document:JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;
        current.Id=document.Id;
        return current;
    }
    static readonly SemaphoreSlim WarehouseFulfillmentSchemaLock=new(1,1);
    static bool warehouseFulfillmentSchemaReady;
    static readonly SemaphoreSlim WarehouseAnalyticsLock=new(1,1);
    static object WarehouseEmptyAnalytics()=>new{requests=0,units=0,articles=Array.Empty<object>(),departments=Array.Empty<object>(),agencies=Array.Empty<object>(),groups=Array.Empty<object>(),terminals=Array.Empty<object>()};
    static object warehouseAnalyticsCache=WarehouseEmptyAnalytics();
    static DateTime warehouseAnalyticsCachedUtc;
    static async Task EnsureWarehouseFulfillmentSchema(SqlConnection connection,CancellationToken ct)
    {
        if(warehouseFulfillmentSchemaReady)return;
        await WarehouseFulfillmentSchemaLock.WaitAsync(ct);
        try
        {
            if(warehouseFulfillmentSchemaReady)return;
            await using var command=new SqlCommand("""
                IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_department' AND (definition NOT LIKE '%WAREHOUSE%' OR definition NOT LIKE '%ADMINISTRATIVE%' OR definition NOT LIKE '%TREASURY%' OR definition NOT LIKE '%ACCOUNTING%' OR definition NOT LIKE '%GENERAL_AUDIT%' OR definition NOT LIKE '%VAULT%' OR definition NOT LIKE '%CASHIER%'))
                    ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT CK_maintenance_department;
                IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_department')
                    ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','WAREHOUSE','ADMINISTRATIVE','WORKSHOP','COMMUNICATION','COMMERCIALIZATION','SUPERVISION','TECHNICAL_FAILURE','CALL_CENTER','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER'));
                IF OBJECT_ID(N'dbo.warehouse_equipment_allocations',N'U') IS NULL
                BEGIN
                  CREATE TABLE dbo.warehouse_equipment_allocations(
                    id bigint IDENTITY PRIMARY KEY,
                    document_id bigint NOT NULL REFERENCES dbo.institutional_files(id),
                    line_index int NOT NULL,
                    department varchar(40) NOT NULL,
                    equipment_name nvarchar(100) NOT NULL,
                    serial_number nvarchar(120) NOT NULL,
                    quantity int NOT NULL,
                    status varchar(20) NOT NULL DEFAULT('PENDING_PICKUP'),
                    delivery_number varchar(40) NULL,
                    delivered_by nvarchar(160) NULL,
                    received_by nvarchar(160) NULL,
                    withdrawn_at datetime2(3) NULL,
                    CONSTRAINT CK_warehouse_allocation_status CHECK(status IN('PENDING_PICKUP','WITHDRAWN','CANCELLED'))
                  );
                  CREATE INDEX IX_warehouse_allocations_document ON dbo.warehouse_equipment_allocations(document_id,status);
                  CREATE INDEX IX_warehouse_allocations_stock ON dbo.warehouse_equipment_allocations(department,equipment_name,status);
                  CREATE UNIQUE INDEX UX_warehouse_pending_serial ON dbo.warehouse_equipment_allocations(department,serial_number) WHERE status='PENDING_PICKUP';
                END;
                IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.warehouse_equipment_allocations') AND name='CK_warehouse_allocation_status' AND definition NOT LIKE '%CANCELLED%')
                BEGIN
                  ALTER TABLE dbo.warehouse_equipment_allocations DROP CONSTRAINT CK_warehouse_allocation_status;
                  ALTER TABLE dbo.warehouse_equipment_allocations WITH CHECK ADD CONSTRAINT CK_warehouse_allocation_status CHECK(status IN('PENDING_PICKUP','WITHDRAWN','CANCELLED'));
                END;
                IF COL_LENGTH('dbo.warehouse_equipment_allocations','product_id') IS NULL
                  ALTER TABLE dbo.warehouse_equipment_allocations ADD product_id nvarchar(40) NULL;
                IF COL_LENGTH('dbo.warehouse_equipment_allocations','equipment_name') IS NOT NULL
                  ALTER TABLE dbo.warehouse_equipment_allocations ALTER COLUMN equipment_name nvarchar(240) NOT NULL;
                IF COL_LENGTH('dbo.maintenance_movements','equipment_type') IS NOT NULL
                  ALTER TABLE dbo.maintenance_movements ALTER COLUMN equipment_type nvarchar(240) NOT NULL;
                IF OBJECT_ID(N'dbo.warehouse_products',N'U') IS NULL
                BEGIN
                  CREATE TABLE dbo.warehouse_products(
                    product_id nvarchar(40) NOT NULL PRIMARY KEY,
                    description nvarchar(240) NOT NULL,
                    is_product bit NOT NULL,
                    warehouse nvarchar(80) NOT NULL,
                    official_stock int NOT NULL,
                    unit_id nvarchar(40) NOT NULL,
                    division nvarchar(120) NOT NULL,
                    product_group nvarchar(120) NOT NULL,
                    last_purchase_date date NULL,
                    tracks_serial bit NOT NULL,
                    catalog_version nvarchar(40) NOT NULL,
                    imported_at datetime2(3) NOT NULL,
                    is_active bit NOT NULL DEFAULT(1)
                  );
                  CREATE INDEX IX_warehouse_products_description ON dbo.warehouse_products(description,is_active);
                END;
                """,connection);
            await command.ExecuteNonQueryAsync(ct);
            await EnsureOfficialWarehouseCatalog(connection,ct);
            var snapshotFile=Path.Combine(AppContext.BaseDirectory,"Data","warehouse-products-2026-09-28.json");
            if(File.Exists(snapshotFile)){
                var snapshot=JsonSerializer.Deserialize<WarehouseCatalogImport>(await File.ReadAllTextAsync(snapshotFile,ct),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
                if(snapshot is null||snapshot.Version!="2026-09-28-inventario-real-172"||snapshot.Products.Count!=172)throw new InvalidOperationException("El inventario actualizado no está completo.");
                await ApplyWarehouseCatalogSnapshot(connection,snapshot,ct);
            }
            var correctedFile=Path.Combine(AppContext.BaseDirectory,"Data","warehouse-products-2026-10-27.json");
            if(File.Exists(correctedFile)){
                var corrected=JsonSerializer.Deserialize<WarehouseCatalogImport>(await File.ReadAllTextAsync(correctedFile,ct),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
                if(corrected is null||corrected.Version!="2026-10-27-inventario-corregido-134"||corrected.Products.Count!=134)throw new InvalidOperationException("El inventario corregido no está completo.");
                await ApplyWarehouseCatalogSnapshot(connection,corrected,ct);
            }
            warehouseFulfillmentSchemaReady=true;
        }
        finally{WarehouseFulfillmentSchemaLock.Release();}
    }

    static async Task EnsureOfficialWarehouseCatalog(SqlConnection connection,CancellationToken ct)
    {
        var file=Path.Combine(AppContext.BaseDirectory,"Data","warehouse-products-2026-09-17.json");
        if(!File.Exists(file))return;
        var json=await File.ReadAllTextAsync(file,ct);
        var catalog=JsonSerializer.Deserialize<WarehouseCatalogImport>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        if(catalog?.Products is null||catalog.Products.Count==0)return;
        // Seed a new installation once. Later manager imports are authoritative and must survive restarts.
        await using(var current=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.warehouse_products",connection))
            if(Convert.ToInt64(await current.ExecuteScalarAsync(ct))>0)return;
        await ReplaceWarehouseCatalog(connection,null,catalog,ct);
    }

    static bool CatalogConsumable(string group,bool tracksSerial)=>!tracksSerial&&(group.StartsWith("Materiales",StringComparison.OrdinalIgnoreCase)||group.Equals("Vasos",StringComparison.OrdinalIgnoreCase));

    internal static async Task ApplyWarehouseCatalogSnapshot(SqlConnection connection,WarehouseCatalogImport catalog,CancellationToken ct)
    {
        if(catalog.Products.Count==0||catalog.Products.Select(p=>p.IdProducto).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=catalog.Products.Count||catalog.Products.Any(p=>p.Existencia<0))throw new InvalidOperationException("Inventario actualizado inválido.");
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using(var gate=new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource='WarehouseCatalogSnapshot',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000; IF @result<0 THROW 50000,'No se pudo bloquear la actualización de inventario',1;",connection,tx))await gate.ExecuteNonQueryAsync(ct);
        await using(var schema=new SqlCommand("IF OBJECT_ID('dbo.warehouse_catalog_snapshots','U') IS NULL CREATE TABLE dbo.warehouse_catalog_snapshots(version nvarchar(120) NOT NULL PRIMARY KEY,source_file nvarchar(240),applied_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),previous_catalog nvarchar(max) NOT NULL,applied_catalog nvarchar(max) NOT NULL);",connection,tx))await schema.ExecuteNonQueryAsync(ct);
        await using(var applied=new SqlCommand("SELECT COUNT(*) FROM dbo.warehouse_catalog_snapshots WITH(UPDLOCK,HOLDLOCK) WHERE version=@version",connection,tx)){
            applied.Parameters.AddWithValue("@version",catalog.Version);
            if(Convert.ToInt32(await applied.ExecuteScalarAsync(ct))>0){await tx.CommitAsync(ct);return;}
        }
        // Keep a recoverable snapshot and never delete movements, allocations or documents.
        await using(var stockLock=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)",connection,tx))await stockLock.ExecuteScalarAsync(ct);
        await using(var audit=new SqlCommand("INSERT dbo.warehouse_catalog_snapshots(version,source_file,previous_catalog,applied_catalog) SELECT @version,@source,(SELECT * FROM dbo.warehouse_products FOR JSON PATH),@catalog",connection,tx)){
            audit.Parameters.AddWithValue("@version",catalog.Version);audit.Parameters.AddWithValue("@source",catalog.SourceFile??"");audit.Parameters.AddWithValue("@catalog",JsonSerializer.Serialize(catalog));await audit.ExecuteNonQueryAsync(ct);
        }
        await ReplaceWarehouseCatalog(connection,tx,catalog,ct);
        await tx.CommitAsync(ct);
    }

    static async Task ReplaceWarehouseCatalog(SqlConnection connection,SqlTransaction? transaction,WarehouseCatalogImport catalog,CancellationToken ct)
    {
        var productsJson=JsonSerializer.Serialize(catalog.Products);
        await using var command=new SqlCommand("""
          DECLARE @now datetime2(3)=SYSUTCDATETIME();
          UPDATE dbo.warehouse_products SET is_active=0;
          MERGE dbo.warehouse_products AS target
          USING (
            SELECT idProducto,descripcion,producto,almacen,existencia,idUnidad,division,grupo,TRY_CONVERT(date,fechaUltimaCompra) fechaUltimaCompra,seriales
            FROM OPENJSON(@products) WITH(
              idProducto nvarchar(40) '$.IdProducto', descripcion nvarchar(240) '$.Descripcion', producto bit '$.Producto', almacen nvarchar(80) '$.Almacen',
              existencia int '$.Existencia', idUnidad nvarchar(40) '$.IdUnidad', division nvarchar(120) '$.Division', grupo nvarchar(120) '$.Grupo',
              fechaUltimaCompra nvarchar(30) '$.FechaUltimaCompra', seriales bit '$.Seriales'
            )
          ) AS source ON target.product_id=source.idProducto
          WHEN MATCHED THEN UPDATE SET description=source.descripcion,is_product=source.producto,warehouse=source.almacen,official_stock=source.existencia,unit_id=source.idUnidad,division=source.division,product_group=source.grupo,last_purchase_date=source.fechaUltimaCompra,tracks_serial=source.seriales,catalog_version=@version,imported_at=@now,is_active=1
          WHEN NOT MATCHED THEN INSERT(product_id,description,is_product,warehouse,official_stock,unit_id,division,product_group,last_purchase_date,tracks_serial,catalog_version,imported_at,is_active)
            VALUES(source.idProducto,source.descripcion,source.producto,source.almacen,source.existencia,source.idUnidad,source.division,source.grupo,source.fechaUltimaCompra,source.seriales,@version,@now,1);
          """,connection,transaction);
        command.Parameters.AddWithValue("@products",productsJson);
        command.Parameters.AddWithValue("@version",catalog.Version);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<string?> WarehouseReplaceCatalog(DocumentActor actor,WarehouseCatalogImport input,CancellationToken ct)
    {
        if(!actor.Manager||actor.Team!="WAREHOUSE"&&actor.Role!="Administrator")return "Solo Almacén o Administración puede reemplazar el catálogo oficial.";
        if(input.Products is null||input.Products.Count is <1 or >5000)return "El catálogo debe contener entre 1 y 5,000 productos.";
        if(input.Products.Any(item=>string.IsNullOrWhiteSpace(item.IdProducto)||item.IdProducto.Length>40||string.IsNullOrWhiteSpace(item.Descripcion)||item.Descripcion.Length>240||item.Existencia<int.MinValue/2))return "Revisa los códigos, descripciones y existencias del archivo.";
        if(input.Products.Select(item=>item.IdProducto.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=input.Products.Count)return "El archivo contiene IdProducto duplicados.";
        await using var connection=await Open(ct);await EnsureWarehouseFulfillmentSchema(connection,ct);
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await ReplaceWarehouseCatalog(connection,tx,input with{Version=string.IsNullOrWhiteSpace(input.Version)?DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss"):input.Version.Trim()},ct);
        await tx.CommitAsync(ct);return null;
    }

    static string? WarehouseDepartmentCode(string name)=>DepartmentAccounts.Names.FirstOrDefault(entry=>entry.Value==name).Key;
    public async Task<WarehouseRequestInfo?> WarehouseCanonicalRequest(WarehouseRequestInfo request,DocumentActor actor,string sourceDepartment,CancellationToken ct)
    {
        return (await WarehouseCanonicalRequestChecked(request,actor,sourceDepartment,ct)).Request;
    }

    public async Task<(WarehouseRequestInfo? Request,string? Error)> WarehouseCanonicalRequestChecked(WarehouseRequestInfo request,DocumentActor actor,string sourceDepartment,CancellationToken ct)
    {
        if(request.WeekendLoan is { } loan)
        {
            if(!WeekendLoans.Requester(actor))return (null,"Solo la cuenta autorizada de Vittini en Avería Técnica puede solicitar este préstamo.");
            if(!WeekendLoans.Valid(loan))return (null,"El préstamo debe iniciar viernes o sábado, terminar el domingo y registrar la devolución dentro de los 7 días siguientes.");
            if(request.DestinationType!="DEPARTMENT"||request.AgencyId!=Guid.Empty||request.TicketId is not null||request.FailureCategory is not null)return (null,"El préstamo de emergencia debe enviarse como solicitud interna de Avería Técnica, sin agencia ni ticket vinculado.");
        }
        var department=WarehouseDepartmentCode(sourceDepartment);if(department is null)return (null,"El departamento solicitante no es válido.");
        if(request.DestinationType is not (null or "AGENCY" or "GROUP" or "DEPARTMENT"))return (null,"El tipo de destino de la solicitud no es válido.");
        if(actor.Role=="GroupAdministrator"&&(request.AgencyId==Guid.Empty||request.DestinationType is not (null or "AGENCY")||!(await WarehouseRequestAgencyScope(actor,ct)).ContainsKey(request.AgencyId)))return (null,"La agencia no pertenece a los grupos asignados al supervisor.");
        var inventory=await WarehouseAvailableInventory(actor,ct);
        foreach(var requested in request.Lines.Where(line=>line.Kind!="RETURN"))
        {
            var product=!string.IsNullOrWhiteSpace(requested.ProductId)?inventory.FirstOrDefault(item=>item.ProductId.Equals(requested.ProductId.Trim(),StringComparison.OrdinalIgnoreCase)):inventory.FirstOrDefault(item=>item.Name.Equals(requested.Name.Trim(),StringComparison.OrdinalIgnoreCase));
            // El comprobante documenta una necesidad, no una reserva. Permite
            // solicitar artículos todavía no catalogados o con existencia cero;
            // la disponibilidad se valida únicamente al preparar el despacho.
            if(requested.Quantity<1)return (null,$"La cantidad de «{requested.Name.Trim()}» debe ser mayor que cero.");
            if(product is null&&!string.IsNullOrWhiteSpace(requested.ProductId))return (null,$"El artículo «{requested.Name.Trim()}» ({requested.ProductId.Trim()}) ya no está activo en el catálogo. Actualiza el formulario y selecciónalo nuevamente.");
        }
        if(request.DestinationType=="GROUP"&&request.AgencyId==Guid.Empty){
            if(!actor.Allowed||(!actor.Manager&&sourceDepartment!=actor.Department)||request.TicketId is not null||request.FailureCategory is not null)return (null,"La solicitud para un grupo no puede incluir una agencia, ticket o falla individual.");
            var groups=await WarehouseRequestGroups(actor,ct);
            var group=groups.FirstOrDefault(item=>string.Equals(item,request.AgencyGroup?.Trim(),StringComparison.OrdinalIgnoreCase));
            if(group is null)return (null,"El grupo seleccionado ya no está disponible. Actualiza el formulario y selecciónalo nuevamente.");
            return (request with{AgencyCode="GRUPO",AgencyName=$"Grupo {group}",AgencyGroup=group,InstallerId="",InstallerName="",TicketNumber=null,Lines=request.Lines.Select(line=>{var product=inventory.FirstOrDefault(item=>!string.IsNullOrWhiteSpace(line.ProductId)?item.ProductId.Equals(line.ProductId.Trim(),StringComparison.OrdinalIgnoreCase):item.Name.Equals(line.Name.Trim(),StringComparison.OrdinalIgnoreCase));return line with{Kind="EQUIPMENT",ProductId=product?.ProductId,Name=product?.Name??line.Name.Trim(),Serial=null};}).ToList()},null);
        }
        if(request.AgencyId==Guid.Empty){
            if(request.DestinationType=="AGENCY")return (null,"Selecciona una agencia válida antes de enviar la solicitud.");
            if(!actor.Allowed||(!actor.Manager&&sourceDepartment!=actor.Department))return (null,"El departamento solicitante no corresponde a la cuenta autenticada.");
            if(request.TicketId is not null)return (null,"Un ticket vinculado requiere una agencia específica.");
            return (request with{DestinationType="DEPARTMENT",AgencyCode="USO INTERNO",AgencyName=$"Departamento de {sourceDepartment}",AgencyGroup="Grupo Tejeda",InstallerId="",InstallerName="",TicketNumber=null,Lines=request.Lines.Select(line=>{var product=inventory.FirstOrDefault(item=>!string.IsNullOrWhiteSpace(line.ProductId)?item.ProductId.Equals(line.ProductId.Trim(),StringComparison.OrdinalIgnoreCase):item.Name.Equals(line.Name.Trim(),StringComparison.OrdinalIgnoreCase));return line with{Kind="EQUIPMENT",ProductId=product?.ProductId,Name=product?.Name??line.Name.Trim(),Serial=null};}).ToList()},null);
        }
        await using var connection=await Open(ct);
        await using var command=new SqlCommand("SELECT codigo,terminal,grupo FROM dbo.agencies WHERE id=@agency",connection);
        command.Parameters.Add("@agency",SqlDbType.Char,36).Value=request.AgencyId.ToString();
        await using var reader=await command.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct))return (null,"La agencia seleccionada ya no existe o no está disponible. Actualiza el formulario y selecciónala nuevamente.");
        var agencyCode=reader.IsDBNull(0)?"":reader.GetString(0);var agencyName=reader.IsDBNull(1)?"":reader.GetString(1);var agencyGroup=reader.IsDBNull(2)?"":reader.GetString(2);await reader.DisposeAsync();
        if(request.DestinationType=="GROUP"){
            var groups=await WarehouseRequestGroups(actor,ct);
            if(!groups.Any(group=>string.Equals(group,agencyGroup.Trim(),StringComparison.OrdinalIgnoreCase)&&string.Equals(group,request.AgencyGroup?.Trim(),StringComparison.OrdinalIgnoreCase)))return (null,"La agencia ya no pertenece al grupo seleccionado. Actualiza el formulario.");
        }
        long? ticketNumber=null;
        if(request.TicketId is Guid ticketId)
        {
            await using var ticket=new SqlCommand("SELECT ticket_number FROM dbo.support_tickets WHERE id=@ticket AND agency_id=@agency AND assigned_department=@department AND status NOT IN('RESOLVED','CLOSED','CANCELLED')",connection);
            ticket.Parameters.AddWithValue("@ticket",ticketId);ticket.Parameters.Add("@agency",SqlDbType.Char,36).Value=request.AgencyId.ToString();ticket.Parameters.AddWithValue("@department",department);
            var linked=await ticket.ExecuteScalarAsync(ct);if(linked is null)return (null,"El ticket vinculado está cerrado, pertenece a otra agencia o no corresponde al departamento solicitante.");ticketNumber=Convert.ToInt64(linked);
        }
        return (request with{AgencyCode=agencyCode,AgencyName=agencyName,AgencyGroup=agencyGroup,InstallerId="",InstallerName="",TicketNumber=ticketNumber,Lines=request.Lines.Select(line=>{var product=!string.IsNullOrWhiteSpace(line.ProductId)?inventory.FirstOrDefault(item=>item.ProductId.Equals(line.ProductId.Trim(),StringComparison.OrdinalIgnoreCase)):inventory.FirstOrDefault(item=>item.Name.Equals(line.Name.Trim(),StringComparison.OrdinalIgnoreCase));return line with{Kind="EQUIPMENT",ProductId=product?.ProductId,Name=product?.Name??line.Name.Trim(),Serial=null};}).ToList()},null);
    }
    async Task<Dictionary<Guid,string>> WarehouseRequestAgencyScope(DocumentActor actor,CancellationToken ct){
        await using var connection=await Open(ct);
        var sql=actor.Role=="GroupAdministrator"?"SELECT id,grupo FROM dbo.agencies a WHERE EXISTS(SELECT 1 FROM dbo.admin_user_groups g WHERE g.user_id=@user AND g.group_name=a.grupo)":"SELECT id,grupo FROM dbo.agencies";
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@user",actor.Id);
        var rows=new Dictionary<Guid,string>();await using var reader=await command.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))if(Guid.TryParse(reader.GetString(0),out var id))rows[id]=reader.IsDBNull(1)?"":reader.GetString(1);return rows;
    }
    public async Task<List<string>> WarehouseRequestGroups(DocumentActor actor,CancellationToken ct){
        if(!actor.Allowed||actor.WarehouseRequestsBlocked||actor.Role=="GroupAdministrator")return [];
        await using var connection=await Open(ct);
        var sql="SELECT DISTINCT LTRIM(RTRIM(grupo)) AS name FROM dbo.agencies WHERE NULLIF(LTRIM(RTRIM(grupo)),N'') IS NOT NULL ORDER BY name";
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@user",actor.Id);
        var groups=new List<string>();await using var reader=await command.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))groups.Add(reader.GetString(0));return groups;
    }
    static string? WarehouseWithdrawalRequestNumber(string? raw)
    {
        if(string.IsNullOrWhiteSpace(raw)||raw.Length>2048)return null;
        var normalized=MaintenanceTraceCode.Normalize(raw);
        var match=System.Text.RegularExpressions.Regex.Match(normalized,@"^(?:REAL-MNT\|)?SOL-ALM-(\d{8})-?(\d{6})-([A-Z0-9]{4,32})$",System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success?$"SOLALM{match.Groups[1].Value}{match.Groups[2].Value}{match.Groups[3].Value}":null;
    }

    static string? WarehouseQrToken(string raw)
    {
        raw=raw.Trim().Trim('\'','"');
        if(raw.Length>2048)return null;
        for(var attempt=0;attempt<2;attempt++){try{var decoded=Uri.UnescapeDataString(raw);if(decoded==raw)break;raw=decoded;}catch(UriFormatException){return null;}}
        raw=raw.Replace("\r","").Replace("\n","").Replace("\t","");
        if(Uri.TryCreate(raw,UriKind.Absolute,out var url))
        {
            var marker="/api/institutional-documents/verify/";
            var at=url.AbsolutePath.IndexOf(marker,StringComparison.OrdinalIgnoreCase);
            if(at>=0)raw=url.AbsolutePath[(at+marker.Length)..];
        }
        if(raw.Length==48&&raw.All(Uri.IsHexDigit))return raw.ToLowerInvariant();
        // USB readers may type URL punctuation using a different keyboard layout:
        // /api/institutional-documents/verify/ becomes -api-institutional'documents-verify-.
        // Accept only the known verification route followed by an exact 48-hex token.
        var scanned=System.Text.RegularExpressions.Regex.Match(raw,@"(?:^|[^a-z0-9])api[^a-z0-9]+institutional[^a-z0-9]+documents[^a-z0-9]+verify[^a-z0-9]+(?<token>[a-f0-9]{48})$",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return scanned.Success?scanned.Groups["token"].Value.ToLowerInvariant():null;
    }

    public async Task<List<WarehouseInventoryItem>> WarehouseAvailableInventory(DocumentActor actor,CancellationToken ct)
    {
        if(!actor.Allowed)return [];
        await using var connection=await Open(ct);
        await EnsureMaintenanceSchema(connection,ct);
        await EnsureInstitutionalSchema(connection,ct);
        await EnsureWarehouseFulfillmentSchema(connection,ct);
        await EnsureRepairedInventorySchema(connection,ct);
        var department=actor.Manager?"":WarehouseDepartmentCode(actor.Department);
        if(!actor.Manager&&department is null)return [];
        await using var command=new SqlCommand("""
          SELECT p.product_id,p.description,
            p.official_stock+
            COALESCE((SELECT SUM(CASE WHEN m.movement_type IN('ENTRY','DAMAGED_RETURN') THEN m.quantity WHEN m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -m.quantity ELSE 0 END) FROM dbo.maintenance_movements m WHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at AND (m.equipment_type=p.description OR m.component_type=p.product_id) AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr JOIN dbo.maintenance_movements repaired ON repaired.id=rr.movement_id WHERE repaired.department=m.department AND UPPER(LTRIM(RTRIM(repaired.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND repaired.occurred_at<=m.occurred_at)),0)-
            COALESCE((SELECT SUM(a.quantity) FROM dbo.warehouse_equipment_allocations a WHERE (a.product_id=p.product_id OR (a.product_id IS NULL AND LOWER(a.equipment_name)=LOWER(p.description))) AND a.status='PENDING_PICKUP'),0) available,
            p.warehouse,p.unit_id,p.division,p.product_group,CONVERT(varchar(10),p.last_purchase_date,23),p.tracks_serial
          FROM dbo.warehouse_products p WHERE p.is_active=1 ORDER BY p.description,p.product_id;
          """,connection);
        var items=new List<WarehouseInventoryItem>();
        await using(var reader=await command.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))
        {
            var group=reader.GetString(6);var tracksSerial=reader.GetBoolean(8);
            items.Add(new(department??"",reader.GetString(0),reader.GetString(1),Math.Max(0,reader.GetInt32(2)),[],CatalogConsumable(group,tracksSerial),reader.GetString(3),reader.GetString(4),reader.GetString(5),group,reader.IsDBNull(7)?null:reader.GetString(7),tracksSerial));
        }
        foreach(var item in items.Where(item=>item.TracksSerial))
        {
            await using var serials=new SqlCommand("""
              SELECT DISTINCT UPPER(LTRIM(RTRIM(m.serial_number))) FROM dbo.maintenance_movements m
              JOIN dbo.warehouse_products p ON p.product_id=@product
              WHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at AND (LOWER(m.equipment_type)=LOWER(p.description) OR m.component_type=p.product_id)
                AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr WHERE rr.movement_id=m.id)
                AND m.movement_type IN('ENTRY','DAMAGED_RETURN') AND m.serial_number IS NOT NULL AND LTRIM(RTRIM(m.serial_number))<>''
                AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_movements exit_m WHERE UPPER(LTRIM(RTRIM(exit_m.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND exit_m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') AND exit_m.occurred_at>=m.occurred_at)
                AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_equipment_allocations a WHERE a.serial_number=UPPER(LTRIM(RTRIM(m.serial_number))) AND a.status='PENDING_PICKUP');
              """,connection);
            serials.Parameters.AddWithValue("@product",item.ProductId);
            await using var reader=await serials.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))item.Serials.Add(reader.GetString(0));
        }
        return items;
    }

    public async Task<List<WarehouseAllocationItem>> WarehouseAllocations(DocumentActor actor,CancellationToken ct)
    {
        if(!actor.Allowed)return [];
        await using var connection=await Open(ct);
        await EnsureInstitutionalSchema(connection,ct);
        await EnsureWarehouseFulfillmentSchema(connection,ct);
        await using var command=new SqlCommand("""
          SELECT a.id,a.document_id,a.line_index,a.department,a.equipment_name,a.serial_number,a.quantity,a.status,a.delivery_number,a.delivered_by,a.received_by,a.withdrawn_at,f.metadata
          FROM dbo.warehouse_equipment_allocations a JOIN dbo.institutional_files f ON f.id=a.document_id
          WHERE (@manager=1 OR (JSON_VALUE(f.metadata,'$.status')='approved' AND (f.owner_id=@owner OR f.department=@department)))
          ORDER BY a.id DESC;
          """,connection);
        command.Parameters.AddWithValue("@manager",actor.Manager);
        command.Parameters.AddWithValue("@owner",actor.Id);
        command.Parameters.AddWithValue("@department",actor.Department);
        var rows=new List<WarehouseAllocationItem>();
        await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))
        {
            var document=JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(12),InstitutionalJson);
            var request=document?.WarehouseRequest;
            if(request is null||document!.Trashed)continue;
            rows.Add(new(reader.GetInt64(0),reader.GetInt64(1),request.Number,document!.Department,request.AgencyCode,request.AgencyName,request.AgencyGroup,reader.GetString(4),reader.GetString(5),reader.GetInt32(6),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetString(8),reader.IsDBNull(9)?null:reader.GetString(9),reader.IsDBNull(10)?null:reader.GetString(10),reader.IsDBNull(11)?null:reader.GetDateTime(11)));
        }
        return rows;
    }

    public async Task<object> WarehouseRequestFrequency(long id,DocumentActor actor,CancellationToken ct)
    {
        if(!actor.Allowed||actor.WarehouseRequestsBlocked)return Array.Empty<object>();
        await using var connection=await Open(ct);await EnsureInstitutionalSchema(connection,ct);
        WarehouseRequestInfo? target=null;string? targetDepartment=null;
        await using(var command=new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id=@id",connection))
        {command.Parameters.AddWithValue("@id",id);var json=await command.ExecuteScalarAsync(ct) as string;if(json is not null){var targetDocument=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson);target=targetDocument?.WarehouseRequest;targetDepartment=targetDocument?.Department;}}
        if(target is null)return Array.Empty<object>();
        var counts=new Dictionary<string,(int Deliveries,int Quantity)>(StringComparer.OrdinalIgnoreCase);
        await using(var command=new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id<>@id AND JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL AND JSON_QUERY(metadata,'$.warehouseDelivery') IS NOT NULL",connection))
        {
            command.Parameters.AddWithValue("@id",id);await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))
            {
                var prior=JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(0),InstitutionalJson);if(prior?.WarehouseRequest?.AgencyId!=target.AgencyId||(target.AgencyId==Guid.Empty&&prior.Department!=targetDepartment))continue;
                if(target.AgencyId==Guid.Empty&&((prior.WarehouseRequest.DestinationType=="GROUP")!=(target.DestinationType=="GROUP")||target.DestinationType=="GROUP"&&!string.Equals(prior.WarehouseRequest.AgencyGroup,target.AgencyGroup,StringComparison.OrdinalIgnoreCase)))continue;
                foreach(var line in prior.WarehouseRequest.Lines.Where(line=>line.Kind!="RETURN"))
                {var key=!string.IsNullOrWhiteSpace(line.ProductId)?line.ProductId:line.Name.Trim();var current=counts.GetValueOrDefault(key);counts[key]=(current.Deliveries+1,current.Quantity+line.Quantity);}
            }
        }
        return target.Lines.Select((line,index)=>{var key=!string.IsNullOrWhiteSpace(line.ProductId)?line.ProductId:line.Name.Trim();var count=counts.GetValueOrDefault(key);return new{lineIndex=index,productId=line.ProductId,name=line.Name,deliveryCount=count.Deliveries,quantityDelivered=count.Quantity,warning=count.Deliveries>=3};}).ToList();
    }

    public async Task<object> WarehouseConsumptionAnalytics(DocumentActor actor,CancellationToken ct)
    {
        if(!actor.Manager)return new{requests=0,units=0,articles=Array.Empty<object>(),departments=Array.Empty<object>(),agencies=Array.Empty<object>(),groups=Array.Empty<object>(),terminals=Array.Empty<object>()};
        var now=DateTime.UtcNow;
        if(now-warehouseAnalyticsCachedUtc<TimeSpan.FromMinutes(2))return warehouseAnalyticsCache;
        await WarehouseAnalyticsLock.WaitAsync(ct);
        try
        {
            now=DateTime.UtcNow;
            if(now-warehouseAnalyticsCachedUtc<TimeSpan.FromMinutes(2))return warehouseAnalyticsCache;
            await using var connection=await Open(ct);await EnsureInstitutionalSchema(connection,ct);
            var requests=new Dictionary<long,(string Department,string AgencyId,string AgencyCode,string AgencyName,string AgencyGroup,List<(string Key,string Label,int Quantity)> Lines)>();
            await using(var command=new SqlCommand("""
              SELECT f.id,f.department,
                     JSON_VALUE(f.metadata,'$.warehouseRequest.agencyId'),
                     JSON_VALUE(f.metadata,'$.warehouseRequest.agencyCode'),
                     JSON_VALUE(f.metadata,'$.warehouseRequest.agencyName'),
                     JSON_VALUE(f.metadata,'$.warehouseRequest.agencyGroup'),
                     COALESCE(NULLIF(line.product_id,N''),line.name),line.name,line.quantity
              FROM dbo.institutional_files f
              CROSS APPLY OPENJSON(f.metadata,'$.warehouseRequest.lines') WITH(
                kind nvarchar(20) '$.kind',
                name nvarchar(240) '$.name',
                quantity int '$.quantity',
                product_id nvarchar(40) '$.productId'
              ) line
              WHERE f.trashed=0 AND ISJSON(f.metadata)=1
                AND JSON_QUERY(f.metadata,'$.warehouseRequest') IS NOT NULL
                AND line.kind<>'RETURN' AND line.quantity>0
              ORDER BY f.id
              """,connection))
            {
                await using var reader=await command.ExecuteReaderAsync(ct);
                while(await reader.ReadAsync(ct))
                {
                    var id=reader.GetInt64(0);
                    if(!requests.TryGetValue(id,out var request))request=(reader.GetString(1),reader.IsDBNull(2)?"":reader.GetString(2),reader.IsDBNull(3)?"":reader.GetString(3),reader.IsDBNull(4)?"":reader.GetString(4),reader.IsDBNull(5)?"":reader.GetString(5),[]);
                    request.Lines.Add((reader.IsDBNull(6)?reader.GetString(7):reader.GetString(6),reader.GetString(7),reader.GetInt32(8)));
                    requests[id]=request;
                }
            }
            var article=new Dictionary<string,(string Label,int Requests,int Units)>(StringComparer.OrdinalIgnoreCase);
            var department=new Dictionary<string,(string Label,int Requests,int Units)>(StringComparer.OrdinalIgnoreCase);
            var agency=new Dictionary<string,(string Label,int Requests,int Units)>(StringComparer.OrdinalIgnoreCase);
            var group=new Dictionary<string,(string Label,int Requests,int Units)>(StringComparer.OrdinalIgnoreCase);
            var terminal=new Dictionary<string,(string Label,int Requests,int Units)>(StringComparer.OrdinalIgnoreCase);
            var units=0;
            static void Add(Dictionary<string,(string Label,int Requests,int Units)> bucket,string key,string label,int quantity){var row=bucket.GetValueOrDefault(key);bucket[key]=(label,row.Requests+1,row.Units+quantity);}
            foreach(var request in requests.Values)
            {
                var requestUnits=request.Lines.Sum(line=>line.Quantity);units+=requestUnits;
                Add(department,request.Department,request.Department,requestUnits);
                Add(agency,request.AgencyId,$"{request.AgencyCode} · {request.AgencyName}",requestUnits);
                Add(group,request.AgencyGroup,request.AgencyGroup,requestUnits);
                Add(terminal,request.AgencyName,request.AgencyName,requestUnits);
                foreach(var line in request.Lines)Add(article,line.Key,line.Label,line.Quantity);
            }
            static object[] Rank(Dictionary<string,(string Label,int Requests,int Units)> rows)=>rows.Select(row=>new{key=row.Key,label=row.Value.Label,requests=row.Value.Requests,units=row.Value.Units}).OrderByDescending(row=>row.units).ThenByDescending(row=>row.requests).ThenBy(row=>row.label).Take(20).Cast<object>().ToArray();
            warehouseAnalyticsCache=new{requests=requests.Count,units,articles=Rank(article),departments=Rank(department),agencies=Rank(agency),groups=Rank(group),terminals=Rank(terminal)};
            warehouseAnalyticsCachedUtc=now;
            return warehouseAnalyticsCache;
        }
        catch(SqlException error) when(!ct.IsCancellationRequested)
        {
            // La analítica no debe derribar el tablero si una lectura posterior
            // encuentra SQL temporalmente ocupado. Conserva la última foto válida.
            Console.Error.WriteLine($"Analítica de Almacén servida desde caché: {error.Message}");
            warehouseAnalyticsCachedUtc=DateTime.UtcNow;
            return warehouseAnalyticsCache;
        }
        finally{WarehouseAnalyticsLock.Release();}
    }

    public async Task<List<object>> WarehousePendingStock(string role,string? team,CancellationToken ct)
    {
        if(role!="Administrator"&&team!="WAREHOUSE")return [];
        await using var connection=await Open(ct);
        await EnsureInstitutionalSchema(connection,ct);
        await EnsureWarehouseFulfillmentSchema(connection,ct);
        await using var command=new SqlCommand("SELECT department,equipment_name,serial_number,quantity FROM dbo.warehouse_equipment_allocations WHERE status='PENDING_PICKUP'",connection);
        var rows=new List<object>();
        await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))rows.Add(new{department=reader.GetString(0),equipmentType=reader.GetString(1),serialNumber=reader.GetString(2),quantity=reader.GetInt32(3)});
        return rows;
    }

    static async Task<string?> WarehouseProtectReservedStock(SqlConnection connection,SqlTransaction tx,string department,string equipment,string? serial,int quantity,CancellationToken ct)
    {
        await using(var stockLock=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)",connection,tx))await stockLock.ExecuteScalarAsync(ct);
        await using var command=new SqlCommand("""
          SELECT COALESCE(SUM(quantity),0),COALESCE(SUM(CASE WHEN serial_number=@serial THEN 1 ELSE 0 END),0)
          FROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK)
          WHERE department=@department AND status='PENDING_PICKUP' AND (LOWER(equipment_name)=LOWER(@equipment) OR serial_number=@serial);
          """,connection,tx);
        command.Parameters.AddWithValue("@department",department);
        command.Parameters.AddWithValue("@equipment",equipment);
        command.Parameters.AddWithValue("@serial",(object?)serial??DBNull.Value);
        int pending;
        await using(var reader=await command.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);
            pending=reader.GetInt32(0);
            if(reader.GetInt32(1)>0)return "Este serial está reservado para una solicitud autorizada. Registra su retiro desde el QR del comprobante.";
        }
        if(pending==0)return null;
        await using var stock=new SqlCommand("SELECT COALESCE(SUM(CASE WHEN movement_type IN('ENTRY','DAMAGED_RETURN') THEN quantity WHEN movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -quantity ELSE 0 END),0) FROM dbo.maintenance_movements WHERE department=@department AND operational_area='WAREHOUSE' AND LOWER(equipment_type)=LOWER(@equipment)",connection,tx);
        stock.Parameters.AddWithValue("@department",department);
        stock.Parameters.AddWithValue("@equipment",equipment);
        if(Convert.ToInt32(await stock.ExecuteScalarAsync(ct))-pending<quantity)return "La salida utilizaría equipos pendientes de retiro. Selecciona existencias disponibles.";
        return null;
    }

    // New manual entries must also be searchable by the warehouse assignment catalog.
    static async Task EnsureWarehouseEntryProduct(SqlConnection connection,SqlTransaction tx,string equipment,int quantity,DateTime occurred,CancellationToken ct,bool hasSerial=true)
    {
        await using var cmd=new SqlCommand("""
          IF NOT EXISTS(SELECT 1 FROM dbo.warehouse_products WITH(UPDLOCK,HOLDLOCK) WHERE description=@equipment AND is_active=1)
          INSERT dbo.warehouse_products(product_id,description,is_product,warehouse,official_stock,unit_id,division,product_group,tracks_serial,catalog_version,imported_at,is_active)
          VALUES(@id,@equipment,1,N'PRINCIPAL',0,N'UND',N'Registro manual',N'Equipos',@serial,N'MANUAL',DATEADD(second,-1,@occurred),1);
          """,connection,tx);
        cmd.Parameters.AddWithValue("@id","MAN-"+Guid.NewGuid().ToString("N"));cmd.Parameters.AddWithValue("@equipment",equipment);
        cmd.Parameters.AddWithValue("@serial",hasSerial&&quantity==1);cmd.Parameters.AddWithValue("@occurred",occurred);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // Save purchasing needs separately from stock reservations and delivery authorization.
    public async Task<string?> WarehouseSavePendingPurchase(long id,DocumentActor actor,WarehousePurchasePendingInput input,CancellationToken ct)
    {
        if(actor.Team!="WAREHOUSE"&&actor.Role is not ("Administrator" or "Warehouse"))return "Solo Almacén puede registrar artículos pendientes de compra.";
        if(input.Note is null||input.Note.Trim().Length is <5 or >2000||input.LineIndexes is null||input.LineIndexes.Count==0||input.Equipment is null||input.Equipment.Count>10000)return "Selecciona los artículos pendientes de compra e indica el motivo.";
        await using var connection=await Open(ct);
        await EnsureInstitutionalSchema(connection,ct);
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var read=new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",connection,tx);
        read.Parameters.AddWithValue("@id",id);
        var json=await read.ExecuteScalarAsync(ct) as string;
        if(json is null)return "Solicitud no encontrada.";
        var document=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;
        var lines=document.WarehouseRequest?.Lines;
        if(lines is null||document.Channel!="requirements"||document.RecipientDepartment!="Almacén"||document.Status!="approved"||document.Trashed||document.WarehouseAssigned.Count>0||document.WarehouseDelivery is not null)return "Esta solicitud no admite cambios de preparación.";
        bool ValidLine(int index)=>index>=0&&index<lines.Count&&lines[index].Kind!="RETURN";
        if(input.LineIndexes.Any(index=>!ValidLine(index))||input.Equipment.Any(row=>row is null||!ValidLine(row.LineIndex)||row.Quantity<1||row.Quantity>10000||string.IsNullOrWhiteSpace(row.Name)||row.Name.Length>240||row.Serial?.Length>120||row.ProductId?.Length>120))return "Revisa los renglones y cantidades.";
        foreach(var group in input.Equipment.GroupBy(row=>row.LineIndex))if(group.Sum(row=>(long)row.Quantity)>lines[group.Key].Quantity)return "La cantidad no puede superar lo solicitado.";
        document.WarehousePendingPurchaseLines=input.LineIndexes.Distinct().Order().ToList();
        document.WarehouseAssignmentDraft=input.Equipment;
        document.WarehousePurchaseNote=input.Note.Trim();
        document.History.Add(new("pending_purchase",actor.Name,actor.Login,DateTime.UtcNow,document.Status,document.ReceiptCode,document.ReceiverLogin,document.ReceivedUtc,null));
        await using var update=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id",connection,tx);
        update.Parameters.AddWithValue("@id",id);update.Parameters.AddWithValue("@json",JsonSerializer.Serialize(document,InstitutionalJson));
        await update.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return null;
    }

    public async Task<string?> WarehousePrepare(long id,DocumentActor actor,WarehouseAuthorizationInput input,CancellationToken ct)
    {
        if(actor.Team!="WAREHOUSE"&&actor.Role is not ("Administrator" or "Warehouse"))return "Solo Almacén puede preparar el despacho.";
        if(input.Note is null||input.Equipment is null||input.Equipment.Any(x=>x is null)||input.Note.Trim().Length is <5 or >2000||input.Equipment.Count>10000)return "Indica un motivo y revisa los equipos asignados.";
        var catalog=await WarehouseAvailableInventory(actor,ct);
        await InstitutionalDetail(id,actor,ct);
        await using var connection=await Open(ct);
        await EnsureMaintenanceSchema(connection,ct);
        await EnsureInstitutionalSchema(connection,ct);
        await EnsureWarehouseFulfillmentSchema(connection,ct);
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var documentCommand=new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",connection,tx);
        documentCommand.Parameters.AddWithValue("@id",id);
        var json=await documentCommand.ExecuteScalarAsync(ct) as string;
        if(json is null)return "Solicitud no encontrada.";
        var document=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;
        document.Id=id;
        var request=document.WarehouseRequest;
        if(request is null||document.Channel!="requirements"||document.RecipientDepartment!="Almacén"||document.Status!="approved"||document.WarehouseAssigned.Count>0||document.WarehouseDelivery is not null||document.Trashed)return "Autoriza primero la solicitud antes de asignar equipos y seriales.";
        var department=WarehouseDepartmentCode(document.Department);
        if(department is null)return "Departamento solicitante no válido.";
        var expected=request.Lines.Select((line,index)=>(line,index)).Where(x=>x.line.Kind!="RETURN").ToList();
        if(!input.Equipment.All(x=>expected.Any(line=>line.index==x.LineIndex))||input.Equipment.Select(x=>x.LineIndex).Distinct().Count()!=expected.Count)return "Asigna un equipo del inventario a cada renglón solicitado.";
        if(input.Equipment.Any(x=>x.Name is null||x.Name.Trim().Length is <1 or >240||x.Quantity<1||x.Serial?.Trim().Length>120))return "Revisa las cantidades y los seriales de los artículos.";
        var serialized=input.Equipment.Where(x=>!string.IsNullOrWhiteSpace(x.Serial)).Select(x=>x.Serial.Trim().ToUpperInvariant()).ToList();
        if(serialized.Distinct().Count()!=serialized.Count)return "Cada equipo debe tener su propio serial válido y único.";
        foreach(var (line,index) in expected)
        {
            var assigned=input.Equipment.Where(x=>x.LineIndex==index).ToList();
            if(assigned.Sum(x=>x.Quantity)!=line.Quantity)return "La cantidad asignada debe coincidir con la solicitud.";
            foreach(var selected in assigned)
            {
                var product=catalog.FirstOrDefault(item=>!string.IsNullOrWhiteSpace(selected.ProductId)?item.ProductId.Equals(selected.ProductId.Trim(),StringComparison.OrdinalIgnoreCase):item.Name.Equals(selected.Name.Trim(),StringComparison.OrdinalIgnoreCase));
                if(product is null||!selected.Name.Trim().Equals(product.Name,StringComparison.OrdinalIgnoreCase))return "Selecciona un artículo válido del inventario para entregar.";
                if(product.TracksSerial&&(selected.Quantity!=1||string.IsNullOrWhiteSpace(selected.Serial)))return $"Registra o escanea un serial por cada unidad de {product.Name}.";
            }
        }
        // Canonical IDs prevent mixed name/ID rows from splitting the same stock check.
        input=input with{Equipment=input.Equipment.Select(selected=>{var product=catalog.First(item=>!string.IsNullOrWhiteSpace(selected.ProductId)?item.ProductId.Equals(selected.ProductId.Trim(),StringComparison.OrdinalIgnoreCase):item.Name.Equals(selected.Name.Trim(),StringComparison.OrdinalIgnoreCase));return selected with{ProductId=product.ProductId,Name=product.Name};}).ToList()};
        // A table-level stock lock synchronizes with existing inventory-entry and exit writes.
        await using(var lockStock=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)",connection,tx))await lockStock.ExecuteScalarAsync(ct);
        foreach(var equipment in input.Equipment.Where(x=>!string.IsNullOrWhiteSpace(x.Serial)))
        {
            var product=catalog.First(item=>!string.IsNullOrWhiteSpace(equipment.ProductId)?item.ProductId.Equals(equipment.ProductId.Trim(),StringComparison.OrdinalIgnoreCase):item.Name.Equals(equipment.Name.Trim(),StringComparison.OrdinalIgnoreCase));
            if(!product.TracksSerial)continue;
            await using var availableSerial=new SqlCommand("""
              SELECT COUNT(*) FROM dbo.maintenance_movements m
              JOIN dbo.warehouse_products p ON p.product_id=@product
              WHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at
                AND (LOWER(m.equipment_type)=LOWER(p.description) OR m.component_type=p.product_id)
                AND m.movement_type IN('ENTRY','DAMAGED_RETURN')
                AND UPPER(LTRIM(RTRIM(m.serial_number)))=@serial
                AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr WHERE rr.movement_id=m.id)
                AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_movements exit_m WHERE UPPER(LTRIM(RTRIM(exit_m.serial_number)))=@serial AND exit_m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') AND exit_m.occurred_at>=m.occurred_at)
                AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_equipment_allocations a WITH(UPDLOCK,HOLDLOCK) WHERE a.serial_number=@serial AND a.status='PENDING_PICKUP');
              """,connection,tx);
            availableSerial.Parameters.AddWithValue("@product",product.ProductId);
            availableSerial.Parameters.AddWithValue("@serial",equipment.Serial!.Trim().ToUpperInvariant());
            if(Convert.ToInt32(await availableSerial.ExecuteScalarAsync(ct))==0)return $"El serial {equipment.Serial.Trim().ToUpperInvariant()} no está disponible en el inventario de {product.Name}.";
        }
        foreach(var grouped in input.Equipment.GroupBy(x=>!string.IsNullOrWhiteSpace(x.ProductId)?x.ProductId.Trim():x.Name.Trim(),StringComparer.OrdinalIgnoreCase))
        {
            await using var stock=new SqlCommand("""
              SELECT p.official_stock+
                COALESCE((SELECT SUM(CASE WHEN m.movement_type IN('ENTRY','DAMAGED_RETURN') THEN m.quantity WHEN m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -m.quantity ELSE 0 END) FROM dbo.maintenance_movements m WHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at AND (m.equipment_type=p.description OR m.component_type=p.product_id) AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr JOIN dbo.maintenance_movements repaired ON repaired.id=rr.movement_id WHERE repaired.department=m.department AND UPPER(LTRIM(RTRIM(repaired.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND repaired.occurred_at<=m.occurred_at)),0)
              FROM dbo.warehouse_products p WHERE p.is_active=1 AND p.product_id=@product
              """,connection,tx);
            var product=catalog.First(item=>item.ProductId.Equals(grouped.Key,StringComparison.OrdinalIgnoreCase)||item.Name.Equals(grouped.Key,StringComparison.OrdinalIgnoreCase));
            stock.Parameters.AddWithValue("@product",product.ProductId);
            var stockCount=Convert.ToInt32(await stock.ExecuteScalarAsync(ct));
            await using var pending=new SqlCommand("SELECT COALESCE(SUM(quantity),0) FROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK) WHERE (product_id=@product OR (product_id IS NULL AND LOWER(equipment_name)=LOWER(@equipment))) AND status='PENDING_PICKUP'",connection,tx);
            pending.Parameters.AddWithValue("@product",product.ProductId);pending.Parameters.AddWithValue("@equipment",product.Name);
            var pendingCount=Convert.ToInt32(await pending.ExecuteScalarAsync(ct));
            if(stockCount-pendingCount<grouped.Sum(x=>x.Quantity))return $"No hay existencias suficientes de {product.Name} en Inventario; la solicitud debe permanecer pendiente de entrega.";
        }
        foreach(var equipment in input.Equipment)
        {
            var product=catalog.First(item=>!string.IsNullOrWhiteSpace(equipment.ProductId)?item.ProductId.Equals(equipment.ProductId.Trim(),StringComparison.OrdinalIgnoreCase):item.Name.Equals(equipment.Name.Trim(),StringComparison.OrdinalIgnoreCase));
            var suppliedSerial=equipment.Serial?.Trim().ToUpperInvariant();
            var serial=!string.IsNullOrWhiteSpace(suppliedSerial)?suppliedSerial:$"CONTROL-{id:D10}-{equipment.LineIndex:D2}";
            if(!string.IsNullOrWhiteSpace(suppliedSerial))
            {
            await using var duplicate=new SqlCommand("SELECT COUNT(*) FROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK) WHERE serial_number=@serial AND status='PENDING_PICKUP'",connection,tx);
            duplicate.Parameters.AddWithValue("@serial",serial);
            if(Convert.ToInt32(await duplicate.ExecuteScalarAsync(ct))>0)return $"El serial {serial} ya está pendiente de retiro.";
            }
            await using var insert=new SqlCommand("INSERT INTO dbo.warehouse_equipment_allocations(document_id,line_index,department,product_id,equipment_name,serial_number,quantity) VALUES(@id,@line,@department,@product,@equipment,@serial,@quantity)",connection,tx);
            insert.Parameters.AddWithValue("@id",id);
            insert.Parameters.AddWithValue("@line",equipment.LineIndex);
            insert.Parameters.AddWithValue("@department",department);
            insert.Parameters.AddWithValue("@product",product.ProductId);
            insert.Parameters.AddWithValue("@equipment",product.Name);
            insert.Parameters.AddWithValue("@serial",serial);
            insert.Parameters.AddWithValue("@quantity",equipment.Quantity);
            await insert.ExecuteNonQueryAsync(ct);
        }
        var preparedUtc=DateTime.UtcNow;
        document.WarehousePendingPurchaseLines=[];
        document.WarehouseAssignmentDraft=[];
        document.WarehousePurchaseNote=null;
        document.History.Add(new("dispatch",actor.Name,actor.Login,preparedUtc,document.Status,document.ReceiptCode,document.ReceiverLogin,document.ReceivedUtc,null));
        document.WarehousePreparationNote=input.Note.Trim();
        document.WarehousePreparedBy=actor.Name;
        document.WarehousePreparedLogin=actor.Login;
        document.WarehousePreparedUtc=preparedUtc;
        document.WarehouseAssigned=input.Equipment.Select(x=>{var product=catalog.First(item=>!string.IsNullOrWhiteSpace(x.ProductId)?item.ProductId.Equals(x.ProductId.Trim(),StringComparison.OrdinalIgnoreCase):item.Name.Equals(x.Name.Trim(),StringComparison.OrdinalIgnoreCase));return x with{ProductId=product.ProductId,Name=product.Name,Serial=string.IsNullOrWhiteSpace(x.Serial)?"CONTROL POR CANTIDAD":x.Serial.Trim().ToUpperInvariant()};}).ToList();
        await using var update=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id",connection,tx);
        update.Parameters.AddWithValue("@json",JsonSerializer.Serialize(document,InstitutionalJson));
        update.Parameters.AddWithValue("@id",id);
        await update.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    public async Task<string?> WarehouseApprovePrepared(long id,DocumentActor actor,WarehouseApprovalInput input,CancellationToken ct)
    {
        if(!actor.Manager||actor.Team!="WAREHOUSE"&&actor.Role is not ("Administrator" or "Warehouse"))return "Solo la encargada autorizada de Almacén puede colocar el sello.";
        if(input.Note is null||input.Note.Trim().Length is <5 or >2000)return "Indica un motivo de autorización válido.";
        await using var connection=await Open(ct);
        await EnsureInstitutionalSchema(connection,ct);
        await EnsureWarehouseFulfillmentSchema(connection,ct);
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var documentCommand=new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",connection,tx);
        documentCommand.Parameters.AddWithValue("@id",id);
        var json=await documentCommand.ExecuteScalarAsync(ct) as string;
        if(json is null)return "Solicitud no encontrada.";
        var document=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;
        document.NormalizeWarehouseReceiptState();
        document.Id=id;
        if(document.WarehouseRequest is null||document.Channel!="requirements"||document.RecipientDepartment!="Almacén"||!document.AwaitingWarehouseAuthorization()||document.Trashed)return "La solicitud no está pendiente de autorización.";
        var approvedUtc=DateTime.UtcNow;
        document.History.Add(new("approve",actor.Name,actor.Login,approvedUtc,document.Status,document.ReceiptCode,document.ReceiverLogin,document.ReceivedUtc,null));
        document.Status="approved";
        document.DecisionBy=actor.Name+" · "+actor.Login;
        document.DecisionUtc=approvedUtc;
        document.CorrectionNote=input.Note.Trim();
        await using var update=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id",connection,tx);
        update.Parameters.AddWithValue("@json",JsonSerializer.Serialize(document,InstitutionalJson));
        update.Parameters.AddWithValue("@id",id);
        await update.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    public async Task<(string? Error,WarehouseDeliveryInfo? Delivery,long DocumentId)> WarehouseWithdraw(DocumentActor actor,WarehouseWithdrawalInput input,CancellationToken ct)
    {
        if(!actor.Manager)return ("Solo Almacén puede registrar el retiro.",null,0);
        var token=input.SealQr is null?null:WarehouseQrToken(input.SealQr);
        var requestNumber=WarehouseWithdrawalRequestNumber(input.SealQr);
        if(input.ReceivedBy is null||input.ReceivedBy.Trim().Length is <3 or >160)return ("Indica el nombre de quien recibe (entre 3 y 160 caracteres).",null,0);
        if(token is null&&requestNumber is null)return ("No se pudo leer el QR. Escanea completo el QR principal del formulario o el del sello autorizado.",null,0);
        await using var connection=await Open(ct);
        await EnsureMaintenanceSchema(connection,ct);
        await EnsureInstitutionalSchema(connection,ct);
        await EnsureWarehouseFulfillmentSchema(connection,ct);
        await EnsureWarehouseWithdrawalSchema(connection,ct);
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var find=new SqlCommand("SELECT TOP(2) id,metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE (@token IS NOT NULL AND JSON_VALUE(metadata,'$.verificationToken')=@token) OR (@token IS NULL AND @number IS NOT NULL AND REPLACE(JSON_VALUE(metadata,'$.warehouseRequest.number'),'-','')=@number)",connection,tx);
        find.Parameters.Add("@token",SqlDbType.NVarChar,48).Value=(object?)token??DBNull.Value;
        find.Parameters.Add("@number",SqlDbType.NVarChar,100).Value=(object?)requestNumber??DBNull.Value;
        long id;
        InstitutionalFile document;
        await using(var reader=await find.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct))return ("El sello QR no corresponde a una solicitud registrada.",null,0);
            id=reader.GetInt64(0);
            document=JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1),InstitutionalJson)!;
            if(await reader.ReadAsync(ct))return ("El QR coincide con más de un formulario. Utiliza el QR único del sello autorizado.",null,0);
        }
        if(input.DocumentId is not null&&input.DocumentId!=id)return ("El QR escaneado no corresponde a esta solicitud. Escanea el comprobante de la tarjeta seleccionada.",null,id);
        if(document.WarehouseRequest is null||document.RecipientDepartment!="Almacén"||document.Status!="approved"||document.DecisionUtc is null||document.Trashed)return ("La solicitud aún no está autorizada para retiro.",null,id);
        if(document.WarehouseDelivery is not null)return ("Este comprobante ya fue retirado; no se duplicó la entrega.",document.WarehouseDelivery,id);
        await using(var stockLock=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)",connection,tx))await stockLock.ExecuteScalarAsync(ct);
        await using var rows=new SqlCommand("SELECT id,line_index,product_id,equipment_name,serial_number,quantity FROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK) WHERE document_id=@id AND status='PENDING_PICKUP' ORDER BY id",connection,tx);
        rows.Parameters.AddWithValue("@id",id);
        var allocations=new List<(long Id,int Line,string? ProductId,string Name,string Serial,int Quantity)>();
        await using(var reader=await rows.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))allocations.Add((reader.GetInt64(0),reader.GetInt32(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetInt32(5)));
        if(allocations.Count==0)return ("Esta solicitud no tiene equipos pendientes de retiro.",null,id);
        var request=document.WarehouseRequest;
        var department=WarehouseDepartmentCode(document.Department)!;
        var now=DateTime.UtcNow;
        var delivery=new WarehouseDeliveryInfo($"ENT-ALM-{now:yyyyMMdd}-{id:D6}",actor.Name,actor.Login,input.ReceivedBy.Trim(),now);
        foreach(var allocation in allocations)
        {
            var number=$"ENT-ALM-{Guid.NewGuid():N}";
            await using var movement=new SqlCommand("""
              INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at)
              VALUES(NEWID(),@agency,@ticket,@department,@installer,@installerName,'NEW_DELIVERY',@equipment,@product,'Retiro por QR de solicitud autorizada',@serial,@quantity,@notes,@actorId,@actorName,'WAREHOUSE',@number,CONCAT('REAL-MNT|',@number),@delivered,@received,@destination,NULL,@at)
              """,connection,tx);
            movement.Parameters.Add("@agency",SqlDbType.Char,36).Value=request.AgencyId==Guid.Empty?DBNull.Value:request.AgencyId.ToString();
            movement.Parameters.AddWithValue("@ticket",Db(request.TicketId));
            movement.Parameters.AddWithValue("@department",department);
            movement.Parameters.AddWithValue("@installer",Db(string.IsNullOrWhiteSpace(request.InstallerId)?null:request.InstallerId));
            movement.Parameters.AddWithValue("@installerName",delivery.ReceivedBy);
            movement.Parameters.AddWithValue("@equipment",allocation.Name);
            movement.Parameters.AddWithValue("@product",Db(allocation.ProductId));
            movement.Parameters.AddWithValue("@serial",allocation.Serial);
            movement.Parameters.AddWithValue("@quantity",allocation.Quantity);
            movement.Parameters.AddWithValue("@notes",$"{request.Number} · {delivery.Number} · QR de sello autorizado");
            movement.Parameters.AddWithValue("@actorId",actor.Id);
            movement.Parameters.AddWithValue("@actorName",actor.Name);
            movement.Parameters.AddWithValue("@number",number);
            movement.Parameters.AddWithValue("@delivered",actor.Name);
            movement.Parameters.AddWithValue("@received",delivery.ReceivedBy);
            var destination=$"{document.Department} · {request.AgencyCode} · {request.AgencyName}";
            movement.Parameters.AddWithValue("@destination",destination.Length>200?destination[..200]:destination);
            movement.Parameters.AddWithValue("@at",now);
            await movement.ExecuteNonQueryAsync(ct);
            await using var allocated=new SqlCommand("UPDATE dbo.warehouse_equipment_allocations SET status='WITHDRAWN',delivery_number=@number,delivered_by=@delivered,received_by=@received,withdrawn_at=@at WHERE id=@id AND status='PENDING_PICKUP'",connection,tx);
            allocated.Parameters.AddWithValue("@number",delivery.Number);
            allocated.Parameters.AddWithValue("@delivered",actor.Name);
            allocated.Parameters.AddWithValue("@received",delivery.ReceivedBy);
            allocated.Parameters.AddWithValue("@at",now);
            allocated.Parameters.AddWithValue("@id",allocation.Id);
            if(await allocated.ExecuteNonQueryAsync(ct)!=1)return ("El retiro cambió; inténtalo de nuevo.",null,id);
        }
        document.WarehouseDelivery=delivery;
        document.History.Add(new("withdraw",actor.Name,actor.Login,now,document.Status,document.ReceiptCode,document.ReceiverLogin,document.ReceivedUtc,null));
        await using var update=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id",connection,tx);
        update.Parameters.AddWithValue("@json",JsonSerializer.Serialize(document,InstitutionalJson));
        update.Parameters.AddWithValue("@id",id);
        await update.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return (null,delivery,id);
    }
}
