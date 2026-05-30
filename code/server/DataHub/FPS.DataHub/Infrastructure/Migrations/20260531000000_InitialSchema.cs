using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FPS.DataHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "datahub_event_inbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessingError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_datahub_event_inbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "datahub_projection_checkpoint",
                columns: table => new
                {
                    ProjectionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastProcessedEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_datahub_projection_checkpoint", x => x.ProjectionName);
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_inbox_tenant_processed",
                table: "datahub_event_inbox",
                columns: new[] { "TenantId", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_event_inbox_source_event_id",
                table: "datahub_event_inbox",
                column: "SourceEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "datahub_event_inbox");
            migrationBuilder.DropTable(name: "datahub_projection_checkpoint");
        }
    }
}
