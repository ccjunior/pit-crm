namespace PitCrm.Domain.Proposals;

public sealed class ProposalRevision
{
    private ProposalRevision() { }

    public ProposalRevision(Guid id, Guid organizationId, Guid requestId, int number, Guid createdByUserId,
        IEnumerable<(string Description, decimal Amount)> items, DateOnly? validUntil, string? notes, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || requestId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Proposal revision identity cannot be empty.");
        if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
        var values = items.ToArray();
        if (values.Length == 0) throw new ArgumentException("At least one proposal item is required.", nameof(items));
        if (values.Any(item => item.Amount < 0)) throw new ArgumentOutOfRangeException(nameof(items));

        Id = id;
        OrganizationId = organizationId;
        CommercialRequestId = requestId;
        Number = number;
        CreatedByUserId = createdByUserId;
        ValidUntil = validUntil;
        Notes = Optional(notes, nameof(notes), 4000);
        CreatedAtUtc = NormalizeTimestamp(createdAtUtc);
        Status = Draft;
        Items = values.Select((item, index) => new ProposalItem(Guid.NewGuid(), organizationId, id,
            index + 1, item.Description, item.Amount)).ToList();
    }

    public const string Draft = "Draft";
    public const string Sent = "Sent";
    public const string Approved = "Approved";

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CommercialRequestId { get; private set; }
    public int Number { get; private set; }
    public string Status { get; private set; } = Draft;
    public DateOnly? ValidUntil { get; private set; }
    public string? Notes { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public string? SendChannel { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public string? ClientApproverName { get; private set; }
    public string? ApprovalChannel { get; private set; }
    public string? ApprovalNotes { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public ICollection<ProposalItem> Items { get; private set; } = new List<ProposalItem>();

    public decimal TotalAmount => Items.Sum(item => item.Amount);

    public void MarkSent(string channel, DateTimeOffset sentAtUtc)
    {
        if (Status != Draft) throw new InvalidOperationException("Only a draft revision can be sent.");
        Status = Sent;
        SendChannel = Required(channel, nameof(channel), 50);
        SentAtUtc = NormalizeTimestamp(sentAtUtc);
    }

    public void MarkApproved(Guid recordedByUserId, string approverName, string channel, string? notes,
        DateTimeOffset approvedAtUtc)
    {
        if (Status != Sent) throw new InvalidOperationException("Only a sent revision can be approved.");
        if (recordedByUserId == Guid.Empty) throw new ArgumentException("Approving user is required.", nameof(recordedByUserId));
        Status = Approved;
        ApprovedByUserId = recordedByUserId;
        ClientApproverName = Required(approverName, nameof(approverName), 200);
        ApprovalChannel = Required(channel, nameof(channel), 50);
        ApprovalNotes = Optional(notes, nameof(notes), 2000);
        ApprovedAtUtc = NormalizeTimestamp(approvedAtUtc);
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
