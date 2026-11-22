using PitCrm.Domain.Demands;

namespace PitCrm.Application.Demands;

public enum DemandUpdateFailure
{
    None,
    DemandNotFound,
    VersionConflict,
    CustomerNotFound,
    LocationNotFound,
    AssigneeNotEligible,
    InvalidSubitemSource
}

public sealed record DemandUpdateData(int Version, Guid CustomerId, Guid LocationId, string Description,
    string Origin, string? Priority, IReadOnlyCollection<(string Type, string Value)>? ExternalReferences,
    Guid? AssignedToUserId, IReadOnlyCollection<(string Description, decimal? Amount, Guid? SourceProposalItemId)>? Subitems = null);

public sealed record DemandUpdateResult(Demand? Demand, DemandUpdateFailure Failure);
