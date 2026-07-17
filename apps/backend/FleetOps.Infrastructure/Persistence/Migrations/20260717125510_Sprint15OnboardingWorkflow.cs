using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // EF migration scaffolding requires literal column arrays.

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint15OnboardingWorkflow : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "RowVersion",
            table: "TenantInvitations",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "RowVersion",
            table: "DriverPairingCodes",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "OnboardingActivationEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EventName = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                Step = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OnboardingActivationEvents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OnboardingImportSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                RowsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 1048576, nullable: false),
                ErrorsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 64000, nullable: false),
                RowCount = table.Column<int>(type: "int", nullable: false),
                ErrorCount = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                SummaryJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OnboardingImportSessions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OnboardingSampleDataSets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OnboardingSampleDataSets", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OnboardingActivationEvents_OrganizationId_OccurredAtUtc",
            table: "OnboardingActivationEvents",
            columns: new[] { "OrganizationId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_OnboardingImportSessions_OrganizationId_CreatedAtUtc",
            table: "OnboardingImportSessions",
            columns: new[] { "OrganizationId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_OnboardingImportSessions_OrganizationId_ExpiresAtUtc",
            table: "OnboardingImportSessions",
            columns: new[] { "OrganizationId", "ExpiresAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_OnboardingSampleDataSets_OrganizationId",
            table: "OnboardingSampleDataSets",
            column: "OrganizationId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OnboardingActivationEvents");

        migrationBuilder.DropTable(
            name: "OnboardingImportSessions");

        migrationBuilder.DropTable(
            name: "OnboardingSampleDataSets");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "TenantInvitations");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "DriverPairingCodes");
    }
}

#pragma warning restore CA1861
