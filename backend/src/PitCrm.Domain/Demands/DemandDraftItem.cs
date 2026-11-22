namespace PitCrm.Domain.Demands;

public sealed class DemandDraftItem
{
    private DemandDraftItem() { }

    public DemandDraftItem(Guid id, Guid organizationId, Guid demandDraftId, Guid sourceProposalItemId,
        int position, string description, decimal amount)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || demandDraftId == Guid.Empty || sourceProposalItemId == Guid.Empty)
            throw new ArgumentException("Demand draft item identity cannot be empty.");
        if (position < 1) throw new ArgumentOutOfRangeException(nameof(position));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Id = id;
        OrganizationId = organizationId;
        DemandDraftId = demandDraftId;
        SourceProposalItemId = sourceProposalItemId;
        Position = position;
        Description = Required(description, nameof(description), 1000);
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DemandDraftId { get; private set; }
    public Guid SourceProposalItemId { get; private set; }
    public int Position { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }

    private static string Required(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A required value is missing.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
