namespace PitCrm.Domain.Proposals;

public sealed class CommercialActivity
{
    private CommercialActivity() { }

    public CommercialActivity(Guid id, Guid organizationId, Guid requestId, Guid actorUserId,
        string eventType, string description, DateTimeOffset occurredAtUtc)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || requestId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Commercial activity identity cannot be empty.");
        Id = id;
        OrganizationId = organizationId;
        CommercialRequestId = requestId;
        ActorUserId = actorUserId;
        EventType = Required(eventType, nameof(eventType), 60);
        Description = Required(description, nameof(description), 1000);
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CommercialRequestId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }

    private static string Required(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A required value is missing.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
