using System.Text.Json;
using Microsoft.Extensions.Configuration;

// Hosting updates replace web.config. Keep this one credential in private App_Data,
// alongside the existing session keys and staged files, never in the static web root.
static class StorageServerConfiguration
{
    internal static string RestoreAndPersist(IConfiguration config,string contentRoot,string? webRoot)
    {
        var folder=Path.GetFullPath(Path.Combine(contentRoot,"App_Data"));
        var publicRoot=Path.GetFullPath(webRoot??Path.Combine(contentRoot,"wwwroot")).TrimEnd(Path.DirectorySeparatorChar);
        if(folder.Equals(publicRoot,StringComparison.OrdinalIgnoreCase)||folder.StartsWith(publicRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La configuración privada de almacenamiento debe estar fuera de wwwroot.");
        var path=Path.Combine(folder,"storage-credentials.json");
        var key=config["STORAGE_KEY"]?.Trim();
        var source="server";
        if(string.IsNullOrWhiteSpace(key)){
            if(!File.Exists(path))return "missing";
            try{key=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))?.GetValueOrDefault("STORAGE_KEY");}
            catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException){Console.Error.WriteLine("No se pudo leer el respaldo privado de almacenamiento.");return "missing";}
            if(string.IsNullOrWhiteSpace(key))return "missing";
            config["STORAGE_KEY"]=key;
            return "private-backup";
        }
        // An explicit server key wins, including rotation. A failed backup does not
        // disable the valid configured provider or prevent the rest of the site starting.
        string? temporary=null;
        try{
            Directory.CreateDirectory(folder);
            var json=JsonSerializer.Serialize(new Dictionary<string,string>{{"STORAGE_KEY",key}});
            if(!File.Exists(path)||File.ReadAllText(path)!=json){
                temporary=Path.Combine(folder,"storage-credentials-"+Guid.NewGuid().ToString("N")+".tmp");
                File.WriteAllText(temporary,json);
                File.Move(temporary,path,true);
            }
        }catch(Exception error) when(error is IOException or UnauthorizedAccessException){Console.Error.WriteLine("Almacenamiento configurado; no se pudo conservar su respaldo privado.");source="server-without-backup";}
        finally{if(temporary is not null&&File.Exists(temporary))try{File.Delete(temporary);}catch(Exception error) when(error is IOException or UnauthorizedAccessException){}}
        return source;
    }
}
