using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;

static class AdministrativeProfiles
{
    public static bool ApiAllowed(string path) {
        if(!path.StartsWith("/api/",StringComparison.OrdinalIgnoreCase))return true;
        if(new[]{"/api/admin/login","/api/admin/logout","/api/admin/session","/api/admin/session/restore","/api/version","/api/health","/api/license/status","/api/live/stream"}.Contains(path,StringComparer.OrdinalIgnoreCase))return true;
        return new[]{"/api/account","/api/institutional-documents","/api/administrative-files","/api/administrative-assets","/api/warehouse/fulfillment","/api/incident-ticket-report"}.Any(prefix=>path.Equals(prefix,StringComparison.OrdinalIgnoreCase)||path.StartsWith(prefix+"/",StringComparison.OrdinalIgnoreCase));
    }
    public static string Login(string login){var normalized=login.Trim().ToLowerInvariant();var slash=Math.Max(normalized.LastIndexOf('\\'),normalized.LastIndexOf('/'));if(slash>=0)normalized=normalized[(slash+1)..];var at=normalized.IndexOf('@');return at>=0?normalized[..at]:normalized;}
    static string IdentityName(string value){var decomposed=value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);var output=new StringBuilder();foreach(var character in decomposed)if(CharUnicodeInfo.GetUnicodeCategory(character)!=UnicodeCategory.NonSpacingMark&&char.IsLetterOrDigit(character))output.Append(character);return output.ToString();}
    public static bool IsIncidentOperator(string login,string? displayName=null)=>Login(login) is "yesica.mercedes" or "yessica.mercedes" or "lucianny.gonzalez" or "lucianny.gonzales"||IdentityName(displayName??"") is "yesicamercedes" or "yessicamercedes" or "luciannygonzalez" or "luciannygonzales";
    public static bool CanManageIncidentReports(DocumentActor actor)=>actor.Role.Equals("Administrator",StringComparison.OrdinalIgnoreCase)||IsIncidentOperator(actor.Login,actor.Name);
    public static string[]? Forms(string login)=>Login(login) switch {
        "gledys.tejeda"=>["SIM","MARBETE","DEVICE","PAYMENT","FLEET","MODEM","CONTRACT_REQUEST","CONTRACT_RENEWAL","RENTAL_CONTRACT","RENT_RECEIPT","INCREASE_LETTER","FUEL_LOG","GAS_LOG","GENERAL","LABOR_BENEFITS_REQUEST","BLANK_CHECKS_DELIVERY","SIGNED_BENEFITS_CHECKS_DELIVERY"],
        "emma.mendoza"=>["SIM","MARBETE","DEVICE","PAYMENT","FLEET"],
        "derihan.alisha"=>["DEVICE","MODEM"],
        "whilmaly.sulay"=>["CONTRACT_REQUEST","CONTRACT_RENEWAL","RENTAL_CONTRACT","RENT_RECEIPT","INCREASE_LETTER","GAS_LOG","GENERAL","LABOR_BENEFITS_REQUEST","BLANK_CHECKS_DELIVERY","SIGNED_BENEFITS_CHECKS_DELIVERY"],
        "sandra.ramirez"=>["FUEL_LOG"],
        "emely.figueroa" or "diana.ventura"=>["CONTRACT_REQUEST"],
        "yessica.mercedes" or "yesica.mercedes" or "lucianny.gonzalez" or "lucianny.gonzales"=>["INCIDENT_REPORT"],
        _=>null
    };
    public static bool CanRead(DocumentActor actor,AdministrativeRequestInfo request) {
        if(request.FormType?.Equals("INCIDENT_REPORT",StringComparison.OrdinalIgnoreCase)==true&&CanManageIncidentReports(actor))return true;
        if((request.FormType is "LABOR_BENEFITS_REQUEST" or "BLANK_CHECKS_DELIVERY" or "SIGNED_BENEFITS_CHECKS_DELIVERY")&&(actor.Department is "Administración" or "Recursos Humanos"))return true;
        var forms=Forms(actor.Login);
        if(forms is null)return actor.Role!="Administrative";
        var kind=request.FormType?.ToUpperInvariant();
        // Older flota/modem forms used GENERAL. Preserve those records and their routing.
        if(kind is null or "GENERAL")kind=request.AssetType switch{"FLOTA"=>"FLEET","MODEM"=>"MODEM",_=>kind??"GENERAL"};
        return forms.Contains(kind);
    }
    public static bool CanReadIncidentReports(DocumentActor actor)=>CanManageIncidentReports(actor);
    public static bool CanDecideIncidentTickets(DocumentActor actor)=>!actor.Role.Equals("GroupAdministrator",StringComparison.OrdinalIgnoreCase)&&!actor.Role.Equals("GROUP_ADMINISTRATOR",StringComparison.OrdinalIgnoreCase)&&CanManageIncidentReports(actor);
}
