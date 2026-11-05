namespace PitCrm.Domain.Locations;

public sealed class CustomerLocation
{
    private CustomerLocation() { }

    public CustomerLocation(Guid id, Guid organizationId, Guid customerId, Guid createdByUserId,
        string name, string address, string? instructions, DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("Location ID cannot be empty.", nameof(id));
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Creating user ID cannot be empty.", nameof(createdByUserId));
        Id = id;
        OrganizationId = organizationId;
        CustomerId = customerId;
        CreatedByUserId = createdByUserId;
        UpdatedByUserId = createdByUserId;
        Name = NormalizeRequired(name, nameof(name), 200);
        Address = NormalizeRequired(address, nameof(address), 500);
        Instructions = NormalizeOptional(instructions, 2000);
        CreatedAtUtc = NormalizeTimestamp(now);
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string? Instructions { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void UpdateDetails(Guid updatedByUserId, string name, string address, string? instructions, DateTimeOffset now)
    {
        if (updatedByUserId == Guid.Empty) throw new ArgumentException("Updating user ID cannot be empty.", nameof(updatedByUserId));
        Name = NormalizeRequired(name, nameof(name), 200);
        Address = NormalizeRequired(address, nameof(address), 500);
        Instructions = NormalizeOptional(instructions, 2000);
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = NormalizeTimestamp(now);
    }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A required value is missing.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName, $"Value is limited to {maxLength} characters.");
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(nameof(value), $"Value is limited to {maxLength} characters.");
        return normalized;
    }

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }
}
