using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetOps.Core.Modules.Demo;
using Microsoft.Extensions.Options;

namespace FleetOps.Worker.Demo;

public sealed class HttpVirtualDriverTools(IHttpClientFactory clients, IOptions<DemoEngineOptions> options) : IVirtualDriverTools
{
    private const string SamplePng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    public async Task<VirtualDriverToolResult> ExecuteAsync(VirtualDriverToolCommand command, CancellationToken cancellationToken)
    {
        try
        {
            return command.Action switch
            {
                VirtualDriverAction.SubmitInspection => await SubmitInspectionAsync(command, cancellationToken),
                VirtualDriverAction.StartMission => await SendMissionCommandAsync(command, 1, cancellationToken),
                VirtualDriverAction.ArriveAtStop => await SendMissionCommandAsync(command, 2, cancellationToken),
                VirtualDriverAction.SubmitProof => await SubmitProofAsync(command, cancellationToken),
                VirtualDriverAction.CompleteMission => await SendMissionCommandAsync(command, 3, cancellationToken),
                VirtualDriverAction.ReportDelay => await ReportDelayAsync(command, cancellationToken),
                VirtualDriverAction.ReportVehicleIssue => await SubmitInspectionAsync(command, cancellationToken, issue: true),
                VirtualDriverAction.ReportOffline => new(true, false, "offline-recorded", "Synthetic connectivity loss recorded."),
                VirtualDriverAction.Recover => new(true, false, "recovered", "Deterministic recovery policy completed."),
                _ => new(false, false, "unsupported", "Unsupported virtual-driver action.")
            };
        }
        catch (HttpRequestException exception)
        {
            return new(false, true, "http-retry", exception.StatusCode?.ToString() ?? "Workflow temporarily unavailable.");
        }
    }

    private async Task<VirtualDriverToolResult> SubmitInspectionAsync(VirtualDriverToolCommand command, CancellationToken cancellationToken, bool issue = false)
    {
        var client = DriverClient(command.Identity.DriverId);
        var workflow = await client.GetFromJsonAsync<InspectionWorkflow>($"/api/v1/driver/missions/{command.Identity.MissionId}/inspection", cancellationToken)
            ?? throw new HttpRequestException("Inspection workflow returned no content.");
        var items = workflow.ChecklistItems.Select(item => new
        {
            item.Sequence,
            item.Code,
            item.Label,
            IsPass = !issue,
            DefectSeverity = issue ? 2 : 0,
            Notes = issue ? "Synthetic vehicle issue." : "Synthetic pre-trip check passed.",
            PhotoAssetId = (Guid?)null
        });
        using var response = await client.PostAsJsonAsync($"/api/v1/driver/missions/{command.Identity.MissionId}/inspection", new
        {
            CommandId = command.IdempotencyKey,
            CompletedAtUtc = command.OccurredAtUtc,
            Notes = issue ? "Controlled Demo issue." : "Deterministic Demo inspection.",
            Items = items
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new(true, false, issue ? "issue-reported" : "inspection-passed", issue ? "Vehicle issue entered through inspection workflow." : "Pre-trip inspection accepted.");
    }

    private async Task<VirtualDriverToolResult> SendMissionCommandAsync(VirtualDriverToolCommand command, int action, CancellationToken cancellationToken)
    {
        var client = DriverClient(command.Identity.DriverId);
        var mission = await client.GetFromJsonAsync<MissionDetail>($"/api/v1/driver/missions/{command.Identity.MissionId}", cancellationToken)
            ?? throw new HttpRequestException("Mission returned no content.");
        using var response = await client.PostAsJsonAsync($"/api/v1/driver/missions/{command.Identity.MissionId}/commands", new
        {
            CommandId = command.IdempotencyKey,
            Action = action,
            mission.RowVersion,
            command.OccurredAtUtc
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new(true, false, "mission-command-accepted", command.Action.ToString());
    }

    private async Task<VirtualDriverToolResult> SubmitProofAsync(VirtualDriverToolCommand command, CancellationToken cancellationToken)
    {
        var client = DriverClient(command.Identity.DriverId);
        var bytes = Convert.FromBase64String(SamplePng);
        using var create = await client.PostAsJsonAsync("/api/v1/driver/uploads/sessions", new { FileName = "synthetic-proof.png", ContentType = "image/png", TotalBytes = bytes.Length, Purpose = 2 }, cancellationToken);
        create.EnsureSuccessStatusCode();
        var session = (await create.Content.ReadFromJsonAsync<UploadSession>(cancellationToken: cancellationToken))!;
        using var append = await client.PostAsJsonAsync($"/api/v1/driver/uploads/sessions/{session.UploadSessionId}/chunks", new { Offset = 0, Base64Content = SamplePng }, cancellationToken);
        append.EnsureSuccessStatusCode();
        using var complete = await client.PostAsync($"/api/v1/driver/uploads/sessions/{session.UploadSessionId}/complete", null, cancellationToken);
        complete.EnsureSuccessStatusCode();
        var asset = (await complete.Content.ReadFromJsonAsync<CompletedUpload>(cancellationToken: cancellationToken))!;
        using var proof = await client.PostAsJsonAsync($"/api/v1/driver/missions/{command.Identity.MissionId}/stops/{command.Identity.StopId}/proof", new
        {
            CommandId = command.IdempotencyKey,
            RecipientName = "Synthetic Recipient",
            SignatureName = "Synthetic Recipient",
            DeliveredAtUtc = command.OccurredAtUtc,
            Notes = "SIMULATED DEMO PROOF",
            Photos = new[] { new { MediaAssetId = asset.AssetId, Caption = "Delivery photo" }, new { MediaAssetId = asset.AssetId, Caption = "Recipient signature" } }
        }, cancellationToken);
        proof.EnsureSuccessStatusCode();
        return new(true, false, "proof-accepted", "Synthetic delivery proof accepted through Driver workflow.");
    }

    private async Task<VirtualDriverToolResult> ReportDelayAsync(VirtualDriverToolCommand command, CancellationToken cancellationToken)
    {
        var client = OperatorClient();
        var mission = await client.GetFromJsonAsync<MissionDetail>($"/api/v1/dispatch/missions/{command.Identity.MissionId}", cancellationToken)
            ?? throw new HttpRequestException("Mission returned no content.");
        using var response = await client.PostAsJsonAsync($"/api/v1/dispatch/missions/{command.Identity.MissionId}/delay-simulation", new { DelayMinutes = 15, mission.RowVersion }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new(true, false, "delay-reported", "Controlled 15-minute delay recorded.");
    }

    private HttpClient DriverClient(Guid driverId)
    {
        var agentToken = options.Value.Agents.FirstOrDefault(agent => agent.DriverId == driverId)?.DriverAccessToken;
        return CreateClient(nameof(HttpVirtualDriverTools) + ":driver", agentToken ?? options.Value.DriverAccessToken);
    }
    private HttpClient OperatorClient() => CreateClient(nameof(HttpVirtualDriverTools) + ":operator", options.Value.ApiAccessToken);
    private HttpClient CreateClient(string name, string? token)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiBaseUrl) || string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Virtual driver tools require API URL and scoped access tokens.");
        var client = clients.CreateClient(name);
        client.BaseAddress = new Uri(options.Value.ApiBaseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed record ChecklistItem(int Sequence, string Code, string Label);
    private sealed record InspectionWorkflow(IReadOnlyList<ChecklistItem> ChecklistItems);
    private sealed record MissionDetail(long RowVersion);
    private sealed record UploadSession(Guid UploadSessionId);
    private sealed record CompletedUpload(Guid AssetId);
}
