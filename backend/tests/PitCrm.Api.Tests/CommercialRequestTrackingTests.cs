using PitCrm.Domain.Proposals;
using PitCrm.Api.Features.Proposals;
using Microsoft.EntityFrameworkCore;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Api.Tests;

public sealed class CommercialRequestTrackingTests
{
    [Fact]
    public void Persistence_model_contains_tracking_constraints_indexes_and_migration()
    {
        var options = new DbContextOptionsBuilder<PitCrmDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=not_used;Username=not_used")
            .Options;
        using var db = new PitCrmDbContext(options);
        var entity = db.Model.FindEntityType(typeof(CommercialRequest))!;
        Assert.NotNull(entity.FindProperty(nameof(CommercialRequest.CustomerDeadline)));
        Assert.NotNull(entity.FindProperty(nameof(CommercialRequest.ReminderOccurrenceId)));
        Assert.Contains(entity.GetIndexes(), value => value.Properties.Any(property => property.Name == nameof(CommercialRequest.CustomerDeadline)));
        Assert.Contains(db.Database.GetMigrations(), value => value == "20261125160000_AddProposalTrackingFields");
    }

    [Fact]
    public void Local_schedule_time_enforces_Sao_Paulo_daylight_saving_rules()
    {
        Assert.False(OrganizationLocalScheduleTime.TryConvert("2018-11-04T00:30:00", null, out _, out var invalidField));
        Assert.Equal("scheduledAtLocal", invalidField);

        Assert.False(OrganizationLocalScheduleTime.TryConvert("2018-02-17T23:30:00", null, out _, out var ambiguousField));
        Assert.Equal("selectedOffset", ambiguousField);
        Assert.True(OrganizationLocalScheduleTime.TryConvert("2018-02-17T23:30:00", "-02:00", out var earlier, out _));
        Assert.True(OrganizationLocalScheduleTime.TryConvert("2018-02-17T23:30:00", "-03:00", out var later, out _));
        Assert.Equal(TimeSpan.FromHours(1), later - earlier);

        Assert.False(OrganizationLocalScheduleTime.TryConvert("2018-02-17T23:30:00", "-04:00", out _, out var mismatchField));
        Assert.Equal("selectedOffset", mismatchField);
    }

    [Fact]
    public void Tracking_updates_dates_and_action_revision_but_action_identity_survives_owner_edits()
    {
        var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var request = new CommercialRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SOL-2026-0001",
            Guid.NewGuid(), null, null, null, null, Guid.NewGuid(), "E-mail", "Escopo", now);
        var scheduled = now.AddDays(1);

        request.UpdateTracking(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), "Ligar para cliente", scheduled, now.AddMinutes(1));
        var occurrenceId = request.ReminderOccurrenceId;
        Assert.NotNull(occurrenceId);
        Assert.Equal(1, request.ReminderOccurrenceRevision);
        Assert.Equal(new DateOnly(2026, 1, 10), request.CustomerDeadline);
        Assert.Equal(scheduled, request.NextActionAtUtc);

        request.UpdateTracking(new DateOnly(2026, 1, 11), new DateOnly(2026, 1, 12), "Ligar para cliente", scheduled, now.AddMinutes(2));
        Assert.Equal(occurrenceId, request.ReminderOccurrenceId);
        Assert.Equal(1, request.ReminderOccurrenceRevision);

        request.UpdateTracking(new DateOnly(2026, 1, 11), new DateOnly(2026, 1, 12), "Enviar proposta", scheduled.AddHours(1), now.AddMinutes(3));
        Assert.Equal(occurrenceId, request.ReminderOccurrenceId);
        Assert.Equal(2, request.ReminderOccurrenceRevision);

        request.UpdateTracking(new DateOnly(2026, 1, 11), new DateOnly(2026, 1, 12), null, null, now.AddMinutes(4));
        Assert.Null(request.ReminderOccurrenceId);
        Assert.Null(request.NextActionAtUtc);
        Assert.Equal(3, request.ReminderOccurrenceRevision);
    }

    [Fact]
    public void Tracking_rejects_partial_next_action_without_mutation()
    {
        var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var request = new CommercialRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SOL-2026-0001",
            Guid.NewGuid(), null, null, null, null, null, "E-mail", "Escopo", now);
        var version = request.Version;

        Assert.Throws<ArgumentException>(() => request.UpdateTracking(null, null, "ligar", null, now.AddMinutes(1)));
        Assert.Equal(version, request.Version);
        Assert.Null(request.ReminderOccurrenceId);
        Assert.Equal(0, request.ReminderOccurrenceRevision);
    }
}
