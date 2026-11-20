static class AdministrativeFuel
{
    public static bool IsSandra(string login) => AdministrativeProfiles.Login(login) == "sandra.ramirez";
    public static bool IsSulay(string login) => AdministrativeProfiles.Login(login) == "whilmaly.sulay";
    public static void Normalize(InstitutionalFile document)
    {
        // Older records shared FUEL_LOG. Resolve Sulay's records by their original owner,
        // never by whoever is reading or editing them.
        if(document.AdministrativeRequest is {FormType:"FUEL_LOG"} request && IsSulay(document.OwnerLogin))
            document.AdministrativeRequest=request with {FormType="GAS_LOG"};
        if(document.AdministrativeRegistrationOnly){
            document.Status="registered";
            if(document.AdministrativeRequest?.FormData is { } data&&data.TryGetProperty("importKey",out _)){
                document.Title=document.Title.Replace("Relación de combustible","Gasolina · gasto diario");
                document.Description=document.Description.Replace(" · Pendiente de revisión","");
            }
        }
    }
    public static bool BlocksAction(InstitutionalFile document,string action) =>
        document.AdministrativeRegistrationOnly && action is "receive" or "approve" or "reject" or "correction" or "resend" or "send" or "sign" or "move" or "cancel";
}
