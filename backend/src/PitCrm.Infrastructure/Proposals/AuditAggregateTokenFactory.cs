using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using PitCrm.Domain.Proposals;

namespace PitCrm.Infrastructure.Proposals;

public sealed class AuditAggregateTokenFactory
{
    private readonly byte[]? masterKey;

    public AuditAggregateTokenFactory(IConfiguration configuration)
    {
        var configured = configuration["AuditAggregation:HmacKey"];
        if (string.IsNullOrWhiteSpace(configured)) return;
        try
        {
            masterKey = Convert.FromBase64String(configured);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Audit aggregation HMAC key must be base64.", exception);
        }
        if (masterKey.Length < 32)
            throw new InvalidOperationException("Audit aggregation HMAC key must contain at least 32 bytes.");
    }

    public string Create(Guid organizationId, DateOnly monthStart, string category, Guid requestId)
    {
        if (masterKey is null)
            throw new InvalidOperationException("Audit aggregation is fail-closed until its HMAC key is configured.");

        var scope = Encoding.UTF8.GetBytes($"pit-crm:ac-t05:audit-aggregate:v1\n{organizationId:N}\n{monthStart:yyyy-MM}\n{category}");
        var scopedKey = HMACSHA256.HashData(masterKey, scope);
        try
        {
            var requestBytes = Convert.FromHexString(requestId.ToString("N"));
            return Convert.ToHexString(HMACSHA256.HashData(scopedKey, requestBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(scopedKey);
        }
    }
}
