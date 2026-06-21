using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPS.DataHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleFactsToBookingOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VehicleLicensePlate",
                table: "datahub_booking_outcome",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                table: "datahub_booking_outcome",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VehicleIsElectric",
                table: "datahub_booking_outcome",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "VehicleLicensePlate", table: "datahub_booking_outcome");
            migrationBuilder.DropColumn(name: "VehicleType", table: "datahub_booking_outcome");
            migrationBuilder.DropColumn(name: "VehicleIsElectric", table: "datahub_booking_outcome");
        }
    }
}
