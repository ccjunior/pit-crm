using PitCrm.Domain.Locations;

namespace PitCrm.Application.Locations;

public interface ICustomerLocationStore
{
    Task<bool> CustomerExistsAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken);
    Task<CustomerLocation?> FindAsync(Guid organizationId, Guid customerId, Guid locationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerLocationDto>> ListAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken);
    Task AddAsync(CustomerLocation location, CancellationToken cancellationToken);
    Task UpdateAsync(CustomerLocation location, CancellationToken cancellationToken);
}
