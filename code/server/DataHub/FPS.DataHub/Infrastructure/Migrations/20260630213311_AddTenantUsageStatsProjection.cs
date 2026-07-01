using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FPS.DataHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantUsageStatsProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "datahub_tenant_usage_stats",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PeriodMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    ActiveRequestorCount = table.Column<int>(type: "integer", nullable: false),
                    BookingRequestCount = table.Column<int>(type: "integer", nullable: false),
                    DrawRunCount = table.Column<int>(type: "integer", nullable: false),
                    AllocatedCount = table.Column<int>(type: "integer", nullable: false),
                    RejectedCount = table.Column<int>(type: "integer", nullable: false),
                    CancelledCount = table.Column<int>(type: "integer", nullable: false),
                    ExpiredCount = table.Column<int>(type: "integer", nullable: false),
                    NoShowCount = table.Column<int>(type: "integer", nullable: false),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_datahub_tenant_usage_stats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_usage_stats_tenant_period",
                table: "datahub_tenant_usage_stats",
                columns: new[] { "TenantId", "PeriodMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "datahub_tenant_usage_stats");
        }
    }
}
