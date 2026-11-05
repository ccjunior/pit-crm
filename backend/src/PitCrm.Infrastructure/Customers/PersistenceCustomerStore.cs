using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Customers;
using PitCrm.Domain.Customers;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Customers;

public sealed class PersistenceCustomerStore(PitCrmDbContext dbContext) : ICustomerStore
{
    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Customer?> FindAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(
                customer => customer.OrganizationId == organizationId && customer.Id == customerId,
                cancellationToken);

    public Task<Customer?> FindForUpdateAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Customers.SingleOrDefaultAsync(
            customer => customer.OrganizationId == organizationId && customer.Id == customerId,
            cancellationToken);

    public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken)
    {
        _ = dbContext.Entry(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerSearchResult> SearchAsync(
        Guid organizationId,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var customers = dbContext.Customers.AsNoTracking()
            .Where(customer => customer.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var escapedTerm = searchTerm.Trim()
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = $"%{escapedTerm}%";
            customers = customers.Where(customer =>
                EF.Functions.ILike(customer.Name, pattern, "\\")
                || (customer.Document != null && EF.Functions.ILike(customer.Document, pattern, "\\")));
        }

        var totalCount = await customers.CountAsync(cancellationToken);
        var items = await customers
            .OrderBy(customer => customer.Name)
            .ThenBy(customer => customer.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(customer => new CustomerDto(
                customer.Id,
                customer.Name,
                customer.Document,
                customer.Instructions,
                customer.CreatedAtUtc,
                customer.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new CustomerSearchResult(items, totalCount);
    }
}
