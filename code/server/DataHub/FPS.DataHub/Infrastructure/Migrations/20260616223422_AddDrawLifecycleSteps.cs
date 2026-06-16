using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPS.DataHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDrawLifecycleSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LifecycleStepsJson",
                table: "datahub_draw_history",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LifecycleStepsJson",
                table: "datahub_draw_history");
        }
    }
}
