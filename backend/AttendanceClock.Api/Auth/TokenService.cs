using System.Security.Claims;
using System.Text;
using AttendanceClock.Api.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AttendanceClock.Api.Auth;

/// <summary>Issues the signed login token (JWT): the "digital badge" that says who you are and your role.</summary>
public sealed class TokenService(IOptions<AuthOptions> options)
{
    /// <summary>Claim names used in our tokens (kept short and standard).</summary>
    public static class Claims
    {
        public const string EmployeeId = "sub";
        public const string UserName = "name";
        public const string Role = "role";
    }

    public (string Token, DateTime ExpiresAtUtc) CreateToken(Employee employee)
    {
        var settings = options.Value;

        // Token expiry uses the server clock. That's allowed: it's infrastructure, not an attendance record (PRD D-7).
        var expiresAtUtc = DateTime.UtcNow.Add(settings.TokenLifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Expires = expiresAtUtc,
            Subject = new ClaimsIdentity(
            [
                new Claim(Claims.EmployeeId, employee.Id.ToString()),
                new Claim(Claims.UserName, employee.UserName),
                new Claim(Claims.Role, employee.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(settings), SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAtUtc);
    }

    public static SymmetricSecurityKey SigningKey(AuthOptions settings) =>
        new(Encoding.UTF8.GetBytes(settings.SigningKey));
}
