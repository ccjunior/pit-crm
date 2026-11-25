namespace PitCrm.Domain.Proposals;

public sealed class CommercialResponsibleRequiredException : InvalidOperationException
{
    public CommercialResponsibleRequiredException()
        : base("A commercial responsible is required before evaluation.")
    {
    }
}

public sealed class CommercialRequest
{
    private CommercialRequest() { }

    public CommercialRequest(Guid id, Guid organizationId, Guid createdByUserId, string number, Guid? customerId,
        string? prospectName, string? prospectContactName, string? prospectEmail, string? prospectPhone,
        Guid? responsibleUserId, string origin, string description, DateTimeOffset now)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Commercial request identity cannot be empty.");
        if (customerId is null && string.IsNullOrWhiteSpace(prospectName))
            throw new ArgumentException("An existing customer or prospect name is required.", nameof(prospectName));
        if (!Origins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(origin));

        Id = id;
        OrganizationId = organizationId;
        Number = Required(number, nameof(number), 24);
        CreatedByUserId = createdByUserId;
        CustomerId = customerId;
        ResponsibleUserId = responsibleUserId;
        ProspectName = Optional(prospectName, nameof(prospectName), 200);
        ProspectContactName = Optional(prospectContactName, nameof(prospectContactName), 200);
        ProspectEmail = Optional(prospectEmail, nameof(prospectEmail), 254);
        ProspectPhone = Optional(prospectPhone, nameof(prospectPhone), 40);
        Origin = Origins.Single(value => string.Equals(value, origin.Trim(), StringComparison.OrdinalIgnoreCase));
        Description = Required(description, nameof(description), 4000);
        Status = Received;
        Version = 1;
        CreatedAtUtc = NormalizeTimestamp(now);
        UpdatedAtUtc = CreatedAtUtc;
    }

    public const string Received = "Received";
    public const string Evaluating = "Evaluating";
    public const string Drafting = "Drafting";
    public const string Sent = "Sent";
    public const string Negotiating = "Negotiating";
    public const string Approved = "Approved";
    public const string NotQuoted = "NotQuoted";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
    public const string ManualSource = "Manual";
    public const string EmailSource = "Email";

    public static readonly string[] Statuses = [Received, Evaluating, Drafting, Sent, Negotiating,
        Approved, NotQuoted, Rejected, Expired, Cancelled];
    public static readonly string[] Origins = ["E-mail", "Telefone", "WhatsApp", "Indicação", "Visita", "Licitação", "Outro"];
    public static readonly string[] ReferenceTypes = ["RFQ/cotação", "Chamado/OS", "Pedido de compra", "Outra"];
    public static readonly string[] ResultStatuses = [NotQuoted, Rejected, Expired, Cancelled];

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid? CustomerId { get; private set; }
    public Guid? ResponsibleUserId { get; private set; }
    public string? ProspectName { get; private set; }
    public string? ProspectContactName { get; private set; }
    public string? ProspectEmail { get; private set; }
    public string? ProspectPhone { get; private set; }
    public string Origin { get; private set; } = string.Empty;
    public string SourceType { get; private set; } = ManualSource;
    public string Description { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? OutcomeReason { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateOnly? CustomerDeadline { get; private set; }
    public DateOnly? InternalDeadline { get; private set; }
    public string? NextActionDescription { get; private set; }
    public DateTimeOffset? NextActionAtUtc { get; private set; }
    public Guid? ReminderOccurrenceId { get; private set; }
    public int ReminderOccurrenceRevision { get; private set; }
    public ICollection<ProposalRevision> Revisions { get; private set; } = new List<ProposalRevision>();
    public ICollection<CommercialReference> References { get; private set; } = new List<CommercialReference>();

    public ProposalRevision StartRevision(Guid revisionId, Guid authorUserId, IEnumerable<(string Description, decimal Amount)> items,
        DateOnly? validUntil, string? notes, DateTimeOffset now, bool demandDraftConverted = false)
    {
        if (Status is not (Evaluating or Sent or Negotiating or Approved))
            throw new InvalidOperationException("A proposal revision cannot be created from the current state.");
        if (Status == Approved && demandDraftConverted)
            throw new InvalidOperationException("A confirmed demand must be changed through an addendum.");

        var previous = Revisions.OrderByDescending(item => item.Number).FirstOrDefault();
        var revision = new ProposalRevision(revisionId, OrganizationId, Id, (previous?.Number ?? 0) + 1,
            authorUserId, items, validUntil, notes, now);
        Revisions.Add(revision);
        TransitionTo(Drafting, null, now);
        return revision;
    }

    public void ChangeStatus(string nextStatus, string? reason, DateTimeOffset now, bool demandDraftConverted = false)
    {
        if (!Statuses.Contains(nextStatus)) throw new ArgumentOutOfRangeException(nameof(nextStatus));
        if (!CanTransition(Status, nextStatus, demandDraftConverted))
            throw new InvalidOperationException("The commercial request cannot transition to the requested state.");
        if (nextStatus == NotQuoted && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required when deciding not to quote.", nameof(reason));
        if (nextStatus == Evaluating && ResponsibleUserId is null)
            throw new CommercialResponsibleRequiredException();

        TransitionTo(nextStatus, reason, now);
    }

    public void RecordSent(Guid revisionId, string channel, DateTimeOffset sentAtUtc, DateTimeOffset now)
    {
        if (Status != Drafting) throw new InvalidOperationException("Only a proposal in drafting can be sent.");
        var revision = Revisions.SingleOrDefault(item => item.Id == revisionId)
            ?? throw new ArgumentException("The proposal revision does not belong to this request.", nameof(revisionId));
        revision.MarkSent(channel, sentAtUtc);
        TransitionTo(Sent, null, now);
    }

    public void RecordApproval(Guid revisionId, Guid recordedByUserId, string approverName,
        string channel, string? notes, DateTimeOffset approvedAtUtc, DateTimeOffset now)
    {
        if (Status is not (Sent or Negotiating))
            throw new InvalidOperationException("Only a sent proposal can be approved.");
        var revision = Revisions.SingleOrDefault(item => item.Id == revisionId)
            ?? throw new ArgumentException("The proposal revision does not belong to this request.", nameof(revisionId));
        revision.MarkApproved(recordedByUserId, approverName, channel, notes, approvedAtUtc);
        TransitionTo(Approved, null, now);
    }

    public void SetCustomer(Guid customerId)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identity cannot be empty.", nameof(customerId));
        CustomerId = customerId;
        ProspectName = null;
        ProspectContactName = null;
        ProspectEmail = null;
        ProspectPhone = null;
    }

    public void UpdateDetails(Guid? customerId, string? prospectName, string? prospectContactName,
        string? prospectEmail, string? prospectPhone, Guid? responsibleUserId, string origin, string description, DateTimeOffset now)
    {
        if (Status is not (Received or Evaluating or Drafting))
            throw new InvalidOperationException("The commercial request can no longer be edited.");
        if (customerId is null && string.IsNullOrWhiteSpace(prospectName))
            throw new ArgumentException("An existing customer or prospect name is required.", nameof(prospectName));
        if (!Origins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(origin));
        if (Status == Evaluating && responsibleUserId is null)
            throw new InvalidOperationException("A commercial responsible is required while the request is under evaluation.");
        CustomerId = customerId;
        ResponsibleUserId = responsibleUserId;
        ProspectName = customerId is null ? Optional(prospectName, nameof(prospectName), 200) : null;
        ProspectContactName = customerId is null ? Optional(prospectContactName, nameof(prospectContactName), 200) : null;
        ProspectEmail = customerId is null ? Optional(prospectEmail, nameof(prospectEmail), 254) : null;
        ProspectPhone = customerId is null ? Optional(prospectPhone, nameof(prospectPhone), 40) : null;
        Origin = Origins.Single(value => string.Equals(value, origin.Trim(), StringComparison.OrdinalIgnoreCase));
        Description = Required(description, nameof(description), 4000);
        UpdatedAtUtc = NormalizeTimestamp(now);
        Version = checked(Version + 1);
    }

    public void SetReferences(IEnumerable<(string Type, string Value)> references)
    {
        References.Clear();
        foreach (var (type, value) in references)
            References.Add(new CommercialReference(Guid.NewGuid(), OrganizationId, Id, type, value));
    }

    public void UpdateTracking(DateOnly? customerDeadline, DateOnly? internalDeadline,
        string? nextActionDescription, DateTimeOffset? nextActionAtUtc, DateTimeOffset now)
    {
        var hasAction = nextActionDescription is not null || nextActionAtUtc is not null;
        if (hasAction && (string.IsNullOrWhiteSpace(nextActionDescription) || nextActionAtUtc is null))
            throw new ArgumentException("A next action requires both a description and a scheduled time.");
        if (nextActionDescription?.Trim().Length > 500)
            throw new ArgumentOutOfRangeException(nameof(nextActionDescription));

        var actionChanged = NextActionDescription != nextActionDescription?.Trim()
            || NextActionAtUtc != nextActionAtUtc?.ToUniversalTime();
        var deadlineChanged = CustomerDeadline != customerDeadline || InternalDeadline != internalDeadline;
        if (!actionChanged && !deadlineChanged) return;
        CustomerDeadline = customerDeadline;
        InternalDeadline = internalDeadline;
        if (actionChanged)
        {
            ReminderOccurrenceRevision = checked(ReminderOccurrenceRevision + 1);
            if (hasAction)
            {
                ReminderOccurrenceId ??= Guid.NewGuid();
                NextActionDescription = nextActionDescription!.Trim();
                NextActionAtUtc = nextActionAtUtc!.Value.ToUniversalTime();
            }
            else
            {
                ReminderOccurrenceId = null;
                NextActionDescription = null;
                NextActionAtUtc = null;
            }
        }
        UpdatedAtUtc = NormalizeTimestamp(now);
        Version = checked(Version + 1);
    }

    private void TransitionTo(string nextStatus, string? reason, DateTimeOffset now)
    {
        Status = nextStatus;
        OutcomeReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        UpdatedAtUtc = NormalizeTimestamp(now);
        Version = checked(Version + 1);
    }

    private static bool CanTransition(string current, string next, bool demandDraftConverted)
    {
        if (next == Cancelled)
            return current is not (NotQuoted or Rejected or Expired or Cancelled)
                && !(current == Approved && demandDraftConverted);
        return current switch
        {
            Received => next == Evaluating,
            Evaluating => next is Drafting or NotQuoted,
            Drafting => next == Cancelled,
            Sent => next is Negotiating or Approved or Rejected or Expired or Drafting,
            Negotiating => next is Drafting or Approved or Rejected or Expired,
            Approved => next == Drafting && !demandDraftConverted,
            _ => false,
        };
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
