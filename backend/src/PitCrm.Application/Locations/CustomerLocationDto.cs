namespace PitCrm.Application.Locations;

public sealed record CustomerLocationDto(Guid Id, Guid CustomerId, string Name, string Address, string? Neighborhood,
    string? City, string Status, string? Instructions, string? MapUrl, Guid? ResponsibleContactId,
    string? ResponsibleContactName, string? ResponsibleContactRole, string? ResponsibleContactPhone,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
