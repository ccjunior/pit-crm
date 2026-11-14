namespace PitCrm.Application.Customers;

public sealed record CustomerContactDto(Guid Id, Guid CustomerId, Guid? LocationId, string Name, string? Role,
    string? Phone, string? Email, bool IsPrimary, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
