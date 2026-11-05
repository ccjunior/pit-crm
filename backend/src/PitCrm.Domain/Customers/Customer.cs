namespace PitCrm.Domain.Customers;

public sealed class Customer
{
    private Customer()
    {
    }

    public Customer(
        Guid id,
        Guid organizationId,
        Guid createdByUserId,
        string name,
        string? document,
        string? instructions,
        DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("Customer ID cannot be empty.", nameof(id));
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Creating user ID cannot be empty.", nameof(createdByUserId));

        Id = id;
        OrganizationId = organizationId;
        CreatedByUserId = createdByUserId;
        Name = NormalizeRequired(name);
        Document = NormalizeOptional(document);
        Instructions = NormalizeOptional(instructions);
        if (Document is { Length: > 30 })
        {
            throw new ArgumentOutOfRangeException(nameof(document), "Customer document is limited to 30 characters.");
        }
        if (Instructions is { Length: > 2000 })
        {
            throw new ArgumentOutOfRangeException(nameof(instructions), "Customer instructions are limited to 2000 characters.");
        }
        CreatedAtUtc = NormalizeTimestamp(now);
        UpdatedAtUtc = NormalizeTimestamp(now);
        UpdatedByUserId = createdByUserId;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Document { get; private set; }

    public string? Instructions { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void UpdateDetails(
        Guid updatedByUserId,
        string name,
        string? document,
        string? instructions,
        DateTimeOffset now)
    {
        if (updatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Updating user ID cannot be empty.", nameof(updatedByUserId));
        }

        var normalizedDocument = NormalizeOptional(document);
        var normalizedInstructions = NormalizeOptional(instructions);
        if (normalizedDocument is { Length: > 30 })
        {
            throw new ArgumentOutOfRangeException(nameof(document), "Customer document is limited to 30 characters.");
        }
        if (normalizedInstructions is { Length: > 2000 })
        {
            throw new ArgumentOutOfRangeException(nameof(instructions), "Customer instructions are limited to 2000 characters.");
        }

        Name = NormalizeRequired(name);
        Document = normalizedDocument;
        Instructions = normalizedInstructions;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = NormalizeTimestamp(now);
    }

    private static string NormalizeRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Customer name is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Customer name is limited to 200 characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }
}
