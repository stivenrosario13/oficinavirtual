using System.Text;
using System.Diagnostics;
using System.Windows;
using BloomCobrosAgent.Services;
using Microsoft.Win32;

namespace BloomCobrosAgent;

public partial class MainWindow : Window
{
    private readonly LocalStore _store = new();
    private ImportSnapshot _snapshot = new();
    private CancellationTokenSource? _campaignCancellation;
    private readonly PlaywrightWhatsAppService _playwrightWhatsApp = new();
    private List<MessageDraft> _filteredCampaignDrafts = [];
    private CampaignFilter? _activeCampaignFilter;
    private bool _campaignViewActive;

    public MainWindow()
    {
        InitializeComponent();
        _snapshot = _store.Load() ?? new ImportSnapshot();
        Refresh();
        RefreshConnectionStatus();
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel o CSV (*.xlsx;*.csv)|*.xlsx;*.csv", Title = "Importar clientes" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var result = new SpreadsheetImporter().Import(dialog.FileName, _snapshot);
            _snapshot = new ImportSnapshot { ImportedAt = DateTime.Now, SourceFile = dialog.FileName, Customers = result.Customers };
            _store.Save(_snapshot);
            _filteredCampaignDrafts = [];
            _activeCampaignFilter = null;
            _campaignViewActive = false;
            SendFilteredButton.IsEnabled = false;
            Refresh();
            var detail = $"Se importaron {result.Customers.Count:N0} clientes. {result.PaidSinceLastImport:N0} aparecen pagados desde la importación anterior.";
            if (result.Warnings.Count > 0) detail += "\n\nAvisos:\n• " + string.Join("\n• ", result.Warnings);
            MessageBox.Show(detail, "Importación completada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo importar el archivo.\n\n" + ex.Message, "Error de importación", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var messageTemplate = _store.LoadSettings().MessageTemplate;
        var drafts = _snapshot.Customers.Select(customer => CollectionEngine.CreateDraft(customer, messageTemplate)).Where(x => x.Eligible).ToList();
        if (drafts.Count == 0)
        {
            MessageBox.Show("No hay borradores elegibles. Verifica consentimiento, teléfono y estado.", "Sin borradores", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new SaveFileDialog { Filter = "Archivo CSV (*.csv)|*.csv", FileName = $"borradores-{DateTime.Now:yyyyMMdd-HHmm}.csv" };
        if (dialog.ShowDialog() != true) return;
        var lines = new List<string> { "contrato,cliente,telefono,balance,estado,mensaje" };
        lines.AddRange(drafts.Select(d => string.Join(',', Csv(d.Customer.Contract), Csv(d.Customer.FullName), Csv(d.Customer.Phone), Csv(d.Customer.Balance.ToString("0.00")), Csv(d.Customer.StageText), Csv(d.Text))));
        File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
        StatusText.Text = $"Se exportaron {drafts.Count:N0} borradores para revisión · {DateTime.Now:t}";
        MessageBox.Show("Borradores exportados. Este archivo es para revisión humana y no realiza envíos.", "Exportación completada", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow { Owner = this };
        window.ShowDialog();
        RefreshConnectionStatus();
    }

    private void MessageButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new MessageEditorWindow { Owner = this };
        if (window.ShowDialog() == true) StatusText.Text = "Mensaje personalizado guardado correctamente.";
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = _store.LoadSettings();
        var recentSuccessful = _store.LoadLogs()
            .Where(x => x.Timestamp >= DateTime.Now.AddHours(-24) && x.Status == "Aceptado" && x.Channel == "Cloud API")
            .Select(x => x.Phone).ToHashSet();
        var filterWindow = new CampaignFilterWindow(_snapshot.Customers, recentSuccessful, settings.MaxPerCampaign) { Owner = this };
        if (filterWindow.ShowDialog() != true) return;
        _filteredCampaignDrafts = filterWindow.SelectedDrafts;
        _activeCampaignFilter = filterWindow.SelectedFilter;
        _campaignViewActive = true;
        SearchBox.Text = "";
        CustomersGrid.ItemsSource = _filteredCampaignDrafts.Select(d => d.Customer).ToList();
        SendFilteredButton.IsEnabled = _filteredCampaignDrafts.Count > 0;
        StatusText.Text = $"Filtro aplicado: {_filteredCampaignDrafts.Count:N0} cliente(s). Revisa la lista y pulsa Enviar filtrados cuando estés listo.";
    }

    private async void SendFilteredButton_Click(object sender, RoutedEventArgs e)
    {
        if (_filteredCampaignDrafts.Count == 0 || _activeCampaignFilter == null)
        {
            MessageBox.Show("Primero pulsa Filtrar y aplica los criterios de la campaña.", "No hay filtro activo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var settings = _store.LoadSettings();
        var token = LocalStore.UnprotectToken(settings.EncryptedAccessToken);
        if (!settings.PolicyConfirmed)
        {
            MessageBox.Show("Abre Configuración y confirma los requisitos de consentimiento, plantilla y política.", "Configuración incompleta", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!settings.SimulationMode && (string.IsNullOrWhiteSpace(settings.PhoneNumberId) || string.IsNullOrWhiteSpace(token)))
        {
            MessageBox.Show("Faltan el Phone Number ID o el Access Token.", "WhatsApp sin conectar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var drafts = _filteredCampaignDrafts.ToList();
        var filter = _activeCampaignFilter;
        var mode = settings.SimulationMode ? "SIMULACIÓN" : "ENVÍO REAL";
        var states = string.Join(", ", new[]
        {
            filter.IncludeOverdue ? "Atrasados" : "",
            filter.IncludeSuspended ? "Suspendidos" : "",
            filter.IncludeUpcoming ? "Próximos a vencer" : ""
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var answer = MessageBox.Show($"Modo: {mode}\nDestinatarios: {drafts.Count:N0}\nEstados: {states}\nOrden: {filter.SortMode}\nPlantilla: {settings.TemplateName}\n\n¿Deseas continuar?",
            "Confirmar campaña", MessageBoxButton.YesNo, settings.SimulationMode ? MessageBoxImage.Information : MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        SendButton.IsEnabled = false;
        SendFilteredButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        _campaignCancellation = new CancellationTokenSource();
        var service = new OfficialWhatsAppService();
        var successful = 0;
        var failed = 0;
        try
        {
            for (var index = 0; index < drafts.Count; index++)
            {
                _campaignCancellation.Token.ThrowIfCancellationRequested();
                StatusText.Text = $"Procesando {index + 1:N0} de {drafts.Count:N0} · Correctos {successful:N0} · Fallidos {failed:N0}";
                var draft = drafts[index];
                var result = await service.SendTemplateAsync(settings, token, draft, _campaignCancellation.Token);
                if (result.Success) successful++; else failed++;
                _store.AppendLog(new DispatchLog
                {
                    Contract = draft.Customer.Contract, Customer = draft.Customer.FullName,
                    Phone = draft.Customer.Phone, Channel = settings.SimulationMode ? "Simulación" : "Cloud API",
                    Status = result.Success ? "Aceptado" : "Fallido", Detail = result.Detail, MessageId = result.MessageId
                });
                if (index + 1 < drafts.Count)
                    await Task.Delay(TimeSpan.FromSeconds(settings.DelaySeconds), _campaignCancellation.Token);
            }
            MessageBox.Show($"Campaña terminada.\n\nCorrectos: {successful:N0}\nFallidos: {failed:N0}", "Resultado", MessageBoxButton.OK,
                failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = $"Campaña cancelada · Correctos {successful:N0} · Fallidos {failed:N0}";
        }
        finally
        {
            _campaignCancellation.Dispose();
            _campaignCancellation = null;
            SendButton.IsEnabled = true;
            SendFilteredButton.IsEnabled = _filteredCampaignDrafts.Count > 0;
            CancelButton.IsEnabled = false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _campaignCancellation?.Cancel();

    private void ExportHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var logs = _store.LoadLogs();
        if (logs.Count == 0)
        {
            MessageBox.Show("Todavía no hay operaciones registradas.", "Historial vacío", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new SaveFileDialog { Filter = "Archivo CSV (*.csv)|*.csv", FileName = $"historial-envios-{DateTime.Now:yyyyMMdd-HHmm}.csv" };
        if (dialog.ShowDialog() != true) return;
        var lines = new List<string> { "fecha,contrato,cliente,telefono,canal,estado,detalle,message_id" };
        lines.AddRange(logs.Select(x => string.Join(',', Csv(x.Timestamp.ToString("s")), Csv(x.Contract), Csv(x.Customer), Csv(x.Phone), Csv(x.Channel), Csv(x.Status), Csv(x.Detail), Csv(x.MessageId))));
        File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
        StatusText.Text = $"Historial exportado: {logs.Count:N0} registros.";
    }

    private async void ManualButton_Click(object sender, RoutedEventArgs e)
    {
        if (CustomersGrid.SelectedItem is not Customer customer)
        {
            MessageBox.Show("Selecciona primero un cliente de la tabla.", "Selecciona un cliente", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!customer.WhatsAppConsent && !TryRecordConsent(customer)) return;
        var draft = CollectionEngine.CreateDraft(customer, _store.LoadSettings().MessageTemplate);
        if (!draft.Eligible)
        {
            MessageBox.Show(draft.Reason, "No se puede preparar el mensaje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            StatusText.Text = "Abriendo Microsoft Edge mediante Playwright...";
            await _playwrightWhatsApp.OpenPreparedChatAsync(customer.Phone, draft.Text);
            _store.AppendLog(new DispatchLog
            {
                Contract = customer.Contract, Customer = customer.FullName, Phone = customer.Phone,
                Channel = "Playwright asistido", Status = "Preparado",
                Detail = "Conversación y mensaje preparados; el operador debe confirmar el envío."
            });
            StatusText.Text = $"Mensaje preparado para {customer.FullName}. Revisa y pulsa Enviar en WhatsApp Web.";
        }
        catch (Exception ex)
        {
            var url = $"https://wa.me/{customer.Phone}?text={Uri.EscapeDataString(draft.Text)}";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            StatusText.Text = "Playwright no pudo iniciar Edge; se abrió el navegador predeterminado.";
            MessageBox.Show("No se pudo iniciar el modo Playwright. Se abrió WhatsApp mediante el navegador predeterminado.\n\n" + ex.Message,
                "Modo alternativo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ConsentButton_Click(object sender, RoutedEventArgs e)
    {
        if (CustomersGrid.SelectedItem is not Customer customer)
        {
            MessageBox.Show("Selecciona primero un cliente de la tabla.", "Selecciona un cliente", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        TryRecordConsent(customer);
    }

    private void BulkConsentButton_Click(object sender, RoutedEventArgs e)
    {
        var visible = CustomersGrid.ItemsSource?.Cast<Customer>().ToList() ?? [];
        var candidates = visible.Where(c => !c.WhatsAppConsent && !c.DoNotContact && c.Stage != CustomerStage.Legal && !string.IsNullOrWhiteSpace(c.Phone)).ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show("No hay clientes visibles pendientes de permiso que puedan actualizarse.", "Sin cambios", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var answer = MessageBox.Show(
            $"Vas a registrar consentimiento para {candidates.Count:N0} cliente(s) visibles.\n\n" +
            "Confirma únicamente si todos ya autorizaron recibir mensajes de esta empresa por WhatsApp. " +
            "La fecha y esta declaración quedarán registradas individualmente.",
            "Confirmar permisos masivos", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        var timestamp = DateTime.Now;
        foreach (var customer in candidates)
        {
            customer.WhatsAppConsent = true;
            customer.WhatsAppConsentRecordedAt = timestamp;
            customer.WhatsAppConsentSource = "Confirmación masiva del operador";
        }
        _store.Save(_snapshot);
        _store.AppendLogs(candidates.Select(customer => new DispatchLog
        {
            Timestamp = timestamp, Contract = customer.Contract, Customer = customer.FullName, Phone = customer.Phone,
            Channel = "Consentimiento", Status = "Registrado",
            Detail = "Autorización confirmada mediante acción masiva del operador."
        }));
        Refresh();
        StatusText.Text = $"Permiso registrado para {candidates.Count:N0} cliente(s).";
    }

    private bool TryRecordConsent(Customer customer)
    {
        if (customer.DoNotContact)
        {
            MessageBox.Show("Este cliente está marcado como No contactar. Esa exclusión no se puede reemplazar desde este botón.",
                "Cliente excluido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (customer.Stage == CustomerStage.Legal)
        {
            MessageBox.Show("Los casos legales permanecen bloqueados para WhatsApp.",
                "Caso bloqueado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (customer.WhatsAppConsent)
        {
            MessageBox.Show($"{customer.FullName} ya tiene consentimiento registrado.",
                "Consentimiento existente", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }
        var answer = MessageBox.Show(
            $"¿Confirmas que {customer.FullName} autorizó recibir mensajes de esta empresa por WhatsApp?\n\n" +
            $"Teléfono: +{customer.Phone}\nContrato: {customer.Contract}\n\n" +
            "Esta confirmación quedará guardada en el historial.",
            "Registrar consentimiento", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return false;

        customer.WhatsAppConsent = true;
        customer.WhatsAppConsentRecordedAt = DateTime.Now;
        customer.WhatsAppConsentSource = "Confirmado manualmente por el operador";
        _store.Save(_snapshot);
        _store.AppendLog(new DispatchLog
        {
            Contract = customer.Contract, Customer = customer.FullName, Phone = customer.Phone,
            Channel = "Consentimiento", Status = "Registrado",
            Detail = $"Autorización confirmada manualmente el {customer.WhatsAppConsentRecordedAt:g}."
        });
        Refresh();
        StatusText.Text = $"Consentimiento registrado para {customer.FullName}.";
        return true;
    }

    private void RefreshConnectionStatus()
    {
        var settings = _store.LoadSettings();
        ConnectionText.Text = settings.SimulationMode
            ? "● Modo simulación"
            : string.IsNullOrWhiteSpace(settings.PhoneNumberId) ? "○ WhatsApp sin configurar" : "● Cloud API configurada";
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplySearch();

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
    }

    private void ResetCampaignFilterButton_Click(object sender, RoutedEventArgs e)
    {
        _campaignViewActive = false;
        _filteredCampaignDrafts = [];
        _activeCampaignFilter = null;
        SendFilteredButton.IsEnabled = false;
        SearchBox.Text = "";
        ApplySearch();
        StatusText.Text = $"Filtro de campaña eliminado. Mostrando {_snapshot.Customers.Count:N0} clientes.";
    }

    private void ApplySearch()
    {
        if (CustomersGrid == null || SearchBox == null) return;
        var baseCustomers = _campaignViewActive
            ? _filteredCampaignDrafts.Select(d => d.Customer).ToList()
            : _snapshot.Customers;
        var query = CollectionEngine.Normalize(SearchBox.Text);
        if (string.IsNullOrWhiteSpace(query))
        {
            CustomersGrid.ItemsSource = baseCustomers;
            StatusText.Text = _campaignViewActive
                ? $"Filtro de campaña activo: {baseCustomers.Count:N0} cliente(s)."
                : $"Mostrando {baseCustomers.Count:N0} clientes.";
            return;
        }
        var digits = new string(SearchBox.Text.Where(char.IsDigit).ToArray());
        var results = baseCustomers.Where(customer =>
            CollectionEngine.Normalize(customer.Contract).Contains(query) ||
            CollectionEngine.Normalize(customer.Id).Contains(query) ||
            CollectionEngine.Normalize(customer.NationalId).Contains(query) ||
            CollectionEngine.Normalize(customer.FullName).Contains(query) ||
            (!string.IsNullOrWhiteSpace(digits) && customer.Phone.Contains(digits))).ToList();
        CustomersGrid.ItemsSource = results;
        StatusText.Text = results.Count == 0
            ? $"No se encontraron clientes para “{SearchBox.Text.Trim()}”."
            : $"{results.Count:N0} resultado(s) para “{SearchBox.Text.Trim()}”.";
    }

    protected override async void OnClosed(EventArgs e)
    {
        await _playwrightWhatsApp.DisposeAsync();
        base.OnClosed(e);
    }

    private void Refresh()
    {
        ApplySearch();
        TotalText.Text = _snapshot.Customers.Count.ToString("N0");
        PendingText.Text = _snapshot.Customers.Count(c => c.Stage is CustomerStage.Atrasado or CustomerStage.Suspendido or CustomerStage.ProximoAVencer).ToString("N0");
        EligibleText.Text = _snapshot.Customers.Count(c => CollectionEngine.CreateDraft(c).Eligible).ToString("N0");
        BlockedText.Text = _snapshot.Customers.Count(c => c.Stage == CustomerStage.Legal || c.DoNotContact).ToString("N0");
        LastImportText.Text = _snapshot.Customers.Count == 0 ? "Importa un Excel o CSV para comenzar" : $"Última importación: {_snapshot.ImportedAt:g} · {Path.GetFileName(_snapshot.SourceFile)}";
    }
}
