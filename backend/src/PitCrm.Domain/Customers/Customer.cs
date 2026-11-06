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
        string? address,
        string? locality,
        string? city,
        string? state,
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
        Address = NormalizeOptional(address);
        Locality = NormalizeOptional(locality);
        City = NormalizeOptional(city);
        State = NormalizeOptional(state);
        if (Document is { Length: > 30 })
        {
            throw new ArgumentOutOfRangeException(nameof(document), "Customer document is limited to 30 characters.");
        }
        if (Instructions is { Length: > 2000 })
        {
            throw new ArgumentOutOfRangeException(nameof(instructions), "Customer instructions are limited to 2000 characters.");
        }
        ValidateAddressDetails(Address, Locality, City, State);
        CreatedAtUtc = NormalizeTimestamp(now);
        UpdatedAtUtc = NormalizeTimestamp(now);
        UpdatedByUserId = createdByUserId;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Document { get; private set; }

    public string? Instructions { get; private set; }

    public string? Address { get; private set; }

    public string? Locality { get; private set; }

    public string? City { get; private set; }

    public string? State { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void UpdateDetails(
        Guid updatedByUserId,
        string name,
        string? document,
        string? instructions,
        string? address,
        string? locality,
        string? city,
        string? state,
        DateTimeOffset now)
    {
        if (updatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Updating user ID cannot be empty.", nameof(updatedByUserId));
        }

        var normalizedDocument = NormalizeOptional(document);
        var normalizedInstructions = NormalizeOptional(instructions);
        var normalizedAddress = NormalizeOptional(address);
        var normalizedLocality = NormalizeOptional(locality);
        var normalizedCity = NormalizeOptional(city);
        var normalizedState = NormalizeOptional(state);
        if (normalizedDocument is { Length: > 30 })
        {
            throw new ArgumentOutOfRangeException(nameof(document), "Customer document is limited to 30 characters.");
        }
        if (normalizedInstructions is { Length: > 2000 })
        {
            throw new ArgumentOutOfRangeException(nameof(instructions), "Customer instructions are limited to 2000 characters.");
        }
        ValidateAddressDetails(normalizedAddress, normalizedLocality, normalizedCity, normalizedState);

        Name = NormalizeRequired(name);
        Document = normalizedDocument;
        Instructions = normalizedInstructions;
        Address = normalizedAddress;
        Locality = normalizedLocality;
        City = normalizedCity;
        State = normalizedState;
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

    private static void ValidateAddressDetails(string? address, string? locality, string? city, string? state)
    {
        if (address is { Length: > 500 }) throw new ArgumentOutOfRangeException(nameof(address), "Customer address is limited to 500 characters.");
        if (locality is { Length: > 200 }) throw new ArgumentOutOfRangeException(nameof(locality), "Customer locality is limited to 200 characters.");
        if (city is { Length: > 200 }) throw new ArgumentOutOfRangeException(nameof(city), "Customer city is limited to 200 characters.");
        if (state is { Length: > 200 }) throw new ArgumentOutOfRangeException(nameof(state), "Customer state is limited to 200 characters.");
    }

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }
}
