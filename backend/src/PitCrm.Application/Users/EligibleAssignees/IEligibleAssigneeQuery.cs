namespace PitCrm.Application.Users.EligibleAssignees;

public interface IEligibleAssigneeQuery
{
    Task<IReadOnlyList<EligibleAssignee>> ExecuteAsync(Guid organizationId, CancellationToken cancellationToken);
}
