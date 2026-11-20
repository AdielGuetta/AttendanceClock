using System.Security.Claims;

namespace AttendanceClock.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The logged-in employee's id, read from the token's "sub" claim.</summary>
    public static int GetEmployeeId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirst(TokenService.Claims.EmployeeId)?.Value
                  ?? throw new InvalidOperationException("No employee id in the token."));
}
