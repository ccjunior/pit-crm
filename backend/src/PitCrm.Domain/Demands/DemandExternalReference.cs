namespace PitCrm.Domain.Demands;

public sealed class DemandExternalReference
{
    private DemandExternalReference() { }

    public DemandExternalReference(Guid id, Guid organizationId, Guid demandId, string type, string value)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || demandId == Guid.Empty)
            throw new ArgumentException("External reference identity cannot be empty.");
        Id = id;
        OrganizationId = organizationId;
        DemandId = demandId;
        Type = Required(type, nameof(type), 50);
        Value = Required(value, nameof(value), 100);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DemandId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    private static string Required(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A required value is missing.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
