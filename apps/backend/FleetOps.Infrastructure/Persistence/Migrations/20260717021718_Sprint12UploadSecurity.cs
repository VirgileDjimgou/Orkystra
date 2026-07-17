using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint12UploadSecurity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ScanDisposition",
            table: "MediaUploadSessions",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ScanReason",
            table: "MediaUploadSessions",
            type: "nvarchar(240)",
            maxLength: 240,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ScanDisposition",
            table: "MediaUploadSessions");

        migrationBuilder.DropColumn(
            name: "ScanReason",
            table: "MediaUploadSessions");
    }
}
