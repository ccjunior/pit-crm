using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Proposals;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Proposals;

public sealed class PersistenceProposalAuditAggregateStore(PitCrmDbContext dbContext)
    : IProposalAuditAggregateStore
{
    public async Task<ProposalAuditAggregateMonthDto> GetPublishedMonthAsync(Guid organizationId,
        DateOnly monthStart, CancellationToken cancellationToken)
    {
        var isSealed = await dbContext.ProposalAuditAggregateMonths.AsNoTracking().AnyAsync(value =>
            value.OrganizationId == organizationId && value.MonthStart == monthStart
            && value.State == ProposalAuditAggregateWindowStates.Sealed, cancellationToken);
        if (!isSealed)
            return new ProposalAuditAggregateMonthDto(monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture), []);

        var counts = await dbContext.ProposalAuditAggregateWindows.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.MonthStart == monthStart
                && value.State == ProposalAuditAggregateWindowStates.Sealed)
            .Select(value => new { value.Category, value.DistinctRequestCount })
            .ToDictionaryAsync(value => value.Category, value => value.DistinctRequestCount,
                StringComparer.Ordinal, cancellationToken);

        var publicCounts = ProposalAuditAggregatePublicationRules.FilterPublicCounts(counts);

        var cells = ProposalAuditAggregateCategories.All
            .Where(publicCounts.ContainsKey)
            .Select(category => new ProposalAuditAggregateCellDto(category, publicCounts[category]))
            .ToArray();
        return new ProposalAuditAggregateMonthDto(monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture), cells);
    }
}
