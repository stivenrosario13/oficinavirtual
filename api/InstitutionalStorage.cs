using System.Text.Json;

partial class Database
{
    // Hydrate only after the existing department/folder/recipient checks succeeded.
    static async Task<InstitutionalFile> HydrateInstitutionalFile(InstitutionalFile document, CancellationToken ct)
    {
        document.ReceiptSignature = await StorageFiles.ReadDataUrl(document.ReceiptSignature, ct);
        for (var i = 0; i < document.History.Count; i++)
            document.History[i] = document.History[i] with { PreviousReceiptSignature = await StorageFiles.ReadDataUrl(document.History[i].PreviousReceiptSignature, ct) };
        // FormData may include a signature/image supplied by an administrative form.
        if (document.AdministrativeRequest?.FormData is JsonElement form)
        {
            var json = await StorageFiles.TransformJson(form.GetRawText(), false, ct);
            document.AdministrativeRequest = document.AdministrativeRequest with { FormData = JsonSerializer.Deserialize<JsonElement>(json!) };
        }
        return document;
    }
}
