using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace PitCrm.Api.Features.Proposals;

public static class AuditAggregateRequestParser
{
    public static bool TryParseMonth(IQueryCollection query, out DateOnly monthStart)
    {
        monthStart = default;
        if (query.Count != 1 || !query.TryGetValue("month", out var values) || values.Count != 1)
            return false;
        var value = values[0];
        if (value is null || value.Length != 7 || value[4] != '-') return false;
        if (!DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var month)) return false;
        monthStart = new DateOnly(month.Year, month.Month, 1);
        return true;
    }
}
