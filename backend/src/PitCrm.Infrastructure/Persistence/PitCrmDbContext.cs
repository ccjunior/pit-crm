using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PitCrm.Domain.Organizations;
using PitCrm.Domain.Customers;
using PitCrm.Domain.Locations;
using PitCrm.Domain.Demands;
using PitCrm.Domain.Proposals;
using PitCrm.Infrastructure.Identity;

namespace PitCrm.Infrastructure.Persistence;

public sealed class PitCrmDbContext(
    DbContextOptions<PitCrmDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerLocation> CustomerLocations => Set<CustomerLocation>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
    public DbSet<Demand> Demands => Set<Demand>();
    public DbSet<DemandExternalReference> DemandExternalReferences => Set<DemandExternalReference>();
    public DbSet<DemandActivity> DemandActivities => Set<DemandActivity>();
    public DbSet<DemandSubitem> DemandSubitems => Set<DemandSubitem>();
    public DbSet<DemandDraft> DemandDrafts => Set<DemandDraft>();
    public DbSet<DemandDraftItem> DemandDraftItems => Set<DemandDraftItem>();
    public DbSet<CommercialRequest> CommercialRequests => Set<CommercialRequest>();
    public DbSet<ProposalRevision> ProposalRevisions => Set<ProposalRevision>();
    public DbSet<ProposalItem> ProposalItems => Set<ProposalItem>();
    public DbSet<CommercialActivity> CommercialActivities => Set<CommercialActivity>();
    public DbSet<CommercialReference> CommercialReferences => Set<CommercialReference>();
    public DbSet<ProposalTimelineEvent> ProposalTimelineEvents => Set<ProposalTimelineEvent>();
    public DbSet<ProposalTimelineMention> ProposalTimelineMentions => Set<ProposalTimelineMention>();
    public DbSet<ProposalReason> ProposalReasons => Set<ProposalReason>();
    public DbSet<ProposalEventSequence> ProposalEventSequences => Set<ProposalEventSequence>();
    public DbSet<ProposalIdempotencyRecord> ProposalIdempotencyRecords => Set<ProposalIdempotencyRecord>();
    public DbSet<ProposalAuditEvent> ProposalAuditEvents => Set<ProposalAuditEvent>();
    public DbSet<ProposalRetentionCycle> ProposalRetentionCycles => Set<ProposalRetentionCycle>();
    public DbSet<ProposalTimelineArchive> ProposalTimelineArchives => Set<ProposalTimelineArchive>();
    public DbSet<ProposalLegacyQuarantine> ProposalLegacyQuarantine => Set<ProposalLegacyQuarantine>();
    public DbSet<ProposalHistoryMigrationReconciliation> ProposalHistoryMigrationReconciliations => Set<ProposalHistoryMigrationReconciliation>();
    public DbSet<ProposalAuditAggregate> ProposalAuditAggregates => Set<ProposalAuditAggregate>();

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
            customer.Property(value => value.PostalCode).HasMaxLength(8);
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
            location.Property(value => value.Neighborhood).HasMaxLength(200);
            location.Property(value => value.City).HasMaxLength(200);
            location.Property(value => value.MapUrl).HasMaxLength(2000);
            location.Property(value => value.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(CustomerLocationStatus.Active).IsRequired();
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
            location.HasOne<CustomerContact>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CustomerId, value.ResponsibleContactId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.CustomerId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            location.HasIndex(value => new { value.OrganizationId, value.CustomerId, value.ResponsibleContactId });
            location.HasIndex(value => value.CreatedByUserId);
            location.HasIndex(value => value.UpdatedByUserId);
        });

        builder.Entity<CustomerContact>(contact =>
        {
            contact.ToTable("CustomerContacts");
            contact.HasKey(value => value.Id);
            contact.HasAlternateKey(value => new { value.OrganizationId, value.CustomerId, value.Id });
            contact.Property(value => value.Name).HasMaxLength(200).IsRequired();
            contact.Property(value => value.Role).HasMaxLength(200);
            contact.Property(value => value.Phone).HasMaxLength(40);
            contact.Property(value => value.Email).HasMaxLength(254);
            contact.Property(value => value.IsPrimary).HasDefaultValue(false).IsRequired();
            contact.Property(value => value.CreatedAtUtc).IsRequired();
            contact.Property(value => value.UpdatedAtUtc).IsRequired();
            contact.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
            contact.HasOne<Customer>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CustomerId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            contact.HasOne<CustomerLocation>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CustomerId, value.LocationId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.CustomerId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            contact.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CreatedByUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            contact.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.UpdatedByUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            contact.HasIndex(value => new { value.OrganizationId, value.CustomerId, value.LocationId });
            contact.HasIndex(value => new { value.OrganizationId, value.CustomerId }).IsUnique()
                .HasFilter("\"IsPrimary\" = TRUE AND \"LocationId\" IS NULL");
            contact.HasIndex(value => new { value.OrganizationId, value.CustomerId, value.LocationId }).IsUnique()
                .HasFilter("\"IsPrimary\" = TRUE AND \"LocationId\" IS NOT NULL");
        });

        builder.Entity<Demand>(demand =>
        {
            demand.ToTable("Demands");
            demand.HasKey(value => value.Id);
            demand.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            demand.Property(value => value.ReferenceNumber).IsRequired();
            demand.Property(value => value.Version).IsConcurrencyToken().IsRequired();
            demand.Property(value => value.Description).HasMaxLength(4000).IsRequired();
            demand.Property(value => value.Origin).HasMaxLength(30).IsRequired();
            demand.Property(value => value.Priority).HasMaxLength(20);
            demand.Property(value => value.Status).HasMaxLength(30).IsRequired();
            demand.Property(value => value.SourceProposalRevisionId);
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
            demand.HasOne<ProposalRevision>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.SourceProposalRevisionId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            demand.HasIndex(value => new { value.OrganizationId, value.CreatedAtUtc });
            demand.HasIndex(value => new { value.OrganizationId, value.CustomerId });
            demand.HasIndex(value => new { value.OrganizationId, value.LocationId });
            demand.HasIndex(value => new { value.OrganizationId, value.AssignedToUserId });
            demand.HasIndex(value => new { value.OrganizationId, value.ReferenceNumber }).IsUnique();
            demand.HasIndex(value => new { value.OrganizationId, value.SourceProposalRevisionId }).IsUnique()
                .HasFilter("\"SourceProposalRevisionId\" IS NOT NULL");
        });

        builder.Entity<DemandSubitem>(subitem =>
        {
            subitem.ToTable("DemandSubitems");
            subitem.HasKey(value => value.Id);
            subitem.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            subitem.Property(value => value.Description).HasMaxLength(1000).IsRequired();
            subitem.Property(value => value.Amount).HasPrecision(18, 2);
            subitem.HasOne<Demand>().WithMany(value => value.Subitems)
                .HasForeignKey(value => new { value.OrganizationId, value.DemandId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Cascade);
            subitem.HasOne<ProposalItem>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.SourceProposalItemId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            subitem.HasIndex(value => new { value.OrganizationId, value.DemandId, value.Position }).IsUnique();
        });

        builder.Entity<DemandDraft>(draft =>
        {
            draft.ToTable("DemandDrafts");
            draft.HasKey(value => value.Id);
            draft.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            draft.Property(value => value.ProspectName).HasMaxLength(200);
            draft.Property(value => value.Description).HasMaxLength(4000).IsRequired();
            draft.Property(value => value.Origin).HasMaxLength(30).IsRequired();
            draft.Property(value => value.Priority).HasMaxLength(20);
            draft.Property(value => value.State).HasMaxLength(20).IsRequired();
            draft.Property(value => value.Version).IsConcurrencyToken().IsRequired();
            draft.Property(value => value.CreatedAtUtc).IsRequired();
            draft.Property(value => value.UpdatedAtUtc).IsRequired();
            draft.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
            draft.HasOne<CommercialRequest>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.ProposalRequestId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            draft.HasOne<ProposalRevision>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.ProposalRevisionId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            draft.HasOne<Customer>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CustomerId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            draft.HasOne<CustomerLocation>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CustomerId, value.LocationId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.CustomerId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            draft.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CreatedByUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            draft.HasOne<Demand>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.ConvertedDemandId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            draft.HasIndex(value => new { value.OrganizationId, value.ProposalRequestId }).IsUnique();
            draft.HasIndex(value => new { value.OrganizationId, value.State, value.CreatedAtUtc });
            draft.HasIndex(value => new { value.OrganizationId, value.CustomerId });
        });

        builder.Entity<DemandDraftItem>(item =>
        {
            item.ToTable("DemandDraftItems");
            item.HasKey(value => value.Id);
            item.Property(value => value.Description).HasMaxLength(1000).IsRequired();
            item.Property(value => value.Amount).HasPrecision(18, 2).IsRequired();
            item.HasOne<DemandDraft>().WithMany(value => value.Items)
                .HasForeignKey(value => new { value.OrganizationId, value.DemandDraftId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Cascade);
            item.HasIndex(value => new { value.OrganizationId, value.DemandDraftId, value.Position }).IsUnique();
            item.HasIndex(value => new { value.OrganizationId, value.SourceProposalItemId });
        });

        builder.Entity<CommercialRequest>(request =>
        {
            request.ToTable("CommercialRequests");
            request.HasKey(value => value.Id);
            request.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            request.Property(value => value.ProspectName).HasMaxLength(200);
            request.Property(value => value.ProspectContactName).HasMaxLength(200);
            request.Property(value => value.ProspectEmail).HasMaxLength(254);
            request.Property(value => value.ProspectPhone).HasMaxLength(40);
            request.Property(value => value.Origin).HasMaxLength(30).IsRequired();
            request.Property(value => value.SourceType).HasMaxLength(20).HasDefaultValue(CommercialRequest.ManualSource).IsRequired();
            request.Property(value => value.Number).HasMaxLength(24).IsRequired();
            request.Property(value => value.Description).HasMaxLength(4000).IsRequired();
            request.Property(value => value.Status).HasMaxLength(20).IsRequired();
            request.Property(value => value.OutcomeReason).HasMaxLength(1000);
            request.Property(value => value.Version).IsConcurrencyToken().IsRequired();
            request.Property(value => value.CreatedAtUtc).IsRequired();
            request.Property(value => value.UpdatedAtUtc).IsRequired();
            request.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
            request.HasOne<Customer>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CustomerId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            request.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CreatedByUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            request.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.ResponsibleUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            request.HasIndex(value => new { value.OrganizationId, value.Status, value.UpdatedAtUtc });
            request.HasIndex(value => new { value.OrganizationId, value.CustomerId });
            request.HasIndex(value => new { value.OrganizationId, value.Number }).IsUnique();
        });

        builder.Entity<CommercialReference>(reference =>
        {
            reference.ToTable("CommercialReferences");
            reference.HasKey(value => value.Id);
            reference.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            reference.Property(value => value.Type).HasMaxLength(60).IsRequired();
            reference.Property(value => value.Value).HasMaxLength(120).IsRequired();
            reference.HasOne<CommercialRequest>().WithMany(value => value.References)
                .HasForeignKey(value => new { value.OrganizationId, value.CommercialRequestId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Cascade);
            reference.HasIndex(value => new { value.OrganizationId, value.Value });
        });

        builder.Entity<ProposalRevision>(revision =>
        {
            revision.ToTable("ProposalRevisions");
            revision.HasKey(value => value.Id);
            revision.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            revision.Property(value => value.Status).HasMaxLength(20).IsRequired();
            revision.Property(value => value.Notes).HasMaxLength(4000);
            revision.Property(value => value.SendChannel).HasMaxLength(50);
            revision.Property(value => value.ClientApproverName).HasMaxLength(200);
            revision.Property(value => value.ApprovalChannel).HasMaxLength(50);
            revision.Property(value => value.ApprovalNotes).HasMaxLength(2000);
            revision.Property(value => value.CreatedAtUtc).IsRequired();
            revision.HasOne<CommercialRequest>().WithMany(value => value.Revisions)
                .HasForeignKey(value => new { value.OrganizationId, value.CommercialRequestId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Cascade);
            revision.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CreatedByUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            revision.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.ApprovedByUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            revision.HasIndex(value => new { value.OrganizationId, value.CommercialRequestId, value.Number }).IsUnique();
            revision.HasIndex(value => new { value.OrganizationId, value.CommercialRequestId, value.Status });
        });

        builder.Entity<ProposalItem>(item =>
        {
            item.ToTable("ProposalItems");
            item.HasKey(value => value.Id);
            item.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            item.Property(value => value.Description).HasMaxLength(1000).IsRequired();
            item.Property(value => value.Amount).HasPrecision(18, 2).IsRequired();
            item.HasOne<ProposalRevision>().WithMany(value => value.Items)
                .HasForeignKey(value => new { value.OrganizationId, value.ProposalRevisionId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Cascade);
            item.HasIndex(value => new { value.OrganizationId, value.ProposalRevisionId, value.Position }).IsUnique();
        });

        builder.Entity<CommercialActivity>(activity =>
        {
            activity.ToTable("CommercialActivities");
            activity.HasKey(value => value.Id);
            activity.Property(value => value.EventType).HasMaxLength(60).IsRequired();
            activity.Property(value => value.Description).HasMaxLength(1000).IsRequired();
            activity.Property(value => value.OccurredAtUtc).IsRequired();
            activity.HasOne<CommercialRequest>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.CommercialRequestId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Cascade);
            activity.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => new { value.OrganizationId, value.ActorUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            activity.HasIndex(value => new { value.OrganizationId, value.CommercialRequestId, value.OccurredAtUtc });
        });

        builder.Entity<ProposalTimelineEvent>(timelineEvent =>
        {
            timelineEvent.ToTable("ProposalTimelineEvents", table =>
                table.HasCheckConstraint("CK_ProposalTimelineEvents_Actor", "(\"ActorType\" = 'Human' AND \"ActorUserId\" IS NOT NULL AND \"SystemCode\" IS NULL) OR (\"ActorType\" = 'System' AND \"ActorUserId\" IS NULL AND \"SystemCode\" IS NOT NULL)"));
            timelineEvent.HasKey(value => value.Id);
            timelineEvent.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            timelineEvent.Property(value => value.EventType).HasMaxLength(40).IsRequired();
            timelineEvent.Property(value => value.ActorType).HasMaxLength(10).IsRequired();
            timelineEvent.Property(value => value.SystemCode).HasMaxLength(80);
            timelineEvent.Property(value => value.FromStatus).HasMaxLength(20);
            timelineEvent.Property(value => value.ToStatus).HasMaxLength(20);
            timelineEvent.Property(value => value.ReasonLabelSnapshot).HasMaxLength(120);
            timelineEvent.Property(value => value.ActivityType).HasMaxLength(60);
            timelineEvent.Property(value => value.Text).HasMaxLength(4000);
            timelineEvent.Property(value => value.Sequence).IsRequired();
            timelineEvent.Property(value => value.OccurredAtUtc).IsRequired();
            timelineEvent.HasOne<CommercialRequest>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.CommercialRequestId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            timelineEvent.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.ActorUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            timelineEvent.HasOne<ProposalReason>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.ReasonId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            timelineEvent.HasIndex(value => new { value.OrganizationId, value.CommercialRequestId, value.Sequence }).IsUnique();
            timelineEvent.HasIndex(value => new { value.OrganizationId, value.CommercialRequestId, value.OccurredAtUtc, value.Id });
            timelineEvent.HasIndex(value => new { value.OrganizationId, value.SourceLegacyId }).IsUnique()
                .HasFilter("\"SourceLegacyId\" IS NOT NULL");
        });

        builder.Entity<ProposalTimelineMention>(mention =>
        {
            mention.ToTable("ProposalTimelineMentions");
            mention.HasKey(value => value.Id);
            mention.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            mention.HasOne<ProposalTimelineEvent>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.EventId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Cascade);
            mention.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.MentionedUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            mention.HasIndex(value => new { value.OrganizationId, value.EventId, value.MentionedUserId }).IsUnique();
        });

        builder.Entity<ProposalReason>(reason =>
        {
            reason.ToTable("ProposalReasons");
            reason.HasKey(value => value.Id);
            reason.HasAlternateKey(value => new { value.OrganizationId, value.Id });
            reason.Property(value => value.ResultStatus).HasMaxLength(20).IsRequired();
            reason.Property(value => value.Label).HasMaxLength(120).IsRequired();
            reason.Property(value => value.IsActive).IsRequired();
            reason.Property(value => value.CreatedAtUtc).IsRequired();
            reason.Property(value => value.DeactivatedAtUtc);
            reason.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
            reason.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.CreatedByUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            reason.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.DeactivatedByUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            reason.HasIndex(value => new { value.OrganizationId, value.ResultStatus, value.Label }).IsUnique();
            reason.HasIndex(value => new { value.OrganizationId, value.ResultStatus, value.IsActive });
        });

        builder.Entity<ProposalEventSequence>(sequence =>
        {
            sequence.ToTable("ProposalEventSequences");
            sequence.HasKey(value => new { value.OrganizationId, value.CommercialRequestId });
            sequence.Property(value => value.LastSequence).IsRequired();
            sequence.HasOne<CommercialRequest>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.CommercialRequestId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProposalIdempotencyRecord>(record =>
        {
            record.ToTable("ProposalIdempotencyRecords");
            record.HasKey(value => new { value.OrganizationId, value.Key });
            record.Property(value => value.TargetType).HasMaxLength(30).IsRequired();
            record.Property(value => value.Operation).HasMaxLength(60).IsRequired();
            record.Property(value => value.Fingerprint).HasMaxLength(64).IsRequired();
            record.Property(value => value.KeyTimestampUtc).IsRequired();
            record.Property(value => value.StoredAtUtc).IsRequired();
            record.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
            record.HasIndex(value => value.KeyTimestampUtc);
        });

        builder.Entity<ProposalAuditEvent>(audit =>
        {
            audit.ToTable("ProposalAuditEvents", table =>
                table.HasCheckConstraint("CK_ProposalAuditEvents_Actor", "(\"ActorType\" = 'Human' AND \"ActorUserId\" IS NOT NULL AND \"SystemCode\" IS NULL) OR (\"ActorType\" = 'System' AND \"ActorUserId\" IS NULL AND \"SystemCode\" IS NOT NULL) OR (\"ActorType\" = 'Redacted' AND \"ActorUserId\" IS NULL AND \"SystemCode\" IS NULL)"));
            audit.HasKey(value => value.Id);
            audit.Property(value => value.EventType).HasMaxLength(40).IsRequired();
            audit.Property(value => value.Category).HasMaxLength(40).IsRequired();
            audit.Property(value => value.ActorType).HasMaxLength(10).IsRequired();
            audit.Property(value => value.SystemCode).HasMaxLength(80);
            audit.Property(value => value.OccurredAtUtc).IsRequired();
            audit.Property(value => value.DetailsJson).HasColumnType("jsonb");
            audit.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
            audit.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.ActorUserId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            audit.HasOne<CommercialRequest>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.CommercialRequestId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).OnDelete(DeleteBehavior.Restrict);
            audit.HasIndex(value => new { value.OrganizationId, value.OccurredAtUtc });
            audit.HasIndex(value => new { value.OrganizationId, value.Category, value.OccurredAtUtc });
        });

        builder.Entity<ProposalRetentionCycle>(cycle =>
        {
            cycle.ToTable("ProposalRetentionCycles");
            cycle.HasKey(value => new { value.OrganizationId, value.CommercialRequestId, value.Generation });
            cycle.Property(value => value.State).HasMaxLength(12).IsRequired();
            cycle.Property(value => value.ClosedAtUtc).IsRequired();
            cycle.Property(value => value.ArchiveAtUtc).IsRequired();
            cycle.HasOne<CommercialRequest>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.CommercialRequestId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
            cycle.HasIndex(value => new { value.State, value.ArchiveAtUtc });
            cycle.HasIndex(value => new { value.State, value.DiscardAtUtc });
        });

        builder.Entity<ProposalTimelineArchive>(archive =>
        {
            archive.ToTable("ProposalTimelineArchives");
            archive.HasKey(value => new { value.OrganizationId, value.CommercialRequestId, value.Generation });
            archive.Property(value => value.PayloadJson).HasColumnType("jsonb").IsRequired();
            archive.Property(value => value.ArchivedAtUtc).IsRequired();
            archive.Property(value => value.DiscardAtUtc).IsRequired();
            archive.HasOne<ProposalRetentionCycle>().WithMany()
                .HasForeignKey(value => new { value.OrganizationId, value.CommercialRequestId, value.Generation })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProposalLegacyQuarantine>(quarantine =>
        {
            quarantine.ToTable("ProposalLegacyQuarantine");
            quarantine.HasKey(value => value.SourceId);
            quarantine.Property(value => value.EventType).HasMaxLength(60).IsRequired();
            quarantine.Property(value => value.Description).HasMaxLength(1000).IsRequired();
            quarantine.Property(value => value.QuarantineReason).HasMaxLength(300).IsRequired();
            quarantine.Property(value => value.OccurredAtUtc).IsRequired();
        });

        builder.Entity<ProposalHistoryMigrationReconciliation>(reconciliation =>
        {
            reconciliation.ToTable("ProposalHistoryMigrationReconciliations");
            reconciliation.HasKey(value => value.OrganizationId);
            reconciliation.Property(value => value.SourceCount).IsRequired();
            reconciliation.Property(value => value.MigratedCount).IsRequired();
            reconciliation.Property(value => value.QuarantinedCount).IsRequired();
            reconciliation.Property(value => value.ReconciledAtUtc).IsRequired();
            reconciliation.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProposalAuditAggregate>(aggregate =>
        {
            aggregate.ToTable("ProposalAuditAggregates", table => table.HasCheckConstraint("CK_ProposalAuditAggregates_MinCount", "\"Count\" >= 10"));
            aggregate.HasKey(value => new { value.OrganizationId, value.Year, value.Month, value.Category });
            aggregate.Property(value => value.Month).IsRequired();
            aggregate.Property(value => value.Category).HasMaxLength(40).IsRequired();
            aggregate.Property(value => value.Count).IsRequired();
            aggregate.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DemandExternalReference>(reference =>
        {
            reference.ToTable("DemandExternalReferences");
            reference.HasKey(value => value.Id);
            reference.Property(value => value.Type).HasMaxLength(50).IsRequired();
            reference.Property(value => value.Value).HasMaxLength(100).IsRequired();
            reference.HasOne<Demand>().WithMany(value => value.ExternalReferences)
                .HasForeignKey(value => new { value.OrganizationId, value.DemandId })
                .HasPrincipalKey(value => new { value.OrganizationId, value.Id })
                .IsRequired().OnDelete(DeleteBehavior.Cascade);
            reference.HasIndex(value => new { value.OrganizationId, value.DemandId });
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
