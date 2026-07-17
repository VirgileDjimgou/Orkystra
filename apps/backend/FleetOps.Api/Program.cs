using System.Text;
using System.Threading.RateLimiting;
using FleetOps.Api;
using FleetOps.Api.Admin;
using FleetOps.Api.Alerts;
using FleetOps.Api.Auditing;
using FleetOps.Api.Auth;
using FleetOps.Api.Dispatch;
using FleetOps.Api.DriverApp;
using FleetOps.Api.Fleet;
using FleetOps.Api.Integrations;
using FleetOps.Api.Media;
using FleetOps.Api.Observability;
using FleetOps.Api.Operations;
using FleetOps.Api.Security;
using FleetOps.Api.Tracking;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Infrastructure;
using FleetOps.Infrastructure.Integrations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var webOrigin = builder.Configuration["FLEETOPS_WEB_URL"] ?? "http://localhost:5183";
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

ProductionConfigurationValidator.Validate(builder.Configuration, builder.Environment);

builder.Services.AddOpenApi();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
});
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadyHealthCheck>("database", tags: ["ready"]);
builder.Services.AddSignalR();
builder.Services.AddFleetOpsInfrastructure(builder.Configuration);
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<TrackingOptions>(builder.Configuration.GetSection(TrackingOptions.SectionName));
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "fleetops-api",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "dev"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
            })
            .AddHttpClientInstrumentation(options => options.RecordException = true);

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddMeter("System.Net.Http");

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter();
        }
    });
builder.Services.AddSingleton<ICurrentTenantAccessor, CurrentTenantAccessor>();
builder.Services.AddScoped<IJwtTokenIssuer, JwtTokenIssuer>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<TrackingIngestionService>();
builder.Services.AddSingleton<TrackingMetricsStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMediaUrlSigner, MediaUrlSigner>();
builder.Services.AddSingleton<IAuthorizationHandler, ApiKeyScopeHandler>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/tracking"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("partner-fleet-read", policy =>
    {
        policy.AuthenticationSchemes.Add(ApiKeyAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new ApiKeyScopeRequirement(IntegrationScope.PartnerFleetRead));
    });
    options.AddPolicy("device-tracking-write", policy =>
    {
        policy.AuthenticationSchemes.Add(ApiKeyAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new ApiKeyScopeRequirement(IntegrationScope.DeviceTrackingWrite));
    });
    options.AddPolicy("partner-webhook-read", policy =>
    {
        policy.AuthenticationSchemes.Add(ApiKeyAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new ApiKeyScopeRequirement(IntegrationScope.PartnerWebhookRead));
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(
                    1,
                    builder.Configuration.GetValue("Security:LoginPermitLimit", 30)),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddFixedWindowLimiter("integration-admin", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("integration-api-key", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(webOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FleetOps.Infrastructure.Identity.ApplicationUser>>();
    var bootstrapOptions = scope.ServiceProvider.GetRequiredService<IOptions<BootstrapOptions>>().Value;
    await FleetOpsSeedData.EnsureSeededAsync(
        dbContext,
        roleManager,
        userManager,
        bootstrapOptions,
        CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapHub<TrackingHub>("/hubs/tracking").RequireAuthorization();
app.MapAuthEndpoints();
app.MapUserAdministrationEndpoints();
app.MapSecurityAdministrationEndpoints();
app.MapDataLifecycleAdministrationEndpoints();
app.MapIntegrationAdministrationEndpoints();
app.MapIntegrationPartnerEndpoints();
app.MapIntegrationSandboxEndpoints();
app.MapVehicleEndpoints();
app.MapDriverEndpoints();
app.MapDeviceEndpoints();
app.MapFleetAlertConfigurationEndpoints();
app.MapDispatchEndpoints();
app.MapDriverAppEndpoints();
app.MapDriverOperationsEndpoints();
app.MapMediaEndpoints();
app.MapTrackingEndpoints();
app.MapAlertEndpoints();

app.MapGet("/api/system/info", () => Results.Ok(new
{
    name = "Orkystra FleetOps",
    status = "tracking-ready",
    utc = DateTimeOffset.UtcNow
}));

app.Run();

public partial class Program;
