static class MaintenanceTraceCode
{
    public static string Normalize(string value)
    {
        var text=value.Trim();
        for(var attempt=0;attempt<2;attempt++){try{var decoded=Uri.UnescapeDataString(text);if(decoded==text)break;text=decoded;}catch(UriFormatException){break;}}
        if(Uri.TryCreate(text,UriKind.Absolute,out var uri)){var query=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);foreach(var key in new[]{"equipment","code","qr","token"})if(query.TryGetValue(key,out var candidate)&&!string.IsNullOrWhiteSpace(candidate)){text=candidate.ToString();break;}}
        text=text.Trim().Trim('\'','"').Replace("\r","").Replace("\n","").Replace("\t","");try{text=Uri.UnescapeDataString(text);}catch(UriFormatException){}text=text.ToUpperInvariant().Replace('’','\'').Replace('‘','\'').Replace('´','\'').Replace('`','\'').Replace('Ç','|').Replace('¦','|');
        var warehouseRequest=System.Text.RegularExpressions.Regex.Match(text,@"REAL\W*MNT\W*SOL\W*ALM\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{4,})$");
        if(warehouseRequest.Success)return $"REAL-MNT|SOL-ALM-{warehouseRequest.Groups[1].Value}-{warehouseRequest.Groups[2].Value}-{warehouseRequest.Groups[3].Value}";
        warehouseRequest=System.Text.RegularExpressions.Regex.Match(text,@"(?:^|\W)SOL\W*ALM\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{4,})$");
        if(warehouseRequest.Success)return $"SOL-ALM-{warehouseRequest.Groups[1].Value}-{warehouseRequest.Groups[2].Value}-{warehouseRequest.Groups[3].Value}";
        var match=System.Text.RegularExpressions.Regex.Match(text,@"REAL\W*MNT\W*(ALM|TAL)\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{5,})$");
        if(match.Success)return $"REAL-MNT|{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}-{match.Groups[4].Value}";
        match=System.Text.RegularExpressions.Regex.Match(text,@"(?:^|\W)(ALM|TAL)\W*(\d{8})\W*(\d{6})\W*([A-Z0-9]{5,})$");
        return match.Success?$"{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}-{match.Groups[4].Value}":text;
    }
}
