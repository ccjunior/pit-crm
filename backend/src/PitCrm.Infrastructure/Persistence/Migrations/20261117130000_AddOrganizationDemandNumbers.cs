using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PitCrm.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PitCrmDbContext))]
[Migration("20261117130000_AddOrganizationDemandNumbers")]
public sealed class AddOrganizationDemandNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ReferenceNumber",
            table: "Demands",
            type: "integer",
            nullable: true);

        migrationBuilder.Sql("""
            WITH numbered AS (
                SELECT "Id", row_number() OVER (PARTITION BY "OrganizationId" ORDER BY "CreatedAtUtc", "Id") AS number
                FROM "Demands"
            )
            UPDATE "Demands" AS demand
            SET "ReferenceNumber" = numbered.number
            FROM numbered
            WHERE demand."Id" = numbered."Id";
            """);

        migrationBuilder.AlterColumn<int>(
            name: "ReferenceNumber",
            table: "Demands",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.CreateTable(
            name: "DemandNumberCounters",
            columns: table => new
            {
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                LastIssuedNumber = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DemandNumberCounters", value => value.OrganizationId);
                table.ForeignKey(
                    name: "FK_DemandNumberCounters_Organizations_OrganizationId",
                    column: value => value.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql("""
            INSERT INTO "DemandNumberCounters" ("OrganizationId", "LastIssuedNumber")
            SELECT "OrganizationId", MAX("ReferenceNumber")
            FROM "Demands"
            GROUP BY "OrganizationId";
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Demands_OrganizationId_ReferenceNumber",
            table: "Demands",
            columns: new[] { "OrganizationId", "ReferenceNumber" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DemandNumberCounters");
        migrationBuilder.DropIndex(name: "IX_Demands_OrganizationId_ReferenceNumber", table: "Demands");
        migrationBuilder.DropColumn(name: "ReferenceNumber", table: "Demands");
    }
}
