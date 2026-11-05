namespace PitCrm.Domain.Demands;

public sealed class Demand
{
    private Demand() { }

    public Demand(Guid id, Guid organizationId, Guid customerId, Guid locationId, Guid createdByUserId,
        Guid? assignedToUserId, string description, string origin, string? priority, string? ticketReference,
        string? purchaseOrderReference, DateTimeOffset now)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || customerId == Guid.Empty || locationId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Demand identity and required references cannot be empty.");
        Id = id;
        OrganizationId = organizationId;
        CustomerId = customerId;
        LocationId = locationId;
        CreatedByUserId = createdByUserId;
        AssignedToUserId = assignedToUserId;
        Description = Required(description, nameof(description), 4000);
        if (!Origins.Contains(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        if (priority is not null && !Priorities.Contains(priority)) throw new ArgumentOutOfRangeException(nameof(priority));
        Origin = origin;
        Priority = priority;
        TicketReference = Optional(ticketReference, nameof(ticketReference), 100);
        PurchaseOrderReference = Optional(purchaseOrderReference, nameof(purchaseOrderReference), 100);
        Status = "Recebida";
        CreatedAtUtc = NormalizeTimestamp(now);
    }

    public static readonly string[] Origins = ["WhatsApp", "Ligação", "E-mail", "Outra"];
    public static readonly string[] Priorities = ["Baixa", "Média", "Alta"];
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid LocationId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string Origin { get; private set; } = string.Empty;
    public string? Priority { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? TicketReference { get; private set; }
    public string? PurchaseOrderReference { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

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

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }
}
