using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint05DriverMobile : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "DriverId",
            table: "AspNetUsers",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "DriverSyncCommandReceipts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CommandId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Action = table.Column<int>(type: "int", nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DriverSyncCommandReceipts", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUsers_DriverId",
            table: "AspNetUsers",
            column: "DriverId");

        migrationBuilder.CreateIndex(
            name: "IX_DriverSyncCommandReceipts_OrganizationId_CommandId",
            table: "DriverSyncCommandReceipts",
            columns: new[] { "OrganizationId", "CommandId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DriverSyncCommandReceipts_OrganizationId_DriverId_MissionId",
            table: "DriverSyncCommandReceipts",
            columns: new[] { "OrganizationId", "DriverId", "MissionId" });

        migrationBuilder.AddForeignKey(
            name: "FK_AspNetUsers_Drivers_DriverId",
            table: "AspNetUsers",
            column: "DriverId",
            principalTable: "Drivers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AspNetUsers_Drivers_DriverId",
            table: "AspNetUsers");

        migrationBuilder.DropTable(
            name: "DriverSyncCommandReceipts");

        migrationBuilder.DropIndex(
            name: "IX_AspNetUsers_DriverId",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "DriverId",
            table: "AspNetUsers");
    }
}

#pragma warning restore CA1861
