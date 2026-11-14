using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrimaryCustomerContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerContacts_OrganizationId_CustomerId_LocationId",
                table: "CustomerContacts");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "CustomerContacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_OrganizationId_CustomerId",
                table: "CustomerContacts",
                columns: new[] { "OrganizationId", "CustomerId" },
                unique: true,
                filter: "\"IsPrimary\" = TRUE AND \"LocationId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_OrganizationId_CustomerId_LocationId",
                table: "CustomerContacts",
                columns: new[] { "OrganizationId", "CustomerId", "LocationId" },
                unique: true,
                filter: "\"IsPrimary\" = TRUE AND \"LocationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerContacts_OrganizationId_CustomerId",
                table: "CustomerContacts");

            migrationBuilder.DropIndex(
                name: "IX_CustomerContacts_OrganizationId_CustomerId_LocationId",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "CustomerContacts");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_OrganizationId_CustomerId_LocationId",
                table: "CustomerContacts",
                columns: new[] { "OrganizationId", "CustomerId", "LocationId" });
        }
    }
}
