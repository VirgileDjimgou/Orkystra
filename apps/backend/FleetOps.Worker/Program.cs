using FleetOps.Infrastructure;
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
await builder.Build().RunAsync();
