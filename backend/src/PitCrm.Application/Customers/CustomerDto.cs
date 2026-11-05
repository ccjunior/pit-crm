namespace PitCrm.Application.Customers;

public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Document,
    string? Instructions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
