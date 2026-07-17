using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint13OperationsCenter : Migration
{
    private static readonly string[] DriverSyncIncidentKeyColumns = ["OrganizationId", "IncidentKey"];
    private static readonly string[] DriverSyncIncidentMissionColumns = ["OrganizationId", "MissionId", "LastOccurredAtUtc"];
    private static readonly string[] OperationsExceptionKeyColumns = ["OrganizationId", "ExceptionKey"];
    private static readonly string[] OperationsExceptionSourceColumns = ["OrganizationId", "SourceType", "SourceEntityId"];
    private static readonly string[] OperationsSavedViewOwnerColumns = ["OrganizationId", "CreatedByUserId", "Name"];
    private static readonly string[] OperationsSavedViewSharedColumns = ["OrganizationId", "IsShared"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DriverSyncExceptionIncidents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IncidentKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                IncidentCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Severity = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                ScopeType = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                Message = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                LastCommandId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                FirstOccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                LastOccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OccurrenceCount = table.Column<int>(type: "int", nullable: false),
                ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DriverSyncExceptionIncidents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OperationsExceptionStates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ExceptionKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                SourceType = table.Column<int>(type: "int", nullable: false),
                SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AssignedToDisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                AcknowledgedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AcknowledgedByDisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ResolvedByDisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                ResolutionReason = table.Column<string>(type: "nvarchar(280)", maxLength: 280, nullable: true),
                SnoozedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                SnoozeReason = table.Column<string>(type: "nvarchar(280)", maxLength: 280, nullable: true),
                LastDetectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperationsExceptionStates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OperationsSavedViews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                FilterJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                IsShared = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperationsSavedViews", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DriverSyncExceptionIncidents_OrganizationId_IncidentKey",
            table: "DriverSyncExceptionIncidents",
            columns: DriverSyncIncidentKeyColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DriverSyncExceptionIncidents_OrganizationId_MissionId_LastOccurredAtUtc",
            table: "DriverSyncExceptionIncidents",
            columns: DriverSyncIncidentMissionColumns);

        migrationBuilder.CreateIndex(
            name: "IX_OperationsExceptionStates_OrganizationId_ExceptionKey",
            table: "OperationsExceptionStates",
            columns: OperationsExceptionKeyColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OperationsExceptionStates_OrganizationId_SourceType_SourceEntityId",
            table: "OperationsExceptionStates",
            columns: OperationsExceptionSourceColumns);

        migrationBuilder.CreateIndex(
            name: "IX_OperationsSavedViews_OrganizationId_CreatedByUserId_Name",
            table: "OperationsSavedViews",
            columns: OperationsSavedViewOwnerColumns);

        migrationBuilder.CreateIndex(
            name: "IX_OperationsSavedViews_OrganizationId_IsShared",
            table: "OperationsSavedViews",
            columns: OperationsSavedViewSharedColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DriverSyncExceptionIncidents");

        migrationBuilder.DropTable(
            name: "OperationsExceptionStates");

        migrationBuilder.DropTable(
            name: "OperationsSavedViews");
    }
}
