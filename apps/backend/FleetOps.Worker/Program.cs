using System.Text.Json;
using FleetOps.Infrastructure;
using FleetOps.Infrastructure.Storage;
using FleetOps.Worker;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
});
builder.Services.AddFleetOpsInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "fleetops-worker",
        serviceVersion: typeof(Worker).Assembly.GetName().Version?.ToString() ?? "dev"))
    .WithTracing(tracing =>
    {
        tracing.AddHttpClientInstrumentation(options => options.RecordException = true);
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("System.Net.Http");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter();
        }
    });
builder.Services.AddHostedService<Worker>();
using var host = builder.Build();
if (args.Contains("--migrate-media", StringComparer.Ordinal))
{
    await using var scope = host.Services.CreateAsyncScope();
    var report = await scope.ServiceProvider.GetRequiredService<MediaMigrationService>().MigrateAsync(CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    Environment.ExitCode = report.Errors == 0 ? 0 : 2;
    return;
}
await host.RunAsync();
