using System.ComponentModel.DataAnnotations;

namespace AttendanceClock.Api.Auth;

/// <summary>Login settings (appsettings, section "Auth"). The signing key must come from configuration, never code.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = "AttendanceClock";
    public string Audience { get; set; } = "AttendanceClock";

    /// <summary>Secret used to sign tokens (HMAC-SHA256 needs at least 32 bytes). Dev value in appsettings.Development.json.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>How long a login token is valid (PRD D-8: short, because a JWT can't be revoked early).</summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>Brute-force protection: login attempts allowed per minute per IP address.</summary>
    [Range(1, 10_000)]
    public int LoginAttemptsPerMinute { get; set; } = 5;
}
