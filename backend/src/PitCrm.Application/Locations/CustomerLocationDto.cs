namespace PitCrm.Application.Locations;

public sealed record CustomerLocationDto(Guid Id, Guid CustomerId, string Name, string Address, string? Instructions,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
