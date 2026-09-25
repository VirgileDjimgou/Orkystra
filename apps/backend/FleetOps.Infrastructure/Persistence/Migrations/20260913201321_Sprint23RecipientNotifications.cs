using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // EF-generated migration metadata uses array literals.

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint23RecipientNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RecipientEmailProtected",
            table: "RecipientStatusLinks",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "RecipientNotificationPreferences",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConsentGranted = table.Column<bool>(type: "bit", nullable: false),
                Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                Language = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                TimeZoneId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                QuietHoursStartHour = table.Column<int>(type: "int", nullable: true),
                QuietHoursEndHour = table.Column<int>(type: "int", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecipientNotificationPreferences", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RecipientStatusNotifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecipientStatusLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                DeduplicationKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                DeliveryStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecipientStatusNotifications", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RecipientNotificationPreferences_OrganizationId",
            table: "RecipientNotificationPreferences",
            column: "OrganizationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RecipientStatusNotifications_DeliveryStatus_NextAttemptAtUtc",
            table: "RecipientStatusNotifications",
            columns: new[] { "DeliveryStatus", "NextAttemptAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_RecipientStatusNotifications_OrganizationId_DeduplicationKey",
            table: "RecipientStatusNotifications",
            columns: new[] { "OrganizationId", "DeduplicationKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RecipientStatusNotifications_OrganizationId_MissionId_OccurredAtUtc",
            table: "RecipientStatusNotifications",
            columns: new[] { "OrganizationId", "MissionId", "OccurredAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RecipientNotificationPreferences");

        migrationBuilder.DropTable(
            name: "RecipientStatusNotifications");

        migrationBuilder.DropColumn(
            name: "RecipientEmailProtected",
            table: "RecipientStatusLinks");
    }
}
