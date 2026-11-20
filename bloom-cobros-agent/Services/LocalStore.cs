using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace BloomCobrosAgent.Services;

public sealed class LocalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BloomCobrosAgent");

    private string DataFile => Path.Combine(_folder, "clientes.json");
    private string SettingsFile => Path.Combine(_folder, "configuracion.json");
    private string LogFile => Path.Combine(_folder, "historial.json");

    public ImportSnapshot? Load()
    {
        if (!File.Exists(DataFile)) return null;
        try
        {
            return JsonSerializer.Deserialize<ImportSnapshot>(File.ReadAllText(DataFile), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(ImportSnapshot snapshot)
    {
        Directory.CreateDirectory(_folder);
        var temporary = DataFile + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot, JsonOptions));
        File.Move(temporary, DataFile, true);
    }

    public WhatsAppSettings LoadSettings()
    {
        try
        {
            return File.Exists(SettingsFile)
                ? JsonSerializer.Deserialize<WhatsAppSettings>(File.ReadAllText(SettingsFile), JsonOptions) ?? new()
                : new();
        }
        catch { return new(); }
    }

    public void SaveSettings(WhatsAppSettings settings)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public List<DispatchLog> LoadLogs()
    {
        try
        {
            return File.Exists(LogFile)
                ? JsonSerializer.Deserialize<List<DispatchLog>>(File.ReadAllText(LogFile), JsonOptions) ?? []
                : [];
        }
        catch { return []; }
    }

    public void AppendLog(DispatchLog entry)
        => AppendLogs([entry]);

    public void AppendLogs(IEnumerable<DispatchLog> entries)
    {
        var logs = LoadLogs();
        logs.InsertRange(0, entries.ToList());
        if (logs.Count > 50000) logs.RemoveRange(50000, logs.Count - 50000);
        Directory.CreateDirectory(_folder);
        File.WriteAllText(LogFile, JsonSerializer.Serialize(logs, JsonOptions));
    }

    public static string ProtectToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "";
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token.Trim()), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public static string UnprotectToken(string encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return "";
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }
}
