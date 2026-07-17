using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint17MaintenanceWorkOrders : Migration
{
    private static readonly string[] OrganizationDueIndex = ["OrganizationId", "DueAtUtc"];
    private static readonly string[] OrganizationSourceIndex = ["OrganizationId", "SourceKey"];
    private static readonly string[] OrganizationVehicleStatusIndex = ["OrganizationId", "VehicleId", "Status"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MaintenanceWorkOrders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                SourceKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Priority = table.Column<int>(type: "int", nullable: false),
                DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                ScheduledStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                ScheduledEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                ImmobilizesVehicle = table.Column<bool>(type: "bit", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                LaborCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PartsCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                SupplierName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                PartsDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                TransitionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                AttachmentMediaAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MaintenanceWorkOrders", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceWorkOrders_OrganizationId_DueAtUtc",
            table: "MaintenanceWorkOrders",
            columns: OrganizationDueIndex);

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceWorkOrders_OrganizationId_SourceKey",
            table: "MaintenanceWorkOrders",
            columns: OrganizationSourceIndex,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceWorkOrders_OrganizationId_VehicleId_Status",
            table: "MaintenanceWorkOrders",
            columns: OrganizationVehicleStatusIndex);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MaintenanceWorkOrders");
    }
}
