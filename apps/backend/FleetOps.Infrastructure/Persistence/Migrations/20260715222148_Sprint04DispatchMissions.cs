using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint04DispatchMissions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Missions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Reference = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                ScheduledStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                ScheduledEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SimulatedDelayMinutes = table.Column<int>(type: "int", nullable: false),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Missions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "MissionStops",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Address = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                PlannedArrivalUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MissionStops", x => x.Id);
                table.ForeignKey(
                    name: "FK_MissionStops_Missions_MissionId",
                    column: x => x.MissionId,
                    principalTable: "Missions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MissionTimelineEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<int>(type: "int", nullable: false),
                Description = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MissionTimelineEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_MissionTimelineEvents_Missions_MissionId",
                    column: x => x.MissionId,
                    principalTable: "Missions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Missions_OrganizationId_Reference",
            table: "Missions",
            columns: new[] { "OrganizationId", "Reference" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MissionStops_MissionId",
            table: "MissionStops",
            column: "MissionId");

        migrationBuilder.CreateIndex(
            name: "IX_MissionStops_OrganizationId_MissionId_Sequence",
            table: "MissionStops",
            columns: new[] { "OrganizationId", "MissionId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MissionTimelineEvents_MissionId",
            table: "MissionTimelineEvents",
            column: "MissionId");

        migrationBuilder.CreateIndex(
            name: "IX_MissionTimelineEvents_OrganizationId_MissionId_OccurredAtUtc",
            table: "MissionTimelineEvents",
            columns: new[] { "OrganizationId", "MissionId", "OccurredAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MissionStops");

        migrationBuilder.DropTable(
            name: "MissionTimelineEvents");

        migrationBuilder.DropTable(
            name: "Missions");
    }
}

#pragma warning restore CA1861
