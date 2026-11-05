namespace PitCrm.Domain.Demands;

public sealed class DemandActivity
{
    private DemandActivity() { }

    public DemandActivity(Guid id, Guid organizationId, Guid demandId, Guid authorUserId,
        string eventType, string description, DateTimeOffset occurredAtUtc)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || demandId == Guid.Empty || authorUserId == Guid.Empty)
            throw new ArgumentException("Activity identity and references cannot be empty.");
        Id = id;
        OrganizationId = organizationId;
        DemandId = demandId;
        AuthorUserId = authorUserId;
        EventType = eventType;
        Description = description;
        var utc = occurredAtUtc.ToUniversalTime();
        OccurredAtUtc = new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DemandId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
