using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationResponsibleContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleContactId",
                table: "CustomerLocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CustomerContacts_OrganizationId_CustomerId_Id",
                table: "CustomerContacts",
                columns: new[] { "OrganizationId", "CustomerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLocations_OrganizationId_CustomerId_ResponsibleCont~",
                table: "CustomerLocations",
                columns: new[] { "OrganizationId", "CustomerId", "ResponsibleContactId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerLocations_CustomerContacts_OrganizationId_CustomerI~",
                table: "CustomerLocations",
                columns: new[] { "OrganizationId", "CustomerId", "ResponsibleContactId" },
                principalTable: "CustomerContacts",
                principalColumns: new[] { "OrganizationId", "CustomerId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerLocations_CustomerContacts_OrganizationId_CustomerI~",
                table: "CustomerLocations");

            migrationBuilder.DropIndex(
                name: "IX_CustomerLocations_OrganizationId_CustomerId_ResponsibleCont~",
                table: "CustomerLocations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CustomerContacts_OrganizationId_CustomerId_Id",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "ResponsibleContactId",
                table: "CustomerLocations");
        }
    }
}
