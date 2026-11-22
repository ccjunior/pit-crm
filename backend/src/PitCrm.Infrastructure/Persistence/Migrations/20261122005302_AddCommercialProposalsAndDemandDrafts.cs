using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialProposalsAndDemandDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceProposalRevisionId",
                table: "Demands",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommercialRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProspectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProspectContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProspectEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    ProspectPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Origin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OutcomeReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialRequests", x => x.Id);
                    table.UniqueConstraint("AK_CommercialRequests_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommercialRequests_AspNetUsers_OrganizationId_CreatedByUser~",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialRequests_Customers_OrganizationId_CustomerId",
                        columns: x => new { x.OrganizationId, x.CustomerId },
                        principalTable: "Customers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialRequests_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommercialActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommercialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommercialActivities_AspNetUsers_OrganizationId_ActorUserId",
                        columns: x => new { x.OrganizationId, x.ActorUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialActivities_CommercialRequests_OrganizationId_Comm~",
                        columns: x => new { x.OrganizationId, x.CommercialRequestId },
                        principalTable: "CommercialRequests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProposalRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommercialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SendChannel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientApproverName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovalChannel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ApprovalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalRevisions", x => x.Id);
                    table.UniqueConstraint("AK_ProposalRevisions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProposalRevisions_AspNetUsers_OrganizationId_ApprovedByUser~",
                        columns: x => new { x.OrganizationId, x.ApprovedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalRevisions_AspNetUsers_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalRevisions_CommercialRequests_OrganizationId_Commerc~",
                        columns: x => new { x.OrganizationId, x.CommercialRequestId },
                        principalTable: "CommercialRequests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemandDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProspectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Origin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ConvertedDemandId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandDrafts", x => x.Id);
                    table.UniqueConstraint("AK_DemandDrafts_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_DemandDrafts_AspNetUsers_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemandDrafts_CommercialRequests_OrganizationId_ProposalRequ~",
                        columns: x => new { x.OrganizationId, x.ProposalRequestId },
                        principalTable: "CommercialRequests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemandDrafts_CustomerLocations_OrganizationId_CustomerId_Lo~",
                        columns: x => new { x.OrganizationId, x.CustomerId, x.LocationId },
                        principalTable: "CustomerLocations",
                        principalColumns: new[] { "OrganizationId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemandDrafts_Customers_OrganizationId_CustomerId",
                        columns: x => new { x.OrganizationId, x.CustomerId },
                        principalTable: "Customers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemandDrafts_Demands_OrganizationId_ConvertedDemandId",
                        columns: x => new { x.OrganizationId, x.ConvertedDemandId },
                        principalTable: "Demands",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemandDrafts_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemandDrafts_ProposalRevisions_OrganizationId_ProposalRevis~",
                        columns: x => new { x.OrganizationId, x.ProposalRevisionId },
                        principalTable: "ProposalRevisions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalItems", x => x.Id);
                    table.UniqueConstraint("AK_ProposalItems_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProposalItems_ProposalRevisions_OrganizationId_ProposalRevi~",
                        columns: x => new { x.OrganizationId, x.ProposalRevisionId },
                        principalTable: "ProposalRevisions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemandDraftItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceProposalItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandDraftItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandDraftItems_DemandDrafts_OrganizationId_DemandDraftId",
                        columns: x => new { x.OrganizationId, x.DemandDraftId },
                        principalTable: "DemandDrafts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemandSubitems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SourceProposalItemId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandSubitems", x => x.Id);
                    table.UniqueConstraint("AK_DemandSubitems_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_DemandSubitems_Demands_OrganizationId_DemandId",
                        columns: x => new { x.OrganizationId, x.DemandId },
                        principalTable: "Demands",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DemandSubitems_ProposalItems_OrganizationId_SourceProposalI~",
                        columns: x => new { x.OrganizationId, x.SourceProposalItemId },
                        principalTable: "ProposalItems",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Demands_OrganizationId_SourceProposalRevisionId",
                table: "Demands",
                columns: new[] { "OrganizationId", "SourceProposalRevisionId" },
                unique: true,
                filter: "\"SourceProposalRevisionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialActivities_OrganizationId_ActorUserId",
                table: "CommercialActivities",
                columns: new[] { "OrganizationId", "ActorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialActivities_OrganizationId_CommercialRequestId_Occ~",
                table: "CommercialActivities",
                columns: new[] { "OrganizationId", "CommercialRequestId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialRequests_OrganizationId_CreatedByUserId",
                table: "CommercialRequests",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialRequests_OrganizationId_CustomerId",
                table: "CommercialRequests",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialRequests_OrganizationId_Status_UpdatedAtUtc",
                table: "CommercialRequests",
                columns: new[] { "OrganizationId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandDraftItems_OrganizationId_DemandDraftId_Position",
                table: "DemandDraftItems",
                columns: new[] { "OrganizationId", "DemandDraftId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemandDraftItems_OrganizationId_SourceProposalItemId",
                table: "DemandDraftItems",
                columns: new[] { "OrganizationId", "SourceProposalItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandDrafts_OrganizationId_ConvertedDemandId",
                table: "DemandDrafts",
                columns: new[] { "OrganizationId", "ConvertedDemandId" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandDrafts_OrganizationId_CreatedByUserId",
                table: "DemandDrafts",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandDrafts_OrganizationId_CustomerId",
                table: "DemandDrafts",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandDrafts_OrganizationId_CustomerId_LocationId",
                table: "DemandDrafts",
                columns: new[] { "OrganizationId", "CustomerId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandDrafts_OrganizationId_ProposalRequestId",
                table: "DemandDrafts",
                columns: new[] { "OrganizationId", "ProposalRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemandDrafts_OrganizationId_ProposalRevisionId",
                table: "DemandDrafts",
                columns: new[] { "OrganizationId", "ProposalRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandDrafts_OrganizationId_State_CreatedAtUtc",
                table: "DemandDrafts",
                columns: new[] { "OrganizationId", "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandSubitems_OrganizationId_DemandId_Position",
                table: "DemandSubitems",
                columns: new[] { "OrganizationId", "DemandId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemandSubitems_OrganizationId_SourceProposalItemId",
                table: "DemandSubitems",
                columns: new[] { "OrganizationId", "SourceProposalItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalItems_OrganizationId_ProposalRevisionId_Position",
                table: "ProposalItems",
                columns: new[] { "OrganizationId", "ProposalRevisionId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalRevisions_OrganizationId_ApprovedByUserId",
                table: "ProposalRevisions",
                columns: new[] { "OrganizationId", "ApprovedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalRevisions_OrganizationId_CommercialRequestId_Number",
                table: "ProposalRevisions",
                columns: new[] { "OrganizationId", "CommercialRequestId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalRevisions_OrganizationId_CommercialRequestId_Status",
                table: "ProposalRevisions",
                columns: new[] { "OrganizationId", "CommercialRequestId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalRevisions_OrganizationId_CreatedByUserId",
                table: "ProposalRevisions",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Demands_ProposalRevisions_OrganizationId_SourceProposalRevi~",
                table: "Demands",
                columns: new[] { "OrganizationId", "SourceProposalRevisionId" },
                principalTable: "ProposalRevisions",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Demands_ProposalRevisions_OrganizationId_SourceProposalRevi~",
                table: "Demands");

            migrationBuilder.DropTable(
                name: "CommercialActivities");

            migrationBuilder.DropTable(
                name: "DemandDraftItems");

            migrationBuilder.DropTable(
                name: "DemandSubitems");

            migrationBuilder.DropTable(
                name: "DemandDrafts");

            migrationBuilder.DropTable(
                name: "ProposalItems");

            migrationBuilder.DropTable(
                name: "ProposalRevisions");

            migrationBuilder.DropTable(
                name: "CommercialRequests");

            migrationBuilder.DropIndex(
                name: "IX_Demands_OrganizationId_SourceProposalRevisionId",
                table: "Demands");

            migrationBuilder.DropColumn(
                name: "SourceProposalRevisionId",
                table: "Demands");
        }
    }
}
