using PitCrm.Domain.Proposals;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using PitCrm.Infrastructure.Persistence;
using PitCrm.Infrastructure.Identity;
using System.Text.Json;
using PitCrm.Api.Features.Proposals;
using PitCrm.Api.Features.Organizations;
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
    public void First_unexpired_audit_instant_matches_per_event_civil_cutoff_at_database_precision()
    {
        var now = DateTimeOffset.Parse("2026-11-26T15:00:00.123456Z");
        var firstUnexpired = ProposalHistoryRetentionRules.FirstUnexpiredOccurrenceUtc(now, 12);

        Assert.Equal(TimeSpan.Zero, firstUnexpired.Offset);
        Assert.True(ProposalHistoryRetentionRules.IsExpired(firstUnexpired.AddTicks(-10), now, 12));
        Assert.False(ProposalHistoryRetentionRules.IsExpired(firstUnexpired, now, 12));
    }

    [Fact]
    public void Aggregate_window_matures_after_the_last_event_cutoff_in_its_local_month()
    {
        var month = new DateOnly(2025, 11, 1);
        var range = ProposalHistoryRetentionRules.AuditAggregateMonthUtcRange(month);
        var lastPossibleEvent = range.EndUtc.AddTicks(-10);
        var maturity = ProposalHistoryRetentionRules.AuditAggregateMonthMaturesAtUtc(month);

        Assert.True(maturity >= ProposalHistoryRetentionRules.AddCivilMonthsUtc(lastPossibleEvent, 12));
    }

    [Fact]
    public void Aggregate_publication_applies_minimum_and_complementary_suppression()
    {
        var suppressedPair = ProposalAuditAggregatePublicationRules.FilterPublicCounts(
            new Dictionary<string, long>
            {
                [ProposalAuditAggregateCategories.Transitions] = 30,
                [ProposalAuditAggregateCategories.Catalog] = 22,
                [ProposalAuditAggregateCategories.ActivityAndComments] = 10
            });
        Assert.DoesNotContain(ProposalAuditAggregateCategories.Transitions, suppressedPair.Keys);
        Assert.DoesNotContain(ProposalAuditAggregateCategories.Catalog, suppressedPair.Keys);
        Assert.Equal(10, suppressedPair[ProposalAuditAggregateCategories.ActivityAndComments]);

        var separatedPair = ProposalAuditAggregatePublicationRules.FilterPublicCounts(
            new Dictionary<string, long>
            {
                [ProposalAuditAggregateCategories.Transitions] = 40,
                [ProposalAuditAggregateCategories.Catalog] = 22,
                [ProposalAuditAggregateCategories.Retention] = 9
            });
        Assert.Equal(40, separatedPair[ProposalAuditAggregateCategories.Transitions]);
        Assert.Equal(22, separatedPair[ProposalAuditAggregateCategories.Catalog]);
        Assert.DoesNotContain(ProposalAuditAggregateCategories.Retention, separatedPair.Keys);
    }

    [Fact]
    public void Aggregate_token_is_deterministic_domain_separated_and_fail_closed_without_secret()
    {
        var key = Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuditAggregation:HmacKey"] = key
        }).Build();
        var tokens = new AuditAggregateTokenFactory(configuration);
        var organizationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var month = new DateOnly(2025, 11, 1);
        var token = tokens.Create(organizationId, month, ProposalAuditAggregateCategories.Transitions, requestId);

        Assert.Equal(64, token.Length);
        Assert.Equal(token, tokens.Create(organizationId, month, ProposalAuditAggregateCategories.Transitions, requestId));
        Assert.NotEqual(token, tokens.Create(Guid.NewGuid(), month, ProposalAuditAggregateCategories.Transitions, requestId));
        Assert.NotEqual(token, tokens.Create(organizationId, month.AddMonths(1), ProposalAuditAggregateCategories.Transitions, requestId));
        Assert.NotEqual(token, tokens.Create(organizationId, month, ProposalAuditAggregateCategories.Catalog, requestId));
        Assert.DoesNotContain(requestId.ToString("N"), token, StringComparison.OrdinalIgnoreCase);

        var missingSecret = new AuditAggregateTokenFactory(new ConfigurationBuilder().Build());
        Assert.Throws<InvalidOperationException>(() => missingSecret.Create(organizationId, month,
            ProposalAuditAggregateCategories.Transitions, requestId));
    }

    [Fact]
    public void Aggregate_classifier_uses_fixed_categories_and_requires_structured_catalog_reason()
    {
        var organizationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.Parse("2025-11-10T12:00:00Z");
        var transition = new ProposalAuditEvent(Guid.NewGuid(), organizationId, "StageChanged", "Transições",
            ProposalHistoryActorTypes.Human, actorId, null, occurredAt, requestId);
        var catalog = new ProposalAuditEvent(Guid.NewGuid(), organizationId, "StageChanged", "Catálogo",
            ProposalHistoryActorTypes.Human, actorId, null, occurredAt, requestId, detailsJson:
            "{\"reasonId\":\"" + Guid.NewGuid() + "\",\"appliedToStatus\":\"NotQuoted\"}");
        var invalidCatalog = new ProposalAuditEvent(Guid.NewGuid(), organizationId, "StageChanged", "Catálogo",
            ProposalHistoryActorTypes.Human, actorId, null, occurredAt, requestId, detailsJson: "{\"reasonId\":null}");
        var archiveRead = new ProposalAuditEvent(Guid.NewGuid(), organizationId, "ArchiveRead", "Consultas ao arquivo",
            ProposalHistoryActorTypes.Human, actorId, null, occurredAt, requestId);
        var retention = new ProposalAuditEvent(Guid.NewGuid(), organizationId, "TimelineArchived", "Retenção",
            ProposalHistoryActorTypes.System, null, "ProposalRetention", occurredAt, requestId);
        var unlinked = new ProposalAuditEvent(Guid.NewGuid(), organizationId, "StageChanged", "Transições",
            ProposalHistoryActorTypes.System, null, "ProposalRetention", occurredAt);

        Assert.True(ProposalAuditAggregateEventClassifier.TryGetCategory(transition, out var transitionCategory));
        Assert.Equal(ProposalAuditAggregateCategories.Transitions, transitionCategory);
        Assert.True(ProposalAuditAggregateEventClassifier.TryGetCategory(catalog, out var catalogCategory));
        Assert.Equal(ProposalAuditAggregateCategories.Catalog, catalogCategory);
        Assert.False(ProposalAuditAggregateEventClassifier.TryGetCategory(invalidCatalog, out _));
        Assert.True(ProposalAuditAggregateEventClassifier.TryGetCategory(archiveRead, out var archiveCategory));
        Assert.Equal(ProposalAuditAggregateCategories.ArchiveReads, archiveCategory);
        Assert.True(ProposalAuditAggregateEventClassifier.TryGetCategory(retention, out var retentionCategory));
        Assert.Equal(ProposalAuditAggregateCategories.Retention, retentionCategory);
        Assert.False(ProposalAuditAggregateEventClassifier.TryGetCategory(unlinked, out _));
    }

    [Fact]
    public void Aggregate_endpoint_accepts_only_one_canonical_month_parameter()
    {
        var valid = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["month"] = "2025-11"
        });
        Assert.True(AuditAggregateRequestParser.TryParseMonth(valid, out var month));
        Assert.Equal(new DateOnly(2025, 11, 1), month);

        var extraFilter = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["month"] = "2025-11",
            ["category"] = "Transições"
        });
        Assert.False(AuditAggregateRequestParser.TryParseMonth(extraFilter, out _));

        var duplicate = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["month"] = new StringValues(["2025-11", "2025-12"])
        });
        Assert.False(AuditAggregateRequestParser.TryParseMonth(duplicate, out _));
        Assert.False(AuditAggregateRequestParser.TryParseMonth(new QueryCollection(new Dictionary<string, StringValues>
        {
            ["month"] = "2025-13"
        }), out _));
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
        var aggregateWindow = dbContext.Model.FindEntityType(typeof(ProposalAuditAggregateWindow))!;
        var aggregateContribution = dbContext.Model.FindEntityType(typeof(ProposalAuditAggregateContribution))!;
        var grantAudit = dbContext.Model.FindEntityType(typeof(OrganizationPermissionGrantAudit))!;

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
        Assert.Equal(new[] { "OrganizationId", "MonthStart", "Category" },
            aggregateWindow.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(new[] { "OrganizationId", "MonthStart", "Category", "RequestToken" },
            aggregateContribution.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Contains(grantAudit.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationUser)
            && foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "OrganizationId", "ActorUserId" }));
        Assert.Contains(grantAudit.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationUser)
            && foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "OrganizationId", "TargetUserId" }));
    }

    [Fact]
    public void Permission_grant_audit_requires_supported_scope_action_and_justification()
    {
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-11-26T12:00:00-03:00");
        var audit = new OrganizationPermissionGrantAudit(organizationId, actorId, recipientId,
            AuthorizationPolicies.ViewProposalAuditGrant, "Granted", now, "Cobertura de auditoria");

        Assert.Equal(now.ToUniversalTime(), audit.OccurredAtUtc);
        Assert.Equal("Cobertura de auditoria", audit.Justification);
        Assert.Throws<ArgumentException>(() => new OrganizationPermissionGrantAudit(organizationId,
            actorId, actorId, AuthorizationPolicies.ViewProposalAuditGrant, "Granted", now, "Justificativa"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrganizationPermissionGrantAudit(organizationId,
            actorId, recipientId, "ManageProposals", "Granted", now, "Justificativa"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrganizationPermissionGrantAudit(organizationId,
            actorId, recipientId, AuthorizationPolicies.ViewProposalAuditGrant, "Updated", now, "Justificativa"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrganizationPermissionGrantAudit(organizationId,
            actorId, recipientId, AuthorizationPolicies.ViewProposalAuditGrant, "Granted", now, " "));
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
    public void Late_aggregate_arrival_invalidates_the_month_window()
    {
        var window = new ProposalAuditAggregateWindow(Guid.NewGuid(), new DateOnly(2025, 1, 1),
            ProposalAuditAggregateCategories.Transitions);
        var now = DateTimeOffset.Parse("2026-02-01T12:00:00Z");

        window.Invalidate(now);

        Assert.Equal(ProposalAuditAggregateWindowStates.Invalidated, window.State);
        Assert.Equal(now, window.InvalidatedAtUtc);
    }

    [Fact]
    public void Aggregate_month_can_seal_without_category_windows_and_late_arrival_is_terminal()
    {
        var month = new ProposalAuditAggregateMonth(Guid.NewGuid(), new DateOnly(2025, 1, 1));
        var sealedAt = DateTimeOffset.Parse("2026-02-01T12:00:00Z");

        month.Seal(sealedAt);

        Assert.Equal(ProposalAuditAggregateWindowStates.Sealed, month.State);
        Assert.Equal(sealedAt, month.SealedAtUtc);
        month.Invalidate(sealedAt.AddMinutes(1));
        Assert.Equal(ProposalAuditAggregateWindowStates.Invalidated, month.State);
        Assert.Throws<InvalidOperationException>(() => month.Seal(sealedAt.AddMinutes(2)));
    }

    [Fact]
    public async Task History_routes_are_mapped_with_explicit_policy_metadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDbContext<PitCrmDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Database=not_used;Username=not_used"));
        builder.Services.AddScoped<ICurrentOrganizationContext, HttpCurrentOrganizationContext>();
        builder.Services.AddScoped<IProposalHistoryStore, PersistenceProposalHistoryStore>();
        builder.Services.AddScoped<IProposalStore, PersistenceProposalStore>();
        builder.Services.AddScoped<IProposalAuditAggregateStore, PersistenceProposalAuditAggregateStore>();
        await using var app = builder.Build();
        ProposalHistoryEndpoints.Map(app);
        PermissionGrantEndpoints.Map(app);

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        var operatorTimeline = Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == "/api/demands/{id:guid}/proposal-timeline");
        var audit = Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == "/api/proposals/{id:guid}/audit");
        var aggregates = Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == "/api/proposals/audit/aggregates");
        var grant = Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == "/api/organization/permission-grants/{permission}/{userId:guid}");
        var revoke = Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == "/api/organization/permission-grants/{permission}/{userId:guid}/revoke");

        Assert.Contains(operatorTimeline.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            metadata => metadata.Policy == AuthorizationPolicies.OperateDemands);
        Assert.Contains(audit.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            metadata => metadata.Policy == AuthorizationPolicies.ViewProposalAudit);
        Assert.Contains(aggregates.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            metadata => metadata.Policy == AuthorizationPolicies.ViewProposalAudit);
        Assert.Contains(grant.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            metadata => metadata.Policy == AuthorizationPolicies.ManageOrganizationPermissionGrants);
        Assert.Contains(revoke.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            metadata => metadata.Policy == AuthorizationPolicies.ManageOrganizationPermissionGrants);
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
