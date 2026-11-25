using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProposalAuditAggregates",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalAuditAggregates", x => new { x.OrganizationId, x.Year, x.Month, x.Category });
                    table.CheckConstraint("CK_ProposalAuditAggregates_MinCount", "\"Count\" >= 10");
                    table.ForeignKey(
                        name: "FK_ProposalAuditAggregates_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommercialRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    TimelineEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: true),
                    RedactedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalAuditEvents", x => x.Id);
                    table.CheckConstraint("CK_ProposalAuditEvents_Actor", "(\"ActorType\" = 'Human' AND \"ActorUserId\" IS NOT NULL AND \"SystemCode\" IS NULL) OR (\"ActorType\" = 'System' AND \"ActorUserId\" IS NULL AND \"SystemCode\" IS NOT NULL) OR (\"ActorType\" = 'Redacted' AND \"ActorUserId\" IS NULL AND \"SystemCode\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ProposalAuditEvents_AspNetUsers_OrganizationId_ActorUserId",
                        columns: x => new { x.OrganizationId, x.ActorUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalAuditEvents_CommercialRequests_OrganizationId_Comme~",
                        columns: x => new { x.OrganizationId, x.CommercialRequestId },
                        principalTable: "CommercialRequests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalAuditEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalEventSequences",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommercialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalEventSequences", x => new { x.OrganizationId, x.CommercialRequestId });
                    table.ForeignKey(
                        name: "FK_ProposalEventSequences_CommercialRequests_OrganizationId_Co~",
                        columns: x => new { x.OrganizationId, x.CommercialRequestId },
                        principalTable: "CommercialRequests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalHistoryMigrationReconciliations",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCount = table.Column<int>(type: "integer", nullable: false),
                    MigratedCount = table.Column<int>(type: "integer", nullable: false),
                    QuarantinedCount = table.Column<int>(type: "integer", nullable: false),
                    ReconciledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalHistoryMigrationReconciliations", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_ProposalHistoryMigrationReconciliations_Organizations_Organ~",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalIdempotencyRecords",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyTimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StoredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalIdempotencyRecords", x => new { x.OrganizationId, x.Key });
                    table.ForeignKey(
                        name: "FK_ProposalIdempotencyRecords_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalLegacyQuarantine",
                columns: table => new
                {
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommercialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    QuarantineReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalLegacyQuarantine", x => x.SourceId);
                });

            migrationBuilder.CreateTable(
                name: "ProposalReasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeactivatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeactivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalReasons", x => x.Id);
                    table.UniqueConstraint("AK_ProposalReasons_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProposalReasons_AspNetUsers_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalReasons_AspNetUsers_OrganizationId_DeactivatedByUse~",
                        columns: x => new { x.OrganizationId, x.DeactivatedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalReasons_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalRetentionCycles",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommercialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtKnown = table.Column<bool>(type: "boolean", nullable: false),
                    ArchiveAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DiscardAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalRetentionCycles", x => new { x.OrganizationId, x.CommercialRequestId, x.Generation });
                    table.ForeignKey(
                        name: "FK_ProposalRetentionCycles_CommercialRequests_OrganizationId_C~",
                        columns: x => new { x.OrganizationId, x.CommercialRequestId },
                        principalTable: "CommercialRequests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalTimelineEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommercialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OccurredAtKnown = table.Column<bool>(type: "boolean", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReasonLabelSnapshot = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ActivityType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SourceLegacyId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalTimelineEvents", x => x.Id);
                    table.UniqueConstraint("AK_ProposalTimelineEvents_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_ProposalTimelineEvents_Actor", "(\"ActorType\" = 'Human' AND \"ActorUserId\" IS NOT NULL AND \"SystemCode\" IS NULL) OR (\"ActorType\" = 'System' AND \"ActorUserId\" IS NULL AND \"SystemCode\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProposalTimelineEvents_AspNetUsers_OrganizationId_ActorUser~",
                        columns: x => new { x.OrganizationId, x.ActorUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalTimelineEvents_CommercialRequests_OrganizationId_Co~",
                        columns: x => new { x.OrganizationId, x.CommercialRequestId },
                        principalTable: "CommercialRequests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalTimelineEvents_ProposalReasons_OrganizationId_Reaso~",
                        columns: x => new { x.OrganizationId, x.ReasonId },
                        principalTable: "ProposalReasons",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalTimelineArchives",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommercialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DiscardAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalTimelineArchives", x => new { x.OrganizationId, x.CommercialRequestId, x.Generation });
                    table.ForeignKey(
                        name: "FK_ProposalTimelineArchives_ProposalRetentionCycles_Organizati~",
                        columns: x => new { x.OrganizationId, x.CommercialRequestId, x.Generation },
                        principalTable: "ProposalRetentionCycles",
                        principalColumns: new[] { "OrganizationId", "CommercialRequestId", "Generation" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalTimelineMentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentionedUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalTimelineMentions", x => x.Id);
                    table.UniqueConstraint("AK_ProposalTimelineMentions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProposalTimelineMentions_AspNetUsers_OrganizationId_Mention~",
                        columns: x => new { x.OrganizationId, x.MentionedUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalTimelineMentions_ProposalTimelineEvents_Organizatio~",
                        columns: x => new { x.OrganizationId, x.EventId },
                        principalTable: "ProposalTimelineEvents",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAuditEvents_OrganizationId_ActorUserId",
                table: "ProposalAuditEvents",
                columns: new[] { "OrganizationId", "ActorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAuditEvents_OrganizationId_Category_OccurredAtUtc",
                table: "ProposalAuditEvents",
                columns: new[] { "OrganizationId", "Category", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAuditEvents_OrganizationId_CommercialRequestId",
                table: "ProposalAuditEvents",
                columns: new[] { "OrganizationId", "CommercialRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAuditEvents_OrganizationId_OccurredAtUtc",
                table: "ProposalAuditEvents",
                columns: new[] { "OrganizationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalIdempotencyRecords_KeyTimestampUtc",
                table: "ProposalIdempotencyRecords",
                column: "KeyTimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalReasons_OrganizationId_CreatedByUserId",
                table: "ProposalReasons",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalReasons_OrganizationId_DeactivatedByUserId",
                table: "ProposalReasons",
                columns: new[] { "OrganizationId", "DeactivatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalReasons_OrganizationId_ResultStatus_IsActive",
                table: "ProposalReasons",
                columns: new[] { "OrganizationId", "ResultStatus", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalReasons_OrganizationId_ResultStatus_Label",
                table: "ProposalReasons",
                columns: new[] { "OrganizationId", "ResultStatus", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalRetentionCycles_State_ArchiveAtUtc",
                table: "ProposalRetentionCycles",
                columns: new[] { "State", "ArchiveAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalRetentionCycles_State_DiscardAtUtc",
                table: "ProposalRetentionCycles",
                columns: new[] { "State", "DiscardAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalTimelineEvents_OrganizationId_ActorUserId",
                table: "ProposalTimelineEvents",
                columns: new[] { "OrganizationId", "ActorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalTimelineEvents_OrganizationId_CommercialRequestId_O~",
                table: "ProposalTimelineEvents",
                columns: new[] { "OrganizationId", "CommercialRequestId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalTimelineEvents_OrganizationId_CommercialRequestId_S~",
                table: "ProposalTimelineEvents",
                columns: new[] { "OrganizationId", "CommercialRequestId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalTimelineEvents_OrganizationId_ReasonId",
                table: "ProposalTimelineEvents",
                columns: new[] { "OrganizationId", "ReasonId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalTimelineEvents_OrganizationId_SourceLegacyId",
                table: "ProposalTimelineEvents",
                columns: new[] { "OrganizationId", "SourceLegacyId" },
                unique: true,
                filter: "\"SourceLegacyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalTimelineMentions_OrganizationId_EventId_MentionedUs~",
                table: "ProposalTimelineMentions",
                columns: new[] { "OrganizationId", "EventId", "MentionedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalTimelineMentions_OrganizationId_MentionedUserId",
                table: "ProposalTimelineMentions",
                columns: new[] { "OrganizationId", "MentionedUserId" });

            migrationBuilder.Sql("""
                DO $$
                DECLARE migration_now timestamptz := clock_timestamp();
                BEGIN
                    INSERT INTO "ProposalLegacyQuarantine" (
                        "SourceId", "OrganizationId", "CommercialRequestId", "ActorUserId",
                        "EventType", "Description", "OccurredAtUtc", "QuarantineReason")
                    SELECT activity."Id", activity."OrganizationId", activity."CommercialRequestId",
                           activity."ActorUserId", activity."EventType", activity."Description",
                           activity."OccurredAtUtc", 'Vínculo legado sem organização, proposta ou ator correspondente.'
                    FROM "CommercialActivities" activity
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "CommercialRequests" request
                        WHERE request."OrganizationId" = activity."OrganizationId"
                          AND request."Id" = activity."CommercialRequestId")
                       OR NOT EXISTS (
                        SELECT 1 FROM "AspNetUsers" actor
                        WHERE actor."OrganizationId" = activity."OrganizationId"
                          AND actor."Id" = activity."ActorUserId")
                    ON CONFLICT ("SourceId") DO NOTHING;

                    WITH valid AS (
                        SELECT activity.* FROM "CommercialActivities" activity
                        WHERE EXISTS (
                            SELECT 1 FROM "CommercialRequests" request
                            WHERE request."OrganizationId" = activity."OrganizationId"
                              AND request."Id" = activity."CommercialRequestId")
                          AND EXISTS (
                            SELECT 1 FROM "AspNetUsers" actor
                            WHERE actor."OrganizationId" = activity."OrganizationId"
                              AND actor."Id" = activity."ActorUserId")
                    ), numbered AS (
                        SELECT valid.*, row_number() OVER (
                            PARTITION BY valid."OrganizationId", valid."CommercialRequestId"
                            ORDER BY valid."OccurredAtUtc", valid."Id") AS event_sequence
                        FROM valid
                    )
                    INSERT INTO "ProposalTimelineEvents" (
                        "Id", "OrganizationId", "CommercialRequestId", "Sequence", "EventType",
                        "ActorType", "ActorUserId", "SystemCode", "OccurredAtUtc", "OccurredAtKnown",
                        "FromStatus", "ToStatus", "ReasonId", "ReasonLabelSnapshot", "ActivityType",
                        "Text", "SourceLegacyId")
                    SELECT numbered."Id", numbered."OrganizationId", numbered."CommercialRequestId",
                           numbered.event_sequence, 'LegacyActivity', 'Human', numbered."ActorUserId", NULL,
                           numbered."OccurredAtUtc", TRUE, NULL, NULL, NULL, NULL, numbered."EventType",
                           numbered."Description", numbered."Id"
                    FROM numbered
                    ON CONFLICT ("Id") DO NOTHING;

                    INSERT INTO "ProposalEventSequences" ("OrganizationId", "CommercialRequestId", "LastSequence")
                    SELECT event."OrganizationId", event."CommercialRequestId", max(event."Sequence")
                    FROM "ProposalTimelineEvents" event
                    WHERE event."SourceLegacyId" IS NOT NULL
                    GROUP BY event."OrganizationId", event."CommercialRequestId"
                    ON CONFLICT ("OrganizationId", "CommercialRequestId") DO UPDATE
                    SET "LastSequence" = GREATEST("ProposalEventSequences"."LastSequence", EXCLUDED."LastSequence");

                    INSERT INTO "ProposalHistoryMigrationReconciliations" (
                        "OrganizationId", "SourceCount", "MigratedCount", "QuarantinedCount", "ReconciledAtUtc")
                    SELECT organization."Id",
                           (SELECT count(*)::integer FROM "CommercialActivities" activity
                            WHERE activity."OrganizationId" = organization."Id"),
                           (SELECT count(*)::integer FROM "ProposalTimelineEvents" event
                            WHERE event."OrganizationId" = organization."Id" AND event."SourceLegacyId" IS NOT NULL),
                           (SELECT count(*)::integer FROM "ProposalLegacyQuarantine" item
                            WHERE item."OrganizationId" = organization."Id"),
                           migration_now
                    FROM "Organizations" organization
                    ON CONFLICT ("OrganizationId") DO UPDATE SET
                        "SourceCount" = EXCLUDED."SourceCount",
                        "MigratedCount" = EXCLUDED."MigratedCount",
                        "QuarantinedCount" = EXCLUDED."QuarantinedCount",
                        "ReconciledAtUtc" = EXCLUDED."ReconciledAtUtc";

                    IF EXISTS (
                        SELECT 1 FROM "ProposalHistoryMigrationReconciliations"
                        WHERE "SourceCount" <> "MigratedCount" + "QuarantinedCount") THEN
                        RAISE EXCEPTION 'Proposal history migration reconciliation failed.';
                    END IF;
                    IF (SELECT count(*) FROM "CommercialActivities") <>
                       (SELECT count(*) FROM "ProposalTimelineEvents" WHERE "SourceLegacyId" IS NOT NULL)
                       + (SELECT count(*) FROM "ProposalLegacyQuarantine") THEN
                        RAISE EXCEPTION 'Global proposal history migration reconciliation failed.';
                    END IF;

                    INSERT INTO "ProposalRetentionCycles" (
                        "OrganizationId", "CommercialRequestId", "Generation", "ClosedAtUtc",
                        "ClosedAtKnown", "ArchiveAtUtc", "ArchivedAtUtc", "DiscardAtUtc", "State")
                    SELECT request."OrganizationId", request."Id", 1, migration_now, FALSE,
                           ((migration_now AT TIME ZONE 'America/Sao_Paulo') + interval '24 months')
                               AT TIME ZONE 'America/Sao_Paulo',
                           NULL, NULL, 'Active'
                    FROM "CommercialRequests" request
                    WHERE request."Status" IN ('Approved', 'NotQuoted', 'Rejected', 'Expired', 'Cancelled')
                    ON CONFLICT ("OrganizationId", "CommercialRequestId", "Generation") DO NOTHING;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalAuditAggregates");

            migrationBuilder.DropTable(
                name: "ProposalAuditEvents");

            migrationBuilder.DropTable(
                name: "ProposalEventSequences");

            migrationBuilder.DropTable(
                name: "ProposalHistoryMigrationReconciliations");

            migrationBuilder.DropTable(
                name: "ProposalIdempotencyRecords");

            migrationBuilder.DropTable(
                name: "ProposalLegacyQuarantine");

            migrationBuilder.DropTable(
                name: "ProposalTimelineArchives");

            migrationBuilder.DropTable(
                name: "ProposalTimelineMentions");

            migrationBuilder.DropTable(
                name: "ProposalRetentionCycles");

            migrationBuilder.DropTable(
                name: "ProposalTimelineEvents");

            migrationBuilder.DropTable(
                name: "ProposalReasons");
        }
    }
}
