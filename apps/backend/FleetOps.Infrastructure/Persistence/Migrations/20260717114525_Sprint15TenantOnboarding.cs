using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // EF migration scaffolding requires literal column arrays.

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint15TenantOnboarding : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DriverPairingCodes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DriverPairingCodes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TenantInvitations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TenantInvitations", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DriverPairingCodes_CodeHash",
            table: "DriverPairingCodes",
            column: "CodeHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DriverPairingCodes_OrganizationId_UserId_ExpiresAtUtc",
            table: "DriverPairingCodes",
            columns: new[] { "OrganizationId", "UserId", "ExpiresAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_TenantInvitations_OrganizationId_ExpiresAtUtc",
            table: "TenantInvitations",
            columns: new[] { "OrganizationId", "ExpiresAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_TenantInvitations_TokenHash",
            table: "TenantInvitations",
            column: "TokenHash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DriverPairingCodes");

        migrationBuilder.DropTable(
            name: "TenantInvitations");
    }
}

#pragma warning restore CA1861
