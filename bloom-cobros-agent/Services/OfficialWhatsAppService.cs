using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace BloomCobrosAgent.Services;

public sealed class OfficialWhatsAppService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<SendResult> TestConnectionAsync(WhatsAppSettings settings, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.PhoneNumberId) || string.IsNullOrWhiteSpace(token))
            return new(false, "", "Faltan Phone Number ID o Access Token.");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://graph.facebook.com/{settings.GraphVersion}/{Uri.EscapeDataString(settings.PhoneNumberId)}?fields=display_phone_number,verified_name");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await ExecuteAsync(request, cancellationToken);
    }

    public async Task<SendResult> SendTemplateAsync(WhatsAppSettings settings, string token, MessageDraft draft, CancellationToken cancellationToken = default)
    {
        if (settings.SimulationMode)
            return new(true, "SIMULATED", "Simulación: no se contactó a WhatsApp.");

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = draft.Customer.Phone,
            type = "template",
            template = new
            {
                name = settings.TemplateName,
                language = new { code = settings.TemplateLanguage },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = new object[]
                        {
                            new { type = "text", text = string.IsNullOrWhiteSpace(draft.Customer.FirstName) ? "cliente" : draft.Customer.FirstName },
                            new { type = "text", text = draft.Customer.BalanceText }
                        }
                    }
                }
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://graph.facebook.com/{settings.GraphVersion}/{Uri.EscapeDataString(settings.PhoneNumberId)}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return await ExecuteAsync(request, cancellationToken);
    }

    private async Task<SendResult> ExecuteAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var detail = ExtractError(body) ?? $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
                return new(false, "", detail);
            }
            var id = "";
            try
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0)
                    id = messages[0].GetProperty("id").GetString() ?? "";
                else if (json.RootElement.TryGetProperty("display_phone_number", out var phone))
                    return new(true, "", $"Conexión correcta: {phone.GetString()}");
            }
            catch { }
            return new(true, id, string.IsNullOrWhiteSpace(id) ? "Conexión correcta." : "Mensaje aceptado por WhatsApp.");
        }
        catch (OperationCanceledException) { return new(false, "", "Operación cancelada."); }
        catch (Exception ex) { return new(false, "", ex.Message); }
    }

    private static string? ExtractError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.GetProperty("error").GetProperty("message").GetString();
        }
        catch { return null; }
    }
}
