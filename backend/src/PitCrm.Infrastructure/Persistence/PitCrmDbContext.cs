using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PitCrm.Domain.Organizations;
using PitCrm.Domain.Customers;
using PitCrm.Domain.Locations;
using PitCrm.Domain.Demands;
using PitCrm.Infrastructure.Identity;

namespace PitCrm.Infrastructure.Persistence;

public sealed class PitCrmDbContext(
    DbContextOptions<PitCrmDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerLocation> CustomerLocations => Set<CustomerLocation>();
    public DbSet<Demand> Demands => Set<Demand>();
    public DbSet<DemandActivity> DemandActivities => Set<DemandActivity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(organization =>
        {
            organization.ToTable("Organizations");
            organization.HasKey(value => value.Id);
            organization.Property(value => value.Name)
                .HasMaxLength(200)
                .IsRequired();
            organization.Property(value => value.IsActive)
                .HasDefaultValue(true)
                .IsRequired();
        });

        builder.Entity<Customer>(customer =>
        {
            customer.ToTable("Customers");
            customer.HasKey(value => value.Id);
            customer.Property(value => value.Name).HasMaxLength(200).IsRequired();
            customer.Property(value => value.Document).HasMaxLength(30);
            customer.Property(value => value.Instructions).HasMaxLength(2000);
            customer.Property(value => value.Address).HasMaxLength(500);
            customer.Property(value => value.Locality).HasMaxLength(200);
            customer.Property(value => value.City).HasMaxLength(200);
            customer.Property(value => value.State).HasMaxLength(200);
            customer.Property(value => value.CreatedAtUtc).IsRequired();
            customer.Property(value => value.UpdatedAtUtc).IsRequired();
            customer.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(value => value.OrganizationId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
            customer.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(value => value.CreatedByUserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
            customer.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(value => value.UpdatedByUserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
            customer.HasIndex(value => new { value.OrganizationId, value.Name });
            customer.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            customer.HasIndex(value => value.CreatedByUserId);
            customer.HasIndex(value => value.UpdatedByUserId);
        });

        builder.Entity<CustomerLocation>(location =>
        {
            location.ToTable("CustomerLocations");
            location.HasKey(value => value.Id);
            location.Property(value => value.Name).HasMaxLength(200).IsRequired();
            location.Property(value => value.Address).HasMaxLength(500).IsRequired();
            location.Property(value => value.Instructions).HasMaxLength(2000);
            location.Property(value => value.CreatedAtUtc).IsRequired();
            location.Property(value => value.UpdatedAtUtc).IsRequired();
            location.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
            location.HasOne<Customer>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CustomerId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id })
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
            location.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.CreatedByUserId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
            location.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UpdatedByUserId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
            location.HasIndex(value => new { value.OrganizationId, value.CustomerId, value.Name });
            location.HasAlternateKey(value => new { value.OrganizationId, value.CustomerId, value.Id });
            location.HasIndex(value => value.CreatedByUserId);
            location.HasIndex(value => value.UpdatedByUserId);
        });

        builder.Entity<Demand>(demand =>
        {
            demand.ToTable("Demands");
            demand.HasKey(value => value.Id);
            demand.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            demand.Property(value => value.Description).HasMaxLength(4000).IsRequired();
            demand.Property(value => value.Origin).HasMaxLength(30).IsRequired();
            demand.Property(value => value.Priority).HasMaxLength(20);
            demand.Property(value => value.Status).HasMaxLength(30).IsRequired();
            demand.Property(value => value.TicketReference).HasMaxLength(100);
            demand.Property(value => value.PurchaseOrderReference).HasMaxLength(100);
            demand.Property(value => value.CreatedAtUtc).IsRequired();
            demand.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
            demand.HasOne<Customer>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CustomerId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            demand.HasOne<CustomerLocation>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CustomerId, value.LocationId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.CustomerId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            demand.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CreatedByUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            demand.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.AssignedToUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            demand.HasIndex(value => new { value.OrganizationId, value.CreatedAtUtc });
            demand.HasIndex(value => new { value.OrganizationId, value.CustomerId });
            demand.HasIndex(value => new { value.OrganizationId, value.LocationId });
            demand.HasIndex(value => new { value.OrganizationId, value.AssignedToUserId });
        });

        builder.Entity<DemandActivity>(activity =>
        {
            activity.ToTable("DemandActivities");
            activity.HasKey(value => value.Id);
            activity.Property(value => value.EventType).HasMaxLength(40).IsRequired();
            activity.Property(value => value.Description).HasMaxLength(500).IsRequired();
            activity.Property(value => value.OccurredAtUtc).IsRequired();
            activity.HasOne<Demand>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.DemandId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            activity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.AuthorUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            activity.HasIndex(value => new { value.OrganizationId, value.DemandId, value.OccurredAtUtc });
        });

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(value => value.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            user.HasOne(value => value.Organization)
                .WithMany()
                .HasForeignKey(value => value.OrganizationId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            user.HasIndex(value => value.OrganizationId);
            user.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            user.HasIndex(value => value.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("EmailIndex");
            user.Property(value => value.IsActive)
                .HasDefaultValue(true)
                .IsRequired();
            user.Property(value => value.MustChangePassword)
                .HasDefaultValue(false)
                .IsRequired();
        });
    }
}
