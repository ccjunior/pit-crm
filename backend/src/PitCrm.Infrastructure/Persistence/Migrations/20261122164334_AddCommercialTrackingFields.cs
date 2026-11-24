using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Number",
                table: "CommercialRequests",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleUserId",
                table: "CommercialRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "CommercialRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT "Id", "OrganizationId", EXTRACT(YEAR FROM "CreatedAtUtc" AT TIME ZONE 'UTC')::integer AS year,
                           ROW_NUMBER() OVER (PARTITION BY "OrganizationId", EXTRACT(YEAR FROM "CreatedAtUtc" AT TIME ZONE 'UTC') ORDER BY "CreatedAtUtc", "Id") AS sequence
                    FROM "CommercialRequests"
                )
                UPDATE "CommercialRequests" AS request
                SET "Number" = 'SOL-' || numbered.year::text || '-' || LPAD(numbered.sequence::text, 4, '0')
                FROM numbered
                WHERE request."Id" = numbered."Id";

                UPDATE "CommercialRequests" SET "Origin" = 'Telefone' WHERE "Origin" = 'Ligação';
                UPDATE "CommercialRequests" SET "Origin" = 'Outro' WHERE "Origin" = 'Outra';
                """);

            migrationBuilder.CreateTable(
                name: "CommercialReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommercialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Value = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialReferences", x => x.Id);
                    table.UniqueConstraint("AK_CommercialReferences_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommercialReferences_CommercialRequests_OrganizationId_Comm~",
                        columns: x => new { x.OrganizationId, x.CommercialRequestId },
                        principalTable: "CommercialRequests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialRequests_OrganizationId_Number",
                table: "CommercialRequests",
                columns: new[] { "OrganizationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommercialRequests_OrganizationId_ResponsibleUserId",
                table: "CommercialRequests",
                columns: new[] { "OrganizationId", "ResponsibleUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialReferences_OrganizationId_CommercialRequestId",
                table: "CommercialReferences",
                columns: new[] { "OrganizationId", "CommercialRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialReferences_OrganizationId_Value",
                table: "CommercialReferences",
                columns: new[] { "OrganizationId", "Value" });

            migrationBuilder.AddForeignKey(
                name: "FK_CommercialRequests_AspNetUsers_OrganizationId_ResponsibleUs~",
                table: "CommercialRequests",
                columns: new[] { "OrganizationId", "ResponsibleUserId" },
                principalTable: "AspNetUsers",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommercialRequests_AspNetUsers_OrganizationId_ResponsibleUs~",
                table: "CommercialRequests");

            migrationBuilder.DropTable(
                name: "CommercialReferences");

            migrationBuilder.DropIndex(
                name: "IX_CommercialRequests_OrganizationId_Number",
                table: "CommercialRequests");

            migrationBuilder.DropIndex(
                name: "IX_CommercialRequests_OrganizationId_ResponsibleUserId",
                table: "CommercialRequests");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "CommercialRequests");

            migrationBuilder.DropColumn(
                name: "ResponsibleUserId",
                table: "CommercialRequests");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "CommercialRequests");
        }
    }
}
