using BloomCobrosAgent;
using BloomCobrosAgent.Services;

var sample = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ejemplo-clientes.csv"));
var result = new SpreadsheetImporter().Import(sample, null);

Assert(result.Customers.Count == 4, "Debe importar cuatro clientes.");
Assert(result.Customers[0].Phone == "18095550101", "Debe normalizar teléfonos dominicanos.");
Assert(result.Customers[0].WhatsAppConsent, "Debe leer el consentimiento.");
Assert(result.Customers[0].NationalId == "00100000001", "Debe importar la cédula como texto consultable.");
Assert(CollectionEngine.CreateDraft(result.Customers[0]).Eligible, "El cliente atrasado y autorizado debe ser elegible.");
Assert(result.Customers[1].Stage == CustomerStage.AlDia, "Balance cero debe quedar al día.");
Assert(result.Customers[2].Stage == CustomerStage.Legal, "Debe reconocer el estado legal.");
Assert(!CollectionEngine.CreateDraft(result.Customers[2]).Eligible, "Un caso legal debe permanecer bloqueado.");
Assert(!CollectionEngine.CreateDraft(result.Customers[3]).Eligible, "Sin consentimiento no debe generar envío.");
var protectedToken = LocalStore.ProtectToken("token-de-prueba");
Assert(LocalStore.UnprotectToken(protectedToken) == "token-de-prueba", "El token debe cifrarse y recuperarse para el usuario actual.");
var simulated = await new OfficialWhatsAppService().SendTemplateAsync(
    new WhatsAppSettings { SimulationMode = true }, "", CollectionEngine.CreateDraft(result.Customers[0]));
Assert(simulated.Success && simulated.MessageId == "SIMULATED", "La simulación no debe requerir red ni credenciales.");
var filtered = CampaignFilterService.Apply(result.Customers, new HashSet<string>(), new CampaignFilter
{
    IncludeOverdue = true,
    IncludeSuspended = false,
    IncludeUpcoming = false,
    MinimumBalance = 1000,
    Location = "santo domingo",
    MaximumRecipients = 10,
    SortMode = "Mayor balance"
});
Assert(filtered.Count == 1 && filtered[0].Customer.Contract == "C-1001", "Debe filtrar por estado, balance y localidad.");
var recentlySent = CampaignFilterService.Apply(result.Customers, new HashSet<string> { "18095550101" }, new CampaignFilter { MaximumRecipients = 10 });
Assert(recentlySent.All(d => d.Customer.Phone != "18095550101"), "Debe excluir teléfonos enviados recientemente.");
var rendered = CollectionEngine.RenderTemplate("{nombre}|{nombre_completo}|{balance}|{contrato}|{fecha_vencimiento}|{estado}", result.Customers[0]);
Assert(rendered.Contains("Ana|Ana Pérez|RD$1,560.00|C-1001|25/09/2026|Atrasado"), "Debe reemplazar todas las variables del mensaje.");

Console.WriteLine("14 verificaciones completadas correctamente.");
return;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
