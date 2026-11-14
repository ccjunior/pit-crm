using PitCrm.Domain.Customers;

namespace PitCrm.Application.Customers;

public interface ICustomerContactStore
{
    Task<bool> CustomerExistsAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken);
    Task<bool> LocationExistsAsync(Guid organizationId, Guid customerId, Guid locationId, CancellationToken cancellationToken);
    Task<bool> IsResponsibleForLocationAsync(Guid organizationId, Guid customerId, Guid contactId, CancellationToken cancellationToken);
    Task<CustomerContact?> FindAsync(Guid organizationId, Guid customerId, Guid contactId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerContactDto>> ListAsync(Guid organizationId, Guid customerId, Guid? locationId, bool includeLocationContacts, CancellationToken cancellationToken);
    Task AddAsync(CustomerContact contact, CancellationToken cancellationToken);
    Task UpdateAsync(CustomerContact contact, CancellationToken cancellationToken);
    Task DeleteAsync(CustomerContact contact, CancellationToken cancellationToken);
}
