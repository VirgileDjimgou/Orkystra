using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FleetOps.Api.Auditing;
using FleetOps.Api.Auth;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Onboarding;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetOps.Api.Onboarding;

public static class OnboardingEndpointExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedActivationEvents = new(StringComparer.Ordinal)
    {
        "step_viewed",
        "help_opened",
        "onboarding_abandoned",
        "onboarding_resumed"
    };

    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        var secured = app.MapGroup("/api/v1/onboarding")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
        secured.MapGet("/status", GetStatusAsync);
        secured.MapGet("/imports/template/{targetType}", GetImportTemplate);
        secured.MapGet("/imports/latest", GetLatestImportAsync);
        secured.MapPost("/imports/preview", PreviewImportAsync);
        secured.MapPost("/imports/{previewId:guid}/confirm", ConfirmImportAsync);
        secured.MapPost("/invitations", CreateInvitationAsync);
        secured.MapPost("/pairing-codes", CreatePairingCodeAsync);
        secured.MapPost("/sample-data", CreateSampleDataAsync);
        secured.MapDelete("/sample-data", DeleteSampleDataAsync);
        secured.MapPost("/events", RecordActivationEventAsync);
        secured.MapGet("/metrics", GetActivationMetricsAsync);
        secured.MapGet("/diagnostics", ExportDiagnosticsAsync);

        app.MapPost("/api/v1/onboarding/invitations/accept", AcceptInvitationAsync)
            .RequireRateLimiting("auth-login");
        app.MapPost("/api/v1/onboarding/driver-pairing/consume", ConsumePairingCodeAsync)
            .RequireRateLimiting("auth-login");
        return app;
    }

    private static async Task<IResult> GetStatusAsync(
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        await EnsureStartedEventAsync(tenant.OrganizationId, tenant.UserId, dbContext, cancellationToken);

        var tenantUsers = await userManager.Users
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .Select(x => new { x.Id, x.DriverId, x.TwoFactorEnabled })
            .ToListAsync(cancellationToken);
        var operatorCount = 0;
        foreach (var user in tenantUsers)
        {
            var applicationUser = await userManager.FindByIdAsync(user.Id.ToString());
            if (applicationUser is not null
                && (await userManager.GetRolesAsync(applicationUser)).Contains(SystemRoles.Operator))
            {
                operatorCount++;
            }
        }

        var startedAtUtc = await dbContext.OnboardingActivationEvents
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.EventName == "onboarding_started")
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => (DateTimeOffset?)x.OccurredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var firstValueAtUtc = await FirstValueAtUtcAsync(tenant.OrganizationId, dbContext, cancellationToken);

        return Results.Ok(new OnboardingStatusResponse(
            await dbContext.Vehicles.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken),
            await dbContext.Drivers.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken),
            await dbContext.GpsDevices.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken),
            operatorCount,
            tenantUsers.Count(x => x.DriverId is not null),
            await dbContext.UserSessions.CountAsync(
                x => x.OrganizationId == tenant.OrganizationId
                    && x.ClientType == "android-pairing"
                    && x.RevokedAtUtc == null
                    && x.ExpiresAtUtc > DateTimeOffset.UtcNow,
                cancellationToken),
            await dbContext.DeviceAssignments.CountAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.UnassignedAtUtc == null,
                cancellationToken),
            await dbContext.ComplianceDocuments.CountAsync(
                x => x.OrganizationId == tenant.OrganizationId,
                cancellationToken),
            await dbContext.Missions.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken),
            await dbContext.Missions.CountAsync(x => x.OrganizationId == tenant.OrganizationId && x.Status == MissionStatus.Completed, cancellationToken),
            tenantUsers.Any(x => x.Id == tenant.UserId && x.TwoFactorEnabled),
            await dbContext.OnboardingSampleDataSets.AnyAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken),
            startedAtUtc,
            firstValueAtUtc));
    }

    private static IResult GetImportTemplate(string targetType)
    {
        try
        {
            return Results.Text(OnboardingCsvParser.Template(targetType), "text/csv", Encoding.UTF8);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "targetType"] = [ex.Message]
            });
        }
    }

    private static async Task<IResult> PreviewImportAsync(
        ImportPreviewRequest request,
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        ParsedOnboardingCsv parsed;
        try
        {
            parsed = OnboardingCsvParser.Parse(request.TargetType, request.Csv);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "targetType"] = [ex.Message]
            });
        }

        var preview = new OnboardingImportSession(
            tenant.OrganizationId,
            tenant.UserId,
            parsed.TargetType,
            JsonSerializer.Serialize(parsed.Rows, SerializerOptions),
            JsonSerializer.Serialize(parsed.Errors, SerializerOptions),
            parsed.Rows.Count,
            parsed.Errors.Count,
            DateTimeOffset.UtcNow.AddHours(24));
        dbContext.OnboardingImportSessions.Add(preview);
        if (parsed.Errors.Count > 0)
        {
            dbContext.OnboardingActivationEvents.Add(new OnboardingActivationEvent(
                tenant.OrganizationId,
                tenant.UserId,
                "import_validation_failed",
                parsed.TargetType,
                DateTimeOffset.UtcNow));
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ImportPreviewResponse(
            preview.Id,
            preview.TargetType,
            preview.RowCount,
            parsed.Errors,
            preview.ExpiresAtUtc,
            preview.CanConfirm(DateTimeOffset.UtcNow),
            preview.RowVersion));
    }

    private static async Task<IResult> GetLatestImportAsync(
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        var preview = await dbContext.OnboardingImportSessions
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (preview is null)
        {
            return Results.NoContent();
        }

        var errors = JsonSerializer.Deserialize<List<ImportRowError>>(preview.ErrorsJson, SerializerOptions) ?? [];
        return Results.Ok(new ImportPreviewResponse(
            preview.Id,
            preview.TargetType,
            preview.RowCount,
            errors,
            preview.ExpiresAtUtc,
            preview.CanConfirm(DateTimeOffset.UtcNow),
            preview.RowVersion));
    }

    private static async Task<IResult> ConfirmImportAsync(
        Guid previewId,
        ConfirmImportRequest request,
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        var preview = await dbContext.OnboardingImportSessions
            .SingleOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.Id == previewId,
                cancellationToken);
        if (preview is null)
        {
            return Results.NotFound();
        }

        if (preview.ConfirmedAtUtc is not null && preview.SummaryJson is not null)
        {
            var prior = JsonSerializer.Deserialize<ConfirmImportResponse>(preview.SummaryJson, SerializerOptions)!;
            return Results.Ok(prior with { WasAlreadyConfirmed = true });
        }

        if (preview.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Import preview changed. Reload before confirming."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        if (!preview.CanConfirm(DateTimeOffset.UtcNow))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["preview"] = [preview.ExpiresAtUtc <= DateTimeOffset.UtcNow
                    ? "Import preview expired. Preview the corrected file again."
                    : "Import preview contains validation errors and cannot be confirmed."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        var rows = JsonSerializer.Deserialize<List<string[]>>(preview.RowsJson, SerializerOptions) ?? [];
        var created = 0;
        var updated = 0;
        switch (preview.TargetType)
        {
            case "vehicles":
                foreach (var row in rows)
                {
                    var existing = await dbContext.Vehicles.SingleOrDefaultAsync(
                        x => x.OrganizationId == tenant.OrganizationId && x.RegistrationNumber == row[0],
                        cancellationToken);
                    if (existing is null)
                    {
                        dbContext.Vehicles.Add(new Vehicle(tenant.OrganizationId, row[0], row[1]));
                        created++;
                    }
                    else if (!string.Equals(existing.DisplayName, row[1], StringComparison.Ordinal))
                    {
                        existing.Rename(row[1]);
                        updated++;
                    }
                }
                break;
            case "drivers":
                foreach (var row in rows)
                {
                    var phone = string.IsNullOrWhiteSpace(row[2]) ? null : row[2];
                    var existing = await dbContext.Drivers.SingleOrDefaultAsync(
                        x => x.OrganizationId == tenant.OrganizationId && x.LicenseNumber == row[1],
                        cancellationToken);
                    if (existing is null)
                    {
                        dbContext.Drivers.Add(new Driver(tenant.OrganizationId, row[0], row[1], phone));
                        created++;
                    }
                    else if (!string.Equals(existing.FullName, row[0], StringComparison.Ordinal)
                        || !string.Equals(existing.PhoneNumber, phone, StringComparison.Ordinal))
                    {
                        existing.Update(row[0], phone);
                        updated++;
                    }
                }
                break;
            case "devices":
                foreach (var row in rows)
                {
                    var displayName = string.IsNullOrWhiteSpace(row[1]) ? null : row[1];
                    var existing = await dbContext.GpsDevices.SingleOrDefaultAsync(
                        x => x.OrganizationId == tenant.OrganizationId && x.SerialNumber == row[0],
                        cancellationToken);
                    if (existing is null)
                    {
                        dbContext.GpsDevices.Add(new GpsDevice(tenant.OrganizationId, row[0], displayName));
                        created++;
                    }
                    else if (!string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal))
                    {
                        existing.Rename(displayName);
                        updated++;
                    }
                }
                break;
            default:
                return Results.Problem("Unsupported import target.", statusCode: StatusCodes.Status500InternalServerError);
        }

        var summary = new ConfirmImportResponse(created, updated, rows.Count - created - updated, false);
        preview.Confirm(JsonSerializer.Serialize(summary, SerializerOptions), DateTimeOffset.UtcNow);
        dbContext.OnboardingActivationEvents.Add(new OnboardingActivationEvent(
            tenant.OrganizationId,
            tenant.UserId,
            "import_confirmed",
            preview.TargetType,
            DateTimeOffset.UtcNow));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["preview"] = ["Import confirmation was processed concurrently. Reload its result."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "onboarding.import_confirmed",
            "import-preview",
            preview.Id.ToString(),
            new { preview.TargetType, summary.Created, summary.Updated, summary.Skipped },
            cancellationToken);
        return Results.Ok(summary);
    }

    private static async Task<IResult> CreateInvitationAsync(
        CreateInvitationRequest request,
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        var email = request.Email.Trim();
        var normalizedEmail = userManager.NormalizeEmail(email);
        if (!SystemRoles.All.Contains(request.Role)
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(request.FullName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["invitation"] = ["Email, full name and a valid role are required."]
            });
        }

        if (request.Role == SystemRoles.Driver)
        {
            if (request.DriverId is not Guid driverId
                || !await dbContext.Drivers.AnyAsync(
                    x => x.OrganizationId == tenant.OrganizationId && x.Id == driverId && x.IsActive,
                    cancellationToken)
                || await dbContext.Users.AnyAsync(
                    x => x.OrganizationId == tenant.OrganizationId && x.DriverId == driverId,
                    cancellationToken))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["driverId"] = ["Select an active unlinked driver from this organization."]
                });
            }
        }
        else if (request.DriverId is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["driverId"] = ["Only Driver invitations can link a driver profile."]
            });
        }

        if (await dbContext.Users.AnyAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.NormalizedEmail == normalizedEmail,
                cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["A user with this email already exists in the organization."]
            });
        }

        if (await dbContext.TenantInvitations.AnyAsync(
                x => x.OrganizationId == tenant.OrganizationId
                    && x.Email == email
                    && x.AcceptedAtUtc == null
                    && x.ExpiresAtUtc > DateTimeOffset.UtcNow,
                cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["An active invitation already exists for this email."]
            });
        }

        var token = CreateToken(32);
        var invitation = new TenantInvitation(
            tenant.OrganizationId,
            email,
            request.FullName.Trim(),
            request.Role,
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(7),
            request.DriverId);
        dbContext.TenantInvitations.Add(invitation);
        dbContext.OnboardingActivationEvents.Add(new OnboardingActivationEvent(
            tenant.OrganizationId,
            tenant.UserId,
            "invitation_created",
            request.Role,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "onboarding.invitation_created",
            "invitation",
            invitation.Id.ToString(),
            new { request.Role },
            cancellationToken);
        return Results.Created(
            $"/api/v1/onboarding/invitations/{invitation.Id}",
            new InvitationResponse(invitation.Id, token, invitation.ExpiresAtUtc));
    }

    private static async Task<IResult> AcceptInvitationAsync(
        AcceptInvitationRequest request,
        FleetOpsDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tokenHash = Hash(request.Token);
        var invitation = await dbContext.TenantInvitations
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (invitation is null || !invitation.IsUsable(DateTimeOffset.UtcNow))
        {
            return Results.Unauthorized();
        }

        var user = new ApplicationUser
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            FullName = invitation.FullName,
            OrganizationId = invitation.OrganizationId,
            EmailConfirmed = true,
            IsActive = true,
            DriverId = invitation.DriverId
        };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return Results.ValidationProblem(ToIdentityErrors(created));
        }

        var role = await userManager.AddToRoleAsync(user, invitation.Role);
        if (!role.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return Results.ValidationProblem(ToIdentityErrors(role));
        }

        invitation.Accept(DateTimeOffset.UtcNow);
        dbContext.OnboardingActivationEvents.Add(new OnboardingActivationEvent(
            invitation.OrganizationId,
            user.Id,
            "invitation_accepted",
            invitation.Role,
            DateTimeOffset.UtcNow));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await userManager.DeleteAsync(user);
            return Results.Unauthorized();
        }

        await auditService.WriteAsync(
            invitation.OrganizationId,
            user.Id,
            "onboarding.invitation_accepted",
            "invitation",
            invitation.Id.ToString(),
            new { invitation.Role },
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CreatePairingCodeAsync(
        CreatePairingCodeRequest request,
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null
            || user.OrganizationId != tenant.OrganizationId
            || user.DriverId is null
            || !(await userManager.GetRolesAsync(user)).Contains(SystemRoles.Driver))
        {
            return Results.NotFound();
        }

        PairingCodeResponse? response = null;
        for (var attempt = 0; attempt < 10 && response is null; attempt++)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            var hash = Hash(code);
            if (await dbContext.DriverPairingCodes.AnyAsync(
                    x => x.CodeHash == hash,
                    cancellationToken))
            {
                continue;
            }

            var pairing = new DriverPairingCode(
                tenant.OrganizationId,
                user.Id,
                hash,
                DateTimeOffset.UtcNow.AddMinutes(10));
            dbContext.DriverPairingCodes.Add(pairing);
            response = new PairingCodeResponse(code, pairing.ExpiresAtUtc);
        }

        if (response is null)
        {
            return Results.Problem("Unable to allocate a pairing code. Try again.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        dbContext.OnboardingActivationEvents.Add(new OnboardingActivationEvent(
            tenant.OrganizationId,
            tenant.UserId,
            "pairing_created",
            "android",
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "onboarding.driver_pairing_created",
            "user",
            user.Id.ToString(),
            null,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ConsumePairingCodeAsync(
        ConsumePairingCodeRequest request,
        FleetOpsDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IJwtTokenIssuer issuer,
        IOptions<JwtOptions> options,
        CancellationToken cancellationToken)
    {
        var pairing = await dbContext.DriverPairingCodes
            .SingleOrDefaultAsync(x => x.CodeHash == Hash(request.Code), cancellationToken);
        if (pairing is null || !pairing.IsUsable(DateTimeOffset.UtcNow))
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(pairing.UserId.ToString());
        var organization = user is null
            ? null
            : await dbContext.Organizations.FindAsync([pairing.OrganizationId], cancellationToken);
        if (user is null
            || organization is null
            || user.OrganizationId != pairing.OrganizationId
            || user.DriverId is null
            || !(await userManager.GetRolesAsync(user)).Contains(SystemRoles.Driver))
        {
            return Results.Unauthorized();
        }

        pairing.Consume(DateTimeOffset.UtcNow);
        var session = new UserSession(
            user.OrganizationId,
            user.Id,
            "android-pairing",
            DateTimeOffset.UtcNow.AddHours(Math.Max(1, options.Value.SessionLifetimeHours)));
        dbContext.UserSessions.Add(session);
        dbContext.OnboardingActivationEvents.Add(new OnboardingActivationEvent(
            user.OrganizationId,
            user.Id,
            "pairing_consumed",
            "android",
            DateTimeOffset.UtcNow));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Unauthorized();
        }

        var token = await issuer.IssueAsync(user, organization.Name, session.Id, cancellationToken);
        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(new LoginResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            new AuthenticatedUserResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                organization.Name,
                user.DriverId,
                roles.ToArray(),
                user.TwoFactorEnabled),
            false,
            null,
            null));
    }

    private static async Task<IResult> CreateSampleDataAsync(
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        var existing = await dbContext.OnboardingSampleDataSets
            .SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken);
        if (existing is not null)
        {
            return Results.Ok(ToSampleResponse(existing));
        }

        var suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8].ToUpperInvariant();
        var vehicle = new Vehicle(tenant.OrganizationId, $"DEMO-{suffix}", "Onboarding demo van");
        var driver = new Driver(tenant.OrganizationId, "Demo Driver", $"DEMO-LIC-{suffix}");
        var device = new GpsDevice(tenant.OrganizationId, $"DEMO-GPS-{suffix}", "Demo tracker");
        var now = DateTimeOffset.UtcNow;
        var mission = new Mission(tenant.OrganizationId, $"DEMO-{suffix}", "First test mission", now.AddMinutes(30), now.AddHours(2));
        mission.ReplaceStops([
            new MissionStop(tenant.OrganizationId, mission.Id, 1, "Test stop", "1 Demo Street", now.AddHours(1))
        ]);
        mission.TransitionTo(MissionStatus.Planned, now);
        mission.SetAssignment(driver.Id, vehicle.Id);
        mission.TransitionTo(MissionStatus.Assigned, now);
        var assignment = new DeviceAssignment(tenant.OrganizationId, device.Id, vehicle.Id, now);
        var dataSet = new OnboardingSampleDataSet(
            tenant.OrganizationId,
            vehicle.Id,
            driver.Id,
            device.Id,
            mission.Id);

        dbContext.AddRange(vehicle, driver, device, mission, assignment, dataSet);
        dbContext.OnboardingActivationEvents.Add(new OnboardingActivationEvent(
            tenant.OrganizationId,
            tenant.UserId,
            "sample_data_created",
            "demo",
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "onboarding.sample_data_created",
            "sample-data",
            dataSet.Id.ToString(),
            new { dataSet.VehicleId, dataSet.DriverId, dataSet.DeviceId, dataSet.MissionId },
            cancellationToken);
        return Results.Created("/api/v1/onboarding/sample-data", ToSampleResponse(dataSet));
    }

    private static async Task<IResult> DeleteSampleDataAsync(
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        var dataSet = await dbContext.OnboardingSampleDataSets
            .SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken);
        if (dataSet is null)
        {
            return Results.NoContent();
        }

        var mission = await dbContext.Missions
            .Include(x => x.Stops)
            .Include(x => x.Timeline)
            .SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == dataSet.MissionId, cancellationToken);
        var assignments = await dbContext.DeviceAssignments
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.DeviceId == dataSet.DeviceId)
            .ToListAsync(cancellationToken);
        var device = await dbContext.GpsDevices.SingleOrDefaultAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.Id == dataSet.DeviceId,
            cancellationToken);
        var vehicle = await dbContext.Vehicles.SingleOrDefaultAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.Id == dataSet.VehicleId,
            cancellationToken);
        var driver = await dbContext.Drivers.SingleOrDefaultAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.Id == dataSet.DriverId,
            cancellationToken);

        dbContext.Remove(dataSet);
        dbContext.RemoveRange(assignments);
        if (mission is not null) dbContext.Remove(mission);
        if (device is not null) dbContext.Remove(device);
        if (vehicle is not null) dbContext.Remove(vehicle);
        if (driver is not null) dbContext.Remove(driver);
        dbContext.OnboardingActivationEvents.Add(new OnboardingActivationEvent(
            tenant.OrganizationId,
            tenant.UserId,
            "sample_data_removed",
            "demo",
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "onboarding.sample_data_removed",
            "sample-data",
            dataSet.Id.ToString(),
            null,
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RecordActivationEventAsync(
        ActivationEventRequest request,
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        CancellationToken cancellationToken)
    {
        var eventName = request.EventName.Trim().ToLowerInvariant();
        var step = request.Step.Trim().ToLowerInvariant();
        if (!AllowedActivationEvents.Contains(eventName) || step.Length is < 1 or > 48)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["event"] = ["Unsupported activation event or step."]
            });
        }

        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        dbContext.OnboardingActivationEvents.Add(new OnboardingActivationEvent(
            tenant.OrganizationId,
            tenant.UserId,
            eventName,
            step,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetActivationMetricsAsync(
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        var events = await dbContext.OnboardingActivationEvents
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => new { x.EventName, x.OccurredAtUtc })
            .ToListAsync(cancellationToken);
        var started = events.FirstOrDefault(x => x.EventName == "onboarding_started")?.OccurredAtUtc;
        var firstValue = await FirstValueAtUtcAsync(tenant.OrganizationId, dbContext, cancellationToken);
        return Results.Ok(new ActivationMetricsResponse(
            started,
            firstValue,
            started is not null && firstValue is not null ? (firstValue.Value - started.Value).TotalMinutes : null,
            events.Count(x => x.EventName == "onboarding_abandoned"),
            events.Count(x => x.EventName == "import_validation_failed"),
            events.Count));
    }

    private static async Task<IResult> ExportDiagnosticsAsync(
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = tenantAccessor.GetRequiredTenant(context.User);
        var now = DateTimeOffset.UtcNow;
        var diagnostics = new
        {
            generatedAtUtc = now,
            organizationId = tenant.OrganizationId,
            counts = new
            {
                vehicles = await dbContext.Vehicles.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken),
                drivers = await dbContext.Drivers.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken),
                devices = await dbContext.GpsDevices.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken),
                missions = await dbContext.Missions.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken),
                activeSessions = await dbContext.UserSessions.CountAsync(
                    x => x.OrganizationId == tenant.OrganizationId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now,
                    cancellationToken)
            },
            onboarding = new
            {
                pendingValidImports = await dbContext.OnboardingImportSessions.CountAsync(
                    x => x.OrganizationId == tenant.OrganizationId
                        && x.ConfirmedAtUtc == null
                        && x.ErrorCount == 0
                        && x.ExpiresAtUtc > now,
                    cancellationToken),
                invalidImports = await dbContext.OnboardingImportSessions.CountAsync(
                    x => x.OrganizationId == tenant.OrganizationId && x.ErrorCount > 0,
                    cancellationToken),
                activePairingCodes = await dbContext.DriverPairingCodes.CountAsync(
                    x => x.OrganizationId == tenant.OrganizationId
                        && x.ConsumedAtUtc == null
                        && x.ExpiresAtUtc > now,
                    cancellationToken),
                hasSampleData = await dbContext.OnboardingSampleDataSets.AnyAsync(
                    x => x.OrganizationId == tenant.OrganizationId,
                    cancellationToken)
            }
        };
        return Results.Text(JsonSerializer.Serialize(diagnostics, SerializerOptions), "application/json", Encoding.UTF8);
    }

    private static async Task EnsureStartedEventAsync(
        Guid organizationId,
        Guid userId,
        FleetOpsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await dbContext.OnboardingActivationEvents.AnyAsync(
                x => x.OrganizationId == organizationId && x.EventName == "onboarding_started",
                cancellationToken))
        {
            return;
        }

        dbContext.OnboardingActivationEvents.Add(new OnboardingActivationEvent(
            organizationId,
            userId,
            "onboarding_started",
            "checklist",
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Task<DateTimeOffset?> FirstValueAtUtcAsync(
        Guid organizationId,
        FleetOpsDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.MissionTimelineEvents
            .Where(x => x.OrganizationId == organizationId
                && x.EventType == MissionTimelineEventType.StatusChanged
                && x.Description.Contains("Completed"))
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => (DateTimeOffset?)x.OccurredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private static SampleDataResponse ToSampleResponse(OnboardingSampleDataSet dataSet) =>
        new(dataSet.Id, dataSet.VehicleId, dataSet.DriverId, dataSet.DeviceId, dataSet.MissionId);

    private static Dictionary<string, string[]> ToIdentityErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(x => x.Code)
            .ToDictionary(x => x.Key, x => x.Select(error => error.Description).ToArray());

    private static string CreateToken(int bytes) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value?.Trim() ?? string.Empty)));
}
