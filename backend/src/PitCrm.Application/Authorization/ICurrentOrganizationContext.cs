namespace PitCrm.Application.Authorization;

public interface ICurrentOrganizationContext
{
    Guid UserId { get; }

    Guid OrganizationId { get; }
}
