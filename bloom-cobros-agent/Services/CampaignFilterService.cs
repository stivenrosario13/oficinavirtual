namespace BloomCobrosAgent.Services;

public static class CampaignFilterService
{
    public static List<MessageDraft> Apply(
        IEnumerable<Customer> customers,
        IReadOnlySet<string> recentlySentPhones,
        CampaignFilter filter)
    {
        var query = customers
            .Select(customer => CollectionEngine.CreateDraft(customer))
            .Where(draft => draft.Eligible)
            .Where(draft => !recentlySentPhones.Contains(draft.Customer.Phone))
            .Where(draft => StageIncluded(draft.Customer.Stage, filter))
            .Where(draft => draft.Customer.Balance >= filter.MinimumBalance)
            .Where(draft => filter.MaximumBalance == null || draft.Customer.Balance <= filter.MaximumBalance)
            .Where(draft => Contains(draft.Customer.Sector, filter.Sector))
            .Where(draft => Contains(draft.Customer.Location, filter.Location));

        query = filter.SortMode switch
        {
            "Menor balance" => query.OrderBy(d => d.Customer.Balance),
            "Nombre" => query.OrderBy(d => d.Customer.FullName),
            "Contrato" => query.OrderBy(d => d.Customer.Contract),
            _ => query.OrderByDescending(d => d.Customer.Balance)
        };
        return query.Take(Math.Max(1, filter.MaximumRecipients)).ToList();
    }

    private static bool StageIncluded(CustomerStage stage, CampaignFilter filter) => stage switch
    {
        CustomerStage.Atrasado => filter.IncludeOverdue,
        CustomerStage.Suspendido => filter.IncludeSuspended,
        CustomerStage.ProximoAVencer => filter.IncludeUpcoming,
        _ => false
    };

    private static bool Contains(string value, string filter) =>
        string.IsNullOrWhiteSpace(filter) || CollectionEngine.Normalize(value).Contains(CollectionEngine.Normalize(filter));
}
