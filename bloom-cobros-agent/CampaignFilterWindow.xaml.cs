using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using BloomCobrosAgent.Services;

namespace BloomCobrosAgent;

public partial class CampaignFilterWindow : Window
{
    private readonly IReadOnlyList<Customer> _customers;
    private readonly IReadOnlySet<string> _recentlySent;
    private readonly int _configuredMaximum;
    public List<MessageDraft> SelectedDrafts { get; private set; } = [];
    public CampaignFilter SelectedFilter { get; private set; } = new();

    public CampaignFilterWindow(IReadOnlyList<Customer> customers, IReadOnlySet<string> recentlySent, int configuredMaximum)
    {
        InitializeComponent();
        _customers = customers;
        _recentlySent = recentlySent;
        _configuredMaximum = Math.Max(1, configuredMaximum);
        LimitBox.Text = _configuredMaximum.ToString();
        Loaded += (_, _) => RefreshPreview();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (!TryBuildFilter(out var filter, false)) return;
        var allMatching = CampaignFilterService.Apply(_customers, _recentlySent, new CampaignFilter
        {
            IncludeOverdue = filter.IncludeOverdue, IncludeSuspended = filter.IncludeSuspended,
            IncludeUpcoming = filter.IncludeUpcoming, MinimumBalance = filter.MinimumBalance,
            MaximumBalance = filter.MaximumBalance, Sector = filter.Sector, Location = filter.Location,
            MaximumRecipients = int.MaxValue, SortMode = filter.SortMode
        });
        var selected = allMatching.Take(filter.MaximumRecipients).ToList();
        PreviewText.Text = $"{selected.Count:N0} destinatario(s)";
        DetailText.Text = allMatching.Count > selected.Count
            ? $"Coinciden {allMatching.Count:N0}; se aplicará el límite de {filter.MaximumRecipients:N0}."
            : "Ya se excluyeron falta de consentimiento, No contactar, Legal y enviados en las últimas 24 horas.";
    }

    private bool TryBuildFilter(out CampaignFilter filter, bool showErrors)
    {
        filter = new CampaignFilter();
        if (!TryMoney(MinimumBox?.Text, out var minimum) || minimum < 0 ||
            (!string.IsNullOrWhiteSpace(MaximumBox?.Text) && (!TryMoney(MaximumBox.Text, out _) || ParseMoney(MaximumBox.Text) < minimum)))
        {
            if (showErrors) MessageBox.Show("Revisa el rango de balance.", "Filtro inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!int.TryParse(LimitBox?.Text, out var limit) || limit < 1 || limit > _configuredMaximum)
        {
            if (showErrors) MessageBox.Show($"El máximo debe estar entre 1 y {_configuredMaximum:N0}. Puedes cambiar el tope general en Configuración.", "Límite inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        filter.IncludeOverdue = OverdueCheck?.IsChecked == true;
        filter.IncludeSuspended = SuspendedCheck?.IsChecked == true;
        filter.IncludeUpcoming = UpcomingCheck?.IsChecked == true;
        filter.MinimumBalance = minimum;
        filter.MaximumBalance = string.IsNullOrWhiteSpace(MaximumBox?.Text) ? null : ParseMoney(MaximumBox.Text);
        filter.Sector = SectorBox?.Text.Trim() ?? "";
        filter.Location = LocationBox?.Text.Trim() ?? "";
        filter.MaximumRecipients = limit;
        filter.SortMode = (SortBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Mayor balance";
        return true;
    }

    private static bool TryMoney(string? value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result) ||
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);

    private static decimal ParseMoney(string value)
    {
        TryMoney(value, out var result);
        return result;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildFilter(out var filter, true)) return;
        SelectedFilter = filter;
        SelectedDrafts = CampaignFilterService.Apply(_customers, _recentlySent, filter);
        if (SelectedDrafts.Count == 0)
        {
            MessageBox.Show("Ningún cliente elegible coincide con estos filtros.", "Sin destinatarios", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
