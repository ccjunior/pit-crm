using PitCrm.Domain.Demands;

namespace PitCrm.Application.Demands;

public interface IDemandStore
{
    Task<DemandCreateResult> CreateAsync(Demand demand, DemandActivity initialActivity, CancellationToken cancellationToken);

    Task<DemandUpdateResult> UpdateAsync(Guid organizationId, Guid demandId, Guid updatedByUserId,
        DemandUpdateData update, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken);

    Task<DemandSearchResult> SearchAsync(Guid organizationId, string? searchTerm, Guid? customerId,
        Guid? locationId, Guid? assignedToUserId, string? status, string? priority, string sortBy,
        int page, int pageSize, CancellationToken cancellationToken);

    Task<DemandDetailsDto?> FindAsync(Guid organizationId, Guid demandId, CancellationToken cancellationToken);
}
