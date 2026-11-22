using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Demands;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Demands;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Demands;

public sealed class PersistenceDemandDraftStore(PitCrmDbContext dbContext) : IDemandDraftStore
{
    public async Task<Guid> CreateFromApprovedProposalAsync(ProposalDemandDraftSeed seed, CancellationToken cancellationToken)
    {
        var draft = await dbContext.DemandDrafts.Include(value => value.Items)
            .SingleOrDefaultAsync(value => value.OrganizationId == seed.OrganizationId
                && value.ProposalRequestId == seed.RequestId, cancellationToken);
        if (draft is not null)
        {
            if (draft.State == DemandDraft.Converted)
            {
                if (draft.ProposalRevisionId != seed.RevisionId)
                    throw new InvalidOperationException("A confirmed demand cannot be replaced by another proposal revision.");
                return draft.Id;
            }
            var priorItems = draft.Items.ToArray();
            draft.UpdateFromApprovedProposal(seed.RevisionId, seed.Description, seed.Origin,
                seed.Items.Select(item => (item.SourceItemId, item.Description, item.Amount)), seed.CreatedAtUtc);
            dbContext.DemandDraftItems.RemoveRange(priorItems);
            dbContext.DemandDraftItems.AddRange(draft.Items);
            await dbContext.SaveChangesAsync(cancellationToken);
            return draft.Id;
        }

        var created = new DemandDraft(Guid.NewGuid(), seed.OrganizationId, seed.RequestId, seed.RevisionId,
            seed.CreatedByUserId, seed.CustomerId, seed.ProspectName, seed.Description, seed.Origin,
            seed.Items.Select(item => (item.SourceItemId, item.Description, item.Amount)), seed.CreatedAtUtc);
        dbContext.DemandDrafts.Add(created);
        await dbContext.SaveChangesAsync(cancellationToken);
        return created.Id;
    }

    public async Task<DemandDraftPageDto> SearchAsync(Guid organizationId, string? searchTerm, int page,
        int pageSize, CancellationToken cancellationToken)
    {
        var drafts = dbContext.DemandDrafts.AsNoTracking()
            .Where(draft => draft.OrganizationId == organizationId && draft.State == DemandDraft.Pending);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            var escaped = term.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = $"%{escaped}%";
            drafts = drafts.Where(draft => EF.Functions.ILike(draft.Description, pattern, "\\")
                || (draft.ProspectName != null && EF.Functions.ILike(draft.ProspectName, pattern, "\\"))
                || dbContext.Customers.Any(customer => customer.OrganizationId == organizationId
                    && customer.Id == draft.CustomerId && EF.Functions.ILike(customer.Name, pattern, "\\")));
        }

        var totalCount = await drafts.CountAsync(cancellationToken);
        var items = await (
            from draft in drafts
            join customer in dbContext.Customers.AsNoTracking()
                on new { draft.OrganizationId, draft.CustomerId } equals new { customer.OrganizationId, CustomerId = (Guid?)customer.Id } into customers
            from customer in customers.DefaultIfEmpty()
            orderby draft.CreatedAtUtc, draft.Id
            select new DemandDraftListItemDto(draft.Id, draft.CustomerId,
                customer == null ? null : customer.Name, draft.ProspectName, draft.Description,
                draft.Origin, draft.Version, draft.CreatedAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new DemandDraftPageDto(items, page, pageSize, totalCount);
    }

    public async Task<DemandDraftDetailsDto?> FindAsync(Guid organizationId, Guid draftId, CancellationToken cancellationToken)
    {
        var draft = await dbContext.DemandDrafts.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.Id == draftId && value.State == DemandDraft.Pending)
            .Select(value => new
            {
                value.Id, value.ProposalRequestId, value.ProposalRevisionId, value.CustomerId, value.LocationId,
                value.ProspectName, value.Description, value.Origin, value.Priority, value.AssignedToUserId,
                value.Version, value.CreatedAtUtc
            }).SingleOrDefaultAsync(cancellationToken);
        if (draft is null) return null;

        var customerName = draft.CustomerId is { } customerId
            ? await dbContext.Customers.AsNoTracking().Where(value => value.OrganizationId == organizationId && value.Id == customerId)
                .Select(value => value.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        var locationName = draft.LocationId is { } locationId
            ? await dbContext.CustomerLocations.AsNoTracking().Where(value => value.OrganizationId == organizationId && value.Id == locationId)
                .Select(value => value.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        var assignedName = draft.AssignedToUserId is { } assignedTo
            ? await dbContext.Users.AsNoTracking().Where(value => value.OrganizationId == organizationId && value.Id == assignedTo)
                .Select(value => value.DisplayName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var items = await dbContext.DemandDraftItems.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.DemandDraftId == draftId)
            .OrderBy(value => value.Position)
            .Select(value => new DemandDraftItemDto(value.Id, value.SourceProposalItemId,
                value.Position, value.Description, value.Amount)).ToListAsync(cancellationToken);
        return new DemandDraftDetailsDto(draft.Id, draft.ProposalRequestId, draft.ProposalRevisionId,
            draft.CustomerId, customerName, draft.LocationId, locationName, draft.ProspectName,
            draft.Description, draft.Origin, draft.Priority, draft.AssignedToUserId, assignedName,
            draft.Version, items, draft.CreatedAtUtc);
    }

    public async Task<DemandDraftCommandResult> UpdateAsync(Guid organizationId, Guid draftId, Guid actorUserId,
        UpdateDemandDraftInput input, bool mayManageCustomers, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var draft = await dbContext.DemandDrafts.Include(value => value.Items)
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId && value.Id == draftId, cancellationToken);
        if (draft is null) return new DemandDraftCommandResult(null, DemandDraftFailure.NotFound);
        if (draft.State != DemandDraft.Pending) return new DemandDraftCommandResult(null, DemandDraftFailure.AlreadyConverted);
        if (draft.Version != input.Version) return new DemandDraftCommandResult(null, DemandDraftFailure.VersionConflict);
        if (draft.CustomerId != input.CustomerId && !mayManageCustomers)
            return new DemandDraftCommandResult(null, DemandDraftFailure.NotAllowed);
        if (input.CustomerId is { } customerId && !await dbContext.Customers.AnyAsync(value =>
                value.OrganizationId == organizationId && value.Id == customerId, cancellationToken))
            return new DemandDraftCommandResult(null, DemandDraftFailure.CustomerNotFound);
        if (input.LocationId is { } locationId
            && (input.CustomerId is not { } linkedCustomerId || !await dbContext.CustomerLocations.AnyAsync(value =>
                value.OrganizationId == organizationId && value.CustomerId == linkedCustomerId
                    && value.Id == locationId, cancellationToken)))
            return new DemandDraftCommandResult(null, DemandDraftFailure.LocationNotFound);
        if (input.AssignedToUserId is { } assigneeId && !await dbContext.Users.AnyAsync(value =>
                value.OrganizationId == organizationId && value.Id == assigneeId && value.IsActive, cancellationToken))
            return new DemandDraftCommandResult(null, DemandDraftFailure.AssigneeNotEligible);
        if (string.IsNullOrWhiteSpace(input.Description) || string.IsNullOrWhiteSpace(input.Origin)
            || !Demand.Origins.Contains(input.Origin.Trim(), StringComparer.OrdinalIgnoreCase)
            || (input.Priority is not null && !Demand.Priorities.Contains(input.Priority.Trim(), StringComparer.OrdinalIgnoreCase)))
            return new DemandDraftCommandResult(null, DemandDraftFailure.InvalidData);

        var priorItems = draft.Items.ToArray();
        draft.Update(input.CustomerId, input.LocationId, input.Description, input.Origin, input.Priority,
            input.AssignedToUserId, draft.Items.OrderBy(item => item.Position)
                .Select(item => (item.SourceProposalItemId, item.Description, item.Amount)), now);
        dbContext.DemandDraftItems.RemoveRange(priorItems);
        dbContext.DemandDraftItems.AddRange(draft.Items);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new DemandDraftCommandResult(null, DemandDraftFailure.VersionConflict);
        }
        return new DemandDraftCommandResult(await FindAsync(organizationId, draftId, cancellationToken), DemandDraftFailure.None);
    }

    public async Task<DemandDraftCommandResult> ConvertAsync(Guid organizationId, Guid draftId, Guid actorUserId,
        int version, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var draft = await dbContext.DemandDrafts.Include(value => value.Items)
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId && value.Id == draftId, cancellationToken);
        if (draft is null) return new DemandDraftCommandResult(null, DemandDraftFailure.NotFound);
        if (draft.State == DemandDraft.Converted)
            return new DemandDraftCommandResult(null, DemandDraftFailure.None, draft.ConvertedDemandId);
        if (draft.Version != version) return new DemandDraftCommandResult(null, DemandDraftFailure.VersionConflict);
        if (draft.CustomerId is not { } customerId) return new DemandDraftCommandResult(null, DemandDraftFailure.CustomerNotFound);
        if (draft.LocationId is not { } locationId
            || !await dbContext.CustomerLocations.AnyAsync(value => value.OrganizationId == organizationId
                && value.CustomerId == customerId && value.Id == locationId, cancellationToken))
            return new DemandDraftCommandResult(null, DemandDraftFailure.LocationNotFound);
        if (!await dbContext.Customers.AnyAsync(value => value.OrganizationId == organizationId && value.Id == customerId, cancellationToken))
            return new DemandDraftCommandResult(null, DemandDraftFailure.CustomerNotFound);
        if (draft.AssignedToUserId is { } assigneeId && !await dbContext.Users.AnyAsync(value =>
                value.OrganizationId == organizationId && value.Id == assigneeId && value.IsActive, cancellationToken))
            return new DemandDraftCommandResult(null, DemandDraftFailure.AssigneeNotEligible);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "DemandNumberCounters" ("OrganizationId", "LastIssuedNumber")
            VALUES ({organizationId}, 1)
            ON CONFLICT ("OrganizationId") DO UPDATE
            SET "LastIssuedNumber" = "DemandNumberCounters"."LastIssuedNumber" + 1;
            """, cancellationToken);
        var referenceNumber = await dbContext.Database.SqlQuery<int>($"""
            SELECT "LastIssuedNumber" AS "Value"
            FROM "DemandNumberCounters"
            WHERE "OrganizationId" = {organizationId}
            """).SingleAsync(cancellationToken);
        var demand = new Demand(Guid.NewGuid(), organizationId, customerId, locationId, actorUserId,
            draft.AssignedToUserId, draft.Description, draft.Origin, draft.Priority, null, now,
            draft.ProposalRevisionId, draft.Items.OrderBy(item => item.Position)
                .Select(item => (item.Description, (decimal?)item.Amount, (Guid?)item.SourceProposalItemId)));
        demand.AssignReferenceNumber(referenceNumber);
        dbContext.Demands.Add(demand);
        dbContext.DemandActivities.Add(new DemandActivity(Guid.NewGuid(), organizationId, demand.Id,
            actorUserId, "DemandCreatedFromProposal", "Demanda confirmada a partir de proposta aprovada.", now));
        draft.MarkConverted(demand.Id, now);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new DemandDraftCommandResult(null, DemandDraftFailure.VersionConflict);
        }
        catch (DbUpdateException)
        {
            return new DemandDraftCommandResult(null, DemandDraftFailure.AlreadyConverted);
        }
        return new DemandDraftCommandResult(null, DemandDraftFailure.None, demand.Id);
    }
}
