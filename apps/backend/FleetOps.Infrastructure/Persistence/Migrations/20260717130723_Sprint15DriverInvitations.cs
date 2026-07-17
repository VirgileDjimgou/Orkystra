using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // EF migration scaffolding requires literal column arrays.

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint15DriverInvitations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "DriverId",
            table: "TenantInvitations",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_TenantInvitations_OrganizationId_DriverId",
            table: "TenantInvitations",
            columns: new[] { "OrganizationId", "DriverId" });

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUsers_OrganizationId_DriverId",
            table: "AspNetUsers",
            columns: new[] { "OrganizationId", "DriverId" },
            unique: true,
            filter: "[DriverId] IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TenantInvitations_OrganizationId_DriverId",
            table: "TenantInvitations");

        migrationBuilder.DropIndex(
            name: "IX_AspNetUsers_OrganizationId_DriverId",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "DriverId",
            table: "TenantInvitations");
    }
}

#pragma warning restore CA1861
