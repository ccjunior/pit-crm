namespace PitCrm.Domain.Proposals;

public static class ProposalHistoryRetentionRules
{
    private static readonly TimeZoneInfo OrganizationTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateTimeOffset AddCivilMonthsUtc(DateTimeOffset instantUtc, int months)
    {
        if (months < 0) throw new ArgumentOutOfRangeException(nameof(months));
        var local = TimeZoneInfo.ConvertTime(instantUtc, OrganizationTimeZone).DateTime.AddMonths(months);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (OrganizationTimeZone.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offset = OrganizationTimeZone.IsAmbiguousTime(local)
            ? OrganizationTimeZone.GetAmbiguousTimeOffsets(local).Max()
            : OrganizationTimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static bool IsExpired(DateTimeOffset occurredAtUtc, DateTimeOffset nowUtc, int retentionMonths) =>
        nowUtc.ToUniversalTime() >= AddCivilMonthsUtc(occurredAtUtc, retentionMonths);

    public static DateOnly LocalMonthStart(DateTimeOffset instantUtc)
    {
        var local = TimeZoneInfo.ConvertTime(instantUtc, OrganizationTimeZone);
        return new DateOnly(local.Year, local.Month, 1);
    }

    public static DateTimeOffset AuditAggregateMonthMaturesAtUtc(DateOnly monthStart)
    {
        if (monthStart.Day != 1) throw new ArgumentOutOfRangeException(nameof(monthStart));
        var local = monthStart.AddMonths(13).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return LocalBoundaryToUtc(local, chooseLaterAmbiguous: false);
    }

    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) AuditAggregateMonthUtcRange(DateOnly monthStart)
    {
        if (monthStart.Day != 1) throw new ArgumentOutOfRangeException(nameof(monthStart));
        var localStart = monthStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = monthStart.AddMonths(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (LocalBoundaryToUtc(localStart, chooseLaterAmbiguous: false),
            LocalBoundaryToUtc(localEnd, chooseLaterAmbiguous: false));
    }

    private static DateTimeOffset LocalBoundaryToUtc(DateTime local, bool chooseLaterAmbiguous)
    {
        while (OrganizationTimeZone.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offsets = OrganizationTimeZone.IsAmbiguousTime(local)
            ? OrganizationTimeZone.GetAmbiguousTimeOffsets(local)
            : [OrganizationTimeZone.GetUtcOffset(local)];
        var offset = chooseLaterAmbiguous ? offsets.Min() : offsets.Max();
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static DateTimeOffset FirstUnexpiredOccurrenceUtc(DateTimeOffset nowUtc, int retentionMonths)
    {
        if (retentionMonths < 1) throw new ArgumentOutOfRangeException(nameof(retentionMonths));
        var now = nowUtc.ToUniversalTime();
        var low = now.AddMonths(-retentionMonths).AddDays(-2).Ticks;
        var high = now.Ticks;
        while (high - low > 1)
        {
            var middle = low + (high - low) / 2;
            if (IsExpired(new DateTimeOffset(middle, TimeSpan.Zero), now, retentionMonths)) low = middle;
            else high = middle;
        }

        // PostgreSQL timestamps have microsecond precision; ceil to the first representable
        // instant that is still inside the retention window.
        var cutoffTicks = ((high + 9) / 10) * 10;
        return new DateTimeOffset(cutoffTicks, TimeSpan.Zero);
    }

    public static bool CanArchive(int generation, int latestGeneration, bool isTerminal) =>
        isTerminal && generation == latestGeneration;
}

public static class ProposalIdempotencyKey
{
    public static bool IsVersion7(Guid value)
    {
        if (value == Guid.Empty) return false;
        var text = value.ToString("N");
        return text[12] == '7' && text[16] is '8' or '9' or 'a' or 'b' or 'A' or 'B';
    }

    public static bool IsWithinValidityWindow(Guid value, DateTimeOffset nowUtc)
    {
        if (!IsVersion7(value)) return false;
        try
        {
            var timestamp = GetTimestampUtc(value);
            var now = nowUtc.ToUniversalTime();
            return timestamp <= now.AddMinutes(5) && now < timestamp.AddDays(30);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static DateTimeOffset GetTimestampUtc(Guid value)
    {
        if (!IsVersion7(value)) throw new ArgumentException("The idempotency key must be UUIDv7.", nameof(value));
        var timestampMilliseconds = Convert.ToInt64(value.ToString("N")[..12], 16);
        return DateTimeOffset.FromUnixTimeMilliseconds(timestampMilliseconds);
    }
}

public static class ProposalHistoryEventTypes
{
    public const string StageChanged = "StageChanged";
    public const string ActivityRecorded = "ActivityRecorded";
    public const string CommentAdded = "CommentAdded";
    public const string LegacyActivity = "LegacyActivity";
    public static readonly string[] Operational = [StageChanged, ActivityRecorded, CommentAdded];
}

public static class ProposalHistoryActorTypes
{
    public const string Human = "Human";
    public const string System = "System";
}

public sealed class ProposalTimelineEvent
{
    private ProposalTimelineEvent() { }

    public ProposalTimelineEvent(Guid id, Guid organizationId, Guid requestId, long sequence,
        string eventType, string actorType, Guid? actorUserId, string? systemCode, DateTimeOffset occurredAtUtc,
        string? fromStatus = null, string? toStatus = null, Guid? reasonId = null,
        string? reasonLabelSnapshot = null, string? activityType = null, string? text = null,
        bool occurredAtKnown = true, Guid? sourceLegacyId = null)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || requestId == Guid.Empty || sequence < 1)
            throw new ArgumentException("Timeline event identity and sequence must be valid.");
        if (actorType == ProposalHistoryActorTypes.Human && (actorUserId is null || actorUserId == Guid.Empty || systemCode is not null)
            || actorType == ProposalHistoryActorTypes.System && (actorUserId is not null || string.IsNullOrWhiteSpace(systemCode))
            || actorType is not (ProposalHistoryActorTypes.Human or ProposalHistoryActorTypes.System))
            throw new ArgumentException("A timeline event must have exactly one valid actor.", nameof(actorType));
        if (eventType is not (ProposalHistoryEventTypes.StageChanged or ProposalHistoryEventTypes.ActivityRecorded
            or ProposalHistoryEventTypes.CommentAdded or ProposalHistoryEventTypes.LegacyActivity))
            throw new ArgumentOutOfRangeException(nameof(eventType));
        if (eventType == ProposalHistoryEventTypes.StageChanged
            && (string.IsNullOrWhiteSpace(fromStatus) || string.IsNullOrWhiteSpace(toStatus)))
            throw new ArgumentException("Stage changes require both recorded stages.");
        if (eventType == ProposalHistoryEventTypes.LegacyActivity && sourceLegacyId is null)
            throw new ArgumentException("A legacy event must identify its source row.", nameof(sourceLegacyId));
        if (text?.Length > 4000 || activityType?.Length > 60 || reasonLabelSnapshot?.Length > 200)
            throw new ArgumentOutOfRangeException(nameof(text));

        Id = id;
        OrganizationId = organizationId;
        CommercialRequestId = requestId;
        Sequence = sequence;
        EventType = eventType;
        ActorType = actorType;
        ActorUserId = actorUserId;
        SystemCode = systemCode;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        OccurredAtKnown = occurredAtKnown;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ReasonId = reasonId;
        ReasonLabelSnapshot = reasonLabelSnapshot;
        ActivityType = activityType;
        Text = text;
        SourceLegacyId = sourceLegacyId;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CommercialRequestId { get; private set; }
    public long Sequence { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string ActorType { get; private set; } = string.Empty;
    public Guid? ActorUserId { get; private set; }
    public string? SystemCode { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public bool OccurredAtKnown { get; private set; }
    public string? FromStatus { get; private set; }
    public string? ToStatus { get; private set; }
    public Guid? ReasonId { get; private set; }
    public string? ReasonLabelSnapshot { get; private set; }
    public string? ActivityType { get; private set; }
    public string? Text { get; private set; }
    public Guid? SourceLegacyId { get; private set; }
}

public sealed class ProposalTimelineMention
{
    private ProposalTimelineMention() { }
    public ProposalTimelineMention(Guid id, Guid organizationId, Guid eventId, Guid mentionedUserId)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || eventId == Guid.Empty || mentionedUserId == Guid.Empty)
            throw new ArgumentException("Timeline mention identities cannot be empty.");
        Id = id;
        OrganizationId = organizationId;
        EventId = eventId;
        MentionedUserId = mentionedUserId;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid MentionedUserId { get; private set; }
}

public sealed class ProposalReason
{
    private ProposalReason() { }
    public ProposalReason(Guid id, Guid organizationId, string resultStatus, string label,
        Guid createdByUserId, DateTimeOffset now)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Reason identities cannot be empty.");
        if (!CommercialRequest.ResultStatuses.Contains(resultStatus, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(resultStatus));
        Id = id;
        OrganizationId = organizationId;
        ResultStatus = resultStatus;
        Label = Required(label, 120, nameof(label));
        IsActive = true;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = now.ToUniversalTime();
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string ResultStatus { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid? DeactivatedByUserId { get; private set; }
    public DateTimeOffset? DeactivatedAtUtc { get; private set; }

    public void Deactivate(Guid actorUserId, DateTimeOffset now)
    {
        if (actorUserId == Guid.Empty) throw new ArgumentException("An actor is required.", nameof(actorUserId));
        if (!IsActive) return;
        IsActive = false;
        DeactivatedByUserId = actorUserId;
        DeactivatedAtUtc = now.ToUniversalTime();
    }

    private static string Required(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName);
        return value.Trim();
    }
}

public sealed class ProposalEventSequence
{
    private ProposalEventSequence() { }
    public ProposalEventSequence(Guid organizationId, Guid requestId, long lastSequence)
    {
        if (organizationId == Guid.Empty || requestId == Guid.Empty || lastSequence < 0)
            throw new ArgumentException("Event sequence identity is invalid.");
        OrganizationId = organizationId;
        CommercialRequestId = requestId;
        LastSequence = lastSequence;
    }
    public Guid OrganizationId { get; private set; }
    public Guid CommercialRequestId { get; private set; }
    public long LastSequence { get; private set; }
    public long ReserveNext() => LastSequence = checked(LastSequence + 1);
}

public sealed class ProposalIdempotencyRecord
{
    private ProposalIdempotencyRecord() { }
    public ProposalIdempotencyRecord(Guid organizationId, string targetType, Guid targetId, Guid key, string operation,
        string fingerprint, Guid resultId, DateTimeOffset keyTimestampUtc, DateTimeOffset storedAtUtc)
    {
        if (organizationId == Guid.Empty || targetId == Guid.Empty || key == Guid.Empty || resultId == Guid.Empty)
            throw new ArgumentException("Idempotency identities cannot be empty.");
        OrganizationId = organizationId;
        TargetType = Required(targetType, 30, nameof(targetType));
        TargetId = targetId;
        Key = key;
        Operation = Required(operation, 60, nameof(operation));
        Fingerprint = Required(fingerprint, 64, nameof(fingerprint));
        ResultId = resultId;
        KeyTimestampUtc = keyTimestampUtc.ToUniversalTime();
        StoredAtUtc = storedAtUtc.ToUniversalTime();
    }
    public Guid OrganizationId { get; private set; }
    public string TargetType { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public Guid Key { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string Fingerprint { get; private set; } = string.Empty;
    public Guid ResultId { get; private set; }
    public string? ResultPayloadJson { get; private set; }
    public DateTimeOffset KeyTimestampUtc { get; private set; }
    public DateTimeOffset StoredAtUtc { get; private set; }

    public void StoreResultPayload(string resultPayloadJson)
    {
        if (string.IsNullOrWhiteSpace(resultPayloadJson))
            throw new ArgumentException("An idempotency response snapshot is required.", nameof(resultPayloadJson));
        ResultPayloadJson = resultPayloadJson;
    }

    private static string Required(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName);
        return value;
    }
}

public sealed class ProposalAuditEvent
{
    private ProposalAuditEvent() { }
    public ProposalAuditEvent(Guid id, Guid organizationId, string eventType, string category,
        string actorType, Guid? actorUserId, string? systemCode, DateTimeOffset occurredAtUtc,
        Guid? requestId = null, Guid? targetEventId = null, string? detailsJson = null)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty) throw new ArgumentException("Audit identity is invalid.");
        if (actorType == ProposalHistoryActorTypes.Human && (actorUserId is null || systemCode is not null)
            || actorType == ProposalHistoryActorTypes.System && (actorUserId is not null || string.IsNullOrWhiteSpace(systemCode)))
            throw new ArgumentException("Audit records require exactly one actor.", nameof(actorType));
        Id = id;
        OrganizationId = organizationId;
        EventType = Required(eventType, 40, nameof(eventType));
        Category = Required(category, 40, nameof(category));
        ActorType = actorType;
        ActorUserId = actorUserId;
        SystemCode = systemCode;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        CommercialRequestId = requestId;
        TimelineEventId = targetEventId;
        DetailsJson = detailsJson;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid? CommercialRequestId { get; private set; }
    public Guid? TimelineEventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string ActorType { get; private set; } = string.Empty;
    public Guid? ActorUserId { get; private set; }
    public string? SystemCode { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string? DetailsJson { get; private set; }
    public DateTimeOffset? RedactedAtUtc { get; private set; }

    public void Redact(DateTimeOffset now)
    {
        DetailsJson = null;
        ActorType = "Redacted";
        ActorUserId = null;
        SystemCode = null;
        CommercialRequestId = null;
        TimelineEventId = null;
        RedactedAtUtc = now.ToUniversalTime();
    }

    private static string Required(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName);
        return value;
    }
}

public static class ProposalAuditAggregateCategories
{
    public const string Transitions = "Transições";
    public const string Catalog = "Catálogo";
    public const string ActivityAndComments = "Atividades e comentários";
    public const string ArchiveReads = "Consultas ao arquivo";
    public const string Retention = "Retenção";

    public static readonly string[] All =
        [Transitions, Catalog, ActivityAndComments, ArchiveReads, Retention];
}

public static class ProposalAuditAggregateWindowStates
{
    public const string Open = "Open";
    public const string Sealed = "Sealed";
    public const string Invalidated = "Invalidated";
}

public sealed class ProposalAuditAggregateMonth
{
    private ProposalAuditAggregateMonth() { }

    public ProposalAuditAggregateMonth(Guid organizationId, DateOnly monthStart)
    {
        if (organizationId == Guid.Empty || monthStart.Day != 1)
            throw new ArgumentException("Audit aggregate month identity is invalid.");
        OrganizationId = organizationId;
        MonthStart = monthStart;
        State = ProposalAuditAggregateWindowStates.Open;
    }

    public Guid OrganizationId { get; private set; }
    public DateOnly MonthStart { get; private set; }
    public string State { get; private set; } = ProposalAuditAggregateWindowStates.Open;
    public DateTimeOffset? SealedAtUtc { get; private set; }
    public DateTimeOffset? InvalidatedAtUtc { get; private set; }

    public void Seal(DateTimeOffset nowUtc)
    {
        if (State != ProposalAuditAggregateWindowStates.Open)
            throw new InvalidOperationException("Only an open aggregate month can be sealed.");
        State = ProposalAuditAggregateWindowStates.Sealed;
        SealedAtUtc = nowUtc.ToUniversalTime();
    }

    public void Invalidate(DateTimeOffset nowUtc)
    {
        if (State is not (ProposalAuditAggregateWindowStates.Open or ProposalAuditAggregateWindowStates.Sealed)) return;
        State = ProposalAuditAggregateWindowStates.Invalidated;
        InvalidatedAtUtc = nowUtc.ToUniversalTime();
    }
}

public static class ProposalAuditAggregatePublicationRules
{
    public const long MinimumDistinctRequests = 10;

    public static IReadOnlyDictionary<string, long> FilterPublicCounts(IReadOnlyDictionary<string, long> counts)
    {
        var visible = counts.Where(value => value.Value >= MinimumDistinctRequests)
            .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
        var hasTransitions = counts.TryGetValue(ProposalAuditAggregateCategories.Transitions, out var transitions);
        var hasCatalog = counts.TryGetValue(ProposalAuditAggregateCategories.Catalog, out var catalog);
        if ((hasTransitions || hasCatalog)
            && (!hasTransitions || !hasCatalog || catalog < MinimumDistinctRequests
                || catalog > transitions || transitions - catalog < MinimumDistinctRequests))
        {
            visible.Remove(ProposalAuditAggregateCategories.Transitions);
            visible.Remove(ProposalAuditAggregateCategories.Catalog);
        }
        return visible;
    }
}

public sealed class ProposalAuditAggregateWindow
{
    private ProposalAuditAggregateWindow() { }

    public ProposalAuditAggregateWindow(Guid organizationId, DateOnly monthStart, string category)
    {
        if (organizationId == Guid.Empty || monthStart.Day != 1
            || !ProposalAuditAggregateCategories.All.Contains(category, StringComparer.Ordinal))
            throw new ArgumentException("Audit aggregate window identity is invalid.");
        OrganizationId = organizationId;
        MonthStart = monthStart;
        Category = category;
        State = ProposalAuditAggregateWindowStates.Open;
    }

    public Guid OrganizationId { get; private set; }
    public DateOnly MonthStart { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string State { get; private set; } = ProposalAuditAggregateWindowStates.Open;
    public long DistinctRequestCount { get; private set; }
    public DateTimeOffset? SealedAtUtc { get; private set; }
    public DateTimeOffset? InvalidatedAtUtc { get; private set; }

    public void Seal(long distinctRequestCount, DateTimeOffset nowUtc)
    {
        if (State != ProposalAuditAggregateWindowStates.Open || distinctRequestCount < 0)
            throw new InvalidOperationException("Only an open aggregate window can be sealed.");
        State = ProposalAuditAggregateWindowStates.Sealed;
        DistinctRequestCount = distinctRequestCount;
        SealedAtUtc = nowUtc.ToUniversalTime();
    }

    public void Invalidate(DateTimeOffset nowUtc)
    {
        if (State is not (ProposalAuditAggregateWindowStates.Open or ProposalAuditAggregateWindowStates.Sealed)) return;
        State = ProposalAuditAggregateWindowStates.Invalidated;
        InvalidatedAtUtc = nowUtc.ToUniversalTime();
    }
}

public sealed class ProposalAuditAggregateContribution
{
    private ProposalAuditAggregateContribution() { }

    public ProposalAuditAggregateContribution(Guid organizationId, DateOnly monthStart, string category,
        string requestToken)
    {
        if (organizationId == Guid.Empty || monthStart.Day != 1
            || !ProposalAuditAggregateCategories.All.Contains(category, StringComparer.Ordinal)
            || requestToken.Length != 64 || requestToken.Any(value => !Uri.IsHexDigit(value)))
            throw new ArgumentException("Audit aggregate contribution is invalid.");
        OrganizationId = organizationId;
        MonthStart = monthStart;
        Category = category;
        RequestToken = requestToken.ToUpperInvariant();
    }

    public Guid OrganizationId { get; private set; }
    public DateOnly MonthStart { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string RequestToken { get; private set; } = string.Empty;
}

public sealed class ProposalAuditAggregateQuarantine
{
    private ProposalAuditAggregateQuarantine() { }

    public ProposalAuditAggregateQuarantine(Guid organizationId, DateOnly monthStart, string category,
        DateTimeOffset quarantinedAtUtc)
    {
        if (organizationId == Guid.Empty || monthStart.Day != 1
            || !ProposalAuditAggregateCategories.All.Contains(category, StringComparer.Ordinal))
            throw new ArgumentException("Audit aggregate quarantine identity is invalid.");
        OrganizationId = organizationId;
        MonthStart = monthStart;
        Category = category;
        EventCount = 1;
        QuarantinedAtUtc = quarantinedAtUtc.ToUniversalTime();
    }

    public Guid OrganizationId { get; private set; }
    public DateOnly MonthStart { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public long EventCount { get; private set; }
    public DateTimeOffset QuarantinedAtUtc { get; private set; }

    public void Add(DateTimeOffset nowUtc)
    {
        EventCount++;
        QuarantinedAtUtc = nowUtc.ToUniversalTime();
    }
}

public sealed class ProposalRetentionCycle
{
    private ProposalRetentionCycle() { }
    public ProposalRetentionCycle(Guid organizationId, Guid requestId, int generation,
        DateTimeOffset closedAtUtc, DateTimeOffset archiveAtUtc, bool closedAtKnown)
    {
        if (organizationId == Guid.Empty || requestId == Guid.Empty || generation < 1)
            throw new ArgumentException("Retention cycle identity is invalid.");
        OrganizationId = organizationId;
        CommercialRequestId = requestId;
        Generation = generation;
        ClosedAtUtc = closedAtUtc.ToUniversalTime();
        ArchiveAtUtc = archiveAtUtc.ToUniversalTime();
        ClosedAtKnown = closedAtKnown;
        State = "Active";
    }
    public Guid OrganizationId { get; private set; }
    public Guid CommercialRequestId { get; private set; }
    public int Generation { get; private set; }
    public DateTimeOffset ClosedAtUtc { get; private set; }
    public bool ClosedAtKnown { get; private set; }
    public DateTimeOffset ArchiveAtUtc { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }
    public DateTimeOffset? DiscardAtUtc { get; private set; }
    public string State { get; private set; } = "Active";

    public void Archive(DateTimeOffset now, DateTimeOffset discardAtUtc)
    {
        if (State != "Active") return;
        State = "Archived";
        ArchivedAtUtc = now.ToUniversalTime();
        DiscardAtUtc = discardAtUtc.ToUniversalTime();
    }
    public void Restore()
    {
        if (State is "Active" or "Archived") State = "Restored";
    }
    public void Discard()
    {
        if (State == "Archived") State = "Discarded";
    }
}

public sealed class ProposalTimelineArchive
{
    private ProposalTimelineArchive() { }
    public ProposalTimelineArchive(Guid organizationId, Guid requestId, int generation,
        string payloadJson, DateTimeOffset archivedAtUtc, DateTimeOffset discardAtUtc)
    {
        OrganizationId = organizationId;
        CommercialRequestId = requestId;
        Generation = generation;
        PayloadJson = payloadJson;
        ArchivedAtUtc = archivedAtUtc.ToUniversalTime();
        DiscardAtUtc = discardAtUtc.ToUniversalTime();
    }
    public Guid OrganizationId { get; private set; }
    public Guid CommercialRequestId { get; private set; }
    public int Generation { get; private set; }
    public string PayloadJson { get; private set; } = "[]";
    public DateTimeOffset ArchivedAtUtc { get; private set; }
    public DateTimeOffset DiscardAtUtc { get; private set; }
}

public sealed class ProposalLegacyQuarantine
{
    private ProposalLegacyQuarantine() { }
    public ProposalLegacyQuarantine(Guid sourceId, Guid organizationId, Guid requestId, Guid actorUserId,
        string eventType, string description, DateTimeOffset occurredAtUtc, string reason)
    {
        SourceId = sourceId;
        OrganizationId = organizationId;
        CommercialRequestId = requestId;
        ActorUserId = actorUserId;
        EventType = eventType;
        Description = description;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        QuarantineReason = reason;
    }
    public Guid SourceId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CommercialRequestId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string QuarantineReason { get; private set; } = string.Empty;
}

public sealed class ProposalHistoryMigrationReconciliation
{
    private ProposalHistoryMigrationReconciliation() { }
    public Guid OrganizationId { get; private set; }
    public int SourceCount { get; private set; }
    public int MigratedCount { get; private set; }
    public int QuarantinedCount { get; private set; }
    public DateTimeOffset ReconciledAtUtc { get; private set; }
}
