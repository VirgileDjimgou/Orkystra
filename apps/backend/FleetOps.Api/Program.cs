using System.Collections.Concurrent;
using System.Text;
using FleetOps.Api;
using FleetOps.Api.Admin;
using FleetOps.Api.Auditing;
using FleetOps.Api.Auth;
using FleetOps.Api.Fleet;
using FleetOps.Api.Security;
using FleetOps.Infrastructure;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var webOrigin = builder.Configuration["FLEETOPS_WEB_URL"] ?? "http://localhost:5183";

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();
builder.Services.AddFleetOpsInfrastructure(builder.Configuration);
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<ICurrentTenantAccessor, CurrentTenantAccessor>();
builder.Services.AddScoped<IJwtTokenIssuer, JwtTokenIssuer>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(webOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
builder.Services.AddSingleton<ConcurrentDictionary<Guid, TelemetryContract>>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FleetOps.Infrastructure.Identity.ApplicationUser>>();
    await FleetOpsSeedData.EnsureSeededAsync(dbContext, roleManager, userManager, CancellationToken.None);
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
app.MapHealthChecks("/health");
app.MapHub<TrackingHub>("/hubs/tracking").RequireAuthorization();
app.MapAuthEndpoints();
app.MapUserAdministrationEndpoints();
app.MapVehicleEndpoints();
app.MapDriverEndpoints();
app.MapDeviceEndpoints();

app.MapGet("/api/system/info", () => Results.Ok(new
{
    name = "Zynro Fleet",
    status = "identity-ready",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/tracking/latest", (
    HttpContext httpContext,
    ConcurrentDictionary<Guid, TelemetryContract> positions,
    ICurrentTenantAccessor currentTenantAccessor) =>
{
    var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
    return Results.Ok(positions.Values
        .Where(x => x.OrganizationId == tenant.OrganizationId)
        .OrderBy(x => x.VehicleId));
}).RequireAuthorization();

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
    await hub.Clients.Group($"organization:{point.OrganizationId}")
        .SendAsync("telemetryUpdated", point, cancellationToken);
    return Results.Accepted();
});

app.Run();

public partial class Program;
