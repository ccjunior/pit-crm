using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalAuditAggregateMonths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProposalAuditAggregateMonths",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthStart = table.Column<DateOnly>(type: "date", nullable: false),
                    State = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    SealedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InvalidatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalAuditAggregateMonths", x => new { x.OrganizationId, x.MonthStart });
                    table.ForeignKey(
                        name: "FK_ProposalAuditAggregateMonths_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAuditAggregateMonths_State_MonthStart",
                table: "ProposalAuditAggregateMonths",
                columns: new[] { "State", "MonthStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalAuditAggregateMonths");
        }
    }
}
