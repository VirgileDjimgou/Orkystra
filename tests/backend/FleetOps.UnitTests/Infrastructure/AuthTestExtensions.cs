using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FleetOps.Api.Auth;
using FleetOps.Api.Security;
using Microsoft.IdentityModel.Tokens;

namespace FleetOps.UnitTests.Infrastructure;

public static class AuthTestExtensions
{
    public static async Task<LoginResponse> LoginAsync(
        this HttpClient client,
        string email,
        string password,
        string? twoFactorCode = null)
    {
        var login = await client.RequestLoginAsync(email, password, twoFactorCode);
        if (login.RequiresTwoFactor)
        {
            throw new InvalidOperationException("A two-factor login challenge was returned unexpectedly.");
        }

        if (string.IsNullOrWhiteSpace(login.AccessToken))
        {
            throw new InvalidOperationException("The login response did not include a complete authenticated session.");
        }

        return login;
    }

    public static async Task<LoginResponse> RequestLoginAsync(
        this HttpClient client,
        string email,
        string password,
        string? twoFactorCode = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password, twoFactorCode));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    public static void SetBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static string CreateExpiredToken(Guid userId, Guid organizationId, string email, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Role, role),
            new(TenantClaimTypes.OrganizationId, organizationId.ToString()),
            new(TenantClaimTypes.OrganizationName, "Expired Test Org"),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("FleetOps_Tests_Signing_Key_12345678901234567890")),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "FleetOps.Tests",
            audience: "FleetOps.Tests.Web",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: DateTime.UtcNow.AddMinutes(-1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string CreateAuthenticatorCode(string manualEntryKey, DateTimeOffset? timestamp = null)
    {
        var secret = DecodeBase32(manualEntryKey);
        var unixTimeStep = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;
        Span<byte> counterBytes = stackalloc byte[8];
        for (var index = 7; index >= 0; index--)
        {
            counterBytes[index] = (byte)(unixTimeStep & 0xFF);
            unixTimeStep >>= 8;
        }

#pragma warning disable CA5350 // RFC 6238 authenticator codes use HMAC-SHA1.
        using var hmac = new HMACSHA1(secret);
#pragma warning restore CA5350
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string input)
    {
        var alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var normalized = input
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .TrimEnd('=')
            .ToUpperInvariant();

        var output = new List<byte>((normalized.Length * 5) / 8);
        var bits = 0;
        var value = 0;

        foreach (var character in normalized)
        {
            var index = alphabet.IndexOf(character);
            if (index < 0)
            {
                throw new FormatException("Authenticator key is not valid Base32.");
            }

            value = (value << 5) | index;
            bits += 5;

            if (bits < 8)
            {
                continue;
            }

            output.Add((byte)((value >> (bits - 8)) & 0xFF));
            bits -= 8;
        }

        return output.ToArray();
    }
}
