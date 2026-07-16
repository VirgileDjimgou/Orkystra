using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint07AlertsMaintenance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "CurrentOdometerKm",
            table: "Vehicles",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "AlertNotifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AlertId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Channel = table.Column<int>(type: "int", nullable: false),
                Subject = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Body = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertNotifications", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ComplianceDocuments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetType = table.Column<int>(type: "int", nullable: false),
                TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DocumentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                DocumentNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(280)", maxLength: 280, nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ComplianceDocuments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OperationalAlerts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RuleType = table.Column<int>(type: "int", nullable: false),
                DeduplicationKey = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Severity = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Message = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                TargetType = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AssignedToDisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                AcknowledgedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AcknowledgedByDisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                LastDetectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperationalAlerts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "VehicleMaintenancePlans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                IntervalKilometers = table.Column<int>(type: "int", nullable: true),
                IntervalDays = table.Column<int>(type: "int", nullable: true),
                LastCompletedOdometerKm = table.Column<int>(type: "int", nullable: false),
                LastCompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleMaintenancePlans", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AlertNotifications_OrganizationId_AlertId_Channel",
            table: "AlertNotifications",
            columns: new[] { "OrganizationId", "AlertId", "Channel" });

        migrationBuilder.CreateIndex(
            name: "IX_AlertNotifications_OrganizationId_SentAtUtc",
            table: "AlertNotifications",
            columns: new[] { "OrganizationId", "SentAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceDocuments_OrganizationId_ExpiresAtUtc",
            table: "ComplianceDocuments",
            columns: new[] { "OrganizationId", "ExpiresAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceDocuments_OrganizationId_TargetType_TargetEntityId_DocumentType",
            table: "ComplianceDocuments",
            columns: new[] { "OrganizationId", "TargetType", "TargetEntityId", "DocumentType" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OperationalAlerts_OrganizationId_DeduplicationKey",
            table: "OperationalAlerts",
            columns: new[] { "OrganizationId", "DeduplicationKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OperationalAlerts_OrganizationId_Status_Severity",
            table: "OperationalAlerts",
            columns: new[] { "OrganizationId", "Status", "Severity" });

        migrationBuilder.CreateIndex(
            name: "IX_OperationalAlerts_OrganizationId_TargetType_TargetEntityId",
            table: "OperationalAlerts",
            columns: new[] { "OrganizationId", "TargetType", "TargetEntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_VehicleMaintenancePlans_OrganizationId_IsActive",
            table: "VehicleMaintenancePlans",
            columns: new[] { "OrganizationId", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_VehicleMaintenancePlans_OrganizationId_VehicleId_Title",
            table: "VehicleMaintenancePlans",
            columns: new[] { "OrganizationId", "VehicleId", "Title" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertNotifications");

        migrationBuilder.DropTable(
            name: "ComplianceDocuments");

        migrationBuilder.DropTable(
            name: "OperationalAlerts");

        migrationBuilder.DropTable(
            name: "VehicleMaintenancePlans");

        migrationBuilder.DropColumn(
            name: "CurrentOdometerKm",
            table: "Vehicles");
    }
}
#pragma warning restore CA1861
