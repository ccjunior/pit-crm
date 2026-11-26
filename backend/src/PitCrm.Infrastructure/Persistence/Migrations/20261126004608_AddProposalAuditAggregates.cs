using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalAuditAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProposalAuditAggregateQuarantines",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EventCount = table.Column<long>(type: "bigint", nullable: false),
                    QuarantinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalAuditAggregateQuarantines", x => new { x.OrganizationId, x.MonthStart, x.Category });
                    table.ForeignKey(
                        name: "FK_ProposalAuditAggregateQuarantines_Organizations_Organizatio~",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalAuditAggregateWindows",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    State = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    DistinctRequestCount = table.Column<long>(type: "bigint", nullable: false),
                    SealedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InvalidatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalAuditAggregateWindows", x => new { x.OrganizationId, x.MonthStart, x.Category });
                    table.ForeignKey(
                        name: "FK_ProposalAuditAggregateWindows_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalAuditAggregateContributions",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequestToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalAuditAggregateContributions", x => new { x.OrganizationId, x.MonthStart, x.Category, x.RequestToken });
                    table.ForeignKey(
                        name: "FK_ProposalAuditAggregateContributions_ProposalAuditAggregateW~",
                        columns: x => new { x.OrganizationId, x.MonthStart, x.Category },
                        principalTable: "ProposalAuditAggregateWindows",
                        principalColumns: new[] { "OrganizationId", "MonthStart", "Category" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAuditEvents_OccurredAtUtc",
                table: "ProposalAuditEvents",
                column: "OccurredAtUtc",
                filter: "\"RedactedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAuditAggregateWindows_State_MonthStart",
                table: "ProposalAuditAggregateWindows",
                columns: new[] { "State", "MonthStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalAuditAggregateContributions");

            migrationBuilder.DropTable(
                name: "ProposalAuditAggregateQuarantines");

            migrationBuilder.DropTable(
                name: "ProposalAuditAggregateWindows");

            migrationBuilder.DropIndex(
                name: "IX_ProposalAuditEvents_OccurredAtUtc",
                table: "ProposalAuditEvents");
        }
    }
}
