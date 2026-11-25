namespace PitCrm.Application.Proposals;

public sealed record ProposalActivityInput(string? Type, string? Text, IReadOnlyList<Guid>? MentionedUserIds);
public sealed record ProposalReasonInput(string? ResultStatus, string? Label);

public sealed record ProposalHistoryEventDto(Guid Id, long Sequence, string Type, DateTimeOffset? OccurredAtUtc,
    bool OccurredAtKnown, string? ActorName, string? FromStatus, string? ToStatus,
    string? Reason, string? ActivityType, string? Text, IReadOnlyList<Guid> MentionedUserIds);

public sealed record ProposalHistoryPageDto(IReadOnlyList<ProposalHistoryEventDto> Items, int Page, int PageSize, int TotalCount);
public sealed record ProposalOperatorHistoryItemDto(string Type, DateTimeOffset? OccurredAtUtc,
    bool OccurredAtKnown, string? FromStatus, string? ToStatus);
public sealed record ProposalOperatorHistoryPageDto(IReadOnlyList<ProposalOperatorHistoryItemDto> Items,
    int Page, int PageSize, int TotalCount);
public sealed record ProposalReasonDto(Guid Id, string ResultStatus, string Label, bool IsActive);
public sealed record ProposalAuditEventDto(Guid Id, string Type, string Category, DateTimeOffset OccurredAtUtc,
    string? ActorName, string? DetailsJson);
public sealed record ProposalAuditPageDto(IReadOnlyList<ProposalAuditEventDto> Items, int Page, int PageSize, int TotalCount);
public sealed record ProposalHistoryWriteResult(ProposalHistoryEventDto? Event, ProposalFailure Failure, bool IsReplay = false);
public sealed record ProposalReasonWriteResult(ProposalReasonDto? Reason, ProposalFailure Failure, bool IsReplay = false);

public interface IProposalHistoryStore
{
    Task<ProposalHistoryPageDto?> GetTimelineAsync(Guid organizationId, Guid requestId, int page, int pageSize,
        CancellationToken cancellationToken);
    Task<ProposalOperatorHistoryPageDto?> GetOperatorTimelineAsync(Guid organizationId, Guid demandId, int page,
        int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProposalReasonDto>> GetReasonsAsync(Guid organizationId, string? resultStatus,
        CancellationToken cancellationToken);
    Task<ProposalHistoryWriteResult> RecordActivityAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        Guid idempotencyKey, ProposalActivityInput input, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ProposalHistoryWriteResult> RecordCommentAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        Guid idempotencyKey, ProposalActivityInput input, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ProposalHistoryWriteResult> ChangeStatusAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        Guid idempotencyKey, ProposalOutcomeInput input, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ProposalReasonWriteResult> CreateReasonAsync(Guid organizationId, Guid actorUserId,
        Guid idempotencyKey, ProposalReasonInput input, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ProposalReasonWriteResult> DeactivateReasonAsync(Guid organizationId, Guid reasonId, Guid actorUserId,
        Guid idempotencyKey, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ProposalAuditPageDto?> GetAuditAsync(Guid organizationId, Guid requestId, int page, int pageSize,
        CancellationToken cancellationToken);
    Task<ProposalHistoryPageDto?> ReadArchiveAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        int page, int pageSize, DateTimeOffset now, CancellationToken cancellationToken);
}

public interface IProposalRetentionProcessor
{
    Task ProcessDueAsync(CancellationToken cancellationToken);
}
