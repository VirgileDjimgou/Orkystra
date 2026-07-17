using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // EF Core-generated migration index columns use arrays.
#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint18ComplianceCampaigns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ComplianceDocuments_OrganizationId_TargetType_TargetEntityId_DocumentType",
            table: "ComplianceDocuments");

        migrationBuilder.AddColumn<Guid>(
            name: "ComplianceDocumentTypeId",
            table: "ComplianceDocuments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "MediaAssetId",
            table: "ComplianceDocuments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReplacedByDocumentId",
            table: "ComplianceDocuments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ReviewStatus",
            table: "ComplianceDocuments",
            type: "nvarchar(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ReviewedAtUtc",
            table: "ComplianceDocuments",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReviewedByUserId",
            table: "ComplianceDocuments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ComplianceDocumentTypes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                SubjectType = table.Column<int>(type: "int", nullable: false),
                IsBlocking = table.Column<bool>(type: "bit", nullable: false),
                RequiresReview = table.Column<bool>(type: "bit", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ComplianceDocumentTypes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ComplianceInspectionCampaigns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                OpensAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ClosesAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ComplianceInspectionCampaigns", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CompliancePolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BlocksAssignments = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CompliancePolicies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ComplianceInspectionCampaignTasks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TemplateCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                SubmissionCommandId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ComplianceInspectionCampaignTasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_ComplianceInspectionCampaignTasks_ComplianceInspectionCampaigns_CampaignId",
                    column: x => x.CampaignId,
                    principalTable: "ComplianceInspectionCampaigns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceDocuments_OrganizationId_TargetType_TargetEntityId_DocumentType_ReplacedByDocumentId",
            table: "ComplianceDocuments",
            columns: new[] { "OrganizationId", "TargetType", "TargetEntityId", "DocumentType", "ReplacedByDocumentId" },
            unique: true,
            filter: "[ReplacedByDocumentId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceDocumentTypes_OrganizationId_Name_SubjectType",
            table: "ComplianceDocumentTypes",
            columns: new[] { "OrganizationId", "Name", "SubjectType" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceInspectionCampaigns_OrganizationId_Status",
            table: "ComplianceInspectionCampaigns",
            columns: new[] { "OrganizationId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceInspectionCampaignTasks_CampaignId",
            table: "ComplianceInspectionCampaignTasks",
            column: "CampaignId");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceInspectionCampaignTasks_OrganizationId_CampaignId_VehicleId",
            table: "ComplianceInspectionCampaignTasks",
            columns: new[] { "OrganizationId", "CampaignId", "VehicleId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceInspectionCampaignTasks_OrganizationId_DriverId_Status",
            table: "ComplianceInspectionCampaignTasks",
            columns: new[] { "OrganizationId", "DriverId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_CompliancePolicies_OrganizationId",
            table: "CompliancePolicies",
            column: "OrganizationId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ComplianceDocumentTypes");

        migrationBuilder.DropTable(
            name: "ComplianceInspectionCampaignTasks");

        migrationBuilder.DropTable(
            name: "CompliancePolicies");

        migrationBuilder.DropTable(
            name: "ComplianceInspectionCampaigns");

        migrationBuilder.DropIndex(
            name: "IX_ComplianceDocuments_OrganizationId_TargetType_TargetEntityId_DocumentType_ReplacedByDocumentId",
            table: "ComplianceDocuments");

        migrationBuilder.DropColumn(
            name: "ComplianceDocumentTypeId",
            table: "ComplianceDocuments");

        migrationBuilder.DropColumn(
            name: "MediaAssetId",
            table: "ComplianceDocuments");

        migrationBuilder.DropColumn(
            name: "ReplacedByDocumentId",
            table: "ComplianceDocuments");

        migrationBuilder.DropColumn(
            name: "ReviewStatus",
            table: "ComplianceDocuments");

        migrationBuilder.DropColumn(
            name: "ReviewedAtUtc",
            table: "ComplianceDocuments");

        migrationBuilder.DropColumn(
            name: "ReviewedByUserId",
            table: "ComplianceDocuments");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceDocuments_OrganizationId_TargetType_TargetEntityId_DocumentType",
            table: "ComplianceDocuments",
            columns: new[] { "OrganizationId", "TargetType", "TargetEntityId", "DocumentType" },
            unique: true);
    }
}
#pragma warning restore CA1861
