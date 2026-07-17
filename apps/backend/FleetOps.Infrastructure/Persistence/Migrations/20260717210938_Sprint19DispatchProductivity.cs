using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // EF Core-generated migration index columns use arrays.
#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint19DispatchProductivity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DispatchImportReceipts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ImportKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                ImportedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DispatchImportReceipts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DispatchSavedViews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                FilterJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DispatchSavedViews", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "MissionTemplates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MissionTemplates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "MissionTemplateStops",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Address = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                ArrivalOffsetMinutes = table.Column<int>(type: "int", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MissionTemplateStops", x => x.Id);
                table.ForeignKey(
                    name: "FK_MissionTemplateStops_MissionTemplates_TemplateId",
                    column: x => x.TemplateId,
                    principalTable: "MissionTemplates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DispatchImportReceipts_OrganizationId_ImportKey",
            table: "DispatchImportReceipts",
            columns: new[] { "OrganizationId", "ImportKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DispatchSavedViews_OrganizationId_UserId_Name",
            table: "DispatchSavedViews",
            columns: new[] { "OrganizationId", "UserId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MissionTemplates_OrganizationId_Name",
            table: "MissionTemplates",
            columns: new[] { "OrganizationId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MissionTemplateStops_OrganizationId_TemplateId_Sequence",
            table: "MissionTemplateStops",
            columns: new[] { "OrganizationId", "TemplateId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MissionTemplateStops_TemplateId",
            table: "MissionTemplateStops",
            column: "TemplateId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DispatchImportReceipts");

        migrationBuilder.DropTable(
            name: "DispatchSavedViews");

        migrationBuilder.DropTable(
            name: "MissionTemplateStops");

        migrationBuilder.DropTable(
            name: "MissionTemplates");
    }
}
#pragma warning restore CA1861
