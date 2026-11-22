using PitCrm.Application.Proposals;

namespace PitCrm.Application.Demands;

public sealed record DemandDraftPageDto(IReadOnlyList<DemandDraftListItemDto> Items, int Page, int PageSize, int TotalCount);

public sealed record DemandDraftListItemDto(Guid Id, Guid? CustomerId, string? CustomerName,
    string? ProspectName, string Description, string Origin, int Version, DateTimeOffset CreatedAtUtc);

public sealed record DemandDraftDetailsDto(Guid Id, Guid ProposalRequestId, Guid ProposalRevisionId,
    Guid? CustomerId, string? CustomerName, Guid? LocationId, string? LocationName,
    string? ProspectName, string Description, string Origin, string? Priority,
    Guid? AssignedToUserId, string? AssignedToDisplayName, int Version,
    IReadOnlyList<DemandDraftItemDto> Items, DateTimeOffset CreatedAtUtc);

public sealed record DemandDraftItemDto(Guid Id, Guid SourceProposalItemId, int Position, string Description, decimal Amount);

public sealed record UpdateDemandDraftInput(int Version, Guid? CustomerId, Guid? LocationId,
    string? Description, string? Origin, string? Priority, Guid? AssignedToUserId);

public enum DemandDraftFailure
{
    None,
    NotFound,
    VersionConflict,
    AlreadyConverted,
    CustomerNotFound,
    LocationNotFound,
    AssigneeNotEligible,
    NotAllowed,
    InvalidData,
}

public sealed record DemandDraftCommandResult(DemandDraftDetailsDto? Draft,
    DemandDraftFailure Failure, Guid? DemandId = null);

public interface IDemandDraftStore
{
    Task<Guid> CreateFromApprovedProposalAsync(ProposalDemandDraftSeed seed, CancellationToken cancellationToken);
    Task<DemandDraftPageDto> SearchAsync(Guid organizationId, string? searchTerm, int page,
        int pageSize, CancellationToken cancellationToken);
    Task<DemandDraftDetailsDto?> FindAsync(Guid organizationId, Guid draftId, CancellationToken cancellationToken);
    Task<DemandDraftCommandResult> UpdateAsync(Guid organizationId, Guid draftId, Guid actorUserId,
        UpdateDemandDraftInput input, bool mayManageCustomers, DateTimeOffset now, CancellationToken cancellationToken);
    Task<DemandDraftCommandResult> ConvertAsync(Guid organizationId, Guid draftId, Guid actorUserId,
        int version, DateTimeOffset now, CancellationToken cancellationToken);
}
