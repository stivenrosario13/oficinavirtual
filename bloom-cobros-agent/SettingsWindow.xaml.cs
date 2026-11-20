using System.Windows;
using BloomCobrosAgent.Services;

namespace BloomCobrosAgent;

public partial class SettingsWindow : Window
{
    private readonly LocalStore _store = new();
    private readonly WhatsAppSettings _settings;

    public SettingsWindow()
    {
        InitializeComponent();
        _settings = _store.LoadSettings();
        VersionBox.Text = _settings.GraphVersion;
        PhoneIdBox.Text = _settings.PhoneNumberId;
        TokenBox.Password = LocalStore.UnprotectToken(_settings.EncryptedAccessToken);
        TestPhoneBox.Text = _settings.TestRecipientPhone;
        TemplateBox.Text = _settings.TemplateName;
        LanguageBox.Text = _settings.TemplateLanguage;
        DelayBox.Text = _settings.DelaySeconds.ToString();
        MaximumBox.Text = _settings.MaxPerCampaign.ToString();
        SimulationCheck.IsChecked = _settings.SimulationMode;
        PolicyCheck.IsChecked = _settings.PolicyConfirmed;
    }

    private bool Capture()
    {
        if (!int.TryParse(DelayBox.Text, out var delay) || delay < 1 || delay > 300 ||
            !int.TryParse(MaximumBox.Text, out var maximum) || maximum < 1 || maximum > 10000)
        {
            ResultText.Text = "La pausa debe ser 1–300 segundos y el máximo 1–10,000.";
            return false;
        }
        _settings.GraphVersion = VersionBox.Text.Trim();
        _settings.PhoneNumberId = PhoneIdBox.Text.Trim();
        _settings.TestRecipientPhone = CollectionEngine.NormalizePhone(TestPhoneBox.Text);
        _settings.TemplateName = TemplateBox.Text.Trim();
        _settings.TemplateLanguage = LanguageBox.Text.Trim();
        _settings.DelaySeconds = delay;
        _settings.MaxPerCampaign = maximum;
        _settings.SimulationMode = SimulationCheck.IsChecked == true;
        _settings.PolicyConfirmed = PolicyCheck.IsChecked == true;
        _settings.EncryptedAccessToken = LocalStore.ProtectToken(TokenBox.Password);
        return true;
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Capture()) return;
        ResultText.Text = _settings.SimulationMode ? "Probando credenciales aunque el modo simulación esté activo..." : "Probando conexión...";
        var result = await new OfficialWhatsAppService().TestConnectionAsync(_settings, TokenBox.Password);
        ResultText.Text = result.Success ? "✓ " + result.Detail : "Error: " + result.Detail;
    }

    private async void SendTestButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Capture()) return;
        if (string.IsNullOrWhiteSpace(_settings.TestRecipientPhone))
        {
            ResultText.Text = "Escribe un número receptor válido de 11 a 15 dígitos.";
            return;
        }
        if (!_settings.PolicyConfirmed)
        {
            ResultText.Text = "Confirma primero los requisitos de consentimiento, plantilla y política.";
            return;
        }
        if (_settings.SimulationMode)
        {
            ResultText.Text = "Desactiva Modo simulación para enviar una prueba real.";
            return;
        }
        var token = TokenBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(_settings.PhoneNumberId) || string.IsNullOrWhiteSpace(token))
        {
            ResultText.Text = "Faltan Phone Number ID o Access Token.";
            return;
        }
        var answer = MessageBox.Show(
            $"Se enviará una sola plantilla '{_settings.TemplateName}' a +{_settings.TestRecipientPhone}.\n\n¿Continuar?",
            "Confirmar prueba real", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        ResultText.Text = "Enviando mensaje de prueba...";
        var customer = new Customer
        {
            FirstName = "Prueba",
            LastName = "Bloom",
            Phone = _settings.TestRecipientPhone,
            Balance = 1,
            WhatsAppConsent = true,
            Stage = CustomerStage.ProximoAVencer
        };
        var draft = CollectionEngine.CreateDraft(customer);
        var result = await new OfficialWhatsAppService().SendTemplateAsync(_settings, token, draft);
        ResultText.Text = result.Success
            ? $"✓ Prueba aceptada por WhatsApp. ID: {result.MessageId}"
            : "Error de envío: " + result.Detail;
        _store.AppendLog(new DispatchLog
        {
            Customer = customer.FullName, Phone = customer.Phone, Channel = "Cloud API - Prueba",
            Status = result.Success ? "Aceptado" : "Fallido", Detail = result.Detail, MessageId = result.MessageId
        });
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Capture()) return;
        _store.SaveSettings(_settings);
        DialogResult = true;
    }
}
