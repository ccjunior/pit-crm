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
        string? city,
        string sortBy,
        bool descending,
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
            var documentDigits = CustomerDocument.DigitsForSearch(searchTerm);
            var documentPattern = $"%{documentDigits}%";
            customers = customers.Where(customer =>
                EF.Functions.ILike(customer.Name, pattern, "\\")
                || (documentDigits.Length > 0 && customer.Document != null
                    && EF.Functions.ILike(customer.Document.Replace(".", "").Replace("-", "").Replace("/", "").Replace(" ", ""), documentPattern, "\\")));
        }
        if (!string.IsNullOrWhiteSpace(city))
        {
            var escapedCity = city.Trim().Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
            customers = customers.Where(customer => customer.City != null && EF.Functions.ILike(customer.City, $"%{escapedCity}%", "\\"));
        }

        var totalCount = await customers.CountAsync(cancellationToken);
        customers = (sortBy, descending) switch
        {
            ("city", true) => customers.OrderByDescending(customer => customer.City).ThenBy(customer => customer.Name),
            ("city", false) => customers.OrderBy(customer => customer.City).ThenBy(customer => customer.Name),
            (_, true) => customers.OrderByDescending(customer => customer.Name).ThenBy(customer => customer.Id),
            _ => customers.OrderBy(customer => customer.Name).ThenBy(customer => customer.Id),
        };
        var items = await customers
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(customer => new CustomerDto(
                customer.Id,
                customer.Name,
                customer.Document,
                customer.Instructions,
                customer.PostalCode,
                customer.Address,
                customer.Locality,
                customer.City,
                customer.State,
                0, 0, 0, 0, null, null,
                customer.CreatedAtUtc,
                customer.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        if (items.Count == 0) return new CustomerSearchResult(items, totalCount);

        var customerIds = items.Select(customer => customer.Id).ToArray();
        var locationCounts = await dbContext.CustomerLocations.AsNoTracking()
            .Where(location => location.OrganizationId == organizationId && customerIds.Contains(location.CustomerId))
            .GroupBy(location => location.CustomerId)
            .Select(group => new { CustomerId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CustomerId, item => item.Count, cancellationToken);
        var activeLocationCounts = await dbContext.CustomerLocations.AsNoTracking()
            .Where(location => location.OrganizationId == organizationId && customerIds.Contains(location.CustomerId)
                && location.Status == PitCrm.Domain.Locations.CustomerLocationStatus.Active)
            .GroupBy(location => location.CustomerId)
            .Select(group => new { CustomerId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CustomerId, item => item.Count, cancellationToken);
        var contactCounts = await dbContext.CustomerContacts.AsNoTracking()
            .Where(contact => contact.OrganizationId == organizationId && customerIds.Contains(contact.CustomerId))
            .GroupBy(contact => contact.CustomerId)
            .Select(group => new { CustomerId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CustomerId, item => item.Count, cancellationToken);
        var receivedDemandCounts = await dbContext.Demands.AsNoTracking()
            .Where(demand => demand.OrganizationId == organizationId && customerIds.Contains(demand.CustomerId)
                && demand.Status == "Recebida")
            .GroupBy(demand => demand.CustomerId)
            .Select(group => new { CustomerId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CustomerId, item => item.Count, cancellationToken);
        var primaryContacts = await dbContext.CustomerContacts.AsNoTracking()
            .Where(contact => contact.OrganizationId == organizationId && customerIds.Contains(contact.CustomerId)
                && contact.LocationId == null && contact.IsPrimary)
            .ToDictionaryAsync(contact => contact.CustomerId,
                contact => new { contact.Name, contact.Phone }, cancellationToken);

        items = items.Select(customer =>
        {
            primaryContacts.TryGetValue(customer.Id, out var primary);
            return customer with
            {
                LocationCount = locationCounts.GetValueOrDefault(customer.Id),
                ActiveLocationCount = activeLocationCounts.GetValueOrDefault(customer.Id),
                ContactCount = contactCounts.GetValueOrDefault(customer.Id),
                ReceivedDemandCount = receivedDemandCounts.GetValueOrDefault(customer.Id),
                PrimaryContactName = primary?.Name,
                PrimaryContactPhone = primary?.Phone,
            };
        }).ToList();

        return new CustomerSearchResult(items, totalCount);
    }
}
