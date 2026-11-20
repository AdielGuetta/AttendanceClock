using Microsoft.AspNetCore.Mvc;

namespace AttendanceClock.Api.Common;

/// <summary>
/// Builds error responses in one consistent shape: ProblemDetails (RFC 9457) plus a stable "code"
/// the UI can rely on (e.g. INVALID_CREDENTIALS, ALREADY_ON_SHIFT).
/// </summary>
public static class ApiProblem
{
    public static ObjectResult Create(int status, string code, string title, string? detail = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Extensions = { ["code"] = code },
        };
        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
