namespace PitCrm.Domain.Demands;

public sealed class DemandDraft
{
    private DemandDraft() { }

    public DemandDraft(Guid id, Guid organizationId, Guid proposalRequestId, Guid proposalRevisionId,
        Guid createdByUserId, Guid? customerId, string? prospectName, string description, string origin,
        IEnumerable<(Guid SourceItemId, string Description, decimal Amount)> items, DateTimeOffset now)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || proposalRequestId == Guid.Empty
            || proposalRevisionId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Demand draft identity cannot be empty.");
        if (customerId is null && string.IsNullOrWhiteSpace(prospectName))
            throw new ArgumentException("A prospect name is required when no customer is linked.", nameof(prospectName));
        if (!Demand.Origins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(origin));

        Id = id;
        OrganizationId = organizationId;
        ProposalRequestId = proposalRequestId;
        ProposalRevisionId = proposalRevisionId;
        CreatedByUserId = createdByUserId;
        CustomerId = customerId;
        ProspectName = Optional(prospectName, nameof(prospectName), 200);
        Description = Required(description, nameof(description), 4000);
        Origin = Demand.Origins.Single(value => string.Equals(value, origin.Trim(), StringComparison.OrdinalIgnoreCase));
        State = Pending;
        Version = 1;
        CreatedAtUtc = NormalizeTimestamp(now);
        UpdatedAtUtc = CreatedAtUtc;
        ReplaceItems(items);
    }

    public const string Pending = "Pending";
    public const string Converted = "Converted";

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProposalRequestId { get; private set; }
    public Guid ProposalRevisionId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Guid? LocationId { get; private set; }
    public string? ProspectName { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string Origin { get; private set; } = string.Empty;
    public string? Priority { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public string State { get; private set; } = Pending;
    public Guid CreatedByUserId { get; private set; }
    public int Version { get; private set; }
    public Guid? ConvertedDemandId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public ICollection<DemandDraftItem> Items { get; private set; } = new List<DemandDraftItem>();

    public void Update(Guid? customerId, Guid? locationId, string description, string origin, string? priority,
        Guid? assignedToUserId, IEnumerable<(Guid SourceItemId, string Description, decimal Amount)> items,
        DateTimeOffset now)
    {
        if (State != Pending) throw new InvalidOperationException("Only a pending demand draft can be edited.");
        if (customerId is null && string.IsNullOrWhiteSpace(ProspectName))
            throw new ArgumentException("A customer or prospect is required.", nameof(customerId));
        if (!Demand.Origins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(origin));
        if (priority is not null && !Demand.Priorities.Contains(priority, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(priority));

        CustomerId = customerId;
        LocationId = locationId;
        if (customerId is not null) ProspectName = null;
        Description = Required(description, nameof(description), 4000);
        Origin = Demand.Origins.Single(value => string.Equals(value, origin.Trim(), StringComparison.OrdinalIgnoreCase));
        Priority = priority is null ? null
            : Demand.Priorities.Single(value => string.Equals(value, priority.Trim(), StringComparison.OrdinalIgnoreCase));
        AssignedToUserId = assignedToUserId;
        ReplaceItems(items);
        Version = checked(Version + 1);
        UpdatedAtUtc = NormalizeTimestamp(now);
    }

    public void UpdateFromApprovedProposal(Guid proposalRevisionId, string description, string origin,
        IEnumerable<(Guid SourceItemId, string Description, decimal Amount)> items, DateTimeOffset now)
    {
        if (State != Pending) throw new InvalidOperationException("A converted demand cannot be changed from a proposal revision.");
        if (proposalRevisionId == Guid.Empty) throw new ArgumentException("Proposal revision identity cannot be empty.", nameof(proposalRevisionId));
        if (!Demand.Origins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(origin));
        ProposalRevisionId = proposalRevisionId;
        Description = Required(description, nameof(description), 4000);
        Origin = Demand.Origins.Single(value => string.Equals(value, origin.Trim(), StringComparison.OrdinalIgnoreCase));
        ReplaceItems(items);
        Version = checked(Version + 1);
        UpdatedAtUtc = NormalizeTimestamp(now);
    }

    public void MarkConverted(Guid demandId, DateTimeOffset now)
    {
        if (State != Pending) throw new InvalidOperationException("Only a pending demand draft can be converted.");
        if (demandId == Guid.Empty) throw new ArgumentException("Demand identity cannot be empty.", nameof(demandId));
        State = Converted;
        ConvertedDemandId = demandId;
        Version = checked(Version + 1);
        UpdatedAtUtc = NormalizeTimestamp(now);
    }

    private void ReplaceItems(IEnumerable<(Guid SourceItemId, string Description, decimal Amount)> items)
    {
        var values = items.ToArray();
        Items = values.Select((item, index) => new DemandDraftItem(Guid.NewGuid(), OrganizationId, Id,
            item.SourceItemId, index + 1, item.Description, item.Amount)).ToList();
    }

    private static string Required(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A required value is missing.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static string? Optional(string? value, string parameterName, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, parameterName, maxLength);

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }
}
