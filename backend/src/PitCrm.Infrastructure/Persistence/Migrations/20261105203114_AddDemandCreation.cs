using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_CustomerLocations_OrganizationId_CustomerId_Id",
                table: "CustomerLocations",
                columns: new[] { "OrganizationId", "CustomerId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AspNetUsers_OrganizationId_Id",
                table: "AspNetUsers",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateTable(
                name: "Demands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Origin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TicketReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PurchaseOrderReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Demands", x => x.Id);
                    table.UniqueConstraint("AK_Demands_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Demands_AspNetUsers_OrganizationId_AssignedToUserId",
                        columns: x => new { x.OrganizationId, x.AssignedToUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Demands_AspNetUsers_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Demands_CustomerLocations_OrganizationId_CustomerId_Locatio~",
                        columns: x => new { x.OrganizationId, x.CustomerId, x.LocationId },
                        principalTable: "CustomerLocations",
                        principalColumns: new[] { "OrganizationId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Demands_Customers_OrganizationId_CustomerId",
                        columns: x => new { x.OrganizationId, x.CustomerId },
                        principalTable: "Customers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Demands_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DemandActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandActivities_AspNetUsers_OrganizationId_AuthorUserId",
                        columns: x => new { x.OrganizationId, x.AuthorUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemandActivities_Demands_OrganizationId_DemandId",
                        columns: x => new { x.OrganizationId, x.DemandId },
                        principalTable: "Demands",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemandActivities_OrganizationId_AuthorUserId",
                table: "DemandActivities",
                columns: new[] { "OrganizationId", "AuthorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandActivities_OrganizationId_DemandId_OccurredAtUtc",
                table: "DemandActivities",
                columns: new[] { "OrganizationId", "DemandId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Demands_OrganizationId_AssignedToUserId",
                table: "Demands",
                columns: new[] { "OrganizationId", "AssignedToUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Demands_OrganizationId_CreatedAtUtc",
                table: "Demands",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Demands_OrganizationId_CreatedByUserId",
                table: "Demands",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Demands_OrganizationId_CustomerId",
                table: "Demands",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Demands_OrganizationId_CustomerId_LocationId",
                table: "Demands",
                columns: new[] { "OrganizationId", "CustomerId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Demands_OrganizationId_LocationId",
                table: "Demands",
                columns: new[] { "OrganizationId", "LocationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandActivities");

            migrationBuilder.DropTable(
                name: "Demands");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CustomerLocations_OrganizationId_CustomerId_Id",
                table: "CustomerLocations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AspNetUsers_OrganizationId_Id",
                table: "AspNetUsers");
        }
    }
}
