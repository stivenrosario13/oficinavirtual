static class ExistingUserLogin
{
    public static bool Unchanged(string? requested,string? stored)=>
        !string.IsNullOrWhiteSpace(stored)&&string.Equals(requested?.Trim(),stored.Trim(),StringComparison.OrdinalIgnoreCase);
}
