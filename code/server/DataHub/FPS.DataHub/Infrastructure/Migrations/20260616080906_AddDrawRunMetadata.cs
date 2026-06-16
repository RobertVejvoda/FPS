using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPS.DataHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDrawRunMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "EventVersion",
                table: "datahub_event_inbox",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RunReason",
                table: "datahub_draw_history",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggeredBy",
                table: "datahub_draw_history",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RunReason",
                table: "datahub_draw_history");

            migrationBuilder.DropColumn(
                name: "TriggeredBy",
                table: "datahub_draw_history");

            migrationBuilder.AlterColumn<int>(
                name: "EventVersion",
                table: "datahub_event_inbox",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
