using System.Windows;
using BloomCobrosAgent.Services;

namespace BloomCobrosAgent;

public partial class MessageEditorWindow : Window
{
    private const string DefaultMessage = "Hola {nombre}.\n\nTu cuenta de servicio presenta un balance pendiente de {balance}.\nContrato: {contrato}\n\nSi ya realizaste el pago, puedes ignorar este aviso. Para asistencia, responde a este mensaje.";
    private readonly LocalStore _store = new();
    private readonly WhatsAppSettings _settings;
    private readonly Customer _sample = new() { FirstName = "Stiven", LastName = "Rosario", Contract = "PRUEBA-001", Balance = 1500, DueDate = DateTime.Today.AddDays(5), Stage = CustomerStage.Atrasado };

    public MessageEditorWindow()
    {
        InitializeComponent();
        _settings = _store.LoadSettings();
        TemplateBox.Text = string.IsNullOrWhiteSpace(_settings.MessageTemplate) ? DefaultMessage : _settings.MessageTemplate;
        UpdatePreview();
    }

    private void TemplateBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (PreviewText == null || TemplateBox == null) return;
        PreviewText.Text = CollectionEngine.RenderTemplate(TemplateBox.Text, _sample);
        CounterText.Text = $"{TemplateBox.Text.Length:N0} / 4,096 caracteres";
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e) => TemplateBox.Text = DefaultMessage;
    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var text = TemplateBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) { MessageBox.Show("El mensaje no puede estar vacío.", "Mensaje inválido", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (text.Length > 4096) { MessageBox.Show("El mensaje supera 4,096 caracteres.", "Mensaje demasiado largo", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        _settings.MessageTemplate = text;
        _store.SaveSettings(_settings);
        DialogResult = true;
    }
}
