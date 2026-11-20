using System.Globalization;

namespace BloomCobrosAgent;

public enum CustomerStage
{
    AlDia,
    ProximoAVencer,
    Atrasado,
    Suspendido,
    Legal,
    SinClasificar
}

public sealed class Customer
{
    public string Id { get; set; } = "";
    public string Contract { get; set; } = "";
    public string NationalId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal MonthlyFee { get; set; }
    public DateTime? DueDate { get; set; }
    public string SourceStatus { get; set; } = "";
    public string Sector { get; set; } = "";
    public string Location { get; set; } = "";
    public bool WhatsAppConsent { get; set; }
    public DateTime? WhatsAppConsentRecordedAt { get; set; }
    public string WhatsAppConsentSource { get; set; } = "";
    public bool DoNotContact { get; set; }
    public CustomerStage Stage { get; set; }
    public string FullName => string.Join(" ", new[] { FirstName, LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string BalanceText => Balance.ToString("C2", CultureInfo.GetCultureInfo("es-DO"));
    public string StageText => Stage switch
    {
        CustomerStage.AlDia => "Al día",
        CustomerStage.ProximoAVencer => "Próximo a vencer",
        CustomerStage.Atrasado => "Atrasado",
        CustomerStage.Suspendido => "Suspendido",
        CustomerStage.Legal => "Legal (bloqueado)",
        _ => "Sin clasificar"
    };
    public string ConsentText => WhatsAppConsent ? "Sí" : "No";
}

public sealed class ImportSnapshot
{
    public DateTime ImportedAt { get; set; } = DateTime.Now;
    public string SourceFile { get; set; } = "";
    public List<Customer> Customers { get; set; } = [];
}

public sealed record ImportResult(List<Customer> Customers, int PaidSinceLastImport, List<string> Warnings);

public sealed record MessageDraft(Customer Customer, string Text, bool Eligible, string Reason);

public sealed class WhatsAppSettings
{
    public string GraphVersion { get; set; } = "v23.0";
    public string PhoneNumberId { get; set; } = "";
    public string EncryptedAccessToken { get; set; } = "";
    public string TemplateName { get; set; } = "recordatorio_factura";
    public string TemplateLanguage { get; set; } = "es";
    public string MessageTemplate { get; set; } = "Hola {nombre}.\n\nTu cuenta de servicio presenta un balance pendiente de {balance}.\nContrato: {contrato}\n\nSi ya realizaste el pago, puedes ignorar este aviso. Para asistencia, responde a este mensaje.";
    public string TestRecipientPhone { get; set; } = "";
    public int DelaySeconds { get; set; } = 3;
    public int MaxPerCampaign { get; set; } = 100;
    public bool SimulationMode { get; set; } = true;
    public bool PolicyConfirmed { get; set; }
}

public sealed class DispatchLog
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Contract { get; set; } = "";
    public string Customer { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Status { get; set; } = "";
    public string Detail { get; set; } = "";
    public string MessageId { get; set; } = "";
}

public sealed record SendResult(bool Success, string MessageId, string Detail);

public sealed class CampaignFilter
{
    public bool IncludeOverdue { get; set; } = true;
    public bool IncludeSuspended { get; set; } = true;
    public bool IncludeUpcoming { get; set; }
    public decimal MinimumBalance { get; set; }
    public decimal? MaximumBalance { get; set; }
    public string Sector { get; set; } = "";
    public string Location { get; set; } = "";
    public int MaximumRecipients { get; set; } = 100;
    public string SortMode { get; set; } = "Mayor balance";
}
