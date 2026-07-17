using System.IdentityModel.Tokens.Jwt;
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
using FleetOps.Api.Maintenance;
using FleetOps.Api.Media;
using FleetOps.Api.Observability;
using FleetOps.Api.Onboarding;
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
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<IOperationsRealtimeNotifier, OperationsRealtimeNotifier>();
builder.Services.AddScoped<IDriverSyncIncidentService, DriverSyncIncidentService>();
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
                else if (string.IsNullOrEmpty(context.Token)
                    && context.Request.Cookies.TryGetValue(WebSessionSecurity.AuthenticationCookie, out var cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var sessionClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;
                if (!Guid.TryParse(sessionClaim, out var sessionId))
                {
                    context.Fail("The token is not associated with a server-side session.");
                    return;
                }

                var userClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var organizationClaim = context.Principal?.FindFirst(TenantClaimTypes.OrganizationId)?.Value;
                if (!Guid.TryParse(userClaim, out var userId)
                    || !Guid.TryParse(organizationClaim, out var organizationId))
                {
                    context.Fail("The token does not contain a valid tenant identity.");
                    return;
                }

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<FleetOpsDbContext>();
                var now = DateTimeOffset.UtcNow;
                var isActive = await dbContext.UserSessions.AnyAsync(
                    x => x.Id == sessionId
                        && x.UserId == userId
                        && x.OrganizationId == organizationId
                        && x.RevokedAtUtc == null
                        && x.ExpiresAtUtc > now,
                    context.HttpContext.RequestAborted);
                if (!isActive)
                {
                    context.Fail("The session has expired or has been revoked.");
                }
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    foreach (var (policyName, roles) in AuthorizationPolicies.RoleMatrix)
    {
        options.AddPolicy(policyName, policy => policy.RequireRole(roles));
    }

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
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    await next();
});
app.UseAuthentication();
app.UseMiddleware<CsrfProtectionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapHub<TrackingHub>("/hubs/tracking").RequireAuthorization();
app.MapHub<OperationsHub>("/hubs/operations").RequireAuthorization();
app.MapAuthEndpoints();
app.MapUserAdministrationEndpoints();
app.MapSecurityAdministrationEndpoints();
app.MapOnboardingEndpoints();
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
app.MapOperationsCenterEndpoints();
app.MapMediaEndpoints();
app.MapMaintenanceEndpoints();
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
