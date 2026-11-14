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
        string? postalCode,
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
        Document = CustomerDocument.Normalize(document);
        Instructions = NormalizeOptional(instructions);
        PostalCode = NormalizePostalCode(postalCode);
        Address = NormalizeOptional(address);
        Locality = NormalizeOptional(locality);
        City = NormalizeOptional(city);
        State = NormalizeState(state);
        if (Instructions is { Length: > 2000 })
        {
            throw new ArgumentOutOfRangeException(nameof(instructions), "Customer instructions are limited to 2000 characters.");
        }
        ValidateAddressDetails(PostalCode, Address, Locality, City, State);
        CreatedAtUtc = NormalizeTimestamp(now);
        UpdatedAtUtc = NormalizeTimestamp(now);
        UpdatedByUserId = createdByUserId;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Document { get; private set; }

    public string? Instructions { get; private set; }

    public string? PostalCode { get; private set; }

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
        string? postalCode,
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

        var normalizedDocument = CustomerDocument.Normalize(document);
        var normalizedInstructions = NormalizeOptional(instructions);
        var normalizedPostalCode = NormalizePostalCode(postalCode);
        var normalizedAddress = NormalizeOptional(address);
        var normalizedLocality = NormalizeOptional(locality);
        var normalizedCity = NormalizeOptional(city);
        var normalizedState = NormalizeState(state);
        if (normalizedInstructions is { Length: > 2000 })
        {
            throw new ArgumentOutOfRangeException(nameof(instructions), "Customer instructions are limited to 2000 characters.");
        }
        ValidateAddressDetails(normalizedPostalCode, normalizedAddress, normalizedLocality, normalizedCity, normalizedState);

        Name = NormalizeRequired(name);
        Document = normalizedDocument;
        Instructions = normalizedInstructions;
        PostalCode = normalizedPostalCode;
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

    private static string? NormalizePostalCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != 8) throw new ArgumentException("Postal code must contain exactly 8 digits.", nameof(value));
        return digits;
    }

    private static string? NormalizeState(string? value) => NormalizeOptional(value)?.ToUpperInvariant();

    private static void ValidateAddressDetails(string? postalCode, string? address, string? locality, string? city, string? state)
    {
        if (postalCode is { Length: > 8 }) throw new ArgumentOutOfRangeException(nameof(postalCode), "Customer postal code is limited to 8 digits.");
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
