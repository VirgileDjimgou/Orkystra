using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint16PrivateObjectMedia : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ContentChecksumSha256",
            table: "MediaUploadSessions",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ChecksumSha256",
            table: "MediaAssets",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "IsReadRevoked",
            table: "MediaAssets",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ReadRevokedAtUtc",
            table: "MediaAssets",
            type: "datetimeoffset(7)",
            precision: 7,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RetainUntilUtc",
            table: "MediaAssets",
            type: "datetimeoffset(7)",
            precision: 7,
            nullable: false,
            defaultValueSql: "DATEADD(day, 365, SYSUTCDATETIME())");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ContentChecksumSha256",
            table: "MediaUploadSessions");

        migrationBuilder.DropColumn(
            name: "ChecksumSha256",
            table: "MediaAssets");

        migrationBuilder.DropColumn(
            name: "IsReadRevoked",
            table: "MediaAssets");

        migrationBuilder.DropColumn(
            name: "ReadRevokedAtUtc",
            table: "MediaAssets");

        migrationBuilder.DropColumn(
            name: "RetainUntilUtc",
            table: "MediaAssets");
    }
}
