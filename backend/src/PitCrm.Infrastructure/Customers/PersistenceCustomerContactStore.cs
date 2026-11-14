using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Customers;
using PitCrm.Domain.Customers;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Customers;

public sealed class PersistenceCustomerContactStore(PitCrmDbContext dbContext) : ICustomerContactStore
{
    public Task<bool> CustomerExistsAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(customer => customer.OrganizationId == organizationId && customer.Id == customerId, cancellationToken);

    public Task<bool> LocationExistsAsync(Guid organizationId, Guid customerId, Guid locationId, CancellationToken cancellationToken) =>
        dbContext.CustomerLocations.AnyAsync(location => location.OrganizationId == organizationId
            && location.CustomerId == customerId && location.Id == locationId, cancellationToken);

    public Task<bool> IsResponsibleForLocationAsync(Guid organizationId, Guid customerId, Guid contactId, CancellationToken cancellationToken) =>
        dbContext.CustomerLocations.AnyAsync(location => location.OrganizationId == organizationId
            && location.CustomerId == customerId && location.ResponsibleContactId == contactId, cancellationToken);

    public Task<CustomerContact?> FindAsync(Guid organizationId, Guid customerId, Guid contactId, CancellationToken cancellationToken) =>
        dbContext.CustomerContacts.SingleOrDefaultAsync(contact => contact.OrganizationId == organizationId
            && contact.CustomerId == customerId && contact.Id == contactId, cancellationToken);

    public async Task<IReadOnlyList<CustomerContactDto>> ListAsync(Guid organizationId, Guid customerId, Guid? locationId, bool includeLocationContacts,
        CancellationToken cancellationToken) =>
        await dbContext.CustomerContacts.AsNoTracking()
            .Where(contact => contact.OrganizationId == organizationId && contact.CustomerId == customerId
                && (includeLocationContacts || contact.LocationId == locationId))
            .OrderBy(contact => contact.LocationId == null ? 0 : 1).ThenBy(contact => contact.Name).ThenBy(contact => contact.Id)
            .Select(contact => new CustomerContactDto(contact.Id, contact.CustomerId, contact.LocationId, contact.Name,
                contact.Role, contact.Phone, contact.Email, contact.IsPrimary, contact.CreatedAtUtc, contact.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(CustomerContact contact, CancellationToken cancellationToken)
    {
        await using var transaction = contact.IsPrimary ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        if (contact.IsPrimary) await ResetOtherPrimaryContactsAsync(contact, cancellationToken);
        dbContext.CustomerContacts.Add(contact);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomerContact contact, CancellationToken cancellationToken)
    {
        await using var transaction = contact.IsPrimary ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        if (contact.IsPrimary) await ResetOtherPrimaryContactsAsync(contact, cancellationToken);
        _ = dbContext.Entry(contact);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(CustomerContact contact, CancellationToken cancellationToken)
    {
        dbContext.CustomerContacts.Remove(contact);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<int> ResetOtherPrimaryContactsAsync(CustomerContact contact, CancellationToken cancellationToken) =>
        dbContext.CustomerContacts
            .Where(other => other.OrganizationId == contact.OrganizationId && other.CustomerId == contact.CustomerId
                && other.LocationId == contact.LocationId && other.Id != contact.Id && other.IsPrimary)
            .ExecuteUpdateAsync(update => update
                .SetProperty(other => other.IsPrimary, false)
                .SetProperty(other => other.UpdatedByUserId, contact.UpdatedByUserId)
                .SetProperty(other => other.UpdatedAtUtc, contact.UpdatedAtUtc), cancellationToken);
}
