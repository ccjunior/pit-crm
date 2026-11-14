namespace PitCrm.Application.Customers;

public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Document,
    string? Instructions,
    string? PostalCode,
    string? Address,
    string? Locality,
    string? City,
    string? State,
    int LocationCount,
    int ActiveLocationCount,
    int ContactCount,
    int ReceivedDemandCount,
    string? PrimaryContactName,
    string? PrimaryContactPhone,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
