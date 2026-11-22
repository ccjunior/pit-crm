namespace PitCrm.Domain.Demands;

public sealed class DemandSubitem
{
    private DemandSubitem() { }

    public DemandSubitem(Guid id, Guid organizationId, Guid demandId, int position,
        string description, decimal? amount, Guid? sourceProposalItemId = null)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || demandId == Guid.Empty)
            throw new ArgumentException("Demand subitem identity cannot be empty.");
        if (position < 1) throw new ArgumentOutOfRangeException(nameof(position));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (sourceProposalItemId == Guid.Empty) throw new ArgumentException("Source proposal item identity cannot be empty.", nameof(sourceProposalItemId));
        Id = id;
        OrganizationId = organizationId;
        DemandId = demandId;
        Position = position;
        Description = Required(description, nameof(description), 1000);
        Amount = amount is null ? null : decimal.Round(amount.Value, 2, MidpointRounding.AwayFromZero);
        SourceProposalItemId = sourceProposalItemId;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DemandId { get; private set; }
    public int Position { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal? Amount { get; private set; }
    public Guid? SourceProposalItemId { get; private set; }

    private static string Required(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A required value is missing.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
