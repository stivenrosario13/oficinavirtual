using System;

internal record DashboardAdministrator(Guid Id, string DisplayName, string Email, string? Region, int Groups, int Total, int Completed, int Pending, int Review, int Percent, DateTime? LastUpdated);
