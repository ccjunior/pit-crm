namespace PitCrm.Application.Customers;

public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Document,
    string? Instructions,
    string? Address,
    string? Locality,
    string? City,
    string? State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
