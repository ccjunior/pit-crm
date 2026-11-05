using PitCrm.Domain.Customers;

namespace PitCrm.Application.Customers;

public interface ICustomerStore
{
    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    Task<Customer?> FindAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken);

    Task<Customer?> FindForUpdateAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken);

    Task UpdateAsync(Customer customer, CancellationToken cancellationToken);

    Task<CustomerSearchResult> SearchAsync(
        Guid organizationId,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
