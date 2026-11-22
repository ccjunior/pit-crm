namespace PitCrm.Application.Demands;

public sealed record DemandSearchResult(IReadOnlyList<DemandListItemDto> Items, int TotalCount);

public sealed record DemandListItemDto(Guid Id, int ReferenceNumber, Guid CustomerId, string CustomerName, Guid LocationId,
    string LocationName, string Description, string Origin, string? Priority, string Status,
    Guid? AssignedToUserId, string? AssignedToDisplayName, DateTimeOffset CreatedAtUtc);

public sealed record DemandDetailsDto(Guid Id, int ReferenceNumber, int Version, Guid CustomerId, string CustomerName, Guid LocationId,
    string LocationName, string Description, string Origin, string? Priority, string Status,
    IReadOnlyList<DemandExternalReferenceDto> ExternalReferences, IReadOnlyList<DemandSubitemDto> Subitems, Guid? AssignedToUserId,
    string? AssignedToDisplayName, Guid CreatedByUserId, string CreatedByDisplayName,
    DateTimeOffset CreatedAtUtc, IReadOnlyList<DemandActivityDto> Activities);

public sealed record DemandExternalReferenceDto(string Type, string Value);

public sealed record DemandSubitemDto(Guid Id, Guid? SourceProposalItemId, int Position, string Description, decimal? Amount);

public sealed record DemandActivityDto(Guid Id, Guid AuthorUserId, string AuthorDisplayName,
    string EventType, string Description, DateTimeOffset OccurredAtUtc);
