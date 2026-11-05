using Microsoft.EntityFrameworkCore;
using PitCrm.Domain.Organizations;
using PitCrm.Infrastructure.Identity;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Api.Tests;

public sealed class OrganizationPersistenceModelTests
{
    [Fact]
    public void User_requires_an_organization_with_restricted_deletion()
    {
        var options = new DbContextOptionsBuilder<PitCrmDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=not_used;Username=not_used")
            .Options;
        using var dbContext = new PitCrmDbContext(options);

        var user = dbContext.Model.FindEntityType(typeof(ApplicationUser))!;
        var organizationLink = Assert.Single(user.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Organization));
        var displayName = user.FindProperty(nameof(ApplicationUser.DisplayName))!;

        Assert.True(organizationLink.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, organizationLink.DeleteBehavior);
        Assert.False(displayName.IsNullable);
        Assert.Equal(200, displayName.GetMaxLength());
    }
}
