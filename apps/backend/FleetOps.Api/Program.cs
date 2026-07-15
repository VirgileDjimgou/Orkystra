using System.Collections.Concurrent;
using FleetOps.Api;
using FleetOps.Infrastructure;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();
builder.Services.AddFleetOpsInfrastructure(builder.Configuration);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
builder.Services.AddSingleton<ConcurrentDictionary<Guid, TelemetryContract>>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.MapHealthChecks("/health");
app.MapHub<TrackingHub>("/hubs/tracking");

app.MapGet("/api/system/info", () => Results.Ok(new
{
    name = "FleetOps",
    status = "bootstrap",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/tracking/latest", (
    ConcurrentDictionary<Guid, TelemetryContract> positions) =>
    Results.Ok(positions.Values.OrderBy(x => x.VehicleId)));

app.MapPost("/api/simulation/telemetry", async (
    TelemetryContract point,
    ConcurrentDictionary<Guid, TelemetryContract> positions,
    IHubContext<TrackingHub> hub,
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    if (!environment.IsDevelopment())
    {
        return Results.NotFound();
    }

    if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["coordinates"] = ["Latitude or longitude is outside the valid range."]
        });
    }

    positions[point.VehicleId] = point with { RecordedAtUtc = point.RecordedAtUtc.ToUniversalTime() };
    await hub.Clients.All.SendAsync("telemetryUpdated", point, cancellationToken);
    return Results.Accepted();
});

app.Run();

public sealed record TelemetryContract(
    Guid OrganizationId,
    Guid VehicleId,
    string DeviceId,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees);

public partial class Program;
