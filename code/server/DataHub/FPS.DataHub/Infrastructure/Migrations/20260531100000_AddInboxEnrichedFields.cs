using System;
using FPS.DataHub.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPS.DataHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DataHubDbContext))]
    [Migration("20260531100000_AddInboxEnrichedFields")]
    public partial class AddInboxEnrichedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EventVersion",
                table: "datahub_event_inbox",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "SourceService",
                table: "datahub_event_inbox",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AggregateId",
                table: "datahub_event_inbox",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "datahub_event_inbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingStatus",
                table: "datahub_event_inbox",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "datahub_event_inbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                table: "datahub_event_inbox",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_inbox_status_retry",
                table: "datahub_event_inbox",
                columns: new[] { "ProcessingStatus", "RetryCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_event_inbox_status_retry",
                table: "datahub_event_inbox");

            migrationBuilder.DropColumn(name: "EventVersion", table: "datahub_event_inbox");
            migrationBuilder.DropColumn(name: "SourceService", table: "datahub_event_inbox");
            migrationBuilder.DropColumn(name: "AggregateId", table: "datahub_event_inbox");
            migrationBuilder.DropColumn(name: "PublishedAt", table: "datahub_event_inbox");
            migrationBuilder.DropColumn(name: "ProcessingStatus", table: "datahub_event_inbox");
            migrationBuilder.DropColumn(name: "RetryCount", table: "datahub_event_inbox");
            migrationBuilder.DropColumn(name: "PayloadHash", table: "datahub_event_inbox");
        }
    }
}
