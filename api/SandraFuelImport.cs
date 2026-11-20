using Microsoft.Data.SqlClient;
using System.Text.Json;

partial class Database
{
    // Historical source records are added once, without modifying existing documents,
    // account permissions, approvals or deliveries. A transaction also guards parallel logins.
    internal static async Task EnsureSandraFuelImport(SqlConnection c,CancellationToken ct)
    {
        const string key="SANDRA_FUEL_EXCEL_20261020";
        await using(var done=new SqlCommand("IF OBJECT_ID(N'dbo.admin_users',N'U') IS NULL SELECT 1; ELSE SELECT COUNT(*) FROM dbo.application_one_time_actions WHERE action_key=@key",c)){
            done.Parameters.AddWithValue("@key",key);if(Convert.ToInt32(await done.ExecuteScalarAsync(ct))>0)return;
        }
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        await using(var gate=new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 50001,'No se pudo importar combustible.',1; SELECT COUNT(*) FROM dbo.application_one_time_actions WHERE action_key=@key;",c,tx)){
            gate.Parameters.AddWithValue("@key",key);if(Convert.ToInt32(await gate.ExecuteScalarAsync(ct))>0){await tx.CommitAsync(ct);return;}
        }
        string id,login,name;
        await using(var user=new SqlCommand("SELECT CONVERT(nvarchar(80),id),email,display_name FROM dbo.admin_users WHERE LOWER(LTRIM(RTRIM(email))) IN ('sandra.ramirez','sandra.ramirez@grupotejeda.local')",c,tx))
        await using(var r=await user.ExecuteReaderAsync(ct)){
            if(!await r.ReadAsync(ct)){await r.DisposeAsync();await tx.CommitAsync(ct);return;}
            id=r.GetString(0);login=r.GetString(1);name=r.GetString(2);
            if(await r.ReadAsync(ct))throw new InvalidOperationException("Hay más de una cuenta de Sandra Ramírez; no se importó combustible.");
        }
        await using var stream=typeof(Database).Assembly.GetManifestResourceStream("SandraFuel20261020.json")??throw new InvalidOperationException("Falta el registro de combustible.");
        using var source=await JsonDocument.ParseAsync(stream,cancellationToken:ct);
        foreach(var form in source.RootElement.GetProperty("forms").EnumerateArray()){
            var number=form.GetProperty("number").GetString()!;
            // Never replace a document if a prior manual import already used this folio.
            await using var exists=new SqlCommand("SELECT COUNT(*) FROM dbo.institutional_files WHERE JSON_VALUE(metadata,'$.administrativeRequest.number')=@number",c,tx);
            exists.Parameters.AddWithValue("@number",number);if(Convert.ToInt32(await exists.ExecuteScalarAsync(ct))>0)continue;
            var bytes=Convert.FromBase64String(form.GetProperty("pdfBase64").GetString()!);
            var date=form.GetProperty("date").GetString()!;var now=DateTime.UtcNow;
            var formData=JsonSerializer.SerializeToElement(new{
                importKey=key,sourceFile=source.RootElement.GetProperty("sourceFile").GetString(),sourceSha256=source.RootElement.GetProperty("sourceSha256").GetString(),sheet="Hoja1",
                fuelDate=date,fuelPeriod=date[..7],sections=form.GetProperty("sections"),sourceTotal=form.GetProperty("sourceTotal"),calculatedTotal=form.GetProperty("calculatedTotal"),
                rowCount=form.GetProperty("rowCount"),recordedAmounts=form.GetProperty("recordedAmounts"),sourceCells=form.GetProperty("sourceCells"),
                notes="Registro diario de gasolina importado del Excel. Las celdas vacías se conservan."
            });
            var document=new InstitutionalFile{
                Area="communications",Channel="requirements",Title="Gasolina · gasto diario · "+date,
                Description="Registro importado · "+date+" · "+form.GetProperty("rowCount").GetInt32()+" filas de vehículos",
                OriginalFileName=number+".pdf",ContentType="application/pdf",Size=bytes.Length,
                OwnerUserName=id,OwnerLogin=login,OwnerDisplayName=name,Department="Administración",RecipientDepartment="Administración",
                Status="registered",CreatedUtc=now,SentUtc=now,
                AdministrativeRequest=new(number,"OTRO","EMPLEADO","Control de combustible",null,"Importación de registros existentes",null,"FUEL_LOG",formData),
                History=[new("import","Importación de registros existentes","system:import",now,"",null,null,null,null)]
            };
            await using var insert=new SqlCommand("INSERT dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) VALUES(@owner,N'Administración','communications','requirements',@json,@bytes)",c,tx);
            insert.Parameters.AddWithValue("@owner",id);insert.Parameters.AddWithValue("@json",JsonSerializer.Serialize(document,InstitutionalJson));insert.Parameters.AddWithValue("@bytes",await StorageFiles.Store(bytes,"generated-document",ct));await insert.ExecuteNonQueryAsync(ct);
        }
        await using var marker=new SqlCommand("INSERT dbo.application_one_time_actions(action_key) VALUES(@key)",c,tx);marker.Parameters.AddWithValue("@key",key);await marker.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
    }
}
