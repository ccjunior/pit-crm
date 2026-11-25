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
public sealed record ProposalTrackingInput(int Version, DateOnly? CustomerDeadline, DateOnly? InternalDeadline,
    ProposalNextActionInput? NextAction);
public sealed record ProposalNextActionInput(string? Description, string? ScheduledAtLocal, string? SelectedOffset);
public sealed record ProposalNextActionDto(Guid ReminderOccurrenceId, int Revision, string Description,
    DateTimeOffset ScheduledAtUtc);
public sealed record ProposalDeadlineOverdueDto(bool Customer, bool Internal, bool Either, DateOnly AsOfLocalDate)
{
    public static ProposalDeadlineOverdueDto Calculate(DateOnly? customerDeadline, DateOnly? internalDeadline,
        DateOnly asOfLocalDate)
    {
        var customer = customerDeadline is { } customerDate && customerDate < asOfLocalDate;
        var internalDeadlineOverdue = internalDeadline is { } internalDate && internalDate < asOfLocalDate;
        return new ProposalDeadlineOverdueDto(customer, internalDeadlineOverdue,
            customer || internalDeadlineOverdue, asOfLocalDate);
    }
}
public static class ProposalDateRules
{
    public const string OrganizationTimeZoneId = "America/Sao_Paulo";
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,
        TimeZoneInfo.FindSystemTimeZoneById(OrganizationTimeZoneId)).DateTime);
}
public sealed record ProposalTrackingDto(Guid Id, int Version, DateOnly? CustomerDeadline,
    DateOnly? InternalDeadline, int ReminderOccurrenceRevision, ProposalNextActionDto? NextAction);
public sealed record ProposalTrackingCommandResult(ProposalTrackingDto? Tracking, ProposalFailure Failure);
public sealed record ProposalKanbanColumnDto(string Status, IReadOnlyList<ProposalListItemDto> Items,
    int TotalCount, string? NextCursor);
public sealed record ProposalKanbanDto(int PageSizePerColumn, int TotalCount,
    IReadOnlyList<ProposalKanbanColumnDto> Columns);
public sealed record MyActionItemDto(Guid ProposalId, string Number, string Status, DateOnly? CustomerDeadline,
    DateOnly? InternalDeadline, bool IsOverdue, ProposalNextActionDto NextAction,
    ProposalDeadlineOverdueDto Overdue);
public sealed record MyActionsPageDto(IReadOnlyList<MyActionItemDto> Items, int Page, int PageSize, int TotalCount);

public sealed record ProposalListItemDto(Guid Id, Guid? CustomerId, string? CustomerName, string? ProspectName,
    string Number, Guid? ResponsibleUserId, string? ResponsibleName, string Description, string Origin, string Status, int Version, int? CurrentRevisionNumber,
    decimal? CurrentRevisionTotal, DateTimeOffset UpdatedAtUtc, DateOnly? CustomerDeadline,
    DateOnly? InternalDeadline, ProposalNextActionDto? NextAction, ProposalDeadlineOverdueDto Overdue);

public sealed record ProposalDetailsDto(Guid Id, Guid? CustomerId, string? CustomerName,
    string? ProspectName, string? ProspectContactName, string? ProspectEmail, string? ProspectPhone,
    string Number, Guid? ResponsibleUserId, string? ResponsibleName, IReadOnlyList<CommercialReferenceDto> References,
    string Description, string Origin, string Status, int Version, int? CurrentRevisionNumber,
    IReadOnlyList<ProposalRevisionDto> Revisions, IReadOnlyList<ProposalActivityDto> Activities,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string? OutcomeReason,
    string? DemandDraftState, Guid? ConvertedDemandId, DateOnly? CustomerDeadline,
    DateOnly? InternalDeadline, int ReminderOccurrenceRevision, ProposalNextActionDto? NextAction,
    ProposalDeadlineOverdueDto Overdue);

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
    ResponsibleRequired,
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
        Guid? customerId, Guid? responsibleUserId, string? origin, string? overdueFor,
        DateOnly? from, DateOnly? to, DateOnly? customerDeadlineFrom, DateOnly? customerDeadlineTo,
        DateOnly? internalDeadlineFrom, DateOnly? internalDeadlineTo,
        DateTimeOffset? nextActionFromUtc, DateTimeOffset? nextActionToUtc, string sortBy, bool sortAscending,
        int page, int pageSize, DateOnly today, CancellationToken cancellationToken);
    Task<ProposalTrackingCommandResult> UpdateTrackingAsync(Guid organizationId, Guid requestId, int version,
        DateOnly? customerDeadline, DateOnly? internalDeadline, string? nextActionDescription,
        DateTimeOffset? nextActionAtUtc, DateTimeOffset now, CancellationToken cancellationToken);
    Task<MyActionsPageDto> MyActionsAsync(Guid organizationId, Guid userId, int page, int pageSize,
        DateOnly today, CancellationToken cancellationToken);
    Task<ProposalKanbanDto> KanbanAsync(Guid organizationId, string? searchTerm, Guid? customerId,
        Guid? responsibleUserId, string? origin, string? overdueFor, DateOnly? from, DateOnly? to,
        DateOnly? customerDeadlineFrom, DateOnly? customerDeadlineTo, DateOnly? internalDeadlineFrom,
        DateOnly? internalDeadlineTo, DateTimeOffset? nextActionFromUtc, DateTimeOffset? nextActionToUtc,
        string sortBy, bool sortAscending, int pageSizePerColumn, IReadOnlyDictionary<string, string?> cursors, DateOnly today,
        CancellationToken cancellationToken);
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
