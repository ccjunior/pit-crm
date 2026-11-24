namespace PitCrm.Application.Proposals;

public sealed record ProposalItemInput(string? Description, decimal Amount);

public sealed record CreateCommercialRequestInput(Guid? CustomerId, string? ProspectName,
    string? ProspectContactName, string? ProspectEmail, string? ProspectPhone,
    Guid? ResponsibleUserId, string? Origin, string? Description,
    IReadOnlyList<CommercialReferenceInput>? References);

public sealed record UpdateCommercialRequestInput(int Version, Guid? CustomerId, string? ProspectName,
    string? ProspectContactName, string? ProspectEmail, string? ProspectPhone,
    Guid? ResponsibleUserId, string? Origin, string? Description,
    IReadOnlyList<CommercialReferenceInput>? References);

public sealed record CommercialReferenceInput(string? Type, string? Value);
public sealed record CommercialReferenceDto(Guid Id, string Type, string Value);

public sealed record CreateProposalRevisionInput(int Version, IReadOnlyList<ProposalItemInput>? Items,
    DateOnly? ValidUntil, string? Notes);

public sealed record RecordProposalSentInput(int Version, Guid RevisionId, string? Channel, DateTimeOffset SentAtUtc);

public sealed record RecordProposalApprovalInput(int Version, Guid RevisionId, string? ApproverName,
    string? Channel, string? Notes, DateTimeOffset ApprovedAtUtc);

public sealed record ProposalOutcomeInput(int Version, string? Status, string? Reason);

public sealed record ProposalPageDto(IReadOnlyList<ProposalListItemDto> Items, int Page, int PageSize, int TotalCount);

public sealed record ProposalListItemDto(Guid Id, Guid? CustomerId, string? CustomerName, string? ProspectName,
    string Number, Guid? ResponsibleUserId, string? ResponsibleName, string Description, string Origin, string Status, int Version, int? CurrentRevisionNumber,
    decimal? CurrentRevisionTotal, DateTimeOffset UpdatedAtUtc);

public sealed record ProposalDetailsDto(Guid Id, Guid? CustomerId, string? CustomerName,
    string? ProspectName, string? ProspectContactName, string? ProspectEmail, string? ProspectPhone,
    string Number, Guid? ResponsibleUserId, string? ResponsibleName, IReadOnlyList<CommercialReferenceDto> References,
    string Description, string Origin, string Status, int Version, int? CurrentRevisionNumber,
    IReadOnlyList<ProposalRevisionDto> Revisions, IReadOnlyList<ProposalActivityDto> Activities,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string? OutcomeReason,
    string? DemandDraftState, Guid? ConvertedDemandId);

public sealed record ProposalRevisionDto(Guid Id, int Number, string Status, DateOnly? ValidUntil,
    string? Notes, decimal TotalAmount, IReadOnlyList<ProposalItemDto> Items,
    Guid CreatedByUserId, DateTimeOffset CreatedAtUtc, DateTimeOffset? SentAtUtc, string? SendChannel,
    Guid? ApprovedByUserId, string? ClientApproverName, string? ApprovalChannel,
    string? ApprovalNotes, DateTimeOffset? ApprovedAtUtc);

public sealed record ProposalItemDto(Guid Id, int Position, string Description, decimal Amount);

public sealed record ProposalActivityDto(Guid Id, Guid ActorUserId, string ActorDisplayName,
    string EventType, string Description, DateTimeOffset OccurredAtUtc);

public enum ProposalFailure
{
    None,
    NotFound,
    VersionConflict,
    InvalidState,
    CustomerNotFound,
    InvalidData,
    DemandAlreadyConverted,
}

public sealed record ProposalCommandResult(ProposalDetailsDto? Proposal, ProposalFailure Failure,
    Guid? DemandDraftId = null);

public sealed record ProposalDemandDraftSeed(Guid OrganizationId, Guid RequestId, Guid RevisionId,
    Guid CreatedByUserId, Guid? CustomerId, string? ProspectName, string Description, string Origin,
    IReadOnlyList<ProposalDemandDraftItemSeed> Items, DateTimeOffset CreatedAtUtc);

public sealed record ProposalDemandDraftItemSeed(Guid SourceItemId, string Description, decimal Amount);

public interface IProposalStore
{
    Task<ProposalPageDto> SearchAsync(Guid organizationId, string? searchTerm, string? status,
        Guid? customerId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken);
    Task<ProposalDetailsDto?> FindAsync(Guid organizationId, Guid requestId, CancellationToken cancellationToken);
    Task<ProposalCommandResult> CreateAsync(Guid organizationId, Guid actorUserId,
        CreateCommercialRequestInput input, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ProposalCommandResult> UpdateAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        UpdateCommercialRequestInput input, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ProposalCommandResult> ChangeStatusAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        ProposalOutcomeInput input, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ProposalCommandResult> CreateRevisionAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        CreateProposalRevisionInput input, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ProposalCommandResult> RecordSentAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        RecordProposalSentInput input, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ProposalCommandResult> ApproveAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        RecordProposalApprovalInput input, DateTimeOffset now, CancellationToken cancellationToken);
}
