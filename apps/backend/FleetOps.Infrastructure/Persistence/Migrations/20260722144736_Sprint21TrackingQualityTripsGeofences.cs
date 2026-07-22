using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration metadata uses array literals.

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint21TrackingQualityTripsGeofences : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "AccuracyMeters",
            table: "TelemetryPoints",
            type: "float(8)",
            precision: 8,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AnomalyFlags",
            table: "TelemetryPoints",
            type: "nvarchar(160)",
            maxLength: 160,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "QualityScore",
            table: "TelemetryPoints",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<long>(
            name: "SequenceNumber",
            table: "TelemetryPoints",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Source",
            table: "TelemetryPoints",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<double>(
            name: "AccuracyMeters",
            table: "CurrentVehiclePositions",
            type: "float(8)",
            precision: 8,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AnomalyFlags",
            table: "CurrentVehiclePositions",
            type: "nvarchar(160)",
            maxLength: 160,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "IngestedAtUtc",
            table: "CurrentVehiclePositions",
            type: "datetimeoffset(7)",
            precision: 7,
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

        migrationBuilder.AddColumn<int>(
            name: "QualityScore",
            table: "CurrentVehiclePositions",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<long>(
            name: "SequenceNumber",
            table: "CurrentVehiclePositions",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Source",
            table: "CurrentVehiclePositions",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateTable(
            name: "TrackingGeofenceEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GeofenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TelemetryEventId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Transition = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrackingGeofenceEvents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TrackingGeofences",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Shape = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                CenterLatitude = table.Column<double>(type: "float", nullable: true),
                CenterLongitude = table.Column<double>(type: "float", nullable: true),
                RadiusMeters = table.Column<double>(type: "float(10)", precision: 10, scale: 2, nullable: true),
                PolygonJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrackingGeofences", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TrackingTrips",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                DistanceKm = table.Column<double>(type: "float(12)", precision: 12, scale: 3, nullable: false),
                StopCount = table.Column<int>(type: "int", nullable: false),
                PointCount = table.Column<int>(type: "int", nullable: false),
                AlgorithmVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CalculatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrackingTrips", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TrackingGeofenceEvents_OrganizationId_GeofenceId_VehicleId_TelemetryEventId_Transition",
            table: "TrackingGeofenceEvents",
            columns: new[] { "OrganizationId", "GeofenceId", "VehicleId", "TelemetryEventId", "Transition" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TrackingGeofenceEvents_OrganizationId_OccurredAtUtc",
            table: "TrackingGeofenceEvents",
            columns: new[] { "OrganizationId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_TrackingGeofences_OrganizationId_Name",
            table: "TrackingGeofences",
            columns: new[] { "OrganizationId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TrackingTrips_OrganizationId_VehicleId_StartedAtUtc",
            table: "TrackingTrips",
            columns: new[] { "OrganizationId", "VehicleId", "StartedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TrackingGeofenceEvents");

        migrationBuilder.DropTable(
            name: "TrackingGeofences");

        migrationBuilder.DropTable(
            name: "TrackingTrips");

        migrationBuilder.DropColumn(
            name: "AccuracyMeters",
            table: "TelemetryPoints");

        migrationBuilder.DropColumn(
            name: "AnomalyFlags",
            table: "TelemetryPoints");

        migrationBuilder.DropColumn(
            name: "QualityScore",
            table: "TelemetryPoints");

        migrationBuilder.DropColumn(
            name: "SequenceNumber",
            table: "TelemetryPoints");

        migrationBuilder.DropColumn(
            name: "Source",
            table: "TelemetryPoints");

        migrationBuilder.DropColumn(
            name: "AccuracyMeters",
            table: "CurrentVehiclePositions");

        migrationBuilder.DropColumn(
            name: "AnomalyFlags",
            table: "CurrentVehiclePositions");

        migrationBuilder.DropColumn(
            name: "IngestedAtUtc",
            table: "CurrentVehiclePositions");

        migrationBuilder.DropColumn(
            name: "QualityScore",
            table: "CurrentVehiclePositions");

        migrationBuilder.DropColumn(
            name: "SequenceNumber",
            table: "CurrentVehiclePositions");

        migrationBuilder.DropColumn(
            name: "Source",
            table: "CurrentVehiclePositions");
    }
}
#pragma warning restore CA1861
