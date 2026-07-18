using System.Net;
using System.Text.Json.Nodes;

namespace FleetOps.Simulation;

public sealed record TenantSimulationResult(
    SimulationTenant Tenant,
    SimulationSession Admin,
    SimulationSession Operator,
    SimulationSession Driver,
    Guid MissionId);

public sealed class FullFleetScenario(FleetOpsSimulationClient client, SimulationReport report)
{
    private const string SamplePngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9pN96ZQAAAAASUVORK5CYII=";

    public async Task RunAsync(IReadOnlyList<SimulationTenant> tenants, CancellationToken cancellationToken)
    {
        var results = new List<TenantSimulationResult>();
        for (var index = 0; index < tenants.Count; index++)
        {
            results.Add(await RunTenantAsync(tenants[index], index, cancellationToken));
        }

        if (results.Count > 1)
        {
            await VerifyTenantIsolationAsync(results, cancellationToken);
        }
        report.Complete();
    }

    private async Task<TenantSimulationResult> RunTenantAsync(
        SimulationTenant tenant,
        int tenantIndex,
        CancellationToken cancellationToken)
    {
        var admin = await client.LoginAsync(tenant.Admin, tenant.Name, cancellationToken);
        var operatorSession = await client.LoginAsync(tenant.Operator, tenant.Name, cancellationToken);
        var driver = await client.LoginAsync(tenant.Driver, tenant.Name, cancellationToken);
        if (driver.DriverId is null)
        {
            throw new InvalidOperationException($"{tenant.Name} Driver is not bound to a driver profile.");
        }

        report.Add(tenant.Name, "Admin / Operator / Driver", "Identity", "All three authenticated sessions resolve to the expected tenant and role.");
        await client.AssertStatusAsync(operatorSession, HttpMethod.Get, "/api/v1/pilot/metrics", HttpStatusCode.Forbidden, cancellationToken);
        await client.AssertStatusAsync(driver, HttpMethod.Get, "/api/v1/pilot/metrics", HttpStatusCode.Forbidden, cancellationToken);
        report.Add(tenant.Name, "Operator / Driver", "Authorization", "Pilot administration is denied with HTTP 403 outside the Admin role.");

        var vehicles = RequireArray(await client.GetAsync(operatorSession, "/api/v1/fleet/vehicles", cancellationToken), "vehicles");
        var drivers = RequireArray(await client.GetAsync(operatorSession, "/api/v1/fleet/drivers", cancellationToken), "drivers");
        var devices = RequireArray(await client.GetAsync(operatorSession, "/api/v1/fleet/devices", cancellationToken), "devices");
        var vehicle = vehicles.FirstOrDefault(x => x?["registrationNumber"]?.GetValue<string>().StartsWith(tenant.RegistrationPrefix, StringComparison.Ordinal) == true)
            ?? throw new InvalidOperationException($"No vehicle found for {tenant.Name}.");
        var driverRecord = drivers.FirstOrDefault(x => x?["licenseNumber"]?.GetValue<string>() == tenant.DriverLicense)
            ?? throw new InvalidOperationException($"No driver found for {tenant.Name}.");
        report.Add(tenant.Name, "Operator", "Fleet registry", $"Read {vehicles.Count} vehicle(s), {drivers.Count} driver(s), and {devices.Count} device(s) inside the tenant.");

        await SimulateTrackingAsync(tenant, tenantIndex, cancellationToken);
        report.Add(tenant.Name, "Device", "Tracking", "Published deterministic telemetry through the internal device ingestion contract.");

        var mission = await CreateAssignedMissionAsync(tenant, tenantIndex, operatorSession, driverRecord, vehicle, cancellationToken);
        var missionId = RequireGuid(mission, "id");
        var stops = RequireArray(mission["stops"], "mission stops");
        report.Add(tenant.Name, "Operator", "Dispatch", $"Created, planned and assigned mission {mission["reference"]?.GetValue<string>()}.");

        await SubmitInspectionAsync(tenant, driver, missionId, cancellationToken);
        var assetId = await UploadProofAssetAsync(tenant, driver, cancellationToken);
        await SubmitProofAsync(tenant, driver, missionId, RequireGuid(stops[^1]!, "id"), assetId, cancellationToken);
        mission = await ExecuteDriverCommandsAsync(tenant, driver, mission, cancellationToken);
        report.Add(tenant.Name, "Driver", "Field workflow", "Submitted a pre-departure inspection, private proof image and idempotent Start/Arrive/Complete commands.");

        await RunMaintenanceAsync(tenant, admin, vehicle, cancellationToken);
        report.Add(tenant.Name, "Admin", "Maintenance", "Created, costed, scheduled and completed a tenant-scoped work order.");

        await RunComplianceAndAlertsAsync(tenant, admin, vehicle, cancellationToken);
        report.Add(tenant.Name, "Admin", "Compliance and alerts", "Configured a fictitious expiring vehicle document, scanned alerts and read the compliance matrix.");

        await client.GetAsync(admin, "/api/v1/admin/integrations/contracts", cancellationToken);
        await client.GetAsync(operatorSession, "/api/v1/operations/exceptions?page=1&pageSize=20", cancellationToken);
        report.Add(tenant.Name, "Admin / Operator", "Integrations and operations", "Read integration contracts and the exception-driven operations queue.");

        await client.PutNoContentAsync(admin, "/api/v1/pilot/consent", new { analyticsConsent = true }, cancellationToken);
        await client.PostAsync(admin, "/api/v1/pilot/metrics/collect", null, cancellationToken);
        var incident = await client.PostCreatedAsync(
            admin,
            "/api/v1/pilot/incidents",
            new
            {
                severity = 2,
                category = "simulation",
                summary = $"Fictitious {tenant.Sector} support rehearsal.",
                workaround = "No action required; generated by the development simulator.",
            },
            cancellationToken);
        await client.PostAsync(
            admin,
            $"/api/v1/pilot/incidents/{RequireGuid(incident, "id")}/resolve",
            new { workaround = "Simulation completed successfully." },
            cancellationToken);
        var export = await client.GetAsync(admin, "/api/v1/pilot/export", cancellationToken);
        if (RequireArray(export["decisions"], "pilot decisions").Count != 0)
        {
            throw new InvalidOperationException("A clean simulation must not fabricate a commercial pilot decision.");
        }

        report.Add(tenant.Name, "Admin", "Measured alpha", "Enabled aggregate consent, refreshed daily metrics, resolved a labelled simulation incident and left commercial decisions empty.");

        _ = mission;
        return new TenantSimulationResult(tenant, admin, operatorSession, driver, missionId);
    }

    private async Task SimulateTrackingAsync(SimulationTenant tenant, int tenantIndex, CancellationToken cancellationToken)
    {
        var scenario = await client.SendAnonymousAsync(
            HttpMethod.Get,
            $"/api/internal/v1/tracking/scenarios/{tenant.Slug}",
            null,
            HttpStatusCode.OK,
            cancellationToken);
        var scenarioVehicle = RequireArray(scenario["vehicles"], "tracking scenario vehicles")[0]
            ?? throw new InvalidOperationException("Tracking scenario has no vehicle.");
        var recordedAtUtc = DateTimeOffset.UtcNow.AddSeconds(tenantIndex);
        await client.SendAnonymousAsync(
            HttpMethod.Post,
            "/api/internal/v1/tracking/events",
            new
            {
                organizationId = RequireGuid(scenario, "organizationId"),
                vehicleId = RequireGuid(scenarioVehicle, "vehicleId"),
                deviceId = RequireString(scenarioVehicle, "deviceId"),
                eventId = $"full-simulation:{report.RunId}:{tenant.Slug}",
                recordedAtUtc,
                latitude = 48.49 + tenantIndex * 0.02,
                longitude = 9.20 + tenantIndex * 0.02,
                speedKph = 31 + tenantIndex * 7,
                headingDegrees = 85 + tenantIndex * 20,
            },
            HttpStatusCode.Accepted,
            cancellationToken);
    }

    private async Task<JsonNode> CreateAssignedMissionAsync(
        SimulationTenant tenant,
        int tenantIndex,
        SimulationSession operatorSession,
        JsonNode driver,
        JsonNode vehicle,
        CancellationToken cancellationToken)
    {
        var start = DateTimeOffset.UtcNow.AddHours(3 + tenantIndex * 4);
        var reference = $"SIM-{tenant.RegistrationPrefix.TrimEnd('-')}-{report.RunId}";
        var mission = await client.PostCreatedAsync(
            operatorSession,
            "/api/v1/dispatch/missions",
            new
            {
                reference,
                title = $"{tenant.Sector} full workflow",
                scheduledStartUtc = start,
                scheduledEndUtc = start.AddHours(2),
                stops = new[]
                {
                    new { sequence = 1, name = $"{tenant.Name} depot", address = "1 Simulation Way", plannedArrivalUtc = start.AddMinutes(15) },
                    new { sequence = 2, name = "Fictitious customer", address = "22 Demonstration Street", plannedArrivalUtc = start.AddMinutes(90) },
                },
            },
            cancellationToken);
        mission = await client.PostAsync(
            operatorSession,
            $"/api/v1/dispatch/missions/{RequireGuid(mission, "id")}/status",
            new { targetStatus = 1, rowVersion = RequireLong(mission, "rowVersion") },
            cancellationToken);
        mission = await client.PutAsync(
            operatorSession,
            $"/api/v1/dispatch/missions/{RequireGuid(mission, "id")}/assignment",
            new { driverId = RequireGuid(driver, "id"), vehicleId = RequireGuid(vehicle, "id"), rowVersion = RequireLong(mission, "rowVersion") },
            cancellationToken);
        return await client.PostAsync(
            operatorSession,
            $"/api/v1/dispatch/missions/{RequireGuid(mission, "id")}/status",
            new { targetStatus = 2, rowVersion = RequireLong(mission, "rowVersion") },
            cancellationToken);
    }

    private async Task SubmitInspectionAsync(
        SimulationTenant tenant,
        SimulationSession driver,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brakes"] = "Brakes and steering",
            ["lights"] = "Lights and signals",
            ["cargo-secured"] = "Cargo and doors secured",
            ["equipment-secured"] = "Tools and equipment secured",
        };
        await client.PostAsync(
            driver,
            $"/api/v1/driver/missions/{missionId}/inspection",
            new
            {
                commandId = $"inspection:{report.RunId}:{tenant.Slug}",
                completedAtUtc = DateTimeOffset.UtcNow,
                notes = "Fictitious vehicle readiness check generated by the full simulator.",
                items = tenant.ChecklistCodes.Select((code, index) => new
                {
                    sequence = index + 1,
                    code,
                    label = labels[code],
                    isPass = true,
                    defectSeverity = 0,
                    notes = (string?)null,
                    photoAssetId = (Guid?)null,
                }).ToArray(),
            },
            cancellationToken);
    }

    private async Task<Guid> UploadProofAssetAsync(
        SimulationTenant tenant,
        SimulationSession driver,
        CancellationToken cancellationToken)
    {
        var byteCount = Convert.FromBase64String(SamplePngBase64).LongLength;
        var upload = await client.PostAsync(
            driver,
            "/api/v1/driver/uploads/sessions",
            new { fileName = $"{tenant.Slug}-simulation-proof.png", contentType = "image/png", totalBytes = byteCount, purpose = 2 },
            cancellationToken);
        var uploadId = RequireGuid(upload, "uploadSessionId");
        await client.PostAsync(
            driver,
            $"/api/v1/driver/uploads/sessions/{uploadId}/chunks",
            new { offset = 0, base64Content = SamplePngBase64 },
            cancellationToken);
        var asset = await client.PostAsync(
            driver,
            $"/api/v1/driver/uploads/sessions/{uploadId}/complete",
            null,
            cancellationToken);
        return RequireGuid(asset, "assetId");
    }

    private Task<JsonNode> SubmitProofAsync(
        SimulationTenant tenant,
        SimulationSession driver,
        Guid missionId,
        Guid stopId,
        Guid assetId,
        CancellationToken cancellationToken) =>
        client.PostAsync(
            driver,
            $"/api/v1/driver/missions/{missionId}/stops/{stopId}/proof",
            new
            {
                commandId = $"proof:{report.RunId}:{tenant.Slug}",
                recipientName = "Fictitious Receiver",
                signatureName = "Fictitious Receiver",
                deliveredAtUtc = DateTimeOffset.UtcNow,
                notes = "Simulation proof; no real customer or personal data.",
                photos = new[]
                {
                    new { mediaAssetId = assetId, caption = "Delivery photo" },
                    new { mediaAssetId = assetId, caption = "Recipient signature" },
                },
            },
            cancellationToken);

    private async Task<JsonNode> ExecuteDriverCommandsAsync(
        SimulationTenant tenant,
        SimulationSession driver,
        JsonNode mission,
        CancellationToken cancellationToken)
    {
        foreach (var (action, label) in new[] { (1, "start"), (2, "arrive"), (3, "complete") })
        {
            var response = await client.PostAsync(
                driver,
                $"/api/v1/driver/missions/{RequireGuid(mission, "id")}/commands",
                new
                {
                    commandId = $"{label}:{report.RunId}:{tenant.Slug}",
                    action,
                    rowVersion = RequireLong(mission, "rowVersion"),
                    occurredAtUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken);
            mission = response["mission"] ?? throw new InvalidOperationException("Driver command returned no mission.");
        }

        return mission;
    }

    private async Task RunMaintenanceAsync(
        SimulationTenant tenant,
        SimulationSession admin,
        JsonNode vehicle,
        CancellationToken cancellationToken)
    {
        var workOrder = await client.PostCreatedAsync(
            admin,
            "/api/v1/maintenance/work-orders",
            new
            {
                vehicleId = RequireGuid(vehicle, "id"),
                title = "Simulation preventive inspection",
                sourceKey = $"simulation:{report.RunId}:{tenant.Slug}",
                priority = 2,
                dueAtUtc = DateTimeOffset.UtcNow.AddDays(3),
                immobilizesVehicle = false,
            },
            cancellationToken);
        workOrder = await client.PutAsync(
            admin,
            $"/api/v1/maintenance/work-orders/{RequireGuid(workOrder, "id")}/cost",
            new
            {
                laborCost = 89.50m,
                partsCost = 24.90m,
                currencyCode = "EUR",
                supplierName = "Fictitious Workshop",
                partsDescription = "Simulation consumables",
                attachmentMediaAssetId = (Guid?)null,
                rowVersion = RequireLong(workOrder, "rowVersion"),
            },
            cancellationToken);
        workOrder = await client.PostAsync(
            admin,
            $"/api/v1/maintenance/work-orders/{RequireGuid(workOrder, "id")}/schedule",
            new
            {
                scheduledStartUtc = DateTimeOffset.UtcNow.AddDays(1),
                scheduledEndUtc = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
                reason = "Simulation workshop slot",
                rowVersion = RequireLong(workOrder, "rowVersion"),
            },
            cancellationToken);
        await client.PostAsync(
            admin,
            $"/api/v1/maintenance/work-orders/{RequireGuid(workOrder, "id")}/complete",
            new { reason = "Simulation work completed", rowVersion = RequireLong(workOrder, "rowVersion") },
            cancellationToken);
    }

    private async Task RunComplianceAndAlertsAsync(
        SimulationTenant tenant,
        SimulationSession admin,
        JsonNode vehicle,
        CancellationToken cancellationToken)
    {
        var documentType = await client.PostAsync(
            admin,
            "/api/v1/compliance/document-types",
            new { name = "Simulation vehicle certificate", subjectType = 0, isBlocking = false, requiresReview = false, isActive = true, rowVersion = (long?)null },
            cancellationToken);
        await client.PostCreatedAsync(
            admin,
            "/api/v1/compliance/documents",
            new
            {
                documentTypeId = RequireGuid(documentType, "id"),
                subjectType = (int?)null,
                targetEntityId = RequireGuid(vehicle, "id"),
                documentType = "Simulation vehicle certificate",
                documentNumber = $"SIM-{tenant.Slug.ToUpperInvariant()}-{report.RunId}",
                expiresAtUtc = DateTimeOffset.UtcNow.AddDays(14),
                notes = "Fictitious document generated by the development simulator.",
                mediaAssetId = (Guid?)null,
                replacesDocumentId = (Guid?)null,
            },
            cancellationToken);
        await client.GetAsync(admin, "/api/v1/compliance/matrix", cancellationToken);
        await client.PostAsync(admin, "/api/v1/alerts/scan", null, cancellationToken);
        await client.GetAsync(admin, "/api/v1/alerts/dashboard", cancellationToken);
    }

    private async Task VerifyTenantIsolationAsync(
        List<TenantSimulationResult> results,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < results.Count; index++)
        {
            var owner = results[index];
            var foreign = results[(index + 1) % results.Count];
            await client.AssertStatusAsync(
                foreign.Operator,
                HttpMethod.Get,
                $"/api/v1/dispatch/missions/{owner.MissionId}",
                HttpStatusCode.NotFound,
                cancellationToken);
            report.Add(foreign.Tenant.Name, "Operator", "Tenant isolation", $"Could not discover {owner.Tenant.Name} mission {owner.MissionId}; API returned 404.");
        }
    }

    private static JsonArray RequireArray(JsonNode? node, string name) =>
        node as JsonArray ?? throw new InvalidOperationException($"Expected JSON array for {name}.");

    private static Guid RequireGuid(JsonNode node, string property) =>
        Guid.TryParse(node[property]?.ToString(), out var value)
            ? value
            : throw new InvalidOperationException($"Expected GUID property {property}.");

    private static long RequireLong(JsonNode node, string property) =>
        node[property]?.GetValue<long>() ?? throw new InvalidOperationException($"Expected numeric property {property}.");

    private static string RequireString(JsonNode node, string property) =>
        node[property]?.GetValue<string>() ?? throw new InvalidOperationException($"Expected string property {property}.");
}
