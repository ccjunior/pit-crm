using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationDetailsAndCustomerContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Customers",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "CustomerLocations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Neighborhood",
                table: "CustomerLocations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CustomerLocations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.CreateTable(
                name: "CustomerContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_AspNetUsers_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_AspNetUsers_OrganizationId_UpdatedByUserId",
                        columns: x => new { x.OrganizationId, x.UpdatedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_CustomerLocations_OrganizationId_CustomerI~",
                        columns: x => new { x.OrganizationId, x.CustomerId, x.LocationId },
                        principalTable: "CustomerLocations",
                        principalColumns: new[] { "OrganizationId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_Customers_OrganizationId_CustomerId",
                        columns: x => new { x.OrganizationId, x.CustomerId },
                        principalTable: "Customers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_OrganizationId_CreatedByUserId",
                table: "CustomerContacts",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_OrganizationId_CustomerId_LocationId",
                table: "CustomerContacts",
                columns: new[] { "OrganizationId", "CustomerId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_OrganizationId_UpdatedByUserId",
                table: "CustomerContacts",
                columns: new[] { "OrganizationId", "UpdatedByUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "City",
                table: "CustomerLocations");

            migrationBuilder.DropColumn(
                name: "Neighborhood",
                table: "CustomerLocations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CustomerLocations");
        }
    }
}
