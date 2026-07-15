using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

public partial class Sprint03TrackingSimulation : Migration
{
    private static readonly string[] TelemetryPointEventIndexColumns = ["OrganizationId", "EventId"];
    private static readonly string[] CurrentVehiclePositionVehicleIndexColumns = ["OrganizationId", "VehicleId"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "RecordedAtUtc",
            table: "TelemetryPoints",
            type: "datetimeoffset(7)",
            precision: 7,
            nullable: false,
            oldClrType: typeof(DateTimeOffset),
            oldType: "datetimeoffset");

        migrationBuilder.AlterColumn<string>(
            name: "DeviceId",
            table: "TelemetryPoints",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(128)",
            oldMaxLength: 128);

        migrationBuilder.AddColumn<string>(
            name: "EventId",
            table: "TelemetryPoints",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<double>(
            name: "HeadingDegrees",
            table: "TelemetryPoints",
            type: "float(6)",
            precision: 6,
            scale: 2,
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "IngestedAtUtc",
            table: "TelemetryPoints",
            type: "datetimeoffset(7)",
            precision: 7,
            nullable: false,
            defaultValue: new DateTimeOffset(
                new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                new TimeSpan(0, 0, 0, 0, 0)));

        migrationBuilder.CreateTable(
            name: "CurrentVehiclePositions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DeviceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                EventId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                Latitude = table.Column<double>(type: "float", nullable: false),
                Longitude = table.Column<double>(type: "float", nullable: false),
                SpeedKph = table.Column<double>(type: "float", nullable: false),
                HeadingDegrees = table.Column<double>(type: "float(6)", precision: 6, scale: 2, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CurrentVehiclePositions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TelemetryPoints_OrganizationId_EventId",
            table: "TelemetryPoints",
            columns: TelemetryPointEventIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CurrentVehiclePositions_OrganizationId_VehicleId",
            table: "CurrentVehiclePositions",
            columns: CurrentVehiclePositionVehicleIndexColumns,
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CurrentVehiclePositions");

        migrationBuilder.DropIndex(
            name: "IX_TelemetryPoints_OrganizationId_EventId",
            table: "TelemetryPoints");

        migrationBuilder.DropColumn(
            name: "EventId",
            table: "TelemetryPoints");

        migrationBuilder.DropColumn(
            name: "HeadingDegrees",
            table: "TelemetryPoints");

        migrationBuilder.DropColumn(
            name: "IngestedAtUtc",
            table: "TelemetryPoints");

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "RecordedAtUtc",
            table: "TelemetryPoints",
            type: "datetimeoffset",
            nullable: false,
            oldClrType: typeof(DateTimeOffset),
            oldType: "datetimeoffset(7)",
            oldPrecision: 7);

        migrationBuilder.AlterColumn<string>(
            name: "DeviceId",
            table: "TelemetryPoints",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(64)",
            oldMaxLength: 64);
    }
}
