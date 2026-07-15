using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint02FleetRegistry : Migration
{
    private static readonly string[] DeviceAssignmentIndexColumns = ["OrganizationId", "DeviceId", "UnassignedAtUtc"];
    private static readonly string[] DeviceAssignmentVehicleIndexColumns = ["OrganizationId", "VehicleId", "UnassignedAtUtc"];
    private static readonly string[] DriverLicenseIndexColumns = ["OrganizationId", "LicenseNumber"];
    private static readonly string[] GpsDeviceSerialIndexColumns = ["OrganizationId", "SerialNumber"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "RowVersion",
            table: "Vehicles",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "DeviceAssignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                UnassignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeviceAssignments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Drivers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                LicenseNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                PhoneNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Drivers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "GpsDevices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SerialNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GpsDevices", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DeviceAssignments_OrganizationId_DeviceId_UnassignedAtUtc",
            table: "DeviceAssignments",
            columns: DeviceAssignmentIndexColumns,
            unique: true,
            filter: "[UnassignedAtUtc] IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_DeviceAssignments_OrganizationId_VehicleId_UnassignedAtUtc",
            table: "DeviceAssignments",
            columns: DeviceAssignmentVehicleIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_Drivers_OrganizationId_LicenseNumber",
            table: "Drivers",
            columns: DriverLicenseIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GpsDevices_OrganizationId_SerialNumber",
            table: "GpsDevices",
            columns: GpsDeviceSerialIndexColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DeviceAssignments");

        migrationBuilder.DropTable(
            name: "Drivers");

        migrationBuilder.DropTable(
            name: "GpsDevices");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "Vehicles");
    }
}
