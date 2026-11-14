namespace PitCrm.Domain.Customers;

public sealed class CustomerContact
{
    private CustomerContact() { }

    public CustomerContact(Guid id, Guid organizationId, Guid customerId, Guid? locationId, Guid createdByUserId,
        string name, string? role, string? phone, string? email, bool isPrimary, DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("Contact ID cannot be empty.", nameof(id));
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));
        if (locationId == Guid.Empty) throw new ArgumentException("Location ID cannot be empty.", nameof(locationId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Creating user ID cannot be empty.", nameof(createdByUserId));
        Id = id;
        OrganizationId = organizationId;
        CustomerId = customerId;
        LocationId = locationId;
        CreatedByUserId = UpdatedByUserId = createdByUserId;
        Name = Required(name, nameof(name), 200);
        Role = Optional(role, nameof(role), 200);
        Phone = Optional(phone, nameof(phone), 40);
        Email = Optional(email, nameof(email), 254);
        IsPrimary = isPrimary;
        CreatedAtUtc = UpdatedAtUtc = Utc(now);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? LocationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Role { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsPrimary { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(Guid nameChangedByUserId, string name, string? role, string? phone, string? email, bool isPrimary, DateTimeOffset now)
    {
        if (nameChangedByUserId == Guid.Empty) throw new ArgumentException("Updating user ID cannot be empty.", nameof(nameChangedByUserId));
        Name = Required(name, nameof(name), 200);
        Role = Optional(role, nameof(role), 200);
        Phone = Optional(phone, nameof(phone), 40);
        Email = Optional(email, nameof(email), 254);
        IsPrimary = isPrimary;
        UpdatedByUserId = nameChangedByUserId;
        UpdatedAtUtc = Utc(now);
    }

    private static string Required(string value, string parameter, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A required value is missing.", parameter);
        var result = value.Trim();
        if (result.Length > max) throw new ArgumentOutOfRangeException(parameter);
        return result;
    }

    private static string? Optional(string? value, string parameter, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = value.Trim();
        if (result.Length > max) throw new ArgumentOutOfRangeException(parameter);
        return result;
    }

    private static DateTimeOffset Utc(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }
}
