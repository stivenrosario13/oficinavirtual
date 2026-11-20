using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

static class AdministrativeFormEditing
{
    public static bool CanEdit(InstitutionalFile document, DocumentActor actor) =>
        document.Accessible(actor) && !document.Trashed && document.Area == "communications" &&
        document.RecipientDepartment == "Administración" && document.AdministrativeRequest is not null &&
        (document.AdministrativeRequest.AssetType != "INTERNET" || actor.Role == "Administrator" || actor.Role == "Technology" && actor.Team == "TECHNICAL_FAILURE") &&
        (document.AdministrativeRegistrationOnly || document.AdministrativeDelivery is null && document.Status is "pending" or "received" or "correction_requested" or "rejected") &&
        (document.OwnerUserName == actor.Id || actor.Department == "Administración" && (actor.Manager || actor.Role == "Administrative"));
}

partial class Database
{
    public async Task<string?> InstitutionalEditAdministrativeForm(long id, int revision, DocumentActor actor, InstitutionalFile replacement, byte[] data, CancellationToken ct)
    {
        await using var c = await Open(ct);
        await EnsureInstitutionalSchema(c, ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using var read = new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", c, tx);
        read.Parameters.AddWithValue("@id", id);
        var json = await read.ExecuteScalarAsync(ct) as string;
        if (json is null) return "El formulario ya no está disponible.";
        var document = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson)!;
        document.Id = id;
        if (!AdministrativeFormEditing.CanEdit(document, actor)) return "No puedes editar este formulario. Los autorizados o entregados están cerrados.";
        if (document.AdministrativeRevision != revision) return "Otra persona modificó el formulario. Vuelve a abrirlo antes de guardar.";
        var request = replacement.AdministrativeRequest;
        if(document.AdministrativeRequest?.Internet?.TicketId!=request?.Internet?.TicketId)return "La edición debe conservar el ticket vinculado.";
        if (request is null || request.Number != document.AdministrativeRequest!.Number || request.FormType != document.AdministrativeRequest.FormType || request.AssetType != document.AdministrativeRequest.AssetType)
            return "La edición debe conservar el folio y el tipo de formulario.";
        document.History.Add(new("edit_form", actor.Name, actor.Login, DateTime.UtcNow, document.Status, document.ReceiptCode, document.ReceiverLogin, document.ReceivedUtc, document.ReceiptSignature, document.AdministrativeRequest));
        document.AdministrativeRevision++;
        document.AdministrativeRequest = request;
        document.Title = replacement.Title;
        document.Description = replacement.Description;
        document.OriginalFileName = replacement.OriginalFileName;
        document.ContentType = "application/pdf";
        document.Size = data.Length;
        // A changed PDF must be received and reviewed again; previous seals remain in history.
        document.Status = document.AdministrativeRegistrationOnly ? "registered" : "pending";
        document.ReceivedUtc = null; document.ReceivedByUserName = null; document.ReceiverDisplayName = null;
        document.ReceiverLogin = null; document.ReceiptCode = null; document.VerificationToken = null;
        document.ReceiptSignature = null; document.DecisionBy = null; document.DecisionUtc = null; document.CorrectionNote = null;
        await using var update = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json,file_data=@bytes WHERE id=@id", c, tx);
        update.Parameters.AddWithValue("@id", id);
        update.Parameters.AddWithValue("@json", await StorageFiles.TransformJson(JsonSerializer.Serialize(document, InstitutionalJson), true, ct));
        update.Parameters.AddWithValue("@bytes", await StorageFiles.Store(data, "institutional-document", ct));
        await update.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    async Task<long> InstitutionalAdministrativeUpload(InstitutionalFile document, byte[] data, CancellationToken ct)
    {
        AdministrativeFuel.Normalize(document);
        await using var c = await Open(ct);
        await EnsureInstitutionalSchema(c, ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        // Keep the client folio across retries, including a lost success response during a batch.
        await using var gate = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 50001,'No se pudo confirmar el formulario.',1;", c, tx);
        gate.Parameters.AddWithValue("@key", "administrative-form:" + document.OwnerUserName + ":" + document.AdministrativeRequest!.Number);
        await gate.ExecuteNonQueryAsync(ct);
        await using var find = new SqlCommand("SELECT TOP(1) id,metadata FROM dbo.institutional_files WHERE owner_id=@owner AND JSON_VALUE(metadata,'$.administrativeRequest.number')=@number", c, tx);
        find.Parameters.AddWithValue("@owner", document.OwnerUserName);
        find.Parameters.AddWithValue("@number", document.AdministrativeRequest.Number);
        await using (var reader = await find.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                var id = reader.GetInt64(0);
                var previous = JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1), InstitutionalJson)!;
                if (previous.Department != document.Department || JsonSerializer.Serialize(previous.AdministrativeRequest, InstitutionalJson) != JsonSerializer.Serialize(document.AdministrativeRequest, InstitutionalJson))
                    throw new AdministrativeFormConflictException();
                await reader.DisposeAsync(); await tx.CommitAsync(ct); return id;
            }
        }
        await using var insert = new SqlCommand("INSERT dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES(@owner,@department,'communications','requirements',@json,@bytes)", c, tx);
        insert.Parameters.AddWithValue("@owner", document.OwnerUserName);
        insert.Parameters.AddWithValue("@department", document.Department);
        insert.Parameters.AddWithValue("@json", await StorageFiles.TransformJson(JsonSerializer.Serialize(document, InstitutionalJson), true, ct));
        insert.Parameters.AddWithValue("@bytes", await StorageFiles.Store(data, "institutional-document", ct));
        var result = Convert.ToInt64(await insert.ExecuteScalarAsync(ct));
        await tx.CommitAsync(ct);
        return result;
    }
}

sealed class AdministrativeFormConflictException() : Exception("Este folio ya se guardó con otros datos. Abre el formulario existente para editarlo.");
