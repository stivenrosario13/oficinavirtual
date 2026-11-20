using System.Globalization;
using System.Text;

namespace BloomCobrosAgent.Services;

public static class CollectionEngine
{
    public static void Classify(Customer customer, DateTime today)
    {
        var status = Normalize(customer.SourceStatus);
        if (status.Contains("legal")) customer.Stage = CustomerStage.Legal;
        else if (status.Contains("cortad") || status.Contains("suspend")) customer.Stage = CustomerStage.Suspendido;
        else if (customer.Balance <= 0) customer.Stage = CustomerStage.AlDia;
        else if (customer.DueDate is { } due && due.Date >= today.Date) customer.Stage = CustomerStage.ProximoAVencer;
        else if (customer.Balance > 0) customer.Stage = CustomerStage.Atrasado;
        else customer.Stage = CustomerStage.SinClasificar;
    }

    public static MessageDraft CreateDraft(Customer customer, string? customTemplate = null)
    {
        if (customer.Stage == CustomerStage.Legal)
            return new(customer, "", false, "WhatsApp restringe el cobro de deudas y los casos legales.");
        if (customer.DoNotContact)
            return new(customer, "", false, "Cliente incluido en No contactar.");
        if (!customer.WhatsAppConsent)
            return new(customer, "", false, "No hay consentimiento registrado para WhatsApp.");
        if (string.IsNullOrWhiteSpace(customer.Phone))
            return new(customer, "", false, "No tiene un teléfono válido.");
        if (customer.Stage is CustomerStage.AlDia or CustomerStage.SinClasificar)
            return new(customer, "", false, "El estado no requiere aviso.");

        var name = string.IsNullOrWhiteSpace(customer.FirstName) ? "cliente" : customer.FirstName.Trim();
        var text = string.IsNullOrWhiteSpace(customTemplate)
            ? customer.Stage == CustomerStage.ProximoAVencer
                ? $"Hola {name}. Te recordamos que próximamente vence tu factura por {customer.BalanceText}. Si ya realizaste el pago, puedes ignorar este aviso. Para asistencia, responde a este mensaje."
                : $"Hola {name}. Tu cuenta de servicio presenta un balance pendiente de {customer.BalanceText}. Si ya realizaste el pago, puedes ignorar este aviso. Para asistencia, responde a este mensaje."
            : RenderTemplate(customTemplate, customer);
        return new(customer, text, true, "Listo para revisión humana.");
    }

    public static string RenderTemplate(string template, Customer customer) => template
        .Replace("{nombre}", string.IsNullOrWhiteSpace(customer.FirstName) ? "cliente" : customer.FirstName.Trim(), StringComparison.OrdinalIgnoreCase)
        .Replace("{nombre_completo}", string.IsNullOrWhiteSpace(customer.FullName) ? "cliente" : customer.FullName, StringComparison.OrdinalIgnoreCase)
        .Replace("{balance}", customer.BalanceText, StringComparison.OrdinalIgnoreCase)
        .Replace("{contrato}", string.IsNullOrWhiteSpace(customer.Contract) ? "N/D" : customer.Contract, StringComparison.OrdinalIgnoreCase)
        .Replace("{fecha_vencimiento}", customer.DueDate?.ToString("dd/MM/yyyy") ?? "N/D", StringComparison.OrdinalIgnoreCase)
        .Replace("{estado}", customer.StageText, StringComparison.OrdinalIgnoreCase);

    public static string NormalizePhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) digits = "1" + digits;
        return digits.Length is >= 11 and <= 15 ? digits : "";
    }

    public static string Normalize(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }
}
