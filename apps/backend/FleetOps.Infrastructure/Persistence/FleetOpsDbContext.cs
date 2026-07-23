using FleetOps.Core.Modules.Alerts;
using FleetOps.Core.Modules.Compliance;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Core.Modules.Maintenance;
using FleetOps.Core.Modules.Onboarding;
using FleetOps.Core.Modules.Operations;
using FleetOps.Core.Modules.Pilot;
using FleetOps.Core.Modules.Tracking;
using FleetOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Infrastructure.Persistence;

public sealed class FleetOpsDbContext(DbContextOptions<FleetOpsDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionStop> MissionStops => Set<MissionStop>();
    public DbSet<MissionTimelineEvent> MissionTimelineEvents => Set<MissionTimelineEvent>();
    public DbSet<RecipientStatusLink> RecipientStatusLinks => Set<RecipientStatusLink>();
    public DbSet<DriverSyncCommandReceipt> DriverSyncCommandReceipts => Set<DriverSyncCommandReceipt>();
    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<ChecklistTemplateItem> ChecklistTemplateItems => Set<ChecklistTemplateItem>();
    public DbSet<PreDepartureInspection> PreDepartureInspections => Set<PreDepartureInspection>();
    public DbSet<InspectionItemResult> InspectionItemResults => Set<InspectionItemResult>();
    public DbSet<DeliveryProof> DeliveryProofs => Set<DeliveryProof>();
    public DbSet<DeliveryProofPhoto> DeliveryProofPhotos => Set<DeliveryProofPhoto>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<MediaUploadSession> MediaUploadSessions => Set<MediaUploadSession>();
    public DbSet<DriverWorkflowCommandReceipt> DriverWorkflowCommandReceipts => Set<DriverWorkflowCommandReceipt>();
    public DbSet<OperationsExceptionState> OperationsExceptionStates => Set<OperationsExceptionState>();
    public DbSet<OperationsSavedView> OperationsSavedViews => Set<OperationsSavedView>();
    public DbSet<DriverSyncExceptionIncident> DriverSyncExceptionIncidents => Set<DriverSyncExceptionIncident>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<GpsDevice> GpsDevices => Set<GpsDevice>();
    public DbSet<DeviceAssignment> DeviceAssignments => Set<DeviceAssignment>();
    public DbSet<TelemetryPoint> TelemetryPoints => Set<TelemetryPoint>();
    public DbSet<CurrentVehiclePosition> CurrentVehiclePositions => Set<CurrentVehiclePosition>();
    public DbSet<TrackingTrip> TrackingTrips => Set<TrackingTrip>();
    public DbSet<TrackingGeofence> TrackingGeofences => Set<TrackingGeofence>();
    public DbSet<TrackingGeofenceEvent> TrackingGeofenceEvents => Set<TrackingGeofenceEvent>();
    public DbSet<ComplianceDocument> ComplianceDocuments => Set<ComplianceDocument>();
    public DbSet<VehicleMaintenancePlan> VehicleMaintenancePlans => Set<VehicleMaintenancePlan>();
    public DbSet<OperationalAlert> OperationalAlerts => Set<OperationalAlert>();
    public DbSet<AlertNotification> AlertNotifications => Set<AlertNotification>();
    public DbSet<ApiClientCredential> ApiClientCredentials => Set<ApiClientCredential>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<IntegrationOutboxMessage> IntegrationOutboxMessages => Set<IntegrationOutboxMessage>();
    public DbSet<WebhookDeliveryAttempt> WebhookDeliveryAttempts => Set<WebhookDeliveryAttempt>();
    public DbSet<SandboxWebhookReceipt> SandboxWebhookReceipts => Set<SandboxWebhookReceipt>();
    public DbSet<SandboxTelematicsConnection> SandboxTelematicsConnections => Set<SandboxTelematicsConnection>();
    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();
    public DbSet<DriverPairingCode> DriverPairingCodes => Set<DriverPairingCode>();
    public DbSet<OnboardingImportSession> OnboardingImportSessions => Set<OnboardingImportSession>();
    public DbSet<OnboardingSampleDataSet> OnboardingSampleDataSets => Set<OnboardingSampleDataSet>();
    public DbSet<OnboardingActivationEvent> OnboardingActivationEvents => Set<OnboardingActivationEvent>();
    public DbSet<MaintenanceWorkOrder> MaintenanceWorkOrders => Set<MaintenanceWorkOrder>();
    public DbSet<MissionTemplate> MissionTemplates => Set<MissionTemplate>();
    public DbSet<MissionTemplateStop> MissionTemplateStops => Set<MissionTemplateStop>();
    public DbSet<DispatchImportReceipt> DispatchImportReceipts => Set<DispatchImportReceipt>();
    public DbSet<DispatchSavedView> DispatchSavedViews => Set<DispatchSavedView>();
    public DbSet<PilotEnrollment> PilotEnrollments => Set<PilotEnrollment>();
    public DbSet<PilotDailyMetric> PilotDailyMetrics => Set<PilotDailyMetric>();
    public DbSet<PilotSupportIncident> PilotSupportIncidents => Set<PilotSupportIncident>();
    public DbSet<PilotDecision> PilotDecisions => Set<PilotDecision>();
    public DbSet<ComplianceDocumentType> ComplianceDocumentTypes => Set<ComplianceDocumentType>();
    public DbSet<CompliancePolicy> CompliancePolicies => Set<CompliancePolicy>();
    public DbSet<ComplianceInspectionCampaign> ComplianceInspectionCampaigns => Set<ComplianceInspectionCampaign>();
    public DbSet<ComplianceInspectionCampaignTask> ComplianceInspectionCampaignTasks => Set<ComplianceInspectionCampaignTask>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuditLogsAreImmutable();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        EnsureAuditLogsAreImmutable();
        return base.SaveChanges();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ComplianceDocumentType>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.Name, x.SubjectType }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<MissionTemplate>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.Navigation(x => x.Stops).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.Stops).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<MissionTemplateStop>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.TemplateId, x.Sequence }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Address).HasMaxLength(240);
        });
        builder.Entity<DispatchImportReceipt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.ImportKey }).IsUnique();
            entity.Property(x => x.ImportKey).HasMaxLength(120);
            entity.Property(x => x.ImportedAtUtc).HasPrecision(7);
        });
        builder.Entity<DispatchSavedView>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.UserId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.FilterJson).HasMaxLength(2000);
        });
        builder.Entity<CompliancePolicy>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrganizationId).IsUnique();
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });
        builder.Entity<ComplianceInspectionCampaign>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.Navigation(x => x.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Entity<ComplianceInspectionCampaignTask>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.CampaignId, x.VehicleId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.DriverId, x.Status });
            entity.Property(x => x.TemplateCode).HasMaxLength(64);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.SubmissionCommandId).HasMaxLength(120);
            entity.HasOne<ComplianceInspectionCampaign>().WithMany(x => x.Tasks).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Organization>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Slug).HasMaxLength(80);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(160);
            entity.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique()
                .HasFilter("[Email] IS NOT NULL");
            entity.HasIndex(x => new { x.OrganizationId, x.DriverId }).IsUnique()
                .HasFilter("[DriverId] IS NOT NULL");
            entity.HasOne<Driver>()
                .WithMany()
                .HasForeignKey(x => x.DriverId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.OccurredAtUtc });
            entity.Property(x => x.ActionType).HasMaxLength(64);
            entity.Property(x => x.TargetType).HasMaxLength(64);
            entity.Property(x => x.TargetId).HasMaxLength(128);
            entity.Property(x => x.Metadata).HasMaxLength(2048);
        });

        builder.Entity<UserSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.UserId, x.ExpiresAtUtc });
            entity.HasIndex(x => new { x.UserId, x.RevokedAtUtc });
            entity.Property(x => x.ClientType).HasMaxLength(32);
            entity.Property(x => x.CreatedAtUtc).HasPrecision(7);
            entity.Property(x => x.ExpiresAtUtc).HasPrecision(7);
            entity.Property(x => x.RevokedAtUtc).HasPrecision(7);
            entity.Property(x => x.RevocationReason).HasMaxLength(120);
        });

        builder.Entity<TenantInvitation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.ExpiresAtUtc });
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.FullName).HasMaxLength(160);
            entity.Property(x => x.Role).HasMaxLength(32);
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.HasIndex(x => new { x.OrganizationId, x.DriverId });
            entity.Property(x => x.CreatedAtUtc).HasPrecision(7);
            entity.Property(x => x.ExpiresAtUtc).HasPrecision(7);
            entity.Property(x => x.AcceptedAtUtc).HasPrecision(7);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<DriverPairingCode>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CodeHash).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.UserId, x.ExpiresAtUtc });
            entity.Property(x => x.CodeHash).HasMaxLength(64);
            entity.Property(x => x.CreatedAtUtc).HasPrecision(7);
            entity.Property(x => x.ExpiresAtUtc).HasPrecision(7);
            entity.Property(x => x.ConsumedAtUtc).HasPrecision(7);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<OnboardingImportSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.ExpiresAtUtc });
            entity.Property(x => x.TargetType).HasMaxLength(24);
            entity.Property(x => x.RowsJson).HasMaxLength(1_048_576);
            entity.Property(x => x.ErrorsJson).HasMaxLength(64_000);
            entity.Property(x => x.SummaryJson).HasMaxLength(1_000);
            entity.Property(x => x.CreatedAtUtc).HasPrecision(7);
            entity.Property(x => x.ExpiresAtUtc).HasPrecision(7);
            entity.Property(x => x.ConfirmedAtUtc).HasPrecision(7);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<OnboardingSampleDataSet>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrganizationId).IsUnique();
            entity.Property(x => x.CreatedAtUtc).HasPrecision(7);
        });

        builder.Entity<OnboardingActivationEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.OccurredAtUtc });
            entity.Property(x => x.EventName).HasMaxLength(48);
            entity.Property(x => x.Step).HasMaxLength(48);
            entity.Property(x => x.OccurredAtUtc).HasPrecision(7);
        });

        builder.Entity<PilotEnrollment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrganizationId).IsUnique();
            entity.Property(x => x.RecordedAtUtc).HasPrecision(7);
        });

        builder.Entity<PilotDailyMetric>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.CapturedOnUtc }).IsUnique();
            entity.Property(x => x.CapturedOnUtc).HasColumnType("date");
            entity.Property(x => x.RefreshedAtUtc).HasPrecision(7);
        });

        builder.Entity<PilotSupportIncident>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.OccurredAtUtc });
            entity.Property(x => x.Category).HasMaxLength(48);
            entity.Property(x => x.Summary).HasMaxLength(500);
            entity.Property(x => x.Workaround).HasMaxLength(500);
            entity.Property(x => x.OccurredAtUtc).HasPrecision(7);
            entity.Property(x => x.ResolvedAtUtc).HasPrecision(7);
        });

        builder.Entity<PilotDecision>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.DecidedAtUtc });
            entity.Property(x => x.Outcome).HasMaxLength(12);
            entity.Property(x => x.PrimarySegment).HasMaxLength(80);
            entity.Property(x => x.Rationale).HasMaxLength(1000);
            entity.Property(x => x.DecidedAtUtc).HasPrecision(7);
        });

        builder.Entity<Mission>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.Reference }).IsUnique();
            entity.Property(x => x.Reference).HasMaxLength(48);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.ScheduledStartUtc).HasPrecision(7);
            entity.Property(x => x.ScheduledEndUtc).HasPrecision(7);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.Navigation(x => x.Stops).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(x => x.Timeline).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.Stops)
                .WithOne()
                .HasForeignKey(x => x.MissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Timeline)
                .WithOne()
                .HasForeignKey(x => x.MissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MissionStop>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.MissionId, x.Sequence }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Address).HasMaxLength(240);
            entity.Property(x => x.PlannedArrivalUtc).HasPrecision(7);
        });

        builder.Entity<MissionTimelineEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.MissionId, x.OccurredAtUtc });
            entity.Property(x => x.Description).HasMaxLength(240);
            entity.Property(x => x.OccurredAtUtc).HasPrecision(7);
        });

        builder.Entity<DriverSyncCommandReceipt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.CommandId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.DriverId, x.MissionId });
            entity.Property(x => x.CommandId).HasMaxLength(80);
            entity.Property(x => x.ProcessedAtUtc).HasPrecision(7);
        });

        builder.Entity<ChecklistTemplate>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(48);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(x => x.ChecklistTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChecklistTemplateItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.ChecklistTemplateId, x.Sequence }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(48);
            entity.Property(x => x.Label).HasMaxLength(200);
        });

        builder.Entity<PreDepartureInspection>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.MissionId, x.CompletedAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.MissionId, x.DriverId, x.CreatedAtUtc });
            entity.Property(x => x.CompletedAtUtc).HasPrecision(7);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(x => x.InspectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InspectionItemResult>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.InspectionId, x.Sequence }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(48);
            entity.Property(x => x.Label).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        builder.Entity<DeliveryProof>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.MissionId, x.MissionStopId }).IsUnique();
            entity.Property(x => x.RecipientName).HasMaxLength(160);
            entity.Property(x => x.SignatureName).HasMaxLength(160);
            entity.Property(x => x.DeliveredAtUtc).HasPrecision(7);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Navigation(x => x.Photos).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.Photos)
                .WithOne()
                .HasForeignKey(x => x.DeliveryProofId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DeliveryProofPhoto>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.DeliveryProofId, x.MediaAssetId }).IsUnique();
            entity.Property(x => x.Caption).HasMaxLength(240);
        });

        builder.Entity<RecipientStatusLink>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.MissionId, x.ExpiresAtUtc });
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.Property(x => x.ExpiresAtUtc).HasPrecision(7);
            entity.Property(x => x.RevokedAtUtc).HasPrecision(7);
            entity.Property(x => x.LastViewedAtUtc).HasPrecision(7);
        });

        builder.Entity<MediaAsset>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.StorageKey }).IsUnique();
            entity.Property(x => x.StorageKey).HasMaxLength(240);
            entity.Property(x => x.FileName).HasMaxLength(120);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.Property(x => x.ChecksumSha256).HasMaxLength(64);
            entity.Property(x => x.RetainUntilUtc).HasPrecision(7);
            entity.Property(x => x.ReadRevokedAtUtc).HasPrecision(7);
        });

        builder.Entity<MediaUploadSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.DriverId, x.CreatedAtUtc });
            entity.Property(x => x.FileName).HasMaxLength(120);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.Property(x => x.TempStorageKey).HasMaxLength(240);
            entity.Property(x => x.ExpiresAtUtc).HasPrecision(7);
            entity.Property(x => x.ScanReason).HasMaxLength(240);
            entity.Property(x => x.ContentChecksumSha256).HasMaxLength(64);
        });

        builder.Entity<DriverWorkflowCommandReceipt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.CommandId }).IsUnique();
            entity.Property(x => x.CommandId).HasMaxLength(80);
            entity.Property(x => x.ScopeType).HasMaxLength(40);
            entity.Property(x => x.ScopeId).HasMaxLength(80);
            entity.Property(x => x.ProcessedAtUtc).HasPrecision(7);
        });

        builder.Entity<OperationsExceptionState>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.ExceptionKey }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.SourceType, x.SourceEntityId });
            entity.Property(x => x.ExceptionKey).HasMaxLength(120);
            entity.Property(x => x.AssignedToDisplayName).HasMaxLength(120);
            entity.Property(x => x.AcknowledgedByDisplayName).HasMaxLength(120);
            entity.Property(x => x.ResolvedByDisplayName).HasMaxLength(120);
            entity.Property(x => x.ResolutionReason).HasMaxLength(280);
            entity.Property(x => x.SnoozeReason).HasMaxLength(280);
            entity.Property(x => x.AssignedAtUtc).HasPrecision(7);
            entity.Property(x => x.AcknowledgedAtUtc).HasPrecision(7);
            entity.Property(x => x.ResolvedAtUtc).HasPrecision(7);
            entity.Property(x => x.SnoozedUntilUtc).HasPrecision(7);
            entity.Property(x => x.LastDetectedAtUtc).HasPrecision(7);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<OperationsSavedView>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.CreatedByUserId, x.Name });
            entity.HasIndex(x => new { x.OrganizationId, x.IsShared });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.FilterJson).HasMaxLength(2000);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<DriverSyncExceptionIncident>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.IncidentKey }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.MissionId, x.LastOccurredAtUtc });
            entity.Property(x => x.IncidentKey).HasMaxLength(160);
            entity.Property(x => x.IncidentCode).HasMaxLength(64);
            entity.Property(x => x.Severity).HasMaxLength(24);
            entity.Property(x => x.ScopeType).HasMaxLength(48);
            entity.Property(x => x.Message).HasMaxLength(320);
            entity.Property(x => x.LastCommandId).HasMaxLength(80);
            entity.Property(x => x.FirstOccurredAtUtc).HasPrecision(7);
            entity.Property(x => x.LastOccurredAtUtc).HasPrecision(7);
            entity.Property(x => x.ResolvedAtUtc).HasPrecision(7);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.RegistrationNumber }).IsUnique();
            entity.Property(x => x.RegistrationNumber).HasMaxLength(32);
            entity.Property(x => x.DisplayName).HasMaxLength(128);
            entity.Property(x => x.CurrentOdometerKm);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<MaintenanceWorkOrder>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.SourceKey }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.Status });
            entity.HasIndex(x => new { x.OrganizationId, x.DueAtUtc });
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.SourceKey).HasMaxLength(160);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3);
            entity.Property(x => x.SupplierName).HasMaxLength(160);
            entity.Property(x => x.PartsDescription).HasMaxLength(500);
            entity.Property(x => x.TransitionReason).HasMaxLength(500);
            entity.Property(x => x.DueAtUtc).HasPrecision(7);
            entity.Property(x => x.ScheduledStartUtc).HasPrecision(7);
            entity.Property(x => x.ScheduledEndUtc).HasPrecision(7);
            entity.Property(x => x.CompletedAtUtc).HasPrecision(7);
            entity.Property(x => x.LaborCost).HasPrecision(18, 2);
            entity.Property(x => x.PartsCost).HasPrecision(18, 2);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<Driver>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.LicenseNumber }).IsUnique();
            entity.Property(x => x.FullName).HasMaxLength(160);
            entity.Property(x => x.LicenseNumber).HasMaxLength(64);
            entity.Property(x => x.PhoneNumber).HasMaxLength(40);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<GpsDevice>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.SerialNumber }).IsUnique();
            entity.Property(x => x.SerialNumber).HasMaxLength(64);
            entity.Property(x => x.DisplayName).HasMaxLength(128);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<DeviceAssignment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.DeviceId, x.UnassignedAtUtc })
                .IsUnique()
                .HasFilter("[UnassignedAtUtc] IS NULL");
            entity.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.UnassignedAtUtc });
            entity.Property(x => x.AssignedAtUtc).HasPrecision(7);
            entity.Property(x => x.UnassignedAtUtc).HasPrecision(7);
        });

        builder.Entity<TelemetryPoint>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.EventId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.RecordedAtUtc });
            entity.Property(x => x.DeviceId).HasMaxLength(64);
            entity.Property(x => x.EventId).HasMaxLength(128);
            entity.Property(x => x.RecordedAtUtc).HasPrecision(7);
            entity.Property(x => x.IngestedAtUtc).HasPrecision(7);
            entity.Property(x => x.HeadingDegrees).HasPrecision(6, 2);
            entity.Property(x => x.Source).HasMaxLength(32);
            entity.Property(x => x.AnomalyFlags).HasMaxLength(160);
            entity.Property(x => x.AccuracyMeters).HasPrecision(8, 2);
        });

        builder.Entity<CurrentVehiclePosition>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.VehicleId }).IsUnique();
            entity.Property(x => x.DeviceId).HasMaxLength(64);
            entity.Property(x => x.EventId).HasMaxLength(128);
            entity.Property(x => x.RecordedAtUtc).HasPrecision(7);
            entity.Property(x => x.IngestedAtUtc).HasPrecision(7);
            entity.Property(x => x.HeadingDegrees).HasPrecision(6, 2);
            entity.Property(x => x.Source).HasMaxLength(32);
            entity.Property(x => x.AnomalyFlags).HasMaxLength(160);
            entity.Property(x => x.AccuracyMeters).HasPrecision(8, 2);
        });

        builder.Entity<TrackingTrip>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.StartedAtUtc });
            entity.Property(x => x.StartedAtUtc).HasPrecision(7);
            entity.Property(x => x.EndedAtUtc).HasPrecision(7);
            entity.Property(x => x.CalculatedAtUtc).HasPrecision(7);
            entity.Property(x => x.DistanceKm).HasPrecision(12, 3);
            entity.Property(x => x.AlgorithmVersion).HasMaxLength(32);
        });

        builder.Entity<TrackingGeofence>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Shape).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.RadiusMeters).HasPrecision(10, 2);
            entity.Property(x => x.PolygonJson).HasMaxLength(4000);
            entity.Property(x => x.CreatedAtUtc).HasPrecision(7);
        });

        builder.Entity<TrackingGeofenceEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.GeofenceId, x.VehicleId, x.TelemetryEventId, x.Transition }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.OccurredAtUtc });
            entity.Property(x => x.TelemetryEventId).HasMaxLength(128);
            entity.Property(x => x.Transition).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.OccurredAtUtc).HasPrecision(7);
        });

        builder.Entity<ComplianceDocument>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.TargetType, x.TargetEntityId, x.DocumentType, x.ReplacedByDocumentId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.ExpiresAtUtc });
            entity.Property(x => x.DocumentType).HasMaxLength(64);
            entity.Property(x => x.DocumentNumber).HasMaxLength(64);
            entity.Property(x => x.ExpiresAtUtc).HasPrecision(7);
            entity.Property(x => x.Notes).HasMaxLength(280);
            entity.Property(x => x.ReviewStatus).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<VehicleMaintenancePlan>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.Title }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.IsActive });
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.Property(x => x.LastCompletedAtUtc).HasPrecision(7);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<OperationalAlert>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.DeduplicationKey }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.Severity });
            entity.HasIndex(x => new { x.OrganizationId, x.TargetType, x.TargetEntityId });
            entity.Property(x => x.DeduplicationKey).HasMaxLength(180);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Message).HasMaxLength(320);
            entity.Property(x => x.TargetType).HasMaxLength(48);
            entity.Property(x => x.AssignedToDisplayName).HasMaxLength(120);
            entity.Property(x => x.AcknowledgedByDisplayName).HasMaxLength(120);
            entity.Property(x => x.AssignedAtUtc).HasPrecision(7);
            entity.Property(x => x.AcknowledgedAtUtc).HasPrecision(7);
            entity.Property(x => x.LastDetectedAtUtc).HasPrecision(7);
            entity.Property(x => x.ResolvedAtUtc).HasPrecision(7);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<AlertNotification>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.AlertId, x.Channel });
            entity.HasIndex(x => new { x.OrganizationId, x.SentAtUtc });
            entity.Property(x => x.Subject).HasMaxLength(160);
            entity.Property(x => x.Body).HasMaxLength(800);
            entity.Property(x => x.SentAtUtc).HasPrecision(7);
        });

        builder.Entity<ApiClientCredential>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.KeyId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.IsActive, x.CredentialType });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.ScopeList).HasMaxLength(240);
            entity.Property(x => x.KeyId).HasMaxLength(48);
            entity.Property(x => x.SecretHash).HasMaxLength(128);
            entity.Property(x => x.SecretPreview).HasMaxLength(12);
            entity.Property(x => x.LastUsedAtUtc).HasPrecision(7);
            entity.Property(x => x.RevokedAtUtc).HasPrecision(7);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<WebhookEndpoint>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.EventType, x.IsActive });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.EventType).HasMaxLength(80);
            entity.Property(x => x.TargetUrl).HasMaxLength(280);
            entity.Property(x => x.SigningSecret).HasMaxLength(120);
            entity.Property(x => x.LastSucceededAtUtc).HasPrecision(7);
            entity.Property(x => x.DisabledAtUtc).HasPrecision(7);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<IntegrationOutboxMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.NextAttemptAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.WebhookEndpointId, x.EventType, x.OccurredAtUtc });
            entity.Property(x => x.EventType).HasMaxLength(80);
            entity.Property(x => x.AggregateType).HasMaxLength(64);
            entity.Property(x => x.AggregateId).HasMaxLength(80);
            entity.Property(x => x.PayloadJson).HasMaxLength(4000);
            entity.Property(x => x.OccurredAtUtc).HasPrecision(7);
            entity.Property(x => x.NextAttemptAtUtc).HasPrecision(7);
            entity.Property(x => x.DeliveredAtUtc).HasPrecision(7);
            entity.Property(x => x.DeadLetteredAtUtc).HasPrecision(7);
            entity.Property(x => x.LastError).HasMaxLength(500);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<WebhookDeliveryAttempt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.OutboxMessageId, x.WebhookEndpointId, x.AttemptNumber }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.AttemptedAtUtc });
            entity.Property(x => x.ResponseBody).HasMaxLength(1000);
            entity.Property(x => x.AttemptedAtUtc).HasPrecision(7);
        });

        builder.Entity<SandboxWebhookReceipt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.WebhookEndpointId, x.ReceivedAtUtc });
            entity.Property(x => x.EventType).HasMaxLength(80);
            entity.Property(x => x.Signature).HasMaxLength(180);
            entity.Property(x => x.PayloadJson).HasMaxLength(4000);
            entity.Property(x => x.ReceivedAtUtc).HasPrecision(7);
        });

        builder.Entity<SandboxTelematicsConnection>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.LastError).HasMaxLength(300);
            entity.Property(x => x.ResumeCursor).HasMaxLength(128);
            entity.Property(x => x.LastSucceededAtUtc).HasPrecision(7);
        });
    }

    private void EnsureAuditLogsAreImmutable()
    {
        var invalidAuditEntry = ChangeTracker.Entries<AuditLog>()
            .FirstOrDefault(x => x.State is EntityState.Modified or EntityState.Deleted);

        if (invalidAuditEntry is not null)
        {
            throw new InvalidOperationException("Audit logs are immutable and cannot be updated or deleted.");
        }
    }
}
