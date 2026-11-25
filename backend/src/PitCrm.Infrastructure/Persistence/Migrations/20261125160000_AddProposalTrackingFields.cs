using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PitCrm.Infrastructure.Persistence;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PitCrmDbContext))]
[Migration("20261125160000_AddProposalTrackingFields")]
public sealed class AddProposalTrackingFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>("CustomerDeadline", "CommercialRequests", type: "date", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("InternalDeadline", "CommercialRequests", type: "date", nullable: true);
        migrationBuilder.AddColumn<string>("NextActionDescription", "CommercialRequests", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("NextActionAtUtc", "CommercialRequests", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<Guid>("ReminderOccurrenceId", "CommercialRequests", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<int>("ReminderOccurrenceRevision", "CommercialRequests", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.CreateIndex("IX_CommercialRequests_OrganizationId_CustomerDeadline", "CommercialRequests", new[] { "OrganizationId", "CustomerDeadline" });
        migrationBuilder.CreateIndex("IX_CommercialRequests_OrganizationId_InternalDeadline", "CommercialRequests", new[] { "OrganizationId", "InternalDeadline" });
        migrationBuilder.CreateIndex("IX_CommercialRequests_OrganizationId_ResponsibleUserId_NextActionAtUtc", "CommercialRequests", new[] { "OrganizationId", "ResponsibleUserId", "NextActionAtUtc" });
        migrationBuilder.AddCheckConstraint("CK_CommercialRequests_NextAction_Complete", "CommercialRequests",
            "(\"NextActionDescription\" IS NULL AND \"NextActionAtUtc\" IS NULL AND \"ReminderOccurrenceId\" IS NULL) OR (\"NextActionDescription\" IS NOT NULL AND \"NextActionAtUtc\" IS NOT NULL AND \"ReminderOccurrenceId\" IS NOT NULL)");
        migrationBuilder.AddCheckConstraint("CK_CommercialRequests_ReminderOccurrenceRevision_NonNegative", "CommercialRequests",
            "\"ReminderOccurrenceRevision\" >= 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_CommercialRequests_NextAction_Complete", "CommercialRequests");
        migrationBuilder.DropCheckConstraint("CK_CommercialRequests_ReminderOccurrenceRevision_NonNegative", "CommercialRequests");
        migrationBuilder.DropIndex("IX_CommercialRequests_OrganizationId_CustomerDeadline", "CommercialRequests");
        migrationBuilder.DropIndex("IX_CommercialRequests_OrganizationId_InternalDeadline", "CommercialRequests");
        migrationBuilder.DropIndex("IX_CommercialRequests_OrganizationId_ResponsibleUserId_NextActionAtUtc", "CommercialRequests");
        migrationBuilder.DropColumn("CustomerDeadline", "CommercialRequests");
        migrationBuilder.DropColumn("InternalDeadline", "CommercialRequests");
        migrationBuilder.DropColumn("NextActionDescription", "CommercialRequests");
        migrationBuilder.DropColumn("NextActionAtUtc", "CommercialRequests");
        migrationBuilder.DropColumn("ReminderOccurrenceId", "CommercialRequests");
        migrationBuilder.DropColumn("ReminderOccurrenceRevision", "CommercialRequests");
    }
}
