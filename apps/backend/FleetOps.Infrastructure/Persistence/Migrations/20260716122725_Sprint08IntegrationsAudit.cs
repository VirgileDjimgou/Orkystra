using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Sprint08IntegrationsAudit : Migration
{
    private static readonly string[] ApiClientCredentialActiveIndexColumns =
        ["OrganizationId", "IsActive", "CredentialType"];

    private static readonly string[] ApiClientCredentialKeyIdIndexColumns =
        ["OrganizationId", "KeyId"];

    private static readonly string[] IntegrationOutboxPendingIndexColumns =
        ["OrganizationId", "Status", "NextAttemptAtUtc"];

    private static readonly string[] IntegrationOutboxDeliveryIndexColumns =
        ["OrganizationId", "WebhookEndpointId", "EventType", "OccurredAtUtc"];

    private static readonly string[] SandboxReceiptIndexColumns =
        ["OrganizationId", "WebhookEndpointId", "ReceivedAtUtc"];

    private static readonly string[] WebhookAttemptTimelineIndexColumns =
        ["OrganizationId", "AttemptedAtUtc"];

    private static readonly string[] WebhookAttemptUniquenessIndexColumns =
        ["OrganizationId", "OutboxMessageId", "WebhookEndpointId", "AttemptNumber"];

    private static readonly string[] WebhookEndpointStatusIndexColumns =
        ["OrganizationId", "EventType", "IsActive"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApiClientCredentials",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                CredentialType = table.Column<int>(type: "int", nullable: false),
                ScopeList = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                KeyId = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                SecretHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                SecretPreview = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                LastUsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiClientCredentials", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "IntegrationOutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WebhookEndpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                AggregateType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                AggregateId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                PayloadJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntegrationOutboxMessages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SandboxWebhookReceipts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WebhookEndpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Signature = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                PayloadJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SandboxWebhookReceipts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WebhookDeliveryAttempts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OutboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WebhookEndpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttemptNumber = table.Column<int>(type: "int", nullable: false),
                ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                ResponseBody = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                AttemptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebhookDeliveryAttempts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WebhookEndpoints",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                TargetUrl = table.Column<string>(type: "nvarchar(280)", maxLength: 280, nullable: false),
                SigningSecret = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                IsSandbox = table.Column<bool>(type: "bit", nullable: false),
                LastSucceededAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                DisabledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebhookEndpoints", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApiClientCredentials_OrganizationId_IsActive_CredentialType",
            table: "ApiClientCredentials",
            columns: ApiClientCredentialActiveIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_ApiClientCredentials_OrganizationId_KeyId",
            table: "ApiClientCredentials",
            columns: ApiClientCredentialKeyIdIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationOutboxMessages_OrganizationId_Status_NextAttemptAtUtc",
            table: "IntegrationOutboxMessages",
            columns: IntegrationOutboxPendingIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationOutboxMessages_OrganizationId_WebhookEndpointId_EventType_OccurredAtUtc",
            table: "IntegrationOutboxMessages",
            columns: IntegrationOutboxDeliveryIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_SandboxWebhookReceipts_OrganizationId_WebhookEndpointId_ReceivedAtUtc",
            table: "SandboxWebhookReceipts",
            columns: SandboxReceiptIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_WebhookDeliveryAttempts_OrganizationId_AttemptedAtUtc",
            table: "WebhookDeliveryAttempts",
            columns: WebhookAttemptTimelineIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_WebhookDeliveryAttempts_OrganizationId_OutboxMessageId_WebhookEndpointId_AttemptNumber",
            table: "WebhookDeliveryAttempts",
            columns: WebhookAttemptUniquenessIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WebhookEndpoints_OrganizationId_EventType_IsActive",
            table: "WebhookEndpoints",
            columns: WebhookEndpointStatusIndexColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApiClientCredentials");

        migrationBuilder.DropTable(
            name: "IntegrationOutboxMessages");

        migrationBuilder.DropTable(
            name: "SandboxWebhookReceipts");

        migrationBuilder.DropTable(
            name: "WebhookDeliveryAttempts");

        migrationBuilder.DropTable(
            name: "WebhookEndpoints");
    }
}
