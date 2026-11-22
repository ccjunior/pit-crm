using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandExternalReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemandExternalReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandExternalReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandExternalReferences_Demands_OrganizationId_DemandId",
                        columns: x => new { x.OrganizationId, x.DemandId },
                        principalTable: "Demands",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "DemandExternalReferences" ("Id", "OrganizationId", "DemandId", "Type", "Value")
                SELECT gen_random_uuid(), "OrganizationId", "Id", 'Chamado/OS', btrim("TicketReference")
                FROM "Demands"
                WHERE NULLIF(btrim("TicketReference"), '') IS NOT NULL;

                INSERT INTO "DemandExternalReferences" ("Id", "OrganizationId", "DemandId", "Type", "Value")
                SELECT gen_random_uuid(), "OrganizationId", "Id", 'Ordem de Compra', btrim("PurchaseOrderReference")
                FROM "Demands"
                WHERE NULLIF(btrim("PurchaseOrderReference"), '') IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "PurchaseOrderReference",
                table: "Demands");

            migrationBuilder.DropColumn(
                name: "TicketReference",
                table: "Demands");

            migrationBuilder.CreateIndex(
                name: "IX_DemandExternalReferences_OrganizationId_DemandId",
                table: "DemandExternalReferences",
                columns: new[] { "OrganizationId", "DemandId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PurchaseOrderReference",
                table: "Demands",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TicketReference",
                table: "Demands",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "DemandExternalReferences"
                        WHERE "Type" NOT IN ('Chamado/OS', 'Ordem de Compra')
                    ) OR EXISTS (
                        SELECT 1
                        FROM "DemandExternalReferences"
                        GROUP BY "OrganizationId", "DemandId", "Type"
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Demand external references cannot be represented by the legacy ticket and purchase order fields.';
                    END IF;
                END $$;

                UPDATE "Demands" AS demand
                SET "TicketReference" = reference."Value"
                FROM "DemandExternalReferences" AS reference
                WHERE reference."OrganizationId" = demand."OrganizationId"
                    AND reference."DemandId" = demand."Id"
                    AND reference."Type" = 'Chamado/OS';

                UPDATE "Demands" AS demand
                SET "PurchaseOrderReference" = reference."Value"
                FROM "DemandExternalReferences" AS reference
                WHERE reference."OrganizationId" = demand."OrganizationId"
                    AND reference."DemandId" = demand."Id"
                    AND reference."Type" = 'Ordem de Compra';
                """);

            migrationBuilder.DropTable(
                name: "DemandExternalReferences");
        }
    }
}
