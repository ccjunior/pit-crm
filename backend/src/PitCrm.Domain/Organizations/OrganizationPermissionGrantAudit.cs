namespace PitCrm.Domain.Organizations;

public sealed class OrganizationPermissionGrantAudit
{
    private OrganizationPermissionGrantAudit() { }

    public OrganizationPermissionGrantAudit(Guid organizationId, Guid actorUserId, Guid targetUserId,
        string permission, string action, DateTimeOffset occurredAtUtc, string justification)
    {
        if (organizationId == Guid.Empty || actorUserId == Guid.Empty || targetUserId == Guid.Empty
            || actorUserId == targetUserId)
            throw new ArgumentException("Permission grant audit scope is invalid.");
        if (permission is not ("ViewProposalAudit" or "RestoreProposalTimeline"))
            throw new ArgumentOutOfRangeException(nameof(permission));
        if (action is not ("Granted" or "Revoked"))
            throw new ArgumentOutOfRangeException(nameof(action));
        if (string.IsNullOrWhiteSpace(justification) || justification.Trim().Length > 500)
            throw new ArgumentOutOfRangeException(nameof(justification));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ActorUserId = actorUserId;
        TargetUserId = targetUserId;
        Permission = permission;
        Action = action;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        Justification = justification.Trim();
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid TargetUserId { get; private set; }
    public string Permission { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string Justification { get; private set; } = string.Empty;
}
