using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core generates migration index column arrays.

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint12SessionSecurity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClientType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RevocationReason = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSessions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_OrganizationId_UserId_ExpiresAtUtc",
            table: "UserSessions",
            columns: new[] { "OrganizationId", "UserId", "ExpiresAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_UserId_RevokedAtUtc",
            table: "UserSessions",
            columns: new[] { "UserId", "RevokedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserSessions");
    }
}
#pragma warning restore CA1861
