namespace PitCrm.Domain.Demands;

public sealed class Demand
{
    private Demand() { }

    public Demand(Guid id, Guid organizationId, Guid customerId, Guid locationId, Guid createdByUserId,
        Guid? assignedToUserId, string description, string origin, string? priority,
        IEnumerable<(string Type, string Value)>? externalReferences, DateTimeOffset now,
        Guid? sourceProposalRevisionId = null, IEnumerable<(string Description, decimal? Amount, Guid? SourceProposalItemId)>? subitems = null)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || customerId == Guid.Empty || locationId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Demand identity and required references cannot be empty.");
        Id = id;
        OrganizationId = organizationId;
        CustomerId = customerId;
        LocationId = locationId;
        CreatedByUserId = createdByUserId;
        AssignedToUserId = assignedToUserId;
        SourceProposalRevisionId = sourceProposalRevisionId;
        Description = Required(description, nameof(description), 4000);
        if (!Origins.Contains(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        if (priority is not null && !Priorities.Contains(priority)) throw new ArgumentOutOfRangeException(nameof(priority));
        Origin = origin;
        Priority = priority;
        ExternalReferences = CreateExternalReferences(externalReferences);
        Subitems = CreateSubitems(subitems);
        Status = "Recebida";
        Version = 1;
        CreatedAtUtc = NormalizeTimestamp(now);
    }

    public static readonly string[] Origins = ["WhatsApp", "Ligação", "E-mail", "Outra"];
    public static readonly string[] Priorities = ["Baixa", "Média", "Alta"];
    public static readonly string[] Statuses = ["Recebida"];
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid LocationId { get; private set; }
    public int ReferenceNumber { get; private set; }
    public int Version { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string Origin { get; private set; } = string.Empty;
    public string? Priority { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public ICollection<DemandExternalReference> ExternalReferences { get; private set; } = new List<DemandExternalReference>();
    public ICollection<DemandSubitem> Subitems { get; private set; } = new List<DemandSubitem>();
    public Guid? SourceProposalRevisionId { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void AssignReferenceNumber(int referenceNumber)
    {
        if (referenceNumber < 1 || ReferenceNumber != 0)
            throw new InvalidOperationException("A demand reference number must be assigned once and be positive.");
        ReferenceNumber = referenceNumber;
    }

    public void UpdateDetails(Guid customerId, Guid locationId, Guid? assignedToUserId, string description,
        string origin, string? priority, IEnumerable<(string Type, string Value)>? externalReferences,
        IEnumerable<(string Description, decimal? Amount, Guid? SourceProposalItemId)>? subitems = null)
    {
        if (customerId == Guid.Empty || locationId == Guid.Empty)
            throw new ArgumentException("Demand customer and location cannot be empty.");

        Description = Required(description, nameof(description), 4000);
        if (!Origins.Contains(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        if (priority is not null && !Priorities.Contains(priority)) throw new ArgumentOutOfRangeException(nameof(priority));

        CustomerId = customerId;
        LocationId = locationId;
        AssignedToUserId = assignedToUserId;
        Origin = origin;
        Priority = priority;
        if (externalReferences is not null)
        {
            ExternalReferences.Clear();
            foreach (var reference in CreateExternalReferences(externalReferences)) ExternalReferences.Add(reference);
        }
        if (subitems is not null)
        {
            Subitems.Clear();
            foreach (var subitem in CreateSubitems(subitems)) Subitems.Add(subitem);
        }
        Version = checked(Version + 1);
    }

    private static string Required(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A required value is missing.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static string? Optional(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Required(value, parameterName, maxLength);
    }

    private List<DemandExternalReference> CreateExternalReferences(IEnumerable<(string Type, string Value)>? references) =>
        references?.Select(reference => new DemandExternalReference(Guid.NewGuid(), OrganizationId, Id,
            reference.Type, reference.Value)).ToList() ?? [];

    private List<DemandSubitem> CreateSubitems(IEnumerable<(string Description, decimal? Amount, Guid? SourceProposalItemId)>? subitems) =>
        subitems?.Select((item, index) => new DemandSubitem(Guid.NewGuid(), OrganizationId, Id,
            index + 1, item.Description, item.Amount, item.SourceProposalItemId)).ToList() ?? [];

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }
}
