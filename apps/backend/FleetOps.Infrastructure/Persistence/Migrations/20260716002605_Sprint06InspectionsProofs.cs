using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint06InspectionsProofs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChecklistTemplates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChecklistTemplates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DeliveryProofs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionStopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecipientName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                SignatureName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeliveryProofs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DriverWorkflowCommandReceipts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CommandId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ScopeType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ScopeId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DriverWorkflowCommandReceipts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "MediaAssets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StorageKey = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                FileName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MediaAssets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "MediaUploadSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Purpose = table.Column<int>(type: "int", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                TotalBytes = table.Column<long>(type: "bigint", nullable: false),
                UploadedBytes = table.Column<long>(type: "bigint", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                TempStorageKey = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                MediaAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MediaUploadSessions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PreDepartureInspections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ChecklistTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Outcome = table.Column<int>(type: "int", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PreDepartureInspections", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ChecklistTemplateItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ChecklistTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                Code = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChecklistTemplateItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_ChecklistTemplateItems_ChecklistTemplates_ChecklistTemplateId",
                    column: x => x.ChecklistTemplateId,
                    principalTable: "ChecklistTemplates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "DeliveryProofPhotos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DeliveryProofId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MediaAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Caption = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeliveryProofPhotos", x => x.Id);
                table.ForeignKey(
                    name: "FK_DeliveryProofPhotos_DeliveryProofs_DeliveryProofId",
                    column: x => x.DeliveryProofId,
                    principalTable: "DeliveryProofs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "InspectionItemResults",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                Code = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                IsPass = table.Column<bool>(type: "bit", nullable: false),
                DefectSeverity = table.Column<int>(type: "int", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                PhotoAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InspectionItemResults", x => x.Id);
                table.ForeignKey(
                    name: "FK_InspectionItemResults_PreDepartureInspections_InspectionId",
                    column: x => x.InspectionId,
                    principalTable: "PreDepartureInspections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ChecklistTemplateItems_ChecklistTemplateId",
            table: "ChecklistTemplateItems",
            column: "ChecklistTemplateId");

        migrationBuilder.CreateIndex(
            name: "IX_ChecklistTemplateItems_OrganizationId_ChecklistTemplateId_Sequence",
            table: "ChecklistTemplateItems",
            columns: new[] { "OrganizationId", "ChecklistTemplateId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ChecklistTemplates_OrganizationId_Code",
            table: "ChecklistTemplates",
            columns: new[] { "OrganizationId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DeliveryProofPhotos_DeliveryProofId",
            table: "DeliveryProofPhotos",
            column: "DeliveryProofId");

        migrationBuilder.CreateIndex(
            name: "IX_DeliveryProofPhotos_OrganizationId_DeliveryProofId_MediaAssetId",
            table: "DeliveryProofPhotos",
            columns: new[] { "OrganizationId", "DeliveryProofId", "MediaAssetId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DeliveryProofs_OrganizationId_MissionId_MissionStopId",
            table: "DeliveryProofs",
            columns: new[] { "OrganizationId", "MissionId", "MissionStopId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DriverWorkflowCommandReceipts_OrganizationId_CommandId",
            table: "DriverWorkflowCommandReceipts",
            columns: new[] { "OrganizationId", "CommandId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_InspectionItemResults_InspectionId",
            table: "InspectionItemResults",
            column: "InspectionId");

        migrationBuilder.CreateIndex(
            name: "IX_InspectionItemResults_OrganizationId_InspectionId_Sequence",
            table: "InspectionItemResults",
            columns: new[] { "OrganizationId", "InspectionId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MediaAssets_OrganizationId_StorageKey",
            table: "MediaAssets",
            columns: new[] { "OrganizationId", "StorageKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MediaUploadSessions_OrganizationId_DriverId_CreatedAtUtc",
            table: "MediaUploadSessions",
            columns: new[] { "OrganizationId", "DriverId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_PreDepartureInspections_OrganizationId_MissionId_CompletedAtUtc",
            table: "PreDepartureInspections",
            columns: new[] { "OrganizationId", "MissionId", "CompletedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_PreDepartureInspections_OrganizationId_MissionId_DriverId_CreatedAtUtc",
            table: "PreDepartureInspections",
            columns: new[] { "OrganizationId", "MissionId", "DriverId", "CreatedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ChecklistTemplateItems");

        migrationBuilder.DropTable(
            name: "DeliveryProofPhotos");

        migrationBuilder.DropTable(
            name: "DriverWorkflowCommandReceipts");

        migrationBuilder.DropTable(
            name: "InspectionItemResults");

        migrationBuilder.DropTable(
            name: "MediaAssets");

        migrationBuilder.DropTable(
            name: "MediaUploadSessions");

        migrationBuilder.DropTable(
            name: "ChecklistTemplates");

        migrationBuilder.DropTable(
            name: "DeliveryProofs");

        migrationBuilder.DropTable(
            name: "PreDepartureInspections");
    }
}

#pragma warning restore CA1861
