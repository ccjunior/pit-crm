using PitCrm.Domain.Proposals;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PitCrm.Infrastructure.Persistence;
using PitCrm.Infrastructure.Identity;
using System.Text.Json;
using PitCrm.Api.Features.Proposals;
using PitCrm.Api.Authorization;
using PitCrm.Application.Authorization;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Organizations;
using PitCrm.Infrastructure.Proposals;

namespace PitCrm.Api.Tests;

public sealed class ProposalHistoryDomainTests
{
    private static readonly Guid Key = Guid.Parse("018bcfe5-6800-7000-8000-000000000000");

    [Fact]
    public void Idempotency_key_extracts_uuid_v7_timestamp_and_enforces_window_boundaries()
    {
        var issuedAt = DateTimeOffset.Parse("2023-11-14T22:13:20Z");

        Assert.True(ProposalIdempotencyKey.IsVersion7(Key));
        Assert.Equal(issuedAt, ProposalIdempotencyKey.GetTimestampUtc(Key));
        Assert.True(ProposalIdempotencyKey.IsWithinValidityWindow(Key, issuedAt));
        Assert.True(ProposalIdempotencyKey.IsWithinValidityWindow(Key, issuedAt.AddDays(30).AddTicks(-1)));
        Assert.False(ProposalIdempotencyKey.IsWithinValidityWindow(Key, issuedAt.AddDays(30)));
        Assert.True(ProposalIdempotencyKey.IsWithinValidityWindow(Key, issuedAt.AddDays(29)));
        Assert.True(ProposalIdempotencyKey.IsWithinValidityWindow(Key, issuedAt.AddMinutes(-5)));
        Assert.False(ProposalIdempotencyKey.IsWithinValidityWindow(Key, issuedAt.AddMinutes(-5).AddTicks(-1)));
        Assert.False(ProposalIdempotencyKey.IsWithinValidityWindow(Guid.NewGuid(), issuedAt));
        Assert.False(ProposalIdempotencyKey.IsWithinValidityWindow(
            Guid.Parse("ffffffff-ffff-7fff-8000-000000000000"), issuedAt));
        Assert.False(ProposalIdempotencyKey.IsVersion7(Guid.Parse("018bcfe5-6800-7000-7000-000000000000")));
    }

    [Fact]
    public void Retention_cutoff_uses_local_civil_months_and_clamps_month_end()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var local = new DateTime(2024, 2, 29, 9, 30, 0, DateTimeKind.Unspecified);
        var source = new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();

        var cutoff = ProposalHistoryRetentionRules.AddCivilMonthsUtc(source, 24);
        var cutoffLocal = TimeZoneInfo.ConvertTime(cutoff, zone);

        Assert.Equal(new DateOnly(2026, 2, 28), DateOnly.FromDateTime(cutoffLocal.DateTime));
        Assert.Equal(new TimeOnly(9, 30), TimeOnly.FromDateTime(cutoffLocal.DateTime));
    }

    [Fact]
    public void Audit_retention_is_applied_at_each_event_cutoff()
    {
        var occurredAt = DateTimeOffset.Parse("2025-11-25T15:00:00Z");
        var cutoff = ProposalHistoryRetentionRules.AddCivilMonthsUtc(occurredAt, 12);

        Assert.False(ProposalHistoryRetentionRules.IsExpired(occurredAt, cutoff.AddTicks(-1), 12));
        Assert.True(ProposalHistoryRetentionRules.IsExpired(occurredAt, cutoff, 12));
    }

    [Fact]
    public void Retention_cycle_archiving_requires_latest_generation_and_terminal_proposal()
    {
        Assert.True(ProposalHistoryRetentionRules.CanArchive(3, 3, isTerminal: true));
        Assert.False(ProposalHistoryRetentionRules.CanArchive(2, 3, isTerminal: true));
        Assert.False(ProposalHistoryRetentionRules.CanArchive(3, 3, isTerminal: false));
    }

    [Fact]
    public void Timeline_event_requires_exactly_one_valid_actor_and_stage_pair()
    {
        var now = DateTimeOffset.Parse("2026-11-25T12:00:00Z");

        Assert.Throws<ArgumentException>(() => new ProposalTimelineEvent(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, ProposalHistoryEventTypes.ActivityRecorded, ProposalHistoryActorTypes.Human,
            null, null, now));
        Assert.Throws<ArgumentException>(() => new ProposalTimelineEvent(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, ProposalHistoryEventTypes.StageChanged, ProposalHistoryActorTypes.Human,
            Guid.NewGuid(), null, now, fromStatus: CommercialRequest.Received));
        Assert.Throws<ArgumentException>(() => new ProposalTimelineEvent(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, ProposalHistoryEventTypes.ActivityRecorded, ProposalHistoryActorTypes.System,
            null, null, now));
    }

    [Fact]
    public void History_model_keeps_tenant_scoped_keys_and_restrictive_proposal_links()
    {
        var options = new DbContextOptionsBuilder<PitCrmDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=not_used;Username=not_used")
            .Options;
        using var dbContext = new PitCrmDbContext(options);

        var timeline = dbContext.Model.FindEntityType(typeof(ProposalTimelineEvent))!;
        var proposalLink = Assert.Single(timeline.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(CommercialRequest));
        var idempotency = dbContext.Model.FindEntityType(typeof(ProposalIdempotencyRecord))!;
        var grant = dbContext.Model.FindEntityType(typeof(OrganizationPermissionGrant))!;

        Assert.Equal(DeleteBehavior.Restrict, proposalLink.DeleteBehavior);
        Assert.Contains(proposalLink.Properties, property => property.Name == "OrganizationId");
        Assert.Equal(new[] { "OrganizationId", "Key" },
            idempotency.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("jsonb", idempotency.FindProperty(nameof(ProposalIdempotencyRecord.ResultPayloadJson))!
            .GetColumnType());
        Assert.Equal(new[] { "OrganizationId", "UserId", "Permission" },
            grant.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Contains(grant.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationUser)
            && foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "OrganizationId", "UserId" }));
    }

    [Fact]
    public void Audit_redaction_removes_actor_and_target_identifiers()
    {
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var audit = new ProposalAuditEvent(Guid.NewGuid(), orgId, "StageChanged", "Transições",
            ProposalHistoryActorTypes.Human, actorId, null, DateTimeOffset.UtcNow, requestId, eventId,
            "{\"reasonId\":\"private\"}");

        audit.Redact(DateTimeOffset.UtcNow);

        Assert.Null(audit.DetailsJson);
        Assert.Null(audit.ActorUserId);
        Assert.Null(audit.CommercialRequestId);
        Assert.Null(audit.TimelineEventId);
        Assert.Equal("Redacted", audit.ActorType);
        Assert.NotNull(audit.RedactedAtUtc);
    }

    [Fact]
    public void Retention_cycle_can_archive_and_restore_without_losing_generation()
    {
        var now = DateTimeOffset.Parse("2026-11-25T12:00:00Z");
        var cycle = new ProposalRetentionCycle(Guid.NewGuid(), Guid.NewGuid(), 3, now, now.AddYears(2), true);

        cycle.Archive(now.AddYears(2), now.AddYears(3));
        Assert.Equal("Archived", cycle.State);
        cycle.Restore();

        Assert.Equal("Restored", cycle.State);
        Assert.Equal(3, cycle.Generation);
    }

    [Fact]
    public async Task History_routes_are_mapped_with_explicit_policy_metadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentOrganizationContext, HttpCurrentOrganizationContext>();
        builder.Services.AddScoped<IProposalHistoryStore, PersistenceProposalHistoryStore>();
        builder.Services.AddScoped<IProposalStore, PersistenceProposalStore>();
        await using var app = builder.Build();
        ProposalHistoryEndpoints.Map(app);

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        var operatorTimeline = Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == "/api/demands/{id:guid}/proposal-timeline");
        var audit = Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == "/api/proposals/{id:guid}/audit");

        Assert.Contains(operatorTimeline.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            metadata => metadata.Policy == AuthorizationPolicies.OperateDemands);
        Assert.Contains(audit.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            metadata => metadata.Policy == AuthorizationPolicies.ViewProposalAudit);
    }

    [Fact]
    public async Task Audit_grant_authorization_defaults_to_denied_and_uses_context_scope()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lookup = new StubGrantLookup(false);
        var handler = new OrganizationPermissionGrantHandler(new StubOrganizationContext(orgId, userId), lookup,
            new HttpContextAccessor());
        var requirement = new OrganizationPermissionGrantRequirement(AuthorizationPolicies.ViewProposalAuditGrant);
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString())], "test"));
        var denied = new AuthorizationHandlerContext([requirement], principal, null);

        await handler.HandleAsync(denied);

        Assert.False(denied.HasSucceeded);
        Assert.Equal((orgId, userId, AuthorizationPolicies.ViewProposalAuditGrant),
            (lookup.OrganizationId, lookup.UserId, lookup.Permission));

        var allowedLookup = new StubGrantLookup(true);
        var allowedHandler = new OrganizationPermissionGrantHandler(
            new StubOrganizationContext(orgId, userId), allowedLookup, new HttpContextAccessor());
        var allowed = new AuthorizationHandlerContext([requirement], principal, null);
        await allowedHandler.HandleAsync(allowed);
        Assert.True(allowed.HasSucceeded);
    }

    [Fact]
    public void Idempotency_record_preserves_the_original_response_snapshot()
    {
        var result = new ProposalHistoryEventDto(Guid.NewGuid(), 4, ProposalHistoryEventTypes.ActivityRecorded,
            DateTimeOffset.Parse("2026-11-25T12:00:00Z"), true, "Owner", null, null, null, "Call",
            "Follow-up", []);
        var record = new ProposalIdempotencyRecord(Guid.NewGuid(), "Proposal", Guid.NewGuid(), Key,
            "ActivityRecorded", new string('A', 64), result.Id, ProposalIdempotencyKey.GetTimestampUtc(Key),
            DateTimeOffset.UtcNow);

        record.StoreResultPayload(JsonSerializer.Serialize(result));

        var restored = JsonSerializer.Deserialize<ProposalHistoryEventDto>(record.ResultPayloadJson!);
        Assert.NotNull(restored);
        Assert.Equal(result.Id, restored.Id);
        Assert.Equal(result.Text, restored.Text);
        Assert.Equal(result.ActorName, restored.ActorName);
    }

    private sealed class StubOrganizationContext(Guid organizationId, Guid userId) : ICurrentOrganizationContext
    {
        public Guid OrganizationId { get; } = organizationId;
        public Guid UserId { get; } = userId;
    }

    private sealed class StubGrantLookup(bool hasGrant) : IOrganizationPermissionGrantLookup
    {
        public Guid OrganizationId { get; private set; }
        public Guid UserId { get; private set; }
        public string Permission { get; private set; } = string.Empty;

        public Task<bool> HasGrantAsync(Guid organizationId, Guid userId, string permission,
            CancellationToken cancellationToken)
        {
            OrganizationId = organizationId;
            UserId = userId;
            Permission = permission;
            return Task.FromResult(hasGrant);
        }
    }
}
