using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // EF Core-generated migration index columns use arrays.
#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint20PilotDailyMetrics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PilotDailyMetrics",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CapturedOnUtc = table.Column<DateOnly>(type: "date", nullable: false),
                ActivationEvents = table.Column<int>(type: "int", nullable: false),
                ActiveDrivers = table.Column<int>(type: "int", nullable: false),
                ReturningDrivers = table.Column<int>(type: "int", nullable: false),
                ProcessedSyncCommands = table.Column<int>(type: "int", nullable: false),
                CompletedMissions = table.Column<int>(type: "int", nullable: false),
                CompleteProofs = table.Column<int>(type: "int", nullable: false),
                OpenExceptions = table.Column<int>(type: "int", nullable: false),
                RefreshedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PilotDailyMetrics", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PilotDailyMetrics_OrganizationId_CapturedOnUtc",
            table: "PilotDailyMetrics",
            columns: new[] { "OrganizationId", "CapturedOnUtc" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PilotDailyMetrics");
    }
}
