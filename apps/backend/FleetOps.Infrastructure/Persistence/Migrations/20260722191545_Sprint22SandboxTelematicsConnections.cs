using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration metadata uses array literals.

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint22SandboxTelematicsConnections : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SandboxTelematicsConnections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                LastSucceededAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                LastError = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                ResumeCursor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SandboxTelematicsConnections", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SandboxTelematicsConnections_OrganizationId_Name",
            table: "SandboxTelematicsConnections",
            columns: new[] { "OrganizationId", "Name" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SandboxTelematicsConnections");
    }
}
#pragma warning restore CA1861
