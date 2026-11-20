static class AdministrativeDeliveryQr
{
    public static string? RequestNumber(string? raw)
    {
        if(string.IsNullOrWhiteSpace(raw)||raw.Length>2048)return null;
        var value=raw.Trim();
        if(Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https"){
            const string prefix="/api/institutional-documents/lookup/";
            if(uri.AbsolutePath.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))value=Uri.UnescapeDataString(uri.AbsolutePath[prefix.Length..]);
            else{
                var query=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                if(!query.TryGetValue("equipment",out var equipment)||equipment.Count!=1)return null;
                value=equipment.ToString();
            }
        }
        if(value.StartsWith("REAL-MNT|",StringComparison.Ordinal))value=value[9..];
        return value.Length<=40&&System.Text.RegularExpressions.Regex.IsMatch(value,@"^SOL-ADM-[A-Za-z0-9-]+$")?value:null;
    }
}
