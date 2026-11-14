using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Locations;
using PitCrm.Domain.Locations;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Locations;

public sealed class PersistenceCustomerLocationStore(PitCrmDbContext dbContext) : ICustomerLocationStore
{
    public Task<bool> CustomerExistsAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(customer => customer.OrganizationId == organizationId && customer.Id == customerId, cancellationToken);

    public Task<bool> ResponsibleContactExistsAsync(Guid organizationId, Guid customerId, Guid? locationId, Guid contactId,
        CancellationToken cancellationToken) =>
        dbContext.CustomerContacts.AnyAsync(contact => contact.OrganizationId == organizationId && contact.CustomerId == customerId
            && contact.Id == contactId && (contact.LocationId == null || (locationId != null && contact.LocationId == locationId)), cancellationToken);

    public Task<CustomerLocation?> FindAsync(Guid organizationId, Guid customerId, Guid locationId, CancellationToken cancellationToken) =>
        dbContext.CustomerLocations.SingleOrDefaultAsync(location => location.OrganizationId == organizationId
            && location.CustomerId == customerId && location.Id == locationId, cancellationToken);

    public async Task<IReadOnlyList<CustomerLocationDto>> ListAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken) =>
        await dbContext.CustomerLocations.AsNoTracking()
            .Where(location => location.OrganizationId == organizationId && location.CustomerId == customerId)
            .OrderBy(location => location.Name).ThenBy(location => location.Id)
            .Select(location => new CustomerLocationDto(location.Id, location.CustomerId, location.Name, location.Address,
                location.Neighborhood, location.City, location.Status.ToString(), location.Instructions, location.MapUrl, location.ResponsibleContactId,
                dbContext.CustomerContacts.Where(contact => contact.OrganizationId == location.OrganizationId
                    && contact.CustomerId == location.CustomerId && contact.Id == location.ResponsibleContactId).Select(contact => contact.Name).FirstOrDefault(),
                dbContext.CustomerContacts.Where(contact => contact.OrganizationId == location.OrganizationId
                    && contact.CustomerId == location.CustomerId && contact.Id == location.ResponsibleContactId).Select(contact => contact.Role).FirstOrDefault(),
                dbContext.CustomerContacts.Where(contact => contact.OrganizationId == location.OrganizationId
                    && contact.CustomerId == location.CustomerId && contact.Id == location.ResponsibleContactId).Select(contact => contact.Phone).FirstOrDefault(),
                location.CreatedAtUtc, location.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(CustomerLocation location, CancellationToken cancellationToken)
    {
        dbContext.CustomerLocations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomerLocation location, CancellationToken cancellationToken)
    {
        _ = dbContext.Entry(location);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
