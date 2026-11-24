namespace PitCrm.Domain.Proposals;

public sealed class CommercialReference
{
    private CommercialReference() { }

    public CommercialReference(Guid id, Guid organizationId, Guid requestId, string type, string value)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || requestId == Guid.Empty)
            throw new ArgumentException("Commercial reference identity cannot be empty.");
        Id = id;
        OrganizationId = organizationId;
        CommercialRequestId = requestId;
        Type = Required(type, nameof(type), 60);
        Value = Required(value, nameof(value), 120);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CommercialRequestId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    private static string Required(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
            throw new ArgumentException("A valid reference value is required.", parameterName);
        return value.Trim();
    }
}
