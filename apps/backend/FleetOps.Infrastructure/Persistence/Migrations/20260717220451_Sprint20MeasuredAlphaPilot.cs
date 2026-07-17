using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // EF Core-generated migration index columns use arrays.
#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint20MeasuredAlphaPilot : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PilotDecisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Outcome = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                PrimarySegment = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Rationale = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PilotDecisions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PilotEnrollments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AnalyticsConsent = table.Column<bool>(type: "bit", nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PilotEnrollments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PilotSupportIncidents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Severity = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                Category = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Workaround = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PilotSupportIncidents", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PilotDecisions_OrganizationId_DecidedAtUtc",
            table: "PilotDecisions",
            columns: new[] { "OrganizationId", "DecidedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_PilotEnrollments_OrganizationId",
            table: "PilotEnrollments",
            column: "OrganizationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PilotSupportIncidents_OrganizationId_Status_OccurredAtUtc",
            table: "PilotSupportIncidents",
            columns: new[] { "OrganizationId", "Status", "OccurredAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PilotDecisions");

        migrationBuilder.DropTable(
            name: "PilotEnrollments");

        migrationBuilder.DropTable(
            name: "PilotSupportIncidents");
    }
}
#pragma warning restore CA1861
