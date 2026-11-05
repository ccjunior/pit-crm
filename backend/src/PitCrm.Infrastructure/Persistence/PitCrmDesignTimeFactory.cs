using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PitCrm.Infrastructure.Persistence;

public sealed class PitCrmDesignTimeFactory
    : IDesignTimeDbContextFactory<PitCrmDbContext>
{
    public PitCrmDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "PITCRM_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set PITCRM_CONNECTION_STRING before running EF Core migrations.");
        }

        var options = new DbContextOptionsBuilder<PitCrmDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PitCrmDbContext(options);
    }
}
