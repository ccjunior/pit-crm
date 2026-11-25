using System.Globalization;
using PitCrm.Application.Proposals;

namespace PitCrm.Api.Features.Proposals;

public static class OrganizationLocalScheduleTime
{
    public const string TimeZoneId = ProposalDateRules.OrganizationTimeZoneId;
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static bool TryConvert(string? localValue, string? selectedOffset,
        out DateTimeOffset utcValue, out string? errorField)
    {
        utcValue = default;
        errorField = null;
        if (!DateTime.TryParseExact(localValue, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local))
        {
            errorField = "scheduledAtLocal";
            return false;
        }
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local))
        {
            errorField = "scheduledAtLocal";
            return false;
        }
        if (Zone.IsAmbiguousTime(local))
        {
            if (!TryOffset(selectedOffset, out var offset) || !Zone.GetAmbiguousTimeOffsets(local).Contains(offset))
            {
                errorField = "selectedOffset";
                return false;
            }
            utcValue = new DateTimeOffset(local, offset).ToUniversalTime();
            return true;
        }

        var actualOffset = Zone.GetUtcOffset(local);
        if (selectedOffset is not null && (!TryOffset(selectedOffset, out var suppliedOffset) || suppliedOffset != actualOffset))
        {
            errorField = "selectedOffset";
            return false;
        }
        utcValue = new DateTimeOffset(local, actualOffset).ToUniversalTime();
        return true;
    }

    private static bool TryOffset(string? value, out TimeSpan offset)
    {
        offset = default;
        if (value is null || value.Length != 6 || value[0] is not ('+' or '-')
            || !TimeSpan.TryParseExact(value[1..], @"hh\:mm", CultureInfo.InvariantCulture, out var magnitude)
            || magnitude > TimeSpan.FromHours(14)) return false;
        offset = value[0] == '-' ? -magnitude : magnitude;
        return true;
    }
}
