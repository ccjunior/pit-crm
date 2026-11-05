using PitCrm.Domain.Demands;

namespace PitCrm.Application.Demands;

public enum DemandCreateFailure { None, CustomerNotFound, LocationNotFound, AssigneeNotEligible }

public sealed record DemandCreateResult(Demand? Demand, DemandCreateFailure Failure);
