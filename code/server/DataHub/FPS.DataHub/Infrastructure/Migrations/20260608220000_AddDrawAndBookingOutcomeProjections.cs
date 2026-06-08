using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FPS.DataHub.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddDrawAndBookingOutcomeProjections : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "datahub_draw_history",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DrawAttemptId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                LocationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                TimeSlot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                TriggerSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AllocatedCount = table.Column<int>(type: "integer", nullable: false),
                RejectedCount = table.Column<int>(type: "integer", nullable: false),
                WaitlistedCount = table.Column<int>(type: "integer", nullable: false),
                SafeFailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                AlgorithmVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_datahub_draw_history", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "datahub_booking_outcome",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                BookingRequestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                RequestorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                LocationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                TimeSlot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                FinalStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                SafeReasonText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                AllocationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                SlotId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                AllocationSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                DrawAttemptId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_datahub_booking_outcome", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_booking_outcome_draw_attempt",
            table: "datahub_booking_outcome",
            column: "DrawAttemptId");

        migrationBuilder.CreateIndex(
            name: "ix_booking_outcome_tenant_location_date",
            table: "datahub_booking_outcome",
            columns: new[] { "TenantId", "LocationId", "Date" });

        migrationBuilder.CreateIndex(
            name: "ix_booking_outcome_tenant_requestor_date",
            table: "datahub_booking_outcome",
            columns: new[] { "TenantId", "RequestorId", "Date" });

        migrationBuilder.CreateIndex(
            name: "ux_booking_outcome_request_id",
            table: "datahub_booking_outcome",
            column: "BookingRequestId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_draw_history_tenant_completed",
            table: "datahub_draw_history",
            columns: new[] { "TenantId", "CompletedAt" });

        migrationBuilder.CreateIndex(
            name: "ix_draw_history_tenant_location_date",
            table: "datahub_draw_history",
            columns: new[] { "TenantId", "LocationId", "Date", "TimeSlot" });

        migrationBuilder.CreateIndex(
            name: "ux_draw_history_attempt_id",
            table: "datahub_draw_history",
            column: "DrawAttemptId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "datahub_booking_outcome");

        migrationBuilder.DropTable(
            name: "datahub_draw_history");
    }
}
