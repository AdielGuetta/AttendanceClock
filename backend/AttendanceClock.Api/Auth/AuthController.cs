using System.ComponentModel.DataAnnotations;
using AttendanceClock.Api.Common;
using AttendanceClock.Api.Data;
using AttendanceClock.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AttendanceClock.Api.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordHasher<Employee> hasher, TokenService tokens) : ControllerBase
{
    public const string LoginRateLimitPolicy = "login";

    // A real hash of a random password. Checked against when the username doesn't exist, so a failed
    // login takes the same time either way and doesn't reveal which usernames exist.
    private static readonly string DummyHash =
        new PasswordHasher<Employee>().HashPassword(null!, Guid.NewGuid().ToString());

    /// <summary>POST /api/auth/login: exchange username + password for a token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.UserName == request.UserName, cancellationToken);

        var passwordOk = hasher.VerifyHashedPassword(employee!, employee?.PasswordHash ?? DummyHash, request.Password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;

        // Unknown user, wrong password and deactivated account all get the SAME answer (no hints to attackers).
        if (employee is null || !passwordOk || !employee.IsActive)
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS",
                "Invalid username or password.");

        var (token, expiresAtUtc) = tokens.CreateToken(employee);
        return new LoginResponse(token, expiresAtUtc, EmployeeDto.From(employee));
    }

    /// <summary>GET /api/auth/me: who am I? (Requires a valid token.)</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<EmployeeDto>> Me(CancellationToken cancellationToken)
    {
        var id = int.Parse(User.FindFirst(TokenService.Claims.EmployeeId)!.Value);
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == id && e.IsActive, cancellationToken);
        return employee is null ? Unauthorized() : EmployeeDto.From(employee);
    }
}

public sealed record LoginRequest([Required, MaxLength(50)] string UserName, [Required, MaxLength(200)] string Password);

public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, EmployeeDto Employee);

public sealed record EmployeeDto(int Id, string UserName, string DisplayName, string Role)
{
    public static EmployeeDto From(Employee e) => new(e.Id, e.UserName, e.DisplayName, e.Role.ToString());
}
